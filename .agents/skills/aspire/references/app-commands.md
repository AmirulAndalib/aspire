# App Commands

Use this when the task is about app-level lifecycle, bootstrap, or AppHost-wide updates.

## Start, Stop, Or Restart An AppHost

Use these commands when the goal is to launch the app in the background, stop it cleanly, or pick up AppHost changes.

```bash
aspire start
aspire start --isolated
aspire stop
```

Keep these points in mind when using app lifecycle commands:

- Use `--isolated` in git worktrees or when another instance may already be running.
- Re-run `aspire start` to restart the AppHost.
- Avoid `aspire run` in normal agent workflows because it blocks the terminal.

## Bootstrap A New Aspire App

Use these commands when the task is to create or initialize an Aspire app.

```bash
aspire new
aspire init
```

Keep these points in mind when bootstrapping:

- Use `aspire new` when creating a new app from scratch.
- Use `aspire init` when adding Aspire to an existing project.

## Discover Or Update Running AppHosts

Use these commands when the task is about finding the right AppHost or refreshing AppHost package references.

```bash
aspire ps
aspire add
aspire update
aspire restore
```

Keep these points in mind when using app-wide commands:

- Use `aspire ps` first when multiple AppHosts may be running locally.
- Use `aspire add` to add integrations to the AppHost.
- Use `aspire update` when the task is specifically to refresh AppHost package references.
- Use `aspire restore` when local dependencies or tooling need to be restored before running the app again.