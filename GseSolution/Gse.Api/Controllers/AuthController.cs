using Gse.Api.Data;
using Gse.Api.Models;
using Gse.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gse.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    GseDbContext db,
    IPasswordHasher<AppUser> hasher,
    ITokenService tokens,
    TimeProvider clock) : ControllerBase
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Used to spend the same time hashing when the username does not exist (blunts user enumeration by timing).
    private static readonly AppUser Dummy = new();
    private static readonly string DummyHash = new PasswordHasher<AppUser>().HashPassword(Dummy, "dummy-password-for-timing");

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == r.Username, ct);

        if (user is null)
        {
            hasher.VerifyHashedPassword(Dummy, DummyHash, r.Password);
            return Fail();
        }

        if (user.LockoutEndUtc > now) return Fail();

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailures)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct);
            return Fail();
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, r.Password);
        await db.SaveChangesAsync(ct);

        var (token, expires) = tokens.Create(user);
        return new LoginResponse(token, expires, user.Role);
    }

    // Same response for unknown user, bad password and locked account.
    private ActionResult Fail() =>
        Unauthorized(new ProblemDetails { Title = "Invalid username or password.", Status = StatusCodes.Status401Unauthorized });
}
