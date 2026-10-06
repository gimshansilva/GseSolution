namespace Gse.Api.Domain;

public static class Roles
{
    public const string Viewer = "Viewer";
    public const string Engineer = "Engineer";
    public const string Admin = "Admin";
    public static readonly string[] All = [Viewer, Engineer, Admin];
}

public static class AssetStatuses
{
    public const string Available = "Available";
    public const string InService = "InService";
    public const string MaintenanceRequired = "MaintenanceRequired";
    public const string Decommissioned = "Decommissioned";
    public static readonly string[] All = [Available, InService, MaintenanceRequired, Decommissioned];
}

public static class Priorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
    public static readonly string[] All = [Low, Medium, High, Critical];
}

public static class Policies
{
    public const string CanWrite = "CanWrite";
    public const string AdminOnly = "AdminOnly";
}
