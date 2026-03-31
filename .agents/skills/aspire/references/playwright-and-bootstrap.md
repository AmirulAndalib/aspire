# Playwright And Bootstrap

Use this when the task is either browser testing against a running Aspire app or creating an Aspire app from scratch.

## Playwright Handoff

Use these commands when browser testing should begin from a running Aspire app.

```bash
aspire describe
playwright-cli --help
```

Keep these points in mind when handing off to Playwright CLI:

- Use Aspire first to discover the right endpoints before invoking Playwright CLI.

## Bootstrap

Use these commands when the task is to create or initialize an Aspire app.

```bash
aspire new
aspire init
```
