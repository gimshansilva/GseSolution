# GSE Operations API (.NET 8)

Secure REST API over the `GseOperationsDb` database (equipment categories, assets, maintenance orders).
JWT auth with Viewer / Engineer / Admin roles, validation, rate limiting, CORS allow-list, security headers
and an xUnit integration test project.

## 1. Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB, Express or full)
- VS Code with: C# Dev Kit, SQL Server (mssql), REST Client (recommended in `.vscode/extensions.json`)

## 2. Database
Run in this order with the mssql extension (Ctrl+Shift+E on each file):
1. Your original seed script (`TBL Creation & Data.txt`, saved as `.sql`)
2. `sql/02_AppUsers.sql` - login table used by `/api/auth/login` (in Development the API also creates it automatically on startup)
3. `sql/03_LeastPrivilegeLogin.sql` (optional) - SQL login with no DELETE/DDL rights. Edit the password first.

## 3. Configuration and secrets
`Gse.Api/appsettings.Development.json` already points at **LocalDB** (`(localdb)\MSSQLLocalDB`, Windows auth), so
no connection secret is needed on a LocalDB machine. For any other server, override it (user-secrets win over the file):

```powershell
dotnet user-secrets set "ConnectionStrings:Gse" "Server=.\SQLEXPRESS;Database=GseOperationsDb;Trusted_Connection=True;TrustServerCertificate=True" --project Gse.Api
```

The two secrets you must always set (never commit them):

```powershell
$b = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($b)) --project Gse.Api
dotnet user-secrets set "Seed:Password" "<12+ char password for the dev users>" --project Gse.Api
```

`Seed:Password` is read **in Development only**. On startup it creates `viewer1`, `engineer1` and `admin1`
(if missing) with that password. In production, create users another way and never set `Seed:Password`.

## 4. Run
- Open the `GseSolution` folder in VS Code, press **F5** (Gse.Api profile). Swagger opens at `https://localhost:7001/swagger`.
- Or `dotnet run --project Gse.Api` / `dotnet watch --project Gse.Api`.
- First time only: `dotnet dev-certs https --trust`.
- Try requests from `api.http` (REST Client).

## 5. Test
```bash
dotnet test
```
Tests use the EF Core in-memory provider and do not touch your SQL Server.
They cover 401 / 403 / 404 / 409 / 400 / 429 paths as well as the happy paths.

## Getting 401 Unauthorized?
- Opening `/api/...` in the browser address bar always gives 401: the browser can't send the token. Use Swagger or `api.http`.
- In Swagger: run `POST /api/auth/login`, copy `accessToken`, click **Authorize**, paste the token only (no `Bearer`).
- Login returns 401 "Invalid username or password."? Check the console on startup. In Development it prints either
  `Dev users ready` or why seeding was skipped (missing `Seed:Password`, or `AppUsers` table missing).
- Tokens last 15 minutes. Changing `Jwt:Key` invalidates old tokens: restart and log in again.

## Endpoints
| Method | Route | Role |
|---|---|---|
| POST | `/api/auth/login` | anonymous (5/min per IP) |
| GET | `/health` | anonymous |
| GET | `/api/categories`, `/api/categories/{id}` | any signed-in user |
| POST | `/api/categories` | Admin |
| GET | `/api/assets?status=&categoryId=&search=&page=&pageSize=` | any signed-in user |
| GET | `/api/assets/{id}` | any signed-in user |
| POST | `/api/assets` | Engineer, Admin |
| PATCH | `/api/assets/{id}/status` | Engineer, Admin (Decommissioned in or out: Admin only) |
| GET | `/api/maintenance-orders?isResolved=&priority=&assetId=&page=&pageSize=` | any signed-in user |
| GET | `/api/maintenance-orders/{id}` | any signed-in user |
| POST | `/api/maintenance-orders` | Engineer, Admin |
| PATCH | `/api/maintenance-orders/{id}/resolve` | Engineer, Admin |
| GET | `/api/reports/summary` | any signed-in user |

## Business rules
- Orders cannot be opened on Decommissioned assets (409).
- `ReportedBy` is taken from the token, never from the request body.
- A **Critical** order moves an Available/InService asset to MaintenanceRequired.
- Resolving an asset's last open order moves a MaintenanceRequired asset back to Available.
- Order numbers continue the seed format `WO-<year>-<5 digits>`; a unique-index collision is retried up to 2 times.

## Security notes
- Fallback authorization policy: every endpoint requires a signed-in user unless marked `[AllowAnonymous]`.
- JWT: issuer, audience, lifetime and signature validated; HS256 only; 15 minute tokens.
- Login: PBKDF2 hashes (ASP.NET Core `PasswordHasher`), 5 failed attempts locks the account for 15 minutes,
  identical 401 for unknown user / bad password / locked account.
- DTOs only (no over-posting), FluentValidation on every write, EF parameterized queries.
- Rate limits: 100/min per user (or IP), 5/min per IP on login. Both are configurable under `RateLimit` in `appsettings.json`.
- 100 KB request body cap, page size capped at 100, `ProblemDetails` errors with no stack traces.
- CORS allow-list from `Cors:Origins`, security headers, HSTS outside Development, no `Server` header.
- Swagger is mapped in Development only.

## Before deploying
- Set `AllowedHosts` to your real host name(s) (it is `localhost` here).
- Put `Cors:Origins` in config for your real front end.
- Supply `ConnectionStrings:Gse` and `Jwt:Key` through environment variables or a key vault.
- If you run behind a reverse proxy or several instances, configure forwarded headers (so the IP rate limit sees real client IPs) and a shared rate-limit store.
- Add refresh tokens only if you need long sessions.

## Differences from the earlier walkthrough
- Entities are hand-written (`Data/Entities.cs`), so `dotnet ef dbcontext scaffold` is not needed.
- Validation uses a small action filter plus `FluentValidation` instead of the deprecated `FluentValidation.AspNetCore` package.
- Login uses a real `AppUsers` table with hashed passwords and lockout, instead of a hard-coded list.
