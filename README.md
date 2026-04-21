## Environment Analysis App

ASP.NET Core 8 MVC + Identity project for managing environmental emission sources. Authentication now relies on JWT bearer tokens stored in a secure cookie, so every dev needs local configuration before running the site.

### Prerequisites
- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
- SQL Server (localdb or full instance)
- Optional: `dotnet-ef` global tool for migrations

### Local Setup
1. **Restore dependencies**  
   ```bash
   dotnet restore env-analysis-project/env-analysis-project.csproj
   ```
2. **Create your `appsettings.json`** (ignored by git):  
   - Windows PowerShell  
     ```powershell
     Copy-Item env-analysis-project/appsettings.template.json env-analysis-project/appsettings.json
     ```
   - macOS/Linux  
     ```bash
     cp env-analysis-project/appsettings.template.json env-analysis-project/appsettings.json
     ```
3. **Update secrets** in the new `appsettings.json`:  
   - connection string `env_analysis_projectContext` pointing to your SQL Server.  
   - `Jwt:Key` should be a long random string (32+ chars).  
   Consider storing them via `dotnet user-secrets` or environment variables if you don’t want them on disk.
4. **Apply migrations** (creates the Identity schema plus domain tables):  
   ```bash
   dotnet ef database update --project env-analysis-project/env-analysis-project.csproj --startup-project env-analysis-project/env-analysis-project.csproj
   ```
5. **Run the app**  
   ```bash
   dotnet run --project env-analysis-project/env-analysis-project.csproj
   ```

### Docker Deployment
1. Copy env file:
   ```bash
   cp .env.example .env
   ```
   On PowerShell:
   ```powershell
   Copy-Item .env.example .env
   ```
2. (Optional) Update `.env` with your own `SA_PASSWORD` and `JWT_KEY`.
3. Start the stack:
   ```bash
   docker compose up -d --build
   ```
4. Open app at `http://localhost:8080`.

Notes:
- SQL Server runs in container `env-analysis-db` on host port `14333`.
- The web app runs in container `env-analysis-web`.
- Migrations are applied automatically on app startup (`Database.MigrateAsync()` in `Program.cs`).
- Stop stack:
  ```bash
  docker compose down
  ```
- Stop and remove DB data volume:
  ```bash
  docker compose down -v
  ```

### Default admin account
On startup the app runs a Bogus-powered seeder that ensures a ready-made administrator exists:
- Email: `admin@gmail.com`
- Password: `12345678`

Bogus only randomizes friendly profile info (full name, phone) so everyone shares the same credentials for the first login. Change the password immediately in production.

### JWT storage & request flow
- The login page issues a JWT using our custom `LoginModel` and writes it into an HTTP-only cookie named `AccessToken` (`JwtDefaults.AccessTokenCookieName`) via `SetAccessTokenCookie`.
- The browser never exposes this cookie to JavaScript directly; instead, we expose a sanitized token value through `window.authState.accessToken` (see `_Layout.cshtml`) so client scripts can send it when calling APIs.
- `wwwroot/js/jwt-fetch.js` wraps the native `fetch` API, automatically attaching `Authorization: Bearer <token>` headers and redirecting to `/Identity/Account/Login` if a request ever receives `401`.
- On the server, `AccessTokenForwardingMiddleware` reads the same cookie for non-AJAX cases and injects the Authorization header before the JWT bearer handler runs. Controllers then rely on the standard `[Authorize]` attribute, so any request without a valid token gets challenged.
- Logging out deletes the cookie in `LogoutModel`, which effectively removes the JWT everywhere (browser and server).

### Notes
- Because `appsettings*.json` is git-ignored, each developer is responsible for their own secrets.
- If you remove the seeded account from the database it will reappear on the next run unless you update the credentials or disable the seeder in `IdentityDataSeeder`.

### Project report
**Goal**  
This project digitizes environmental monitoring and management (emission sources, parameters, measurement approvals) and provides visual insights through dashboards, trend charts, and threshold alerts.

**Scope / key features**
- Manage emission sources, source types, parameters, and measurement results.
- Role-based access using Identity + JWT, plus user activity logging.
- Trend visualization over time with source/parameter comparisons and detailed tables.
- Bulk import with validation before committing data.

**Architecture**
- ASP.NET Core 8 MVC application with layered concerns: Controllers → Services → EF Core data access.
- SQL Server as the primary store; Identity tables share the same DbContext.
- JWT auth stored in secure cookies, forwarded to APIs via middleware and client fetch wrappers.
- Razor views + JavaScript (ApexCharts) for charts and data grids.
- ML.NET is used for forecasting and anomaly detection in the Alerts & Forecasts flow.

**Core data models**
- `EmissionSource`: emission source master data (type, location, status).
- `SourceType`: categories for emission sources.
- `Parameter`: monitored parameter definitions (unit, standard value, type).
- `MeasurementResult`: time-series measurements linked to source + parameter, with approval flags.
- `ApplicationUser`: Identity user with role-based access.
- `UserActivityLog`: audit trail of user actions.
- Supporting models: `PollutionData`, `PredictionResult`, `PredictionRow`, `FutureForecast` for ML processing and outputs.

**Current status**
- Authentication, authorization, and environmental data management are stable.
- Alerts & Forecasts provide trend lines, limit overlays, and model predictions.
- Data is organized by source and parameter for reporting and analysis.

**Limitations & next steps**
- Standardize data intake formats and units from multiple sources.
- Add KPI dashboards and real-time alerting.
- Enhance ML.NET forecasting with seasonality/exogenous features and stronger evaluation metrics.
