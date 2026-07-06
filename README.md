# Dealer Incentive Management Portal

ASP.NET Core MVC (.NET 10), SQL Server, EF Core, Bootstrap 5, jQuery/AJAX, layered services, repository pattern, JWT APIs, audit logging, branch isolation, and Tally integration-ready service boundaries.

## Run From VS Code

1. Install .NET 10 SDK and SQL Server.
2. Update `appsettings.json` connection string.
3. Open this folder in VS Code.
4. Run:

```powershell
dotnet restore
dotnet build
dotnet run --project IncentivePortal.csproj
```

5. Open the displayed local URL.
6. Seed admin login: `admin` / `Admin@123`.

## Database

Option A: let the app create the database using `EnsureCreated` during first run.

Option B: run SQL scripts:

```powershell
sqlcmd -S . -E -i SQL\001_CreateSchema.sql
sqlcmd -S . -E -i SQL\002_ReportViews.sql
```

## Main Modules

- Authentication with PBKDF2 password hashing, cookie login, and JWT login API.
- Role based authorization: Super Admin, HO Finance, Branch Manager, Associate, Auditor.
- Branch and party master.
- Bank change approval workflow with duplicate account prevention.
- Dynamic versioned scheme slabs with overlap validation.
- Excel monthly sales preview/import structure through ClosedXML.
- Incentive calculation with outstanding adjustment and transfer entry creation.
- NEFT/RTGS transfer tracking and UTR reconciliation.
- Audit logs with old/new values, user, date, and IP address.
- Reports for incentive register, outstanding adjustment, pending approvals, transfer-ready data.
- Tally XML integration-ready service placeholders.

## API Endpoints

- `POST /api/auth/login`
- `GET /api/dashboard`
- `GET /api/parties`

Send JWT token as:

```http
Authorization: Bearer <token>
```

## IIS Deployment

1. Install .NET 10 Hosting Bundle on the IIS server.
2. Publish:

```powershell
dotnet publish -c Release -o .\publish
```

3. Create an IIS site pointing to `publish`.
4. Set the app pool to `No Managed Code`.
5. Configure production values in IIS environment variables:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
SeedData__Enabled=false
```

6. Grant the app pool identity SQL access and write permission to `Logs`.

## Production Notes

- Replace the development JWT key before deployment.
- Disable seed data in production.
- Add SMTP/SMS implementation for forgot password.
- Add DataTables, Excel/PDF exporter packages or server-side export endpoints where required by your reporting policy.
- Add background jobs for scheduled Tally sync and monthly calculation locking.
