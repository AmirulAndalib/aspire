# Operate On A Resource

Use this when the task is scoped to one resource rather than the entire AppHost.

Common commands:

```bash
aspire resource <resource> start
aspire resource <resource> stop
aspire resource <resource> restart
aspire resource <resource> <command>
```

Notes:

- Prefer resource-scoped commands when the task does not require a full AppHost restart.