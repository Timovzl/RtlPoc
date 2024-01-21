using Rtl.News.RtlPoc.Application.Promises;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.IntegrationTests.Shared;

public sealed class CosmosRepositoryTests : IntegrationTestBase
{
	[Fact]
	public async Task GetAsync_WithNonexistentId_ShouldReturnNull()
	{
		var id = IdGenerator.CreateId();

		var repo = this.Host.Services.GetRequiredService<CosmosRepository>();

		var result = await repo.GetAsync<Promise>(id, (DataPartitionKey)id, CancellationToken.None);

		result.ShouldBeNull();
	}
}
