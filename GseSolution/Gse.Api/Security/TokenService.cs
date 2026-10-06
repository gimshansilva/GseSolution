using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gse.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gse.Api.Security;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) Create(AppUser user);
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTime ExpiresAtUtc) Create(AppUser user)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("name", user.Username),
            new Claim("role", user.Role)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, notBefore: now, expires: expires,
                                       signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
