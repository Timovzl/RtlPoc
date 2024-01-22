using Microsoft.Azure.Cosmos;
using Rtl.News.RtlPoc.Application.Promises;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

/// <summary>
/// A CosmosDB implementation of <see cref="StorageTransaction"/>.
/// </summary>
internal sealed class CosmosTransaction(
	DataPartitionKey partitionKey,
	CosmosRepository repository,
	CancellationToken cancellationToken)
	: StorageTransaction
{
	public const ushort MaxOperationCount = 100;

	private readonly DataPartitionKey _partitionKey = partitionKey;
	private readonly PartitionKey _cosmosPartitionKey = new PartitionKey(partitionKey);

	public override IRepository Repository => this._repository;
	private readonly CosmosRepository _repository = repository;

	public override CancellationToken CancellationToken { get; } = cancellationToken;

	public TransactionalBatch Batch => this._batch ??= this._repository.Container.CreateTransactionalBatch(this._cosmosPartitionKey);
	private TransactionalBatch? _batch;

	/// <summary>
	/// The entities that the operations are about (null for deletions), in operational order.
	/// </summary>
	private readonly List<IPocEntity?> _orderedEntities = [];

	private List<Promise> Promises => this._promises ??= [];
	private List<Promise>? _promises;

	public override ValueTask DisposeAsync()
	{
		// Throw if any promises were stored but neither attempted nor suppressed, which indicates that the develop forgot to attempt to fulfill the promise immediately
		if (this._promises is not null && this._promises.FirstOrDefault(promise => promise.IsFirstAttempt && promise.AvailableAttemptCount > 0) is Promise forgottenPromise)
			throw new InvalidOperationException($"Created {forgottenPromise} was not attempted to be fulfilled immediately. If this is deliberate, suppress it explicitly before disposing the {nameof(StorageTransaction)}.");

		// A Cosmos transaction is sent as a single request, so no mutations are ever ongoing
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Call this method for each operation added to the batch, in order.
	/// </summary>
	public void SetEntityForNextOperation(IPocEntity? entity)
	{
		// Protect size limitation
		if (this._orderedEntities.Count >= MaxOperationCount)
			throw new InvalidOperationException($"A CosmosDB transaction supports only up to {MaxOperationCount} operations. Design for smaller batches.");

		// Protect single partition limitation
		if (entity is not null && !this._partitionKey.MatchesId(entity.GetId()))
			throw new InvalidOperationException("A CosmosDB transaction supports only a single partition. To automatically generate IDs for the same partition, start the use case with: using var idGeneratorScope = IdGenerator.CreateIdGeneratorScopeForSinglePartition().");

		this._orderedEntities.Add(entity);

		if (entity is Promise promise)
			this.Promises.Add(promise);
	}

	public override async ValueTask CommitAsync(CancellationToken? cancellationToken = null)
	{
		var ct = cancellationToken ?? this.CancellationToken;

		if (this._orderedEntities.Count == 0)
			return;

		try
		{
			using var response = await this.Batch.ExecuteAsync(/*new TransactionalBatchRequestOptions()
			{
				SessionToken = "", // TODO Enhancement: Pass session token manually? https://github.com/Azure/azure-cosmos-dotnet-v3/discussions/4237
			},*/ ct);

			if (!response.IsSuccessStatusCode)
				throw new CosmosException($"Failed to complete ComosDB {nameof(TransactionalBatch)} of size {response.Count} with status code {(int)response.StatusCode}={response.StatusCode}.",
					response.StatusCode, subStatusCode: 0 /*0=SubstatusCodes.Unknown*/, activityId: response.ActivityId, requestCharge: response.RequestCharge);

			System.Diagnostics.Debug.Assert(response.Count == this._orderedEntities.Count);

			// Update ETags based on the response, in case further writes are attempted
			for (var i = 0; i < this._orderedEntities.Count; i++)
				if (this._orderedEntities[i] is IPocEntity entity)
					entity.ETag = response.GetOperationResultAtIndex<IPocEntity>(i).ETag;
		}
		finally
		{
			this._batch = null;
			this._orderedEntities.Clear();
		}
	}

	public override ValueTask RollBackAsync(CancellationToken? cancellationToken = null)
	{
		this._batch = null;
		this._orderedEntities.Clear();

		// A Cosmos transaction is sent as a single request, so no mutations are ever ongoing
		return ValueTask.CompletedTask;
	}
}
