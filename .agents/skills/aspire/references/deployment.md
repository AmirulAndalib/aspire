# Deployment

Use this when the task is about deployment artifacts, deployment execution, or named pipeline steps.

## Publish And Deploy

Use these commands when the task is to generate deployment artifacts and deploy them.

```bash
aspire publish
aspire deploy
aspire deploy --clear-cache
```

Keep these points in mind when deploying:

- Use `aspire publish` to generate deployment artifacts.
- Use `aspire deploy --clear-cache` when cached deployment state is stale or stuck.

## Run A Deployment Step

Use this command when the task is to execute a named pipeline step without rerunning the full deployment flow.

```bash
aspire do <step>
```

Keep this point in mind when running deployment steps:

- Use `aspire do` when the task is about a specific pipeline step such as seeding data.

Additionally, to see the full deployment step pipeline call:

```bash
aspire do diaganostics
```