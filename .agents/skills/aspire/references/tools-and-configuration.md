# Tools And Configuration

Use this when the task is about docs lookup, secrets, CLI configuration, diagnostics, cache cleanup, or local certificates.

## Docs

Use these commands when the task is to discover or read Aspire documentation before making changes.

```bash
aspire docs search <query>
aspire docs list
aspire docs get <slug>
```

Keep these points in mind when using docs commands:

- Use docs commands before changing integrations when you need to confirm the right workflow.

## Secrets

Use these commands when the task is about AppHost user secrets.

```bash
aspire secret set <key> <value>
aspire secret get <key>
aspire secret list
aspire secret path
aspire secret delete <key>
```

Keep these points in mind when using secret commands:

- Use `aspire secret` for connection strings, passwords, and other AppHost user secrets.
- Use `aspire secret path` when the task is to locate the backing store without opening it manually.

## Config

Use these commands when the task is about Aspire CLI configuration.

```bash
aspire config set <key> <value>
aspire config get <key>
aspire config list
aspire config delete <key>
aspire config info
```

Keep these points in mind when using config commands:

- Use `aspire config info` to see where configuration comes from, including local and global sources, which features are available, and the properties of the settings files.

## Local Diagnostics And Repair

Use these commands when the local Aspire environment looks unhealthy and needs recovery steps.

```bash
aspire doctor
aspire cache clear
aspire certs trust
aspire certs clean
```

Keep these points in mind when repairing the local environment:

- Use `aspire doctor` early when the symptoms suggest local environment drift rather than an app bug.
- Use `aspire cache clear` when cached state is stale or interfering with normal operation.
- Use `aspire certs trust` and `aspire certs clean` when local certificate state is part of the problem.