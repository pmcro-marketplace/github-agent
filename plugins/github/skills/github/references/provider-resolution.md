# Provider Resolution

For every concrete GitHub function:

1. Use the connected GitHub provider when it exposes the exact required operation.
2. If that operation is not exposed, use the native MAF GitHub REST binding with a host-provided authorized credential and exact one-use approval, or authenticated GitHub CLI (`gh`) on an authorized machine for the same operation.
3. Verify the result through the connected GitHub provider whenever possible.
4. If no authorized real provider can execute the operation, return `BLOCKED` or `NOT_INVOKED`.

Do not call a different operation merely because it is available.

Example:

```text
@github-agent /create-repository pmcro-marketplace/example-agent
-> /github:change-management:create-repository
-> GitHub connector create-repository, if exposed
   OR `gh repo create ...`, if connector lacks the mutation
-> GitHub provider get-repository verification
```
