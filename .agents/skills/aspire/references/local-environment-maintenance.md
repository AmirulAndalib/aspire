# Local Environment Maintenance

Use this when the local Aspire environment itself looks unhealthy and you need recovery or cleanup commands before changing app code.

Common commands:

```bash
aspire restore
aspire cache clear
aspire certs trust
aspire certs clean
```

Notes:

- Use `aspire restore` when local dependencies or tooling need to be restored before running the app again.
- Use `aspire cache clear` when cached state is stale or interfering with normal operation.
- Use `aspire certs trust` and `aspire certs clean` when local certificate state is part of the problem.