# Playwright Handoff

Use this when Playwright CLI is already configured and the next step is browser testing against a running Aspire app.

## Discover Endpoints Before Browser Testing

Use these commands when the task is to hand off from Aspire state inspection to Playwright CLI.

```bash
aspire describe
playwright-cli --help
```

Keep these points in mind when handing off to Playwright CLI:

- Use Aspire first to discover the right endpoints before invoking Playwright CLI.
- Do not guess frontend endpoints without first consulting Aspire state.