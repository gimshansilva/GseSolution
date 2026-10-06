using Gse.Api.Data;
using Gse.Api.Domain;
using Gse.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Controllers;

[Route("api/categories")]
public class CategoriesController(GseDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken ct)
    {
        IReadOnlyList<CategoryDto> items = await db.Categories.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.CategoryCode, c.Name, c.Description))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id, CancellationToken ct)
    {
        var c = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? Missing("Category not found.") : new CategoryDto(c.Id, c.CategoryCode, c.Name, c.Description);
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest r, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(c => c.CategoryCode == r.CategoryCode, ct))
            return Conflicted("Category code already exists.");

        var e = new EquipmentCategory { CategoryCode = r.CategoryCode, Name = r.Name, Description = r.Description };
        db.Categories.Add(e);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = e.Id },
            new CategoryDto(e.Id, e.CategoryCode, e.Name, e.Description));
    }
}
