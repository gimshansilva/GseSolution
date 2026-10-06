namespace Gse.Api.Models;

public record CategoryDto(int Id, string CategoryCode, string Name, string? Description);
public record CreateCategoryRequest(string CategoryCode, string Name, string? Description);

public record AssetDto(int Id, string AssetTag, int CategoryId, string Category, string OperationalStatus,
                       decimal TotalOperatingHours, DateTime? LastServicedDate);
public record CreateAssetRequest(string AssetTag, int CategoryId, decimal TotalOperatingHours);
public record UpdateStatusRequest(string OperationalStatus);

public record OrderDto(int Id, string OrderNumber, int AssetId, string AssetTag, string Priority,
                       string IssueDescription, string ReportedBy, bool IsResolved,
                       DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, string? ResolutionRemarks);
public record CreateOrderRequest(int AssetId, string Priority, string IssueDescription);
public record ResolveOrderRequest(string ResolutionRemarks);

public record LoginRequest(string Username, string Password);
public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string Role);

public record SummaryDto(int TotalAssets, int OpenOrders,
                         IReadOnlyDictionary<string, int> AssetsByStatus,
                         IReadOnlyDictionary<string, int> OpenOrdersByPriority);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
