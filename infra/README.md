# Azure deployment preparation

These templates are a starting point for the `westus3` deployment. They have
been compiled locally, but have not been deployed or validated against an Azure
subscription. SQL coordination has been tested on isolated LocalDB with snapshot
isolation; Azure SQL and managed-identity integration have not been tested. Run
an Azure Resource Manager what-if, verify regional quota and
pricing, and review the resources before provisioning. They are not yet a
production-ready deployment.

## Resources and costs

`foundation.bicep` creates a virtual network, internal Container Apps environment,
Log Analytics workspace (30-day retention), Basic container registry, Azure SQL
server and Basic database, private SQL endpoint and DNS, and separate API and
worker managed identities with registry pull permissions. SQL public access is
disabled. The registry uses authenticated public network access, not Private
Link. The environment and SQL endpoint are private; **the templates do not
create a VPN, jump host, or other operator access into the virtual network**.
Provide a secure private connection and DNS resolution for both the environment
and SQL private DNS zones before attempting database setup or operator requests.
Do not enable public access merely to bypass this requirement.

`apps.bicep` creates a single coordinator Container App and a manually triggered
Container Apps Job for workers. Ingress is external to the Container Apps
environment but private to the internal environment's virtual network. TLS is
terminated by Container Apps; the container listens on port 5000. The SQL
connection uses the API user-assigned managed identity. The worker uses its own
managed identity for API tokens. Basic registry, database, network, Container
Apps, and logging resources may all incur charges. Availability, SQL backup
redundancy, quotas and minimum cost must be checked for West US 3.

`budget.bicep` creates an optional resource-group monthly budget with email
alerts at 80% and 100%. A budget is **not a spending limit** and does not stop
resources. Its amount, email, and start date must be supplied by the owner.
Review managed environment infrastructure resource group costs as well; those
resources can sit outside the application resource group.

## Prerequisites before deployment

1. Sign in with an Azure identity that can deploy resources, create RBAC
   assignments, administer Azure SQL, and perform the necessary Entra app-role
   assignments. Choose an Entra SQL administrator and supply its object ID,
   display name, and type (`User` or `Group`). A private network path is required
   for SQL schema setup and API access.
2. Create a single-tenant Microsoft Entra API app registration. Configure an
   Application ID URI of `api://<api-client-id>` and set the manifest property
   `requestedAccessTokenVersion` to `2`. Define app roles `Sieve.Operator`
   (allowed member type `User`) and `Sieve.Worker` (allowed member type
   `Application`). Assign the operator and, after creating the foundation, the
   worker managed identity to their respective roles on the API's enterprise
   application. Use the **client ID** as `Authentication__Audience`, and
   `api://<api-client-id>/.default` as the worker token scope. Verify both role
   assignments and tokens before deploying the apps.
3. Set a resource-group budget after agreeing on an amount and alert email.
   Review the Basic SQL/registry sizes and current prices before starting.
4. Review the old SQL credentials and rotate them. The original watermark must
   be read from the on-premises database and passed as `ExistingLargestPrime`
   when applying `schema.sql`; it is not a progress checkpoint. The first cloud
   job starts at 2. Stop all older workers and API instances before applying a
   schema upgrade.

## Deployment sequence (after approval)

1. Create a dedicated resource group in West US 3. Preview `foundation.bicep`
   with `az deployment group what-if`, supplying `prefix`, `sqlAdminObjectId`,
   `sqlAdminName`, and `sqlAdminPrincipalType`. Choose a 3-15 character lowercase
   alphanumeric prefix and use the same resource group and prefix for both
   templates. Deploy the foundation only after reviewing the preview and costs.
2. Deploy or configure secure private operator connectivity and DNS. From that
   network, connect to the private SQL endpoint as the Entra SQL administrator.
   Run `schema.sql` in SQLCMD mode with `ApiIdentityName` set to the API managed
   identity's name and `ExistingLargestPrime` set to the on-premises value (or
   `0` if explicitly starting with no prior record). Only use trusted values for
   SQLCMD substitution. Confirm the schema and grants applied successfully.
3. Assign the worker managed identity the `Sieve.Worker` app role. Assign the
   operator the `Sieve.Operator` role. Allow time for role propagation.
4. Build and push both images with immutable version tags. Docker can build
   locally using `Dockerfile.api` and `Dockerfile.worker`, or Azure Container
   Registry Tasks can build them remotely from the repository root. Remote builds
   upload the build context: review `.dockerignore` and the context for sensitive
   files before uploading. Verify the registry accepts ARM-audience tokens and
   that both identities have effective `AcrPull` access.
5. Preview `apps.bicep` with `az deployment group what-if`, supplying `prefix`,
   `apiClientId`, `imageTag`, and optionally `workerParallelism`. Deploy after
   the database schema, identities, role grants, and images are ready. The API
   will not start until SQL is reachable and the schema exists.
6. From a private-connected operator machine, obtain an operator token and start
   the small job **before** starting the manual worker job. A finished job causes
   workers to exit; a worker that reaches its 10,000-segment quota also exits,
   so start another execution if more work remains. The default replica timeout
   is one hour. Work assignments expire after ten minutes with no renewal;
   choose segments that complete well inside that time.

## Required validation before scaling

- Run a job from 2 through 1,000,000 with segment size 10,000. The expected
  result is 78,498 primes; the largest prime in that range is 999,983. The
  retained historical largest-prime record can be larger than 999,983.
- Stop and restart the API mid-job; verify completed segments and totals remain
  unchanged. Stop a worker mid-segment, allow the ten-minute lease to expire,
  and verify it is reissued. Replay the same completion and verify totals are
  unchanged. After reassignment, verify the old assignment ID is rejected,
  including when both workers use the same managed identity.
- Confirm unauthenticated/wrong-role requests are rejected and verify private
  DNS, SQL access, HTTPS, logs, budget alerts, and resource deletion procedures.
- Test real Azure SQL concurrency, network failures, role/token propagation,
  and image pulls; local Bicep compilation and a .NET build do not test these.

References: [Container Apps virtual networks](https://learn.microsoft.com/azure/container-apps/vnet-custom),
[managed-identity registry pulls](https://learn.microsoft.com/azure/container-apps/managed-identity-image-pull),
[Azure SQL managed identity](https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-service-principal),
and [Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/).
