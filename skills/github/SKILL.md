---
name: github
description: GitHub domain router that resolves concrete GitHub functions to the narrowest GitHub subagent and real provider. USE FOR routing an unresolved GitHub request to the narrowest GitHub capability. DO NOT USE FOR performing the resolved mutation, Actions operation, local rehearsal, discovery, or synchronization; use the selected sibling skill.
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
5. If that provider lacks the required mutation, use authenticated `gh` only when an authorized machine is available.
6. Verify the resulting GitHub state.

## Validation
A PASS requires target-function evidence, not routing evidence.

## Pitfalls
Emission is not invocation. Connector availability does not imply every GitHub API mutation is exposed.

## Resources
See `references/contract.md`, `references/provider-resolution.md`, and `assets/request.json`, and `scripts/validate.py`.

