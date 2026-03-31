---
name: aspire
description: "Orchestrates Aspire distributed applications using the Aspire CLI for running, debugging, deploying, and managing distributed apps. USE FOR: aspire start, aspire stop, start aspire app, aspire describe, list aspire integrations, debug aspire issues, view aspire logs, add aspire resource, aspire dashboard, update aspire apphost, aspire publish, aspire deploy, aspire secrets, aspire config. DO NOT USE FOR: non-Aspire .NET apps (use dotnet CLI), container-only deployments (use docker/podman). INVOKES: Aspire CLI commands (aspire start, aspire describe, aspire otel logs, aspire docs search, aspire add, aspire publish, aspire deploy), bash. FOR SINGLE OPERATIONS: Use Aspire CLI commands directly for quick resource status or doc lookups."
---

# Aspire Skill

Use this skill when the task is about operating an Aspire distributed application through the Aspire CLI rather than falling back to ad-hoc `dotnet`, `docker`, or shell workflows.

Resources are typically defined in an AppHost project such as `apphost.cs` or `apphost.ts`.

## Use this skill for

- Starting, restarting, and stopping AppHosts with `aspire start` and `aspire stop`
- Inspecting resources, logs, traces, and docs
- Adding integrations with `aspire add`
- Managing AppHost secrets and CLI config
- Publishing and deploying Aspire apps

## Do not use this skill for

- Non-Aspire .NET applications
- Container-only workflows that do not involve an Aspire AppHost
- Replacing normal build and test commands when the task is just compiling code or running unit tests

## Default workflow

1. Confirm that the workspace is an Aspire app and identify the AppHost.
2. Start the app with `aspire start`. Use `--isolated` in git worktrees or whenever shared local state would be risky.
3. Use `aspire wait <resource>` before interacting with a resource that needs to be healthy.
4. Inspect state with `aspire describe`, then use `aspire otel logs`, `aspire logs`, `aspire otel traces`, and `aspire export` before making code changes.
5. Re-run `aspire start` after AppHost changes instead of switching to `aspire run`.

## TypeScript AppHosts

When the AppHost is `apphost.ts`, the `.modules/` folder at the project root contains generated TypeScript modules that expose the Aspire APIs available to the AppHost. Common files include `.modules/aspire.ts`, `base.ts`, and `transport.ts`.

- Do not edit `.modules/` directly.
- Use `aspire add <package>` to add integrations and regenerate the available APIs.
- Inspect `.modules/aspire.ts` after `aspire add` to see the refreshed API surface.
- The local `tsconfig.json` often includes `.modules/**/*.ts` in its compilation scope.

## Key rules

- Prefer `aspire start` over `dotnet run` for AppHosts. `aspire run` blocks the terminal and is a poor fit for agent workflows.
- Re-running `aspire start` is the restart path. Do not combine `aspire stop` and `aspire run`.
- Use `--apphost <path>` when the workspace has multiple AppHosts or discovery is ambiguous.
- Use `--format Json` when another tool or script needs machine-readable output.
- Never install the obsolete Aspire workload.
- When a TypeScript AppHost uses `.modules/`, do not edit generated files directly. Use `aspire add` to regenerate APIs and inspect `.modules/aspire.ts` afterward.
- Prefer official docs from `aspire.dev` and `learn.microsoft.com/microsoft/aspire`.

## Common capabilities

- Use `aspire ps` when you need to discover running AppHosts before targeting one.
- Use `aspire update` when the task is to refresh AppHost package references through the supported CLI workflow.
- Use `aspire doctor` as an early diagnostics step when the local Aspire environment looks unhealthy.
- Use `aspire resource`, `aspire secret`, `aspire config`, `aspire publish`, `aspire deploy`, and `aspire do` when the objective is resource operations, secrets/config management, or deployment.
- Use `aspire restore`, `aspire cache clear`, `aspire certs trust`, and `aspire certs clean` when the task is local environment maintenance or recovery.

## Playwright CLI

If Playwright CLI is already configured in the environment, use Aspire first to discover the running app and its endpoints, then hand browser testing off to Playwright CLI.

## References

- For starting and restarting an AppHost, read `references/start-and-restart-apphost.md`.
- For choosing the right AppHost or getting machine-readable state, read `references/target-apphosts-and-resources.md`.
- For debugging a running app before changing code, read `references/investigate-running-app.md`.
- For resource-scoped operations, read `references/operate-on-a-resource.md`.
- For integrations and TypeScript AppHost workflows, read `references/integrations-and-typescript-apphosts.md`.
- For secrets and config management, read `references/manage-secrets-and-config.md`.
- For local environment maintenance and recovery, read `references/local-environment-maintenance.md`.
- For publish, deploy, and pipeline-step workflows, read `references/publish-deploy-and-pipeline-steps.md`.
- For Playwright handoff and bootstrap flows, read `references/playwright-and-bootstrap.md`.
- For investigation order and common agent workflows, read `references/agent-workflows.md`.