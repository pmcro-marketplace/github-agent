# GitHub Agent

Canonical repository: `pmcro-marketplace/github-agent`.

## Identity

```text
Agent:  @github-agent
Plugin: github
```

Skills are subagents; functions are concrete GitHub operations.

```text
github
├── repository-discovery
├── change-management
├── actions
├── local-actions
└── sync
```

## Provider resolution

For a concrete GitHub function:

1. Prefer the connected GitHub provider when it exposes that function.
2. If the connected provider lacks that mutation, an authenticated `gh` CLI on an authorized machine may implement the same GitHub function.
3. Verify the resulting GitHub state through the connected provider when possible.
4. If no real provider can execute the function, return `BLOCKED` or `NOT_INVOKED`.

Example: `/create-repository` may use `gh repo create` when the connected provider has no create-repository mutation.

See `INVOCATION.md` for the exact function map.
