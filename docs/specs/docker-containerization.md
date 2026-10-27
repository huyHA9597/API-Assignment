# Spec: Docker Containerization

## Objective

Add Docker support for `EventGoNoGoAdvisor.Api` so the service can be built
and run as a container image, and wire the `EventGoNoGoAdvisor.AppHost`
project so it can orchestrate/publish that containerized Api — without
disturbing the existing `dotnet run` dev loop (Swagger, Aspire Dashboard
telemetry, hot reload) that the README documents today.

**Users:** developers running the API locally without the .NET SDK installed,
and anyone deploying this API to a container-based host (e.g. a container
registry + ACA/ECS/K8s) later.

**Success looks like:** `docker compose up --build` serves the same
`/event-advisor`, `/swagger`, and health endpoints as `dotnet run`, over both
HTTP and HTTPS (dev cert), with no changes to `Rules`/`Clients` business
logic and no regressions to `dotnet test`.

## Tech Stack

- Docker Engine + Docker Compose v2 (`docker compose`, not legacy
  `docker-compose`)
- Multi-stage Dockerfile:
  - Build/publish stage: `mcr.microsoft.com/dotnet/sdk:8.0`
  - Runtime stage: `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`
- `Aspire.Hosting` APIs (already present via `Aspire.AppHost.Sdk/13.6.4` in
  `EventGoNoGoAdvisor.AppHost.csproj`) for wiring the Api project resource to
  a Dockerfile-based container build

## Commands

```bash
# Build the image (context = repo root, so project references resolve)
docker build -t event-advisor-api -f src/EventGoNoGoAdvisor.Api/Dockerfile .

# Build + run via Compose (recommended path)
docker compose up --build

# Existing, unchanged dev workflows
dotnet build
dotnet test
dotnet run --project src/EventGoNoGoAdvisor.Api
dotnet run --project src/EventGoNoGoAdvisor.AppHost
```

## Project Structure

New files this spec adds:

```text
.dockerignore                              -> repo root; excludes bin/, obj/, .git/, .vs/, tests/
src/EventGoNoGoAdvisor.Api/Dockerfile      -> multi-stage build; context is repo root
docker-compose.yml                         -> repo root; builds + runs the Api service
src/EventGoNoGoAdvisor.AppHost/AppHost.cs  -> edited to mark the Api project resource
                                               for Dockerfile-based container publish
README.md                                  -> edited; add a "Running with Docker" section
```

No changes to `Rules/`, `Clients/`, `Models/`, `Options/`, or
`EventAdvisorEndpoints.cs` — this is purely a packaging/runtime-hosting
change.

## Code Style

Dockerfile follows the standard .NET multi-stage pattern, pinned tags (never
`latest`), non-root user in the final stage, and restores before copying the
rest of the source so Docker's layer cache survives source-only edits:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj", "src/EventGoNoGoAdvisor.Api/"]
COPY ["src/EventGoNoGoAdvisor.ServiceDefaults/EventGoNoGoAdvisor.ServiceDefaults.csproj", "src/EventGoNoGoAdvisor.ServiceDefaults/"]
RUN dotnet restore "src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj"
COPY src/ src/
RUN dotnet publish "src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
RUN addgroup -S app && adduser -S app -G app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "EventGoNoGoAdvisor.Api.dll"]
```

`docker-compose.yml` mounts the dev HTTPS cert (via `dotnet dev-certs https`
on the host) and sets the matching Kestrel env vars, mirroring the
`https` launch profile already in `Properties/launchSettings.json`:

```yaml
services:
  event-advisor-api:
    build:
      context: .
      dockerfile: src/EventGoNoGoAdvisor.Api/Dockerfile
    ports:
      - "8080:8080"
      - "8081:8081"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=https://+:8081;http://+:8080
      - ASPNETCORE_Kestrel__Certificates__Default__Password=${DEV_CERT_PASSWORD:-devpassword}
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
    volumes:
      - ${USERPROFILE}/.aspnet/https:/https:ro
