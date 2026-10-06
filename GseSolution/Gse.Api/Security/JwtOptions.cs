using System.ComponentModel.DataAnnotations;

namespace Gse.Api.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)] public string Key { get; set; } = "";
    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";
    [Range(1, 120)] public int AccessTokenMinutes { get; set; } = 15;
}
