# Start And Restart An AppHost

Use this when the goal is to launch the app in the background, restart it after AppHost changes, or stop it cleanly.

Common commands:

```bash
aspire start
aspire start --isolated
aspire wait <resource>
aspire wait <resource> --status up --timeout 60
aspire stop
```

Notes:

- Use `--isolated` in git worktrees or when another instance may already be running.
- Re-run `aspire start` to restart the AppHost. Do not switch to `aspire run` for normal agent workflows.