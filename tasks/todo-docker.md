# Tasks: Docker Containerization

Plan: [tasks/plan-docker.md](plan-docker.md) · Spec: [docs/specs/docker-containerization.md](../docs/specs/docker-containerization.md)

## Phase 1: Foundation

### Task 1: Add `.dockerignore`

**Description:** Create a root-level `.dockerignore` so `docker build`'s
context doesn't include local build artifacts, IDE files, or the `tests/`
tree — keeps the build fast and avoids accidentally leaking local state into
image layers.

**Acceptance criteria:**
- [x] `.dockerignore` excludes `**/bin/`, `**/obj/`, `.git/`, `.vs/`, `.idea/`, `tests/`
- [x] `src/` and the `*.sln` file are NOT excluded (needed for restore/publish)

**Verification:**
- [x] Visual review: diff the file against the list above
- [x] Confirmed indirectly by Task 2's `docker build` succeeding

**Dependencies:** None

**Files likely touched:**
- `.dockerignore`

**Estimated scope:** XS (1 file)

---

### Task 2: Multi-stage Dockerfile, standalone build + run

**Description:** Add `src/EventGoNoGoAdvisor.Api/Dockerfile` with a build
stage (`mcr.microsoft.com/dotnet/sdk:8.0`) that restores and publishes
`EventGoNoGoAdvisor.Api.csproj` (pulling in its `ServiceDefaults` project
reference), and a runtime stage (`mcr.microsoft.com/dotnet/aspnet:8.0-alpine`)
that runs the published app as a non-root user, exposing ports 8080 and 8081.
No docker-compose or HTTPS/cert wiring yet — this task proves the image
builds and runs standalone over plain HTTP.

**Acceptance criteria:**
- [x] `docker build -t event-advisor-api -f src/EventGoNoGoAdvisor.Api/Dockerfile .` succeeds from the repo root
- [x] Final (runtime) stage runs as a non-root user
- [x] Both base image tags are pinned to `8.0`/`8.0-alpine` (no `latest`)

**Verification:**
- [x] `docker build ...` exits 0
- [x] `docker run --rm -e ASPNETCORE_URLS=http://+:8080 -p 8080:8080 event-advisor-api` starts and logs "Now listening on: http://[::]:8080" without crashing
- [x] `curl http://localhost:8080/event-advisor` returns a verdict-shaped JSON response (not a crash) — got a `502` because Open-Meteo was unreachable from this network at the time (confirmed independently via a bare host `curl` timing out to the same host, unrelated to Docker/the container)
- [x] Container process is confirmed non-root (`docker exec <id> whoami` → `app`)

**Dependencies:** Task 1

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Dockerfile`

**Estimated scope:** S (1 file)

---

## Checkpoint: Foundation
- [ ] `docker build` succeeds from repo root
- [ ] Container runs standalone over plain HTTP as a non-root user
- [ ] Review with human before proceeding

---

## Phase 2: Compose + AppHost wiring

### Task 3: `docker-compose.yml` with HTTPS dev-cert wiring

**Description:** Add a root `docker-compose.yml` defining the
`event-advisor-api` service, built from the Task 2 Dockerfile, mapping
`8080:8080` (HTTP) and `8081:8081` (HTTPS), setting
`ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS` for both schemes, the
Kestrel default-cert env vars, and a read-only bind mount of the host's
`~/.aspnet/https` directory (where `dotnet dev-certs https` writes the dev
cert).

**Acceptance criteria:**
- [x] `docker compose up --build` starts the container without errors, assuming the host already ran `dotnet dev-certs https` once
- [x] Container serves both `http://localhost:8080` and `https://localhost:8081`
- [x] `/swagger/index.html` returns `200` over HTTPS (Development environment)
- [x] `/health` and `/alive` return `200` over HTTPS (Development environment maps them per `Extensions.cs`); over plain HTTP they return `307` because `UseHttpsRedirection()` in `Program.cs` redirects to HTTPS first — this is existing, unmodified app behavior, not a container issue

**Verification:**
- [x] `curl -sk -X POST https://localhost:8081/event-advisor` with the README's sample GO payload returns a verdict-shaped response — got the expected `502` "Upstream weather service failure" because Open-Meteo is still unreachable from this network (known external issue noted in Task 2, confirmed independent of Docker)
- [x] `curl -k https://localhost:8081/swagger/index.html` returns `200`
- [x] `curl -sk https://localhost:8081/health` and `/alive` both return `200`

**Dependencies:** Task 2

**Files likely touched:**
- `docker-compose.yml`

**Estimated scope:** S (1 file)

