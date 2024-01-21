using Rtl.News.RtlPoc.Application.Promises;
using Rtl.News.RtlPoc.Application.Storage;
using Rtl.News.RtlPoc.Contracts;
using Rtl.News.RtlPoc.Domain;
using Rtl.News.RtlPoc.Infrastructure.Databases.Promises;
using Shouldly;

namespace Rtl.News.RtlPoc.Application.IntegrationTests;

public sealed class ExampleUseCaseTests : IntegrationTestBase
{
	[Fact]
	public async Task ExecuteAsync_Regularly_ShouldHaveExpectedEffect()
	{
		// Arrange

		using var idGeneratorScope = new DistributedId128GeneratorScope(new IncrementalIdGenerator());

		var promiseSalvager = this.Host.Services.GetRequiredService<CosmosPromiseSalvager>();

		// Act (immediate)

		using var clockScope = new ClockScope(FixedTime);
		await this.GetApiResponse(HttpMethod.Post, "Example/AddEntities", new ExampleRequest());

		// Assert (immediate)

		var entities = await this.Repository.ListAsync<ExampleEntity>(query => query.Where(x => x.Name != null),
			CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });
		var promises = await this.Repository.ListAsync<Promise>(query => query.Where(x => x.Due >= default(DateTimeOffset)),
			CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });

		// One entity was deleted immediately, the other promised to be deleted
		var jan = entities.ShouldHaveSingleItem();
		jan.Id.Value.ShouldBe("0000000000100000000par");
		jan.Name.ShouldBe("Jan");
		var promise = promises.ShouldHaveSingleItem();
		promise.Id.ShouldBe("0000000000400000000par");
		promise.Data.ShouldBe(entities.Single().Id.Value);

		// Act (eventual)

		using var laterClockScope = new ClockScope(FixedTime.Add(Promise.ClaimDuration));
		await promiseSalvager.TryFulfillDuePromisesAsync(CancellationToken.None);

		// Assert (eventual)

		entities = await this.Repository.ListAsync<ExampleEntity>(query => query.Where(x => x.Name != null),
			CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });
		promises = await this.Repository.ListAsync<Promise>(query => query.Where(x => x.Due >= default(DateTimeOffset)),
			CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });

		// The remaining promise should have been fulfilled, i.e. deleted the remaining entity and itself
		entities.ShouldBeEmpty();
		promises.ShouldBeEmpty();
	}
}
