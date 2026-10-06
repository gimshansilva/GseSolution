using Gse.Api.Data;
using Gse.Api.Domain;
using Gse.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Controllers;

[Route("api/maintenance-orders")]
public class MaintenanceOrdersController(GseDbContext db) : ApiControllerBase
{
    private static OrderDto ToDto(MaintenanceOrder o, string assetTag) =>
        new(o.Id, o.OrderNumber, o.AssetId, assetTag, o.Priority, o.IssueDescription, o.ReportedBy,
            o.IsResolved, o.CreatedAtUtc, o.ResolvedAtUtc, o.ResolutionRemarks);

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        bool? isResolved, string? priority, int? assetId,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (priority is not null && !Priorities.All.Contains(priority))
            return BadReq($"Invalid priority. Allowed: {string.Join(", ", Priorities.All)}.");

        (page, pageSize) = Clamp(page, pageSize);

        var q = db.Orders.AsNoTracking().AsQueryable();
        if (isResolved is not null) q = q.Where(o => o.IsResolved == isResolved);
        if (priority is not null) q = q.Where(o => o.Priority == priority);
        if (assetId is not null) q = q.Where(o => o.AssetId == assetId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderDto(o.Id, o.OrderNumber, o.AssetId, o.Asset.AssetTag, o.Priority,
                                      o.IssueDescription, o.ReportedBy, o.IsResolved,
                                      o.CreatedAtUtc, o.ResolvedAtUtc, o.ResolutionRemarks))
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(items, page, pageSize, total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> Get(int id, CancellationToken ct)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Asset).FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? Missing("Order not found.") : ToDto(o, o.Asset.AssetTag);
    }

    /// <summary>
    /// Opens an order. ReportedBy always comes from the token. A Critical order flags an
    /// Available/InService asset as MaintenanceRequired.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest r, CancellationToken ct)
    {
        var asset = await db.Assets.FindAsync([r.AssetId], ct);
        if (asset is null) return Missing("Asset not found.");
        if (asset.OperationalStatus == AssetStatuses.Decommissioned) return Conflicted("Asset is decommissioned.");

        if (r.Priority == Priorities.Critical
            && asset.OperationalStatus is AssetStatuses.Available or AssetStatuses.InService)
            asset.OperationalStatus = AssetStatuses.MaintenanceRequired;

        for (var attempt = 0; ; attempt++)
        {
            var order = new MaintenanceOrder
            {
                OrderNumber = await NextOrderNumber(ct),
                AssetId = asset.Id,
                Priority = r.Priority,
                IssueDescription = r.IssueDescription,
                ReportedBy = User.Identity!.Name!
            };
            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync(ct);
                return CreatedAtAction(nameof(Get), new { id = order.Id }, ToDto(order, asset.AssetTag));
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // Two requests picked the same number (unique index on OrderNumber). Recompute and retry.
                db.Entry(order).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Resolves an order. If it was the asset's last open order and the asset is MaintenanceRequired,
    /// the asset goes back to Available.
    /// </summary>
    [HttpPatch("{id:int}/resolve")]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<IActionResult> Resolve(int id, ResolveOrderRequest r, CancellationToken ct)
    {
        var o = await db.Orders.FindAsync([id], ct);
        if (o is null) return NotFound();
        if (o.IsResolved) return Conflicted("Order is already resolved.");

        o.IsResolved = true;
        o.ResolvedAtUtc = DateTime.UtcNow;
        o.ResolutionRemarks = r.ResolutionRemarks;

        var otherOpen = await db.Orders.AnyAsync(x => x.AssetId == o.AssetId && !x.IsResolved && x.Id != o.Id, ct);
        if (!otherOpen)
        {
            var asset = await db.Assets.FindAsync([o.AssetId], ct);
            if (asset is { OperationalStatus: AssetStatuses.MaintenanceRequired })
                asset.OperationalStatus = AssetStatuses.Available;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Continues the seed script's format: WO-<year>-<5 digits>.
    private async Task<string> NextOrderNumber(CancellationToken ct)
    {
        var prefix = $"WO-{DateTime.UtcNow.Year}-";
        var last = await db.Orders.Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);

        var next = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:D5}";
    }
}
