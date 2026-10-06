using Gse.Api.Data;
using Gse.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Gse.Api.Tests;

public class GseApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test-Password-123!";

    // Seeded asset ids (InMemory assigns ids 1..n in insertion order).
    public const int AssetA = 1, AssetB = 2, AssetC = 3, AssetD = 4, AssetE = 5, AssetDecommissioned = 6;

    private readonly string _dbName = Guid.NewGuid().ToString();

    protected virtual Dictionary<string, string> Settings => new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Key", new string('k', 48));
        builder.UseSetting("Jwt:Issuer", "test-issuer");
        builder.UseSetting("Jwt:Audience", "test-audience");
        builder.UseSetting("RateLimit:Api:PermitLimit", "100000");
        builder.UseSetting("RateLimit:Login:PermitLimit", "100000");
        foreach (var kv in Settings) builder.UseSetting(kv.Key, kv.Value);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GseDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<GseDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Seed(host.Services);
        return host;
    }

    private static void Seed(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GseDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();

        var tug = new EquipmentCategory { CategoryCode = "TUG", Name = "Aircraft Pushback Tug" };
        db.Categories.Add(tug);

        for (var i = 1; i <= 5; i++)
            db.Assets.Add(new EquipmentAsset { AssetTag = $"GSE-TUG-000{i}", Category = tug });
        db.Assets.Add(new EquipmentAsset
        {
            AssetTag = "GSE-TUG-0006", Category = tug, OperationalStatus = AssetStatuses.Decommissioned
        });

        foreach (var (name, role) in new[]
                 { ("viewer1", Roles.Viewer), ("engineer1", Roles.Engineer), ("admin1", Roles.Admin) })
        {
            var u = new AppUser { Username = name, Role = role };
            u.PasswordHash = hasher.HashPassword(u, Password);
            db.Users.Add(u);
        }
        db.SaveChanges();
    }
}

/// <summary>Same app, but with a tiny login rate limit so the 429 path can be tested.</summary>
public class LimitedLoginFactory : GseApiFactory
{
    protected override Dictionary<string, string> Settings => new() { ["RateLimit:Login:PermitLimit"] = "2" };
}
