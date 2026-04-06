---
name: aspire-init-csharp
description: "One-time skill for completing Aspire initialization in a C# AppHost workspace. Run this after `aspire init` has dropped the skeleton apphost.cs and aspire.config.json. This skill scans the repository for projects, wires up the AppHost, creates ServiceDefaults, configures project references, and validates that `aspire start` works. Self-removes on success."
---

# Aspire Init — C# AppHost

This is a **one-time setup skill**. It completes the Aspire initialization that `aspire init` started. After this skill finishes successfully, it should be deleted — the evergreen `aspire` skill handles ongoing AppHost work.

## Prerequisites

Before running this skill, `aspire init` must have already:

- Dropped a skeleton `apphost.cs` (single-file) or an AppHost project directory (if a .sln was found)
- Created `aspire.config.json` at the repository root

Verify both exist before proceeding.

## Understanding the two modes

`aspire init` drops different things depending on whether a solution file was found:

- **No .sln/.slnx**: A single-file `apphost.cs` using the `#:sdk` directive. This is a self-contained file with no project directory.
- **With .sln/.slnx**: A full AppHost project directory containing a `.csproj` and `apphost.cs`. This project has been added to the solution.

Check which mode you're in by looking at `aspire.config.json` — the `appHost.path` field tells you.

## Workflow

Follow these steps in order. If any step fails, diagnose and fix before continuing.

### Step 1: Scan the repository

Analyze the repository to discover all projects and services that could be modeled in the AppHost.

**For .NET projects:**

Find all `*.csproj` and `*.fsproj` files. For each, determine:

