# Target AppHosts And Resources

Use this when multiple AppHosts may be running or the task needs a scoped, machine-readable view of the current app state.

Common commands:

```bash
aspire ps
aspire describe
aspire resources
aspire describe --apphost <path>
aspire describe --apphost <path> --format Json
```

Notes:

- Use `aspire ps` first when you need to discover which AppHost to inspect.
- Prefer `--format Json` when another tool or script needs to consume the result.