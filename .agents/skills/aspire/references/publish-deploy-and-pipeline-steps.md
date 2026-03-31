# Publish Deploy And Pipeline Steps

Use this when the task is about deployment artifacts, deployment execution, or a named pipeline step.

Common commands:

```bash
aspire publish
aspire deploy
aspire deploy --clear-cache
aspire do <step>
```

Notes:

- `aspire publish` generates deployment artifacts.
- `aspire deploy --clear-cache` is the reset path when cached deployment state is stale or stuck.