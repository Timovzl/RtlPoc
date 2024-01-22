# RtlPoc

A proof of concept for a CosmosDB-based DDD solution for RTL.

## Local Development

The following dependencies are required to run the bounded context:

- Azure CosmosDB Emulator with Strong consistency: `"%ProgramFiles%\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe" /Consistency=Strong` (recommend editing the shortcut)
	- _Note: Although this bounded context defaults to Session consistency, it occasionally uses BoundedStaleness, which requires the emulator to be configured beyond its default setting._

## Resilience

- **Concurrency conflicts:** Use cases are invoked wrapped in a `ResilientService<T>`, whose implementation automatically retries the entire use case on an optimistic concurrency conflict.
- **Transient failures (RU exhaustion):** The CosmosClient automatically retries on RU exhaustion failures.
- **Transient failures (reads):** The CosmosClient automatically retries on transient failures.
- **Transient failures (writes):** The CosmosClient _cannot_ retry on write failures, because a write may have succeeded and is not guaranteed to be idempotent.

## High Availability

- **CosmosDB**
	- **Scaling:** Seamless autoscaling is enabled up to a factor 10, with the minimum provisioning set as low as possible.
	- **Uptime SLA:** The current configuration is backed by 99.995% read and write availability.

## CosmosDB

- **Resource allocation:** Resources are provisioned at the account level and shared by containers. Containers with specific usage patterns _may_ provisioned their own resources.
- **Regions and zones:** Single region with availability zones.
- **Backups:** Continuous backups up to 7 days.
- **Consistency level:** The account allows a maximum consistency of _Bounded Staleness_, which is as good as _Strong_ for a single-region account.
The SDK's CosmosClient is configured with a default of _Session_, which offers cheaper reads and consistency within each flow.
Occasional reads that need to be fully consistent can be parameterized as such, to make use of _Bounded Staleness_ at the cost of double resource usage.
- **Atomic transactions:** Single-partition transactions are supported. `IRepository`'s write methods are designed around small transactions.
- **Targeted partitioning:** Partition keys are based on IDs. IDs can be generated for specific partitions via inversion of control, using `IdGenerator.CreateIdGeneratorScopeForSinglePartition(DataPartitionKey?)`.
- **Eventual consistency:** A `Promise` can be saved in the same transaction to _guarantee_ that something will eventually be done. Examples include modifying other partitions, cleaning up, or contacting external systems.

## Testing

- **Testing dependencies:** To cover integration with vital dependencies, integration tests use the CosmosDB emulator and ASP.NET Core's in-memory test server.
- **Integration tests:** For maximum coverage, an integration test is used for each happy flow and occasionally an important unhappy flow.
- **Unit tests:** Further details are covered by unit tests.
- **Dependency IoC:** To control dependencies, .NET's built-in DI container is used. NSubstitute provides test doubles where relevant.
- **Time IoC:** To avoid unpredictability, the clock can be ambiently controlled: `using var clockScope = new ClockScope(FixedTime)`.
Time (or a time provider) is not injected, as that leads to unwieldy method signatures.
- **ID generation IoC:** To avoid unpredictability, generated ID values can be ambiently controlled: `using var idGeneratorScope = new DistributedId128GeneratorScope(new IncrementalDistributedIdGenerator());`.
IDs (or ID generators) are not injected, as that leads to unwieldy method signatures.
