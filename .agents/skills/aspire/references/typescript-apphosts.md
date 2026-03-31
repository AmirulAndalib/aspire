# TypeScript AppHosts

Use this when the AppHost is `apphost.ts` and the task involves generated APIs or TypeScript-specific Aspire workflows.

## Generated Modules

Use this guidance when the task touches `.modules/` or newly added integrations.

Keep these points in mind when working with TypeScript AppHosts:

- The `.modules/` folder contains generated TypeScript modules that expose Aspire APIs to the AppHost.
- Common generated files include `.modules/aspire.ts`, `base.ts`, and `transport.ts`.
- Do not edit `.modules/` directly.
- Use `aspire add <package>` to regenerate the available APIs.
- Inspect `.modules/aspire.ts` after `aspire add` to see the refreshed API surface.
- The local `tsconfig.json` often includes `.modules/**/*.ts` in its compilation scope.

Notes:

- `aspire restore` explicitly restores (and generates) `.modules/*`.