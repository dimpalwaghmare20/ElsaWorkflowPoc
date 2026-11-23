# Elsa Workflow POC

A small proof of concept for [Elsa Workflows 3](https://docs.elsaworkflows.io/) on .NET 10.
One ASP.NET Core app hosts both:

- **Elsa Server**: the workflow engine, REST API, and HTTP-triggered workflows. It stores data in SQLite.
- **Elsa Studio**: the browser-based visual designer and monitoring dashboard (Blazor WebAssembly).

## What it shows

| Capability | Where |
|---|---|
| Workflows written in C# (code-first) | [`Workflows/`](src/ElsaPoc.Server/Workflows) |
| Workflows triggered by HTTP requests | `GET /api/workflows/hello`, `POST /api/workflows/expenses` |
| Branching business rules | Expense Approval: claims of 1000 or less are approved automatically |
| Long-running, human-in-the-loop processes | Expense Approval saves its state and waits for an approve/reject event |
| Custom activity (shows up in the Studio toolbox) | [`NotifyStakeholder`](src/ElsaPoc.Server/Activities/NotifyStakeholder.cs) |
| Visual designer, versioning, and instance monitoring | Elsa Studio at http://localhost:5051 |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```powershell
cd src/ElsaPoc.Server
dotnet run
```

Open **http://localhost:5051** and sign in with `admin` / `Demo@12345`.
These are local demo credentials from `appsettings.Development.json`. Change them before deploying anywhere.

## Oil & gas procurement UI

Open **http://localhost:5051/procurement** for the Field Supply dashboard (no Studio login needed).
Submit an equipment/spares request with requester, email, site, estimated cost in USD, and justification.
The new **Equipment & Spares Procurement** Elsa workflow auto-approves costs up to USD 1,000
(an illustrative demo rule, not an industry policy). Larger requests suspend until you click
**Approve purchase** or **Reject purchase** and confirm the decision. Use **Refresh** to read
persisted workflow state, including after an application restart. The original expense workflows
and Studio at `/` remain unchanged.

The dashboard and `/demo/purchases/*` review APIs are unauthenticated, local-demo-only.
Notifications are logged, not emailed; no live procurement or operational safety decisions are made.
Requests and decisions live in Elsa's SQLite workflow state, not browser storage.

With the app running, run `.\tests\Test-Procurement.ps1` from the solution folder to check
thresholds, both manager decisions, persisted details, and API validation.
The checks create synthetic requests in the local demo database.

## Demo script (about 10 minutes)

Use [`demo.http`](demo.http) in VS Code (REST Client extension) or Visual Studio, or use the curl commands below.

1. **Hello World**: open http://localhost:5051/api/workflows/hello?name=Team in a browser. An Elsa workflow produces the response.
2. **Auto-approved expense**:
   ```powershell
   curl -X POST http://localhost:5051/api/workflows/expenses -H "Content-Type: application/json" -d '{"employee":"Asha","email":"asha@example.com","amount":250,"description":"Team lunch"}'
   ```
   The response returns `AutoApproved` right away.
3. **Expense that needs approval**: send the same request with `"amount": 4500`. The response returns `PendingApproval` with `approveUrl` and `rejectUrl`.
   - In Studio, go to **Workflows → Instances**. The instance is **Suspended**. Its state is saved in the database, so it survives an app restart.
   - Approve it: `curl -X POST http://localhost:5051/demo/expenses/{id}/approve`
   - Check its status: `curl http://localhost:5051/demo/expenses/{id}`. The status is `Finished`. The console shows the notifications.
4. **Visual designer**: in Studio, open **Workflows → Definitions** and select **Create workflow**. Drag in *HTTP Endpoint → Write Line → Write HTTP Response*, publish it, and call it. You get a new API without writing code. The custom **NotifyStakeholder** activity is in the toolbox under *ElsaPoc*.

## Project layout

```
src/
  ElsaPoc.Server/          ASP.NET Core host (Elsa engine + API + serves Studio)
    Program.cs             Elsa configuration (persistence, identity, HTTP, scheduling, JS/Liquid)
    Workflows/             Code-first workflows
    Activities/            Custom activities
    Endpoints/             Demo helper endpoints (approve/reject/status)
    Pages/_Host.cshtml     Host page for Elsa Studio
  ElsaPoc.Studio/          Elsa Studio Blazor WebAssembly client
```

## Notes

- Data is stored in `src/ElsaPoc.Server/elsa.sqlite.db`. Delete the file to reset. You can switch to SQL Server or PostgreSQL by changing the persistence provider.
- If `Identity:Tokens:SigningKey` is not configured, the app creates a random signing key on each start. Sign in to Studio again after a restart.
- The `/demo/*` endpoints are unauthenticated for demo purposes only.