**Results (Task 3):** Created `docker-compose.yml` at repo root exactly per the spec's template — the `${USERPROFILE}/.aspnet/https:/https:ro` volume mount and `${DEV_CERT_PASSWORD:-devpassword}` env-var default worked as-is with `docker compose` on this Windows/Docker Desktop host; no changes needed from the spec's example. No dev cert existed on this host beforehand (`C:\Users\HuyHa\.aspnet\https\aspnetapp.pfx` was missing); generated one via `dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\aspnetapp.pfx" -p devpassword` after creating the target directory (the command errors if the directory doesn't exist first). Verified with `docker compose up --build -d`, confirmed via `docker compose logs` that Kestrel bound both `https://[::]:8081` and `http://[::]:8080`, then ran `docker compose down` to free the ports afterward.

---

### Task 4: Confirm Aspire Hosting API; wire AppHost for Dockerfile-based publish

**Description:** Research spike: determine the correct `Aspire.Hosting`
extension method, on the installed `Aspire.AppHost.Sdk/13.6.4`, for
associating a project resource with an existing Dockerfile for
container-based publish (candidates include `PublishAsDockerFile()` /
`WithDockerfile()` — confirm exact name/signature against the installed SDK
rather than guessing). Apply it to the `event-advisor-api` resource in
`AppHost.cs`, pointing at `src/EventGoNoGoAdvisor.Api/Dockerfile`, without
changing plain local-dev behavior.

**Acceptance criteria:**
- [x] `event-advisor-api` project resource in `AppHost.cs` is associated with the Task 2 Dockerfile for container-based publish
- [x] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` (no publish flag) still launches the Aspire Dashboard + Api exactly as before — zero change to the inner dev loop
- [x] If the installed Aspire SDK version does not support this cleanly, this is reported back to the human with findings rather than worked around *(not needed — a working API was found and confirmed; see results below)*

**Verification:**
- [x] `dotnet build` succeeds for the full solution
- [x] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` boots the dashboard; a manual `POST /event-advisor` through the AppHost-launched instance still works
- [x] Aspire's publish/manifest output references the Dockerfile path for the `event-advisor-api` resource

**Dependencies:** Task 2

**Files likely touched:**
- `src/EventGoNoGoAdvisor.AppHost/AppHost.cs`

**Estimated scope:** S (1 file), flagged medium-risk due to unconfirmed Aspire API

**Results (Task 4):** The resolved `Aspire.Hosting` package is exactly
`13.6.4` (matches the SDK version). Found the API by grepping
`Aspire.Hosting.xml`'s doc comments in the NuGet cache
(`~/.nuget/packages/aspire.hosting/13.6.4/lib/net8.0/Aspire.Hosting.xml`):
`Aspire.Hosting.ProjectResourceBuilderExtensions.PublishAsDockerFile<T>(IResourceBuilder<T>, Action<IResourceBuilder<ContainerResource>>? configure = null)`
(capital `F` in `DockerFile`). This only affects publish/manifest
generation — it's a no-op outside publish mode, so plain `dotnet run`
behavior is provably unaffected.

Important nuance confirmed via the actual source
(`ExecutableResourceBuilderExtensions.cs` on source.dot.net, which backs the
project-resource overload too): with no `configure` callback, Aspire
defaults the Dockerfile *build context* to the **project's own directory**
(`Path.GetDirectoryName(projectMetadata.ProjectPath)`), i.e.
`src/EventGoNoGoAdvisor.Api/`. That default conflicts with our Dockerfile,
which assumes a **repo-root** context (its `COPY` lines reach across to the
sibling `EventGoNoGoAdvisor.ServiceDefaults` project). A context scoped to
the Api project's own directory cannot see that sibling project, so the
Dockerfile would fail to build under Aspire's default.

Fixed by using the `configure` callback to re-set the context explicitly:

```csharp
.PublishAsDockerFile(container => container.WithDockerfile(
    contextPath: "../..",
    dockerfilePath: "src/EventGoNoGoAdvisor.Api/Dockerfile"));
```

(`contextPath` is resolved relative to the AppHost project directory;
`../..` from `src/EventGoNoGoAdvisor.AppHost/` reaches the repo root.)

Verified end-to-end, not just from docs:
- `dotnet build` (full solution) — 0 errors, 0 warnings.
- `dotnet run --project src/EventGoNoGoAdvisor.AppHost` — dashboard started
  normally, `EventGoNoGoAdvisor.Api` process spawned under it exactly as
  before.
- `dotnet run --project src/EventGoNoGoAdvisor.AppHost -- --publisher manifest --output-path <dir>`
  produced `aspire-manifest.json` with:
  ```json
  "event-advisor-api": {
    "type": "container.v1",
    "build": {
      "context": "<repo root>",
      "dockerfile": "<repo root>/src/EventGoNoGoAdvisor.Api/Dockerfile"
    }, ...
  }
  ```
  Both paths resolved (after normalizing the `../` segments) to the exact
  repo root and the exact Task 2 Dockerfile — proof the fix works, not an
  assumption.
- Re-ran plain `dotnet run --project src/EventGoNoGoAdvisor.AppHost`, then
  `curl https://localhost:7212/health` → `200` and `POST
  https://localhost:7212/event-advisor` (README's sample payload) → `200`
  with a live `GO` verdict — confirms the inner dev loop is byte-for-byte
  unchanged by this edit.
- No global/local Aspire CLI tool is installed in this environment, so the
  `aspire publish` *command* itself wasn't exercised — the equivalent
  `--publisher manifest` passthrough on `dotnet run` was used instead, which
  exercises the same `Aspire.Hosting.Publishing.ManifestPublisher` code path.

---

## Checkpoint: Compose + AppHost
- [x] `docker compose up --build` serves HTTP + HTTPS, Swagger, health endpoints
- [x] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` (plain) still works unchanged
- [x] Review with human before proceeding — docker-compose.yml reviewed; `devpassword` fallback kept as a documented dev-only default (see Task 6)

---

## Phase 3: Verification & Docs

### Task 5: Full verification pass

**Description:** Re-run the four scenarios documented in the README's
"Sample request/response" section (GO, high-rain NO_GO, high-wind NO_GO,
holiday advisory GO) against the docker-compose-run container, confirm
`dotnet test` is unaffected, and confirm both plain `dotnet run` dev loops
(Api standalone, and via AppHost) still behave exactly as documented.

**Acceptance criteria:**
- [x] All four sample scenarios reproduce the README's documented responses when run against the containerized instance (verdict/shape correct for all 4; absolute weather numbers naturally vary since there's no caching, by design — scenario 3 "Wind NO_GO" happened to be calm at run time, returning GO, but the rule logic evaluated correctly against live data)
- [x] `dotnet test` passes with the same pass count as before this work (19/19, unchanged)
- [x] `dotnet run --project src/EventGoNoGoAdvisor.Api` works exactly as documented (no regression) — Swagger and `/health` both returned 200
- [x] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` works exactly as documented (no regression) — verified in Task 4

**Verification:**
- [x] `dotnet test` run: `Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19`
- [x] Manual curl runs of all four sample payloads against the container via `docker compose up --build -d` on `https://localhost:8081/event-advisor`, then `docker compose down` to clean up. Holiday scenario (Paris, 2026-11-01, FR) matched the README's captured example exactly, including "All Saints' Day".

**Dependencies:** Tasks 2, 3, 4

**Files likely touched:** None (verification only)

**Estimated scope:** XS

---

### Task 6: README "Running with Docker" section

**Description:** Add a new README section documenting `docker build` /
`docker compose up --build`, the one-time
`dotnet dev-certs https -ep ${USERPROFILE}/.aspnet/https/aspnetapp.pfx -p <password>`
step, a note that the container defaults to `ASPNETCORE_ENVIRONMENT=Development`
(so Swagger/health stay reachable), and a pointer that a hardened Production
container profile is future work, not yet implemented.

**Acceptance criteria:**
- [x] README has a "Running with Docker" section with copy-pasteable `docker build`/`docker compose up` commands
- [x] One-time dev-cert generation step is documented
- [x] Note that this is a Development-profile container, with a hardened Production profile called out as future work
- [x] All Success Criteria from `docs/specs/docker-containerization.md` are verifiably met

**Verification:**
- [x] Manual review against the spec's Success Criteria list — all met (see Results note below)

---

**Results (Task 6):** Added a "Running with Docker" subsection under "Running it" in README.md, covering the one-time `dotnet dev-certs https` step, `docker compose up --build`, the `devpassword`/`DEV_CERT_PASSWORD` dev-only-default note, the direct `docker build` command, a note on the AppHost `PublishAsDockerFile` wiring and that it doesn't affect plain `dotnet run`, and a link to the full spec. Also updated the "Project structure" listing to include `.dockerignore`, `docker-compose.yml`, `Dockerfile`, the AppHost comment, the new spec file, and `plan-docker.md`/`todo-docker.md`.

**Dependencies:** Task 5

**Files likely touched:**
- `README.md`

**Estimated scope:** XS

---

## Checkpoint: Complete
- [ ] All Success Criteria in the spec are met
- [ ] `dotnet build` and `dotnet test` both succeed, unchanged
- [ ] README is accurate and complete
- [ ] Ready for human review
