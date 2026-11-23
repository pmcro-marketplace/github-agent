---
name: github
description: GitHub domain router that resolves concrete GitHub functions to the narrowest GitHub subagent and real provider.
---
# github

## Purpose
Route GitHub requests to repository discovery, change management, Actions, local Actions rehearsal, or synchronization.

## When to Use
Use when a GitHub request must first be resolved to the narrowest subagent/function.

## When Not to Use
Do not replace the subagent that owns the concrete function or claim provider execution from routing alone.

## Inputs
A bounded GitHub request, target identity, authorization context, desired function, and evidence.

## Deliverables
A concrete function resolution or `BLOCKED` / `NOT_INVOKED` with evidence.

## Workflow
1. Resolve `@github-agent` -> plugin `github`.
2. Select the narrowest GitHub subagent.
3. Emit the concrete internal function identity.
4. Prefer the connected GitHub provider.
5. If that provider lacks the required mutation, use the available native REST binding with host-approved exact request and credential, or authenticated `gh` on an authorized machine.
6. Verify the resulting GitHub state.

## Validation
A PASS requires target-function evidence, not routing evidence.

## Pitfalls
Emission is not invocation. Connector availability does not imply every GitHub API mutation is exposed.

## Resources
See `references/contract.md`, `references/provider-resolution.md`, `references/write-authorization.md`, and `assets/request.json`.
