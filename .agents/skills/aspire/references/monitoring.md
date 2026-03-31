# Monitoring

Use this when the task is about inspecting app state, logs, traces, endpoints, or sharable diagnostics.

## Inspect App State And Endpoints

Use these commands when you need the current resource view or a machine-readable representation of the running app.

```bash
aspire describe
aspire resources
aspire describe --apphost <path>
aspire describe --apphost <path> --format Json
```

Keep these points in mind when inspecting app state:

- Use `--apphost <path>` when the workspace has multiple AppHosts or discovery is ambiguous.
- Prefer `--format Json` when another tool or script needs to consume the result.

## View Logs And Telemetry

Use these commands when the task is to investigate behavior before changing code.

```bash
aspire logs [resource]
aspire otel logs [resource]
aspire otel logs --trace-id <id>
aspire otel traces [resource]
aspire otel spans [resource]
```

Keep these points in mind when working with telemetry:

- Prefer structured telemetry before raw console logs when possible.
- Use the trace-filtered log command when you already have a trace id.

## Export Diagnostics

Use this command when you need a portable handoff artifact for deeper analysis.

```bash
aspire export [resource]
```

Keep this point in mind when exporting diagnostics:

- Use `aspire export` when you need a sharable bundle of telemetry and resource state.