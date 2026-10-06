using Gse.Api.Data;
using Gse.Api.Domain;
using Gse.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Controllers;

[Route("api/assets")]
public class AssetsController(GseDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetDto>>> List(
        string? status, int? categoryId, string? search,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (status is not null && !AssetStatuses.All.Contains(status))
            return BadReq($"Invalid status. Allowed: {string.Join(", ", AssetStatuses.All)}.");

        (page, pageSize) = Clamp(page, pageSize);

        var q = db.Assets.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(a => a.OperationalStatus == status);
        if (categoryId is not null) q = q.Where(a => a.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(a => a.AssetTag.Contains(search.Trim()));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AssetDto(a.Id, a.AssetTag, a.CategoryId, a.Category.Name, a.OperationalStatus,
                                      a.TotalOperatingHours, a.LastServicedDate))
            .ToListAsync(ct);

        return new PagedResult<AssetDto>(items, page, pageSize, total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetDto>> Get(int id, CancellationToken ct)
    {
        var a = await db.Assets.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null
            ? Missing("Asset not found.")
            : new AssetDto(a.Id, a.AssetTag, a.CategoryId, a.Category.Name, a.OperationalStatus,
                           a.TotalOperatingHours, a.LastServicedDate);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<ActionResult<AssetDto>> Create(CreateAssetRequest r, CancellationToken ct)
    {
        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CategoryId, ct);
        if (category is null) return BadReq("Category does not exist.");

        if (await db.Assets.AnyAsync(a => a.AssetTag == r.AssetTag, ct))
            return Conflicted("Asset tag already exists.");

        var e = new EquipmentAsset
        {
            AssetTag = r.AssetTag,
            CategoryId = category.Id,
            OperationalStatus = AssetStatuses.Available,
            TotalOperatingHours = r.TotalOperatingHours
        };
        db.Assets.Add(e);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = e.Id },
            new AssetDto(e.Id, e.AssetTag, category.Id, category.Name, e.OperationalStatus,
                         e.TotalOperatingHours, e.LastServicedDate));
    }

    /// <summary>Engineers can change status; only Admins can decommission or reactivate.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<IActionResult> SetStatus(int id, UpdateStatusRequest r, CancellationToken ct)
    {
        var a = await db.Assets.FindAsync([id], ct);
        if (a is null) return NotFound();

        var touchesDecommission = r.OperationalStatus == AssetStatuses.Decommissioned
                                  || a.OperationalStatus == AssetStatuses.Decommissioned;
        if (touchesDecommission && !User.IsInRole(Roles.Admin)) return Forbid();

        a.OperationalStatus = r.OperationalStatus;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
