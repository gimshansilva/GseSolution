using System.Text;
using System.Threading.RateLimiting;
using Gse.Api.Data;
using Gse.Api.Domain;
using Gse.Api.Infrastructure;
using Gse.Api.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------- Data ----------
builder.Services.AddDbContext<GseDbContext>((sp, o) =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Gse")
             ?? throw new InvalidOperationException("ConnectionStrings:Gse is not set. See README step 3.");
    o.UseSqlServer(cs, sql =>
    {
        // Fail fast while developing so a bad connection string is obvious; retry transient faults elsewhere.
        if (!builder.Environment.IsDevelopment()) sql.EnableRetryOnFailure();
    });
});

// ---------- MVC, validation, errors ----------
builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// ---------- Swagger (Development only is mapped below) ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GSE Operations API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ---------- Authentication ----------
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        var j = jwt.Value;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidIssuer = j.Issuer,
            ValidAudience = j.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// ---------- Authorization: secure by default ----------
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy(Policies.CanWrite, p => p.RequireRole(Roles.Engineer, Roles.Admin));
    o.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
});

// ---------- Rate limiting (limits come from config so tests can override) ----------
builder.Services.AddRateLimiter(o => o.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
builder.Services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((o, cfg) =>
{
    var apiLimit = cfg.GetValue("RateLimit:Api:PermitLimit", 100);
    var loginLimit = cfg.GetValue("RateLimit:Login:PermitLimit", 5);

    static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "anon";

    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = apiLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- CORS allow-list ----------
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((o, cfg) =>
{
    var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? [];
    o.AddPolicy("web", p => p
        .WithOrigins(origins)
        .WithMethods("GET", "POST", "PATCH")
        .WithHeaders("Authorization", "Content-Type"));
});

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 100_000;
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();      // ProblemDetails, no stack traces
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    if (!ctx.Request.Path.StartsWithSegments("/swagger"))
        h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    // Must come BEFORE UseAuthorization: the fallback policy would otherwise return 401 for the Swagger UI.
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("web");
app.UseAuthentication();
app.UseRateLimiter();           // after authentication so the limiter can key on the user
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    await DevUserSeeder.SeedAsync(app.Services, app.Configuration);

app.MapHealthChecks("/health").AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }   // lets the test project use WebApplicationFactory<Program>
