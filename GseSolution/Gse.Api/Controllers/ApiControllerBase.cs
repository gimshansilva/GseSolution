using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gse.Api.Controllers;

[ApiController]
[EnableRateLimiting("api")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult BadReq(string title) =>
        BadRequest(new ProblemDetails { Title = title, Status = StatusCodes.Status400BadRequest });

    protected ActionResult Conflicted(string title) =>
        Conflict(new ProblemDetails { Title = title, Status = StatusCodes.Status409Conflict });

    protected ActionResult Missing(string title) =>
        NotFound(new ProblemDetails { Title = title, Status = StatusCodes.Status404NotFound });

    protected static (int Page, int PageSize) Clamp(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, 100));
}
