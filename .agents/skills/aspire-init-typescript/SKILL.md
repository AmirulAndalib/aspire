---
name: aspire-init-typescript
description: "One-time skill for completing Aspire initialization in a TypeScript AppHost workspace. Run this after `aspire init` has dropped the skeleton apphost.ts and aspire.config.json. This skill scans the repository, wires up projects in the AppHost, configures package.json/tsconfig/eslint, sets up OpenTelemetry for non-.NET services, installs dependencies, and validates that `aspire start` works. Self-removes on success."
---

# Aspire Init — TypeScript AppHost

This is a **one-time setup skill**. It completes the Aspire initialization that `aspire init` started. After this skill finishes successfully, it should be deleted — the evergreen `aspire` skill handles ongoing AppHost work.

## Prerequisites

Before running this skill, `aspire init` must have already:

- Dropped a skeleton `apphost.ts` at the configured location
- Created `aspire.config.json` at the repository root

Verify both files exist before proceeding.

## Workflow

Follow these steps in order. If any step fails, diagnose and fix before continuing.

### Step 1: Scan the repository

Analyze the repository to discover all projects and services that could be modeled in the AppHost.

Look for:

- **Node.js/TypeScript apps**: directories with `package.json` containing a `start` script, `dev` script, or `main`/`module` entry point
- **.NET projects**: `*.csproj` or `*.fsproj` files (check `OutputType` — `Exe`/`WinExe` are runnable services)
- **Python apps**: directories with `pyproject.toml`, `requirements.txt`, or a `main.py`/`app.py` entry point
- **Go apps**: directories with `go.mod`
- **Java apps**: directories with `pom.xml` or `build.gradle`
- **Dockerfiles**: standalone `Dockerfile` or `docker-compose.yml` entries that represent services
- **Static frontends**: directories with Vite, Next.js, Create React App, or other frontend framework configs

Ignore:

- The AppHost directory itself
- `node_modules/`, `.modules/`, `dist/`, `build/`, `bin/`, `obj/`, `.git/`
- Test projects (directories named `test`, `tests`, `__tests__`, or with test-only package.json scripts)

### Step 2: Present findings and confirm with the user

Show the user what you found. For each discovered project/service, show:

- Name (directory name or project name)
- Type (Node.js app, .NET service, Python app, Dockerfile, etc.)
- Entry point (e.g., `src/index.ts`, `Program.cs`, `app.py`)
- Whether it exposes HTTP endpoints (check for `express`, `fastify`, `koa`, `next`, `vite`, ASP.NET, Flask, etc.)

Ask the user which projects to include in the AppHost. Pre-select all discovered runnable services.

### Step 3: Wire up apphost.ts

Edit the skeleton `apphost.ts` to add resource definitions for each selected project. Use the appropriate Aspire builder methods:

```typescript
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// Example patterns — use the appropriate one for each discovered project type:

// Node.js/TypeScript app
const api = await builder
    .addNodeApp("api", "./api", "src/index.ts")
    .withHttpEndpoint({ env: "PORT" });

// Vite frontend
const frontend = await builder
    .addViteApp("frontend", "./frontend")
    .withReference(api)
    .waitFor(api);

// .NET project
const dotnetSvc = await builder
    .addProject("catalog", "./src/Catalog/Catalog.csproj");

// Dockerfile-based service
const worker = await builder
    .addDockerfile("worker", "./worker");

// Python app
const pyApi = await builder
    .addPythonApp("py-api", "./py-api", "app.py");

await builder.build().run();
```

**Important rules:**

- Use `aspire docs search` and `aspire docs get` to look up the correct builder API for each resource type before writing code. Do not guess API shapes.
- Check `.modules/aspire.ts` (after Step 5) to confirm available APIs.
- Use meaningful resource names derived from the directory/project name.
- Wire up `withReference()` and `waitFor()` for services that depend on each other (ask the user if dependency relationships are unclear).
- Expose HTTP endpoints with `withHttpEndpoint()` for services that serve HTTP traffic.
- Use `withExternalHttpEndpoints()` for user-facing frontends.

### Step 4: Configure package.json

If a root `package.json` already exists, **augment it** — do not overwrite. Add:

```json
{
  "type": "module",
  "scripts": {
    "start": "npx tsc && node --enable-source-maps apphost.js"
  },
  "dependencies": {
    // Added by aspire restore — do not manually add Aspire packages
  }
}
```

If no root `package.json` exists, create one with:

```json
{
  "name": "<repo-name>-apphost",
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "start": "npx tsc && node --enable-source-maps apphost.js"
  }
}
```

**Important rules:**

- Never overwrite existing `scripts`, `dependencies`, or `devDependencies` — merge only.
- Set `"type": "module"` if not already set (required for ESM imports in apphost.ts).
- Do not manually add Aspire SDK packages to dependencies — `aspire restore` handles this.

