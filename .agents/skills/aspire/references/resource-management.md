# Resource Management

Use this when the task is scoped to one resource or depends on a resource becoming healthy.

## Wait For Resource Health

Use these commands when the next step should wait for a resource to become ready.

```bash
aspire wait <resource>
aspire wait <resource> --status up --timeout 60
```

Keep these points in mind when waiting on resources:

- Use `aspire wait` before interacting with a resource that must be healthy.
- Add `--status` and `--timeout` when the task needs more explicit readiness checks.

## Operate On A Resource

Use these commands when the task is about one running resource rather than the whole AppHost.

```bash
aspire resource <resource> start
aspire resource <resource> stop
aspire resource <resource> restart
aspire resource <resource> <command>
```

Keep these points in mind when operating on resources:

- Prefer resource-scoped commands when the task does not require a full AppHost restart.