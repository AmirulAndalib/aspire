# Aspire Agent Workflows

Use these patterns when a task needs investigation or orchestration rather than a one-off command lookup.

## Running in agent environments

Start the AppHost with `aspire start` so the CLI manages background execution. In git worktrees, use `--isolated` to avoid port conflicts and shared local state:

```bash
aspire start --isolated
```

Wait for a resource before interacting with it:

```bash
aspire start --isolated
aspire wait myapi
```

Relaunching is safe. Re-run `aspire start` whenever AppHost changes need to be picked up.

## Debugging before code changes

Inspect the live app before editing code:

1. `aspire describe` to check resource state.
2. `aspire otel logs <resource>` to inspect structured logs.
3. `aspire logs <resource>` to inspect console output.
4. `aspire otel traces <resource>` to follow cross-service activity.
5. `aspire export` when you need a zipped telemetry snapshot for deeper analysis.

## Adding integrations

Use the docs commands first, then add the integration:

```bash
aspire docs search postgres
aspire docs get <slug>
aspire add
```

After adding an integration, restart with `aspire start` so the updated AppHost takes effect.

## TypeScript AppHosts

If the AppHost is `apphost.ts`, the `.modules/` directory contains generated TypeScript modules that expose Aspire APIs.

- Do not edit `.modules/` directly.
- Use `aspire add <package>` to regenerate the available APIs.
- Inspect `.modules/aspire.ts` after `aspire add` to see the newly available APIs.

## Secrets and deployment

Use `aspire secret` for AppHost user secrets, especially connection strings and passwords:

```bash
aspire secret set Parameters:postgres-password MySecretValue
aspire secret list
```

Use `aspire publish` to generate deployment artifacts and `aspire deploy` to deploy them.

## Playwright CLI

If Playwright CLI is configured in the environment, use it for functional testing after you discover the relevant endpoints with `aspire describe`.