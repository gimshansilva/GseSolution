using Gse.Api.Domain;

namespace Gse.Api.Data;

public class EquipmentCategory
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<EquipmentAsset> Assets { get; set; } = [];
}

public class EquipmentAsset
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = "";
    public int CategoryId { get; set; }
    public EquipmentCategory Category { get; set; } = null!;
    public string OperationalStatus { get; set; } = AssetStatuses.Available;
    public decimal TotalOperatingHours { get; set; }
    public DateTime? LastServicedDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<MaintenanceOrder> Orders { get; set; } = [];
}

public class MaintenanceOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int AssetId { get; set; }
    public EquipmentAsset Asset { get; set; } = null!;
    public string Priority { get; set; } = Priorities.Low;
    public string IssueDescription { get; set; } = "";
    public string ReportedBy { get; set; } = "";
    public bool IsResolved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionRemarks { get; set; }
}

/// <summary>API login account. Created by sql/02_AppUsers.sql (not part of the original seed script).</summary>
public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