```

## Testing Strategy

No new automated test project. Verification is manual/CLI, mirroring the
"Sample request/response" section already in the README:

1. `dotnet test` still passes unchanged (proves the container work touched
   no business logic).
2. `docker build ...` completes without errors.
3. `docker compose up --build` starts the container; `curl -k
   https://localhost:8081/swagger/index.html` and
   `http://localhost:8080/event-advisor` (POST, sample body from README)
   both succeed from the host.
4. Re-run the four documented sample requests (GO, rain NO_GO, wind NO_GO,
   holiday advisory) against the containerized instance and confirm the
   responses match the README's captured examples.
5. Confirm `dotnet run --project src/EventGoNoGoAdvisor.AppHost` still boots
   the Aspire Dashboard exactly as before (the AppHost edit must not change
   local dev behavior, only add a publish-time Dockerfile association).

## Boundaries

- **Always:** pin base image tags to `8.0`/`8.0-alpine` (never `latest`);
  run the final container as a non-root user; keep `UseHttpsRedirection`,
  `UseHsts`, the security-header middleware, and the rate limiter
  (ADR-003) active and unmodified inside the container; keep
  `dotnet run --project src/EventGoNoGoAdvisor.Api` and
  `...AppHost` working exactly as documented today.
- **Ask first:** changing the host-mapped ports away from 8080/8081;
  adding any new service, database, or cache to `docker-compose.yml`;
  changing the container's default `ASPNETCORE_ENVIRONMENT` away from
  `Development` (this gates Swagger and `/health`/`/alive` per
  `Extensions.cs`); upgrading the target framework/base image major version.
- **Never:** commit a real/production TLS certificate, password, or secret
  into the repo, Dockerfile, or compose file; bake upstream API credentials
  into the image (none exist today — keep it that way); publish an image to
  a registry as part of this work (out of scope — local build/run only).

## Success Criteria

- `docker build -t event-advisor-api -f src/EventGoNoGoAdvisor.Api/Dockerfile .` succeeds from a clean checkout.
- `docker compose up --build` serves `/event-advisor`, `/swagger`, `/health`,
  and `/alive` and reproduces the README's four sample scenarios.
- `EventGoNoGoAdvisor.AppHost`'s `event-advisor-api` project resource is
  marked for Dockerfile-based container publish, verified via
  `dotnet run --project src/EventGoNoGoAdvisor.AppHost --publisher manifest`
  (or the Aspire CLI's equivalent) showing the Dockerfile path in its output.
- `dotnet run --project src/EventGoNoGoAdvisor.AppHost` (plain, no publish
  flag) still launches the Aspire Dashboard and Api exactly as before —
  zero change to the inner-loop dev experience.
- `dotnet test` passes unchanged.
- README gains a "Running with Docker" section with the build/compose
  commands and a note on the mounted dev cert.
- No secrets, real certs, or `latest`-tagged base images anywhere in the
  new files.

## Decisions (resolved)

1. **Container default environment.** This spec's container defaults to
   `ASPNETCORE_ENVIRONMENT=Development` (Swagger and `/health`/`/alive` stay
   reachable, matching local `dotnet run` today). A separate **hardened
   Production container profile** (Swagger/health endpoints locked down, a
   real cert instead of the dev cert, no debug-friendly defaults) is
   explicitly **out of scope here** and will be specified as its own
   follow-up once this baseline is working.
2. **Aspire Hosting API for Dockerfile-based publish** (e.g.
   `PublishAsDockerFile()` / `WithDockerfile()` — naming varies across Aspire
   versions): confirmed to verify the exact extension method against the
   installed `Aspire.AppHost.Sdk/13.6.4` during the Plan/Implement phases
   rather than lock it here.
3. **Dev cert provisioning.** A one-time manual step
   (`dotnet dev-certs https -ep ${USERPROFILE}/.aspnet/https/aspnetapp.pfx -p <password>`
   run once on the host), documented in the README's new "Running with
   Docker" section — not automated by the Dockerfile/compose.
4. **Registry push / CI image build** is confirmed out of scope for this
   spec. Can be tracked as separate follow-up work if wanted.

## Future Work (explicitly out of scope)

- A hardened Production container profile (see Decision 1 above).
- Pushing the built image to a registry, or a CI pipeline step that builds
  it automatically.
