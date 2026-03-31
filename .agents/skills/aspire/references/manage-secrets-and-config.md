# Manage Secrets And Config

Use this when the objective is to set, inspect, locate, or clean up AppHost secrets or Aspire CLI configuration without editing files manually.

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