- **OutputType**: Run `dotnet msbuild <project> -getProperty:OutputType` — `Exe` or `WinExe` means it's a runnable service
- **TargetFramework**: Run `dotnet msbuild <project> -getProperty:TargetFramework` — must be `net8.0` or newer
- **IsAspireHost**: Run `dotnet msbuild <project> -getProperty:IsAspireHost` — skip if `true` (that's the AppHost itself)

Classify each project:

- **Runnable services**: OutputType is `Exe`/`WinExe`, TFM is net8.0+, not an AppHost
- **Class libraries**: OutputType is `Library` — these are not modeled in the AppHost directly
- **Test projects**: skip (directories named `test`/`tests`, or projects referencing xUnit/NUnit/MSTest)

**For non-.NET projects:**

Also look for:

- **Node.js/TypeScript apps**: directories with `package.json` + start script
- **Python apps**: directories with `pyproject.toml` or `requirements.txt` + entry point
- **Dockerfiles**: standalone `Dockerfile` entries that represent services
- **Docker Compose**: `docker-compose.yml` entries (note: these may need manual translation)

### Step 2: Present findings and confirm with the user

Show the user what you found. For each discovered project/service, show:

- Name (project name or directory name)
- Type (.NET service, Node.js app, Dockerfile, etc.)
- Framework/TFM (e.g., net10.0, Node 20, Python 3.12)
- Whether it exposes HTTP endpoints

Ask the user:

1. Which projects to include in the AppHost (pre-select all runnable .NET services)
2. Which projects should receive ServiceDefaults references (pre-select all .NET services)

### Step 3: Create ServiceDefaults project

If no ServiceDefaults project exists in the repo, create one using the dotnet template:

```bash
dotnet new aspire-servicedefaults -n <SolutionName>.ServiceDefaults -o <path>
```

Where `<path>` is alongside the AppHost (e.g., `src/` or solution root).

If a solution file exists, add the ServiceDefaults project to it:

```bash
dotnet sln <solution> add <ServiceDefaults.csproj>
```

If a ServiceDefaults project already exists (look for a project that references `Microsoft.Extensions.ServiceDiscovery` or `Aspire.ServiceDefaults`), skip creation and use the existing one.

### Step 4: Wire up the AppHost

Edit the `apphost.cs` to add resource definitions for each selected project.

**Single-file mode** (no solution):

```csharp
#:sdk Aspire.AppHost.Sdk@<version>
#:property IsAspireHost=true

// Project references
#:project ../src/Api/Api.csproj
#:project ../src/Web/Web.csproj

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api");

var web = builder.AddProject<Projects.Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
```

**Full project mode** (with solution):

Edit the `apphost.cs` in the AppHost project:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api");

var web = builder.AddProject<Projects.Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
```

And add project references to the AppHost `.csproj`:

```bash
dotnet add <AppHost.csproj> reference <Api.csproj>
dotnet add <AppHost.csproj> reference <Web.csproj>
```

**For non-.NET services in a C# AppHost**, use the appropriate hosting integration:

```csharp
// Node.js app (requires Aspire.Hosting.NodeJs package)
var frontend = builder.AddNpmApp("frontend", "../frontend", "start");

// Dockerfile-based service
var worker = builder.AddDockerfile("worker", "../worker");

// Python app (requires Aspire.Hosting.Python package)
var pyApi = builder.AddPythonApp("py-api", "../py-api", "app.py");
```

For non-.NET resources, add the required hosting NuGet packages:

```bash
dotnet add <AppHost.csproj> package Aspire.Hosting.NodeJs
dotnet add <AppHost.csproj> package Aspire.Hosting.Python
```

**Important rules:**

- Use `aspire docs search` and `aspire docs get` to look up the correct builder API for each resource type before writing code. Do not guess API shapes.
- Use meaningful resource names derived from the project name.
- Wire up `WithReference()` and `WaitFor()` for services that depend on each other (ask the user if dependency relationships are unclear).

### Step 5: Add ServiceDefaults references

For each .NET project that the user selected for ServiceDefaults:

```bash
dotnet add <Project.csproj> reference <ServiceDefaults.csproj>
```

Then check each project's `Program.cs` (or equivalent entry point) and add the ServiceDefaults call if not already present:

```csharp
builder.AddServiceDefaults();
```

This should be added early in the builder pipeline, before `builder.Build()`. Look for the `WebApplicationBuilder` or `HostApplicationBuilder` creation and add it after.

Also add the corresponding endpoint mapping before `app.Run()`:

```csharp
app.MapDefaultEndpoints();
```

**Important**: Be careful with code placement. Look at the existing code structure:

- If using top-level statements, add `builder.AddServiceDefaults()` after `var builder = WebApplication.CreateBuilder(args);`
- If using `Startup.cs` pattern, add to `ConfigureServices`
- If using `Program.Main` method, add in the appropriate location
- Do not duplicate if already present

### Step 6: Wire up OpenTelemetry for non-.NET services

For non-.NET services included in the AppHost, OpenTelemetry should be configured so the Aspire dashboard can show their traces, metrics, and logs. This is the equivalent of what ServiceDefaults does for .NET.

**Node.js/TypeScript services:**

Suggest adding OpenTelemetry packages:

```bash
npm install @opentelemetry/sdk-node @opentelemetry/auto-instrumentations-node @opentelemetry/exporter-otlp-grpc
```

And instrumentation setup that reads the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable (injected by Aspire automatically).

**Python services:** suggest `opentelemetry-distro` and `opentelemetry-exporter-otlp`.

**Important**: Ask the user before modifying any non-.NET service code. OTel setup may conflict with existing instrumentation.

### Step 7: Configure NuGet (if needed)

If the AppHost uses a non-stable Aspire SDK channel (preview, daily, etc.), ensure the appropriate NuGet feed is configured:

Check `aspire.config.json` for the `channel` field. If it's not `stable`, a `NuGet.config` may need to be created or updated with the appropriate feed URL.

For single-file mode, this is handled automatically by the `#:sdk` directive. For full project mode, ensure the NuGet.config is in scope.

### Step 8: Trust development certificates

```bash
aspire certs trust
```

This ensures HTTPS works locally for the Aspire dashboard and service-to-service communication.

### Step 9: Validate

```bash
aspire start
```

Wait for the AppHost to start. Check that:

1. The dashboard URL is printed
2. All modeled resources appear in `aspire describe`
3. No startup errors in `aspire logs`
4. .NET services show health check endpoints (from ServiceDefaults)

If `aspire start` fails:

1. Read the error output carefully
2. Check `aspire logs` for resource-specific failures
3. Common issues:
   - Missing project references — ensure all `dotnet add reference` commands succeeded
   - Missing NuGet packages — run `dotnet restore` on the AppHost
   - TFM mismatches — ensure all referenced projects target compatible frameworks
   - Build errors — run `dotnet build` on the AppHost project to see compiler output
   - Port conflicts — check for hardcoded ports that clash

Iterate until `aspire start` succeeds and all resources are healthy.

### Step 10: Update solution file (if applicable)

If a solution file exists, verify all new projects are included:

```bash
dotnet sln <solution> list
```

Ensure both the AppHost and ServiceDefaults projects appear. If not, add them:

```bash
dotnet sln <solution> add <AppHost.csproj>
dotnet sln <solution> add <ServiceDefaults.csproj>
```

### Step 11: Clean up

After successful validation:

1. Stop the running AppHost: `aspire stop`
2. **Delete this skill** — remove the `aspire-init-csharp/` skill directory from all locations where it was installed (check `.agents/skills/`, `.github/skills/`, `.claude/skills/`)
3. Confirm the evergreen `aspire` skill is present for ongoing AppHost work

## Key rules

- **Use `aspire docs search` before guessing APIs** — look up the correct builder methods for unfamiliar resource types
- **Ask the user before modifying service code** (especially for ServiceDefaults injection and OTel setup)
- **Respect existing project structure** — don't reorganize the repo, work with what's there
- **This is a one-time skill** — delete it after successful init
- **If stuck, use `aspire doctor`** to diagnose environment issues
- **For C# APIs, use `dotnet-inspect` skill** if available to verify method signatures and overloads
