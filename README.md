# Distributed Prime Sieve System

## One-time 56-bit search

For the revised one-time cloud goal, use the standalone `Seive2.OneShot` project
and the separate deployment preparation in `infra\oneshot-README.md`. It does
not require the API, SQL, a private network, or the legacy infrastructure below.
The original distributed system is retained for existing workflows.

A distributed prime number sieve that distributes work across multiple computers using REST API coordination.

## Architecture

- **Server (Seive2)**: ASP.NET Core Web API that coordinates work distribution
- **Worker (Seive2.Worker)**: Console application that processes prime sieve segments

## Quick Start

Before starting the API, create the Entra app roles, deploy or connect to Azure
SQL, and apply `infra/schema.sql` as described below. Configure the API's SQL
connection, tenant, and audience; unauthenticated quick-start usage is no longer
supported.

### 1. Start the Server (Coordinator)

On one machine that will act as the coordinator:

```bash
cd Seive2
dotnet run
```

The application listens on HTTP port 5000; its Azure Container Apps ingress
terminates HTTPS. Local HTTP is only suitable for loopback development. Do not
send bearer tokens over an unencrypted network connection.

### 2. Start Workers on Each Workstation

Set `SIEVE_API_SCOPE` and authenticate using an identity with the worker role.
For loopback development, run `dotnet run --project Seive2.Worker -- http://localhost:5000`.
For remote workers, supply the coordinator's HTTPS URL; HTTP is rejected except
on loopback.

### 3. Start a Sieve Job

The server does not start a job automatically by default. Start a deliberate
validation job manually; jobs always begin at 2 and do not erase the existing
largest-prime record.

Use an operator token for API calls, and replace the example HTTPS URL with the
private coordinator URL. In PowerShell, first get a token:

```powershell
$token = (az account get-access-token --scope api://<api-client-id>/.default | ConvertFrom-Json).accessToken
$body = @{
	MaxNumber = 1000000000
	SegmentSize = 10000000
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://<private-coordinator-host>/api/job/start" `
	-Method POST `
	-Headers @{ Authorization = "Bearer $token" } `
	-Body $body `
	-ContentType "application/json"
```

Or using curl:

```bash
curl -X POST 'https://<private-coordinator-host>/api/job/start' \
  -H "Authorization: Bearer <OPERATOR_ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"MaxNumber": 1000000000, "SegmentSize": 10000000}'
```

### 4. Monitor Progress

```powershell
Invoke-RestMethod -Uri "https://<private-coordinator-host>/api/job/status" `
	-Headers @{ Authorization = "Bearer $token" }
```

Or using curl:

```bash
curl 'https://<private-coordinator-host>/api/job/status' \
  -H "Authorization: Bearer <OPERATOR_ACCESS_TOKEN>"
```

## API Endpoints

### Server Endpoints

Except for health checks, endpoints require a Microsoft Entra bearer token with
the appropriate app role. Job APIs require `Sieve.Operator`; work APIs require
`Sieve.Worker`.

#### Start a Job
- **POST** `/api/job/start`
- Body: `{ "MaxNumber": 1000000000, "SegmentSize": 10000000 }`
- Returns: Job information

#### Get Job Status
- **GET** `/api/job/status`
- Returns: Current job progress and statistics

#### Health Check
- **GET** `/api/job/health`
- Returns: Server health status

### Worker Endpoints (Internal)
- **POST** `/api/work/next` - Get next work segment
- **POST** `/api/work/complete` - Submit completed work

## Configuration

### Server Settings

Set these values in the deployment environment, not in source control:

- `ConnectionStrings__WorkDatabase`: Azure SQL connection string using
  `Authentication=Active Directory Default`, `Encrypt=True`, and
  `TrustServerCertificate=False`; grant the API managed identity database access.
- `Authentication__TenantId`: Microsoft Entra tenant ID.
- `Authentication__Audience`: API app registration's audience / client ID.
- `AutoStartJob__Enabled`: defaults to `false`.

Create an Entra API app registration with app roles `Sieve.Operator` and
`Sieve.Worker`. The API validates the tenant and audience, requires the operator
role for job endpoints and the worker role for work endpoints. Assign the worker
managed identity the worker role, and assign operators the operator role. The
worker requires `SIEVE_API_SCOPE=api://<api-client-id>/.default` and obtains its
token with `DefaultAzureCredential`.

Job configuration, segment assignments, and results are stored in SQL so they
survive coordinator restarts. Segments are created as workers request them.
Expired assignments are requeued after ten minutes; the worker defaults to one
segment per batch. Every assignment has a unique lease ID, so a stale result
cannot complete a reassigned segment even when workers share a managed identity.
The API validates the schema at startup but does not create
it. Apply `infra/schema.sql` as the Azure SQL Entra administrator using SQLCMD
variables `ApiIdentityName` and `ExistingLargestPrime`. The script imports the
existing largest-prime watermark and grants the API identity table-level data
permissions; the runtime identity does not need DDL rights.

Although endpoints now require Entra roles, deploy the API behind private
network ingress until the full Azure network and operator-access setup has been
reviewed. A real Azure SQL integration test and restart test are still required.

### Worker Settings
- Coordinator URL: command-line argument or `SIEVE_SERVER_URL`
- API token scope: required `SIEVE_API_SCOPE`
- Worker ownership: derived from the authenticated Entra identity
- Segment batch size defaults to one; assignments expire after ten minutes

## Algorithm

Uses **Segmented Sieve of Eratosthenes**:
- Efficient for distributed computation
- Divides range into segments
- Each worker processes independent segments
- Scales well across multiple machines

## Azure Deployment and Remaining Work

See `infra\README.md` for the staged deployment, access prerequisites, cost
considerations, and validation checklist. The Bicep templates have only been
compiled locally; they have not been deployed or verified in Azure.

- Azure deployment and cost review
- End-to-end SQL/restart tests and a small cloud validation run
- Operator sign-in experience and managed-identity role assignment automation
