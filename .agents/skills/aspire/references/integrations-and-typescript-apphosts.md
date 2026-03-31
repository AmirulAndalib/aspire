# Integrations And TypeScript AppHosts

Use this when the goal is to discover the right integration first, update the AppHost, or work with a TypeScript AppHost.

## Integration Discovery And Updates

Use these commands when the task is about finding docs, adding an integration, or refreshing AppHost packages.

```bash
aspire docs search <query>
aspire docs list
aspire docs get <slug>
aspire add
aspire update
aspire start
```

Keep these points in mind when working with integrations:

- Use `aspire update` when the task is specifically to refresh AppHost package references.

## TypeScript AppHosts

Use this guidance when the AppHost is `apphost.ts`.

Keep these points in mind when working with TypeScript AppHosts:

- In TypeScript AppHosts, `aspire add` regenerates `.modules/` and refreshes `.modules/aspire.ts`.