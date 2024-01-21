using Architect.AmbientContexts;
using Newtonsoft.Json;
using System.Linq.Expressions;

namespace Rtl.News.RtlPoc.Application.Promises;

/// <summary>
/// The promise to eventually perform an action.
/// </summary>
public sealed class Promise : IPocEntity
{
	public override string ToString() => $"{{{nameof(Promise)} '{this.ActionName}' ({this.Id})}}";

	public static TimeSpan ClaimDuration { get; } = TimeSpan.FromSeconds(60);

	[JsonProperty("id")]
	public string Id { get; private init; }

	string IPocEntity.GetId() => this.Id;

	[JsonProperty("part")]
	public DataPartitionKey PartitionKey => (DataPartitionKey)this.Id;

	[JsonIgnore] 
	public string? ETag
	{
		get => this._eTag;
		set
		{
			// Whenever etag is overwritten, i.e. on inserting to storage or updating claim there, make an attempt available
			this.AvailableAttemptCount = 1;

			this._eTag = value;
		}
	}
	[JsonProperty("_etag")]
	private string? _eTag;

	[JsonProperty("_ts")]
	private long StorageTimestampInSeconds { get; init; }

	/// <summary>
	/// <para>
	/// When the promise is due for the next attempt to be fulfilled.
	/// </para>
	/// <para>
	/// In UTC, for reliable querying.
	/// </para>
	/// </summary>
	[JsonProperty("Promise_Due")]
	public DateTimeOffset Due { get; private set; }

	/// <summary>
	/// May not be perfectly accurate, especially since the initial attempt may or may not be made immediately after creation.
	/// </summary>
	[JsonProperty("Promise_AtpCnt")]
	public uint AttemptCount { get; private set; }

	/// <summary>
	/// The name of the action to be executed eventually.
	/// </summary>
	[JsonProperty("Promise_Act")]
	public string ActionName { get; private init; }

	/// <summary>
	/// Optional data used to parameterize the promise, such as a JSON blob.
	/// </summary>
	[JsonProperty("Promise_Dta")]
	public string Data { get; private init; }

	/// <summary>
	/// Indicates the number of available attempts for the current in-memory instance of the promise.
	/// Newly constructing or freshly claiming a promise permits one attempt.
	/// Attempting or suppressing it costs one attempt.
	/// </summary>
	[JsonIgnore]
	public int AvailableAttemptCount { get; private set; }

	/// <summary>
	/// Indicates whether this is the first attempt to fulfill the promise, and only if it is made while the object has remained in-memory since its creation.
	/// </summary>
	[JsonIgnore]
	public bool IsFirstAttempt => this.StorageTimestampInSeconds == default && // A newly created instance
		this.AvailableAttemptCount == 1;

	/// <summary>
	/// If the caller has claimed (and thus deferred) the current promise, then this indicates if there is currently enough time left to attempt to fulfill it.
	/// </summary>
	[JsonIgnore]
	public bool HasTimeToFulfill => this.Due - Clock.UtcNow >= ClaimDuration / 2;

	private Promise(string id, string actionName, string data)
	{
		this.Id = id;
		this.AttemptCount = 1;
		this.ActionName = actionName ?? throw new ArgumentNullException(nameof(actionName));
		this.Data = data ?? throw new ArgumentNullException(nameof(data));

		this.Delay();
	}

	[JsonConstructor]
	private Promise()
	{
		this.Id = null!;
		this.ActionName = null!;
		this.Data = null!;
	}

	/// <summary>
	/// <para>
	/// Creates a promise to eventually execute the action represented by <typeparamref name="TService"/>.
	/// </para>
	/// <para>
	/// To create a promise in the same partition as other objects, create everything after a call to <see cref="IdGenerator.CreateIdGeneratorScopeForSinglePartition"/>.
	/// </para>
	/// <para>
	/// Alternatively, to create a promise in the same partition as a single existing entity, use <see cref="CreateForEntity{TService}"/>.
	/// </para>
	/// </summary>
	/// <typeparam name="TService">The service type used to fulfill the promise.</typeparam>
	/// <param name="promisedAction">The method on <typeparamref name="TService"/> promised to be called eventually, e.g. (MyUseCase useCase) => useCase.SendEmailAsync.
	/// Must be an idempotent method taking a <see cref="Promise"/> and a <see cref="CancellationToken"/>, returning a <see cref="Task"/>, and annotated with the <see cref="IdempotentPromiseFulfillerAttribute"/>.</param>
	public static Promise Create<TService>(Expression<Func<TService, Func<Promise, CancellationToken, Task>>> promisedAction, string data = "")
	{
		if (IdempotentPromiseFulfillerAttribute.GetAttributeForMethod(promisedAction) is not IdempotentPromiseFulfillerAttribute attribute)
			throw new InvalidOperationException($"The {nameof(IdempotentPromiseFulfillerAttribute)} was expected on method {promisedAction}.");

		var result = new Promise(
			id: IdGenerator.CreateId(),
			actionName: attribute.ActionName,
			data: data);

		return result;
	}

