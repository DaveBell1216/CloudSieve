# One-time 56-bit prime calculation

This is a separate, minimal approach for the revised requirement. Do not deploy
`foundation.bicep` or `apps.bicep` for this job. No Azure resource has been
created or validated. The result is the largest prime below 2^56, not a count of
all primes or a continuation of the historical on-premises watermark.

## Local verification

Run from the solution root:

```powershell
dotnet run --project Seive2.OneShot.Checks\Seive2.OneShot.Checks.csproj --configuration Release
dotnet run --project Seive2.OneShot\Seive2.OneShot.csproj --configuration Release
```

The checks compare all integers and upper bounds from 2 through 10,000 against an
independent sieve and check the exact 56-bit output. The job reports the factors
of larger odd candidates and proves the result by trial division through its
integer square root. Even numbers above the result are composite. Expected
result: `72057594037927931`.

The `Dockerfile.oneshot` image and Azure templates have not been tested with
Docker or deployed to Azure. Build and run the image locally if Docker becomes
available; verify its output and exit code before deploying.

## Estimate and approval

The proposed deployment uses a temporary Basic Azure Container Registry, one
user-assigned identity, and one Linux Azure Container Instance with 1 vCPU and
1 GB of memory. It does not create SQL, a VNet, a VPN, or an exposed endpoint.
Output is in container logs, not durable storage. The registry uses authenticated
public access, not a private endpoint.

On November 13, 2026, West US 3 public retail compute prices were approximately
$0.0405 per vCPU-hour and $0.00445 per GB-hour. One hour at the requested size
would therefore cost about $0.045 for container compute. Billing starts during
image pull; actual duration and charges may differ. Registry Basic is about
$0.1666 per day while provisioned. Remote builds, image transfer, retries,
taxes, and other usage can cost extra. Check the current rates and your
subscription terms before approving deployment. Do not assume a free tier or
that a budget alert is a spending limit.

## Deployment sequence (only after cost and deployment approval)

1. Choose a dedicated resource group in West US 3 and a lowercase alphanumeric
   prefix. Preview `oneshot-registry.bicep` with Azure Resource Manager what-if,
   check regional capacity, permissions and costs, and then deploy it. Save the
   registry login server and pull identity resource ID from its outputs. The
   operator needs permission to create role assignments. Wait for the AcrPull
   role assignment to propagate.
2. Build the image locally and push it, or use Azure Container Registry Tasks to
   build `Dockerfile.oneshot` from the repository root. Remote builds upload the
   Docker build context: review `.dockerignore` and the resulting context first.
   Use the repository name `sieve-oneshot`. Obtain the built image's immutable
   SHA-256 digest; confirm that the image was built from the reviewed source.
   A remote build incurs charges and is not a substitute for testing the image.
3. Preview `oneshot-run.bicep` with the registry login server, digest and pull
   identity ID. Deploy it only after review. Deployment creates and starts the
   calculation immediately. It has no public ingress. The `Never` restart
   policy prevents restarting after successful exit; Azure may still restart a
   container after a nonzero exit, so monitor failures and remove the instance
   promptly if necessary.
4. Check the instance's actual terminated state and exit code, and retrieve and
   save its logs before deleting anything. Confirm the full result and composite
   evidence in the logs. Container logs are not permanent; export them before
   deleting or recreating the instance.
5. After verifying the result and identifying the exact resources created for
   this run, delete the container instance and temporary registry and identity
   (or delete the dedicated resource group only if it contains nothing else you
   need). Verify deletion and review Cost Management for remaining charges.

References: [Container Instances pricing](https://azure.microsoft.com/pricing/details/container-instances/),
[run-once restart policy](https://learn.microsoft.com/azure/container-instances/container-instances-restart-policy),
and [managed-identity registry pulls](https://learn.microsoft.com/azure/container-instances/using-azure-container-registry-mi).
