using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gse.Api.Tests;

public class ApiTests(GseApiFactory factory) : IClassFixture<GseApiFactory>
{
    private async Task<HttpClient> As(string username)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username, password = GseApiFactory.Password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static object NewOrder(int assetId, string priority = "High") =>
        new { assetId, priority, issueDescription = "Hydraulic pressure drop", reportedBy = "someone-else" };

    // ---------- Authentication ----------

    [Fact]
    public async Task Health_is_anonymous()
    {
        var res = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Assets_without_token_is_401()
    {
        var res = await factory.CreateClient().GetAsync("/api/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Garbage_token_is_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assets")).StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_401()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "engineer1", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_user_is_401()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "nobody", password = "whatever-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_returns_token_and_role()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = "viewer1", password = GseApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Viewer", body.GetProperty("role").GetString());
    }

    // ---------- Reads, paging, filters ----------

    [Fact]
    public async Task Viewer_can_list_assets_and_page_size_is_capped()
    {
        var client = await As("viewer1");
        var res = await client.GetAsync("/api/assets?pageSize=1000");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(6, body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invalid_status_filter_is_400()
    {
        var client = await As("viewer1");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/assets?status=Bogus")).StatusCode);
    }

    [Fact]
    public async Task Unknown_asset_is_404()
    {
        var client = await As("viewer1");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/assets/9999")).StatusCode);
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task Viewer_cannot_create_order_403()
    {
        var client = await As("viewer1");
        var res = await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetA));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Engineer_cannot_decommission_but_admin_can()
    {
        var engineer = await As("engineer1");
        var denied = await engineer.PatchAsJsonAsync($"/api/assets/{GseApiFactory.AssetD}/status",
            new { operationalStatus = "Decommissioned" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = await As("admin1");
        var ok = await admin.PatchAsJsonAsync($"/api/assets/{GseApiFactory.AssetD}/status",
            new { operationalStatus = "Decommissioned" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var asset = await admin.GetFromJsonAsync<JsonElement>($"/api/assets/{GseApiFactory.AssetD}");
        Assert.Equal("Decommissioned", asset.GetProperty("operationalStatus").GetString());
    }

    [Fact]
    public async Task Only_admin_can_create_category()
    {
        var body = new { categoryCode = "NEWCAT", name = "New category", description = "x" };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await As("engineer1")).PostAsJsonAsync("/api/categories", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await (await As("admin1")).PostAsJsonAsync("/api/categories", body)).StatusCode);
    }

    // ---------- Maintenance order rules ----------

    [Fact]
    public async Task Engineer_creates_order_and_reported_by_comes_from_token()
    {
        var client = await As("engineer1");
        var res = await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetA));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var order = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith($"WO-{DateTime.UtcNow.Year}-", order.GetProperty("orderNumber").GetString());
        Assert.Equal("engineer1", order.GetProperty("reportedBy").GetString()); // body value ignored
        Assert.False(order.GetProperty("isResolved").GetBoolean());
    }

    [Fact]
    public async Task Order_numbers_increase()
    {
        var client = await As("engineer1");
        var first = await (await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetE, "Low")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetE, "Low")))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(string.CompareOrdinal(second.GetProperty("orderNumber").GetString(),
                                          first.GetProperty("orderNumber").GetString()) > 0);
    }

    [Theory]
    [InlineData("Urgent", "Valid description")]
    [InlineData("High", "")]
    public async Task Invalid_order_is_400(string priority, string description)
    {
        var client = await As("engineer1");
        var res = await client.PostAsJsonAsync("/api/maintenance-orders",
            new { assetId = GseApiFactory.AssetA, priority, issueDescription = description });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Order_for_unknown_asset_is_404()
    {
        var client = await As("engineer1");
        var res = await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(9999));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Order_for_decommissioned_asset_is_409()
    {
        var client = await As("engineer1");
        var res = await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetDecommissioned));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Critical_order_flags_asset_and_resolving_clears_it_and_cannot_repeat()
    {
        var client = await As("engineer1");

        var created = await client.PostAsJsonAsync("/api/maintenance-orders", NewOrder(GseApiFactory.AssetB, "Critical"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var flagged = await client.GetFromJsonAsync<JsonElement>($"/api/assets/{GseApiFactory.AssetB}");
        Assert.Equal("MaintenanceRequired", flagged.GetProperty("operationalStatus").GetString());

        var resolve = await client.PatchAsJsonAsync($"/api/maintenance-orders/{id}/resolve",
            new { resolutionRemarks = "Fixed and tested." });
        Assert.Equal(HttpStatusCode.NoContent, resolve.StatusCode);

        var cleared = await client.GetFromJsonAsync<JsonElement>($"/api/assets/{GseApiFactory.AssetB}");
        Assert.Equal("Available", cleared.GetProperty("operationalStatus").GetString());

        var again = await client.PatchAsJsonAsync($"/api/maintenance-orders/{id}/resolve",
            new { resolutionRemarks = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Summary_endpoint_works()
    {
        var client = await As("viewer1");
        var res = await client.GetAsync("/api/reports/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}

public class LoginRateLimitTests(LimitedLoginFactory factory) : IClassFixture<LimitedLoginFactory>
{
    [Fact]
    public async Task Third_login_attempt_within_a_minute_is_429()
    {
        var client = factory.CreateClient();
        var bad = new { username = "engineer1", password = "wrong-password" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", bad)).StatusCode);
    }
}