	/// <summary>
	/// Creates a promise to eventually execute the action represented by <typeparamref name="TService"/>, with respect to the given <paramref name="entity"/>.
	/// The resulting promise has the same <see cref="DataPartitionKey"/>.
	/// </summary>
	/// <typeparam name="TService">The service type used to fulfill the promise.</typeparam>
	/// <param name="promisedAction">The method on <typeparamref name="TService"/> promised to be called eventually, e.g. (MyUseCase useCase) => useCase.SendEmailAsync.
	/// Must be an idempotent method taking a <see cref="Promise"/> and a <see cref="CancellationToken"/>, returning a <see cref="Task"/>, and annotated with the <see cref="IdempotentPromiseFulfillerAttribute"/>.</param>
	public static Promise CreateForEntity<TService>(IPocEntity entity, Expression<Func<TService, Func<Promise, CancellationToken, Task>>> promisedAction, string data = "")
	{
		if (IdempotentPromiseFulfillerAttribute.GetAttributeForMethod(promisedAction) is not IdempotentPromiseFulfillerAttribute attribute)
			throw new InvalidOperationException($"The {nameof(IdempotentPromiseFulfillerAttribute)} was expected on method {promisedAction}.");

		var result = new Promise(
			id: IdGenerator.CreateIdInPartition((DataPartitionKey)entity.GetId()),
			actionName: attribute.ActionName,
			data: data);

		return result;
	}

	/// <summary>
	/// <para>
	/// Sets when the next attempt is due using the recommended delay.
	/// </para>
	/// <para>
	/// Delays are not additive, but are always relative to the current time.
	/// </para>
	/// </summary>
	public void Delay()
	{
		this.Due = Clock.UtcNow.Add(ClaimDuration);
	}

	/// <summary>
	/// <para>
	/// Sets when the next attempt is due using the given <paramref name="timeSpan"/>.
	/// </para>
	/// <para>
	/// For example, combined with <see cref="SuppressImmediateFulfillment"/>, this allows a promise to be used to undo something only if it is not fully completed in time.
	/// </para>
	/// <para>
	/// Delays are not additive, but are always relative to the current time.
	/// </para>
	/// </summary>
	public void Delay(TimeSpan timeSpan)
	{
		if (timeSpan < TimeSpan.Zero)
			throw new ArgumentException($"The {nameof(timeSpan)} must be positive.");

		this.Due = Clock.UtcNow.Add(timeSpan);
	}

	/// <summary>
	/// Declares that the promise will only be fulfilled later, without an immediate attempt.
	/// </summary>
	public void SuppressImmediateFulfillment()
	{
		if (this.StorageTimestampInSeconds != default) // An instance retrieved from storage
			throw new InvalidOperationException("Immediate fulfillment of a promise need only be suppressed upon its initial creation.");

		if (this.ETag is null)
			throw new InvalidOperationException($"{this} was attempted to be suppressed before being committed to storage.");

		if (this.AvailableAttemptCount <= 0)
			throw new InvalidOperationException("Immediate fulfillment of a promise can only be suppressed once, and only if it has not yet been attempted.");

		// Prevent further attempts on the current in-memory instance, and indicate that the promise has been deliberately addressed
		this.AvailableAttemptCount = 0;
	}

	/// <summary>
	/// <para>
	/// Claims the promise before making an attempt to fulfill it, by delaying its next attempt after the one about to be made.
	/// </para>
	/// <para>
	/// To obtain the exclusive claim to make the attempt, the caller must succeed in updating the promise in storage, where the <see cref="ETag"/> still matches the current value.
	/// </para>
	/// </summary>
	public void ClaimForAttempt()
	{
		if (this.StorageTimestampInSeconds == default) // A newly created instance
			throw new InvalidOperationException("A newly created promise cannot be claimed, but instead is automatically claimed upon insertion into storage.");

		if (this.Due > Clock.UtcNow)
			throw new InvalidOperationException($"{this} was attempted to be claimed at {Clock.UtcNow:O} but is due only at {this.Due:O}.");

		this.AttemptCount++;
		this.Delay();
	}

	/// <summary>
	/// Consumes an available attempt, or throws if none is available.
	/// </summary>
	public void ConsumeAttempt()
	{
		// Require a claim
		if (this.ETag is null)
			throw new InvalidOperationException($"{this} was attempted to be fulfilled before being committed to storage.");
		if (this.AvailableAttemptCount <= 0)
			throw new InvalidOperationException($"{this} was attempted to be fulfilled without a claim. To avoid this, claim it and attempt to fulfill it exactly once.");

		// To globally avoid re-entrancy, require sufficient claim duration available
		if (!this.HasTimeToFulfill && !this.IsFirstAttempt)
			throw new InvalidOperationException($"{this} was attempted to be fulfilled without an up-to-date claim.");

		this.AvailableAttemptCount--;
	}
}
