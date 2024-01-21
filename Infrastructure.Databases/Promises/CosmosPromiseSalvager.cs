using Architect.AmbientContexts;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Rtl.News.RtlPoc.Application.Promises;
using Rtl.News.RtlPoc.Application.Shared;
using System.Net;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Promises;

/// <summary>
/// CosmosDB implementation of the <see cref="PromiseSalvager"/>.
/// </summary>
public sealed class CosmosPromiseSalvager(
	ILogger<CosmosPromiseSalvager> logger,
	IResilienceStrategy resilienceStrategy,
	IPromiseFulfiller promiseFulfiller,
	DatabaseClient databaseClient)
	: PromiseSalvager(logger, resilienceStrategy, promiseFulfiller)
{
	protected override async Task<IReadOnlyList<Promise>> GetNeglectedPromiseBatchAsync(ushort batchSize, CancellationToken cancellationToken)
	{
		var queryable = databaseClient.Container.GetItemLinqQueryable<Promise>(
			allowSynchronousQueryExecution: false,
			requestOptions: new QueryRequestOptions()
			{
				EnableScanInQuery = false,
				ConsistencyLevel = ConsistencyLevel.ConsistentPrefix, // Although a fully consistent result is the most accurate, we are in no rush, so we can favor performance here
				MaxItemCount = batchSize,
				MaxBufferedItemCount = batchSize,
				MaxConcurrency = 1,
			});

		var utcNow = Clock.UtcNow;
		using var iterator = queryable
			.Where(x => x.Due <= utcNow)
			.OrderBy(x => x.Due)
			.ToFeedIterator();

		var response = await iterator.ReadNextAsync(cancellationToken);
		var result = response.ToList();
		return result;
	}

	protected override async Task<bool> TryUpdatePromiseAsync(Promise promise, CancellationToken cancellationToken)
	{
		try
		{
			var response = await databaseClient.Container.PatchItemAsync<Promise>(
				promise.Id,
				new PartitionKey((DataPartitionKey)promise.Id),
				[
					PatchOperation.Set(JsonUtilities.GetPropertyPath(() => promise.Due), promise.Due),
					PatchOperation.Set(JsonUtilities.GetPropertyPath(() => promise.AttemptCount), promise.AttemptCount),
				],
				new PatchItemRequestOptions() { IfMatchEtag = promise.ETag },
				cancellationToken);

			promise.ETag = response.ETag;

			return true;
		}
		catch (CosmosException e) when (e.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			return false;
		}
	}
}