### Step 5: Run aspire restore

```bash
aspire restore
```

This generates the `.modules/` directory with TypeScript SDK bindings. After restore completes, inspect `.modules/aspire.ts` to confirm the available API surface matches what you used in apphost.ts.

If restore fails, diagnose the error. Common issues:

- Missing `aspire.config.json` — ensure it exists at repo root
- Wrong `appHost.path` in config — ensure it points to the correct `apphost.ts`
- Network issues downloading SDK packages

### Step 6: Configure tsconfig.json

If a root `tsconfig.json` already exists, augment it to include the AppHost compilation:

- Ensure `".modules/**/*.ts"` is in the `include` array
- Ensure `"apphost.ts"` is in the `include` array (or covered by an existing glob)
- Ensure `"module"` is set to `"nodenext"` or `"node16"` (ESM required)
- Ensure `"moduleResolution"` matches the module setting

If no `tsconfig.json` exists, check if `aspire restore` created one. If not, create a minimal one:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "nodenext",
    "moduleResolution": "nodenext",
    "esModuleInterop": true,
    "strict": true,
    "outDir": "./dist",
    "rootDir": "."
  },
  "include": ["apphost.ts", ".modules/**/*.ts"]
}
```

If the repo has a separate `tsconfig.apphost.json`, ensure it's referenced properly. If using TypeScript project references, add it as a reference.

### Step 7: Handle ESLint configuration

If the project uses ESLint with `typescript-eslint` project service:

1. Check if `.eslintrc.*` or `eslint.config.*` exists
2. If it uses `parserOptions.project` or `parserOptions.projectService`, ensure the AppHost tsconfig is discoverable
3. Common fix: add `tsconfig.apphost.json` to `parserOptions.project` array, or configure `projectService.allowDefaultProject` to include `apphost.ts`

If no ESLint config exists, skip this step.

**Do not create ESLint configuration from scratch** — only augment existing configs to recognize the AppHost files.

### Step 8: Wire up OpenTelemetry for non-.NET services

For each non-.NET service included in the AppHost, configure OpenTelemetry so the Aspire dashboard can show traces, metrics, and logs. This is the equivalent of what ServiceDefaults does for .NET projects.

**Node.js/TypeScript services:**

Check if the service already has OpenTelemetry configured. If not, suggest adding:

```bash
npm install @opentelemetry/sdk-node @opentelemetry/auto-instrumentations-node @opentelemetry/exporter-otlp-grpc
```

And an instrumentation file (e.g., `instrumentation.ts`):

```typescript
import { NodeSDK } from '@opentelemetry/sdk-node';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-otlp-grpc';

const sdk = new NodeSDK({
  traceExporter: new OTLPTraceExporter(),
  instrumentations: [getNodeAutoInstrumentations()],
});

sdk.start();
```

The OTLP endpoint URL is injected by Aspire via environment variables — the service just needs to read `OTEL_EXPORTER_OTLP_ENDPOINT`.

**Python services**: suggest `opentelemetry-distro` and `opentelemetry-exporter-otlp`.

**Other languages**: point the user to the OpenTelemetry docs for their language and note that the OTLP endpoint will be injected via environment variables by Aspire.

**Important**: Ask the user before modifying any service code. OTel setup may conflict with existing instrumentation. Present it as a recommendation, not an automatic change.

### Step 9: Install dependencies

```bash
npm install
```

Run this from the repo root (or wherever package.json lives) to install all dependencies including any added by aspire restore.

### Step 10: Validate

```bash
aspire start
```

Wait for the AppHost to start. Check that:

1. The dashboard URL is printed
2. All modeled resources appear in `aspire describe`
3. No startup errors in `aspire logs`

If `aspire start` fails:

1. Read the error output carefully
2. Check `aspire logs` for resource-specific failures
3. Common issues:
   - Missing dependencies — run `npm install` again
   - TypeScript compilation errors — check tsconfig and fix type issues
   - Port conflicts — ensure no hardcoded ports clash
   - Missing environment variables — check if services need specific env vars

Iterate until `aspire start` succeeds and all resources are healthy.

### Step 11: Clean up

After successful validation:

1. Stop the running AppHost: `aspire stop`
2. **Delete this skill** — remove the `aspire-init-typescript/` skill directory from all locations where it was installed (check `.agents/skills/`, `.github/skills/`, `.claude/skills/`)
3. Confirm the evergreen `aspire` skill is present for ongoing AppHost work

## Key rules

- **Never overwrite existing files** — always augment/merge
- **Use `aspire docs search` before guessing APIs** — look up the correct builder methods
- **Ask the user before modifying service code** (especially for OTel setup)
- **This is a one-time skill** — delete it after successful init
- **If stuck, use `aspire doctor`** to diagnose environment issues
