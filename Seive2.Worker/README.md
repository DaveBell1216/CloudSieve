# Distributed Prime Sieve - Worker Configuration

## Running the Worker

The worker accepts the server URL as a command-line argument:

```powershell
dotnet run --project Seive2.Worker -- "https://<private-coordinator-host>"
```

Replace the placeholder with the private HTTPS coordinator URL.

## Configuration

The command-line URL takes precedence over `SIEVE_SERVER_URL`; if neither is
provided, the worker uses `http://localhost:5000`. The coordinator requires an
Entra `Sieve.Worker` role token and a configured SQL database.

| Environment variable | Default | Description |
| --- | --- | --- |
| `SIEVE_SERVER_URL` | `http://localhost:5000` | Absolute HTTPS coordinator URL; HTTP is allowed only for loopback. |
| `SIEVE_WORKER_ID` | Machine name | Unique ID for this worker instance. |
| `SIEVE_MAX_SEGMENTS` | `10000` | Positive number of successful submissions before exit. |
| `SIEVE_BATCH_SIZE` | `1` | Segments requested per batch, from 1 to 10000. |
| `SIEVE_API_SCOPE` | Required | API scope, for example `api://<api-client-id>/.default`. |

The worker obtains an Entra token with `DefaultAzureCredential`; in Azure, assign
its managed identity the `Sieve.Worker` app role on the API registration. Locally,
use `az login` with an identity assigned that role. The API derives worker
ownership from the token's object ID, not `SIEVE_WORKER_ID` in the request body.

The default one-segment batch limits lease duration. Assignments older than ten
minutes can be retried, and the lease is not renewed; choose a segment size that
finishes well within ten minutes. The worker never requests more segments than
its remaining quota.

## Network Requirements

- Workers must have private HTTPS access to the coordinator ingress.
- The coordinator container listens on HTTP port 5000 behind HTTPS ingress.
- Local loopback HTTP is supported for development.
