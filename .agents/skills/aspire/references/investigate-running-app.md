# Investigate A Running App

Use this when the right first move is to inspect the running system rather than edit code immediately.

Common commands:

```bash
aspire describe
aspire otel logs <resource>
aspire logs <resource>
aspire otel traces <resource>
aspire otel spans <resource>
aspire otel logs --trace-id <id>
aspire export [resource]
aspire doctor
```

Notes:

- Prefer structured telemetry before raw console logs when possible.
- Use `aspire export` when you need a portable telemetry snapshot for deeper analysis or handoff.
- Use `aspire doctor` early when the symptoms suggest local environment drift rather than an app bug.