using Gse.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Data;

/// <summary>
/// Development only. Makes sure viewer1 / engineer1 / admin1 exist and that their password matches
/// "Seed:Password" (user-secrets). Also clears any lockout. Does nothing if that secret is absent.
/// </summary>
public static class DevUserSeeder
{
    private const string CreateTableSql = """
        IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
        CREATE TABLE dbo.AppUsers
        (
            Id INT IDENTITY(1,1) NOT NULL,
            Username VARCHAR(50) NOT NULL,
            PasswordHash VARCHAR(500) NOT NULL,
            Role VARCHAR(20) NOT NULL,
            FailedLoginCount INT NOT NULL CONSTRAINT DF_AppUsers_Failed DEFAULT 0,
            LockoutEndUtc DATETIME2 NULL,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AppUsers_CreatedAtUtc DEFAULT GETUTCDATE(),
            CONSTRAINT PK_AppUsers PRIMARY KEY (Id),
            CONSTRAINT UQ_AppUsers_Username UNIQUE (Username),
            CONSTRAINT CK_AppUsers_Role CHECK (Role IN ('Viewer', 'Engineer', 'Admin'))
        );
        """;

    public static async Task SeedAsync(IServiceProvider sp, IConfiguration cfg)
    {
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DevUserSeeder");

        var password = cfg["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            log.LogWarning("Seed:Password is not set, so no dev users were created. Login will return 401. " +
                           "Run: dotnet user-secrets set \"Seed:Password\" \"<12+ chars>\" --project Gse.Api");
            return;
        }
        if (password.Length < 12)
        {
            log.LogWarning("Seed:Password must be at least 12 characters; skipping user seeding.");
            return;
        }

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GseDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();

        try
        {
            // Dev convenience: create the AppUsers table if sql/02_AppUsers.sql has not been run yet.
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(CreateTableSql);

            foreach (var (name, role) in new[]
                     { ("viewer1", Roles.Viewer), ("engineer1", Roles.Engineer), ("admin1", Roles.Admin) })
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Username == name);
                if (user is null)
                {
                    user = new AppUser { Username = name };
                    db.Users.Add(user);
                    user.PasswordHash = hasher.HashPassword(user, password);
                }
                else if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
                {
                    // Seed:Password changed since the user was created: bring the stored hash in line.
                    user.PasswordHash = hasher.HashPassword(user, password);
                }

                user.Role = role;
                user.FailedLoginCount = 0;
                user.LockoutEndUtc = null;
            }
            await db.SaveChangesAsync();
            log.LogInformation("Dev users ready: viewer1, engineer1, admin1 (password = Seed:Password).");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not seed dev users. Does the GseOperationsDb database exist (seed script run)?");
        }
    }
}
