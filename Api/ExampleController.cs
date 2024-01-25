using Microsoft.AspNetCore.Mvc;
using Rtl.News.RtlPoc.Application;
using Rtl.News.RtlPoc.Contracts;

namespace Rtl.News.RtlPoc.Api;

// TODO: Remove when implementing the first real request

[ApiController]
[Route("Example/[action]")]
public sealed class ExampleController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddEntities(ResilientService<ExampleUseCase> useCase, [FromBody] ExampleRequest _, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(cancellationToken);
        return Ok();
    }
}
