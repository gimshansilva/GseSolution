using Gse.Api.Data;
using Gse.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Controllers;

[Route("api/reports")]
public class ReportsController(GseDbContext db) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<SummaryDto>> Summary(CancellationToken ct)
    {
        var byStatus = await db.Assets.AsNoTracking()
            .GroupBy(a => a.OperationalStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var openByPriority = await db.Orders.AsNoTracking().Where(o => !o.IsResolved)
            .GroupBy(o => o.Priority)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return new SummaryDto(byStatus.Values.Sum(), openByPriority.Values.Sum(), byStatus, openByPriority);
    }
}
