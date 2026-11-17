---
name: local-actions
description: Rehearse GitHub Actions locally with nektos act and Docker without confusing rehearsal with remote GitHub execution.
---
# local-actions

## Purpose
Provide bounded local workflow rehearsal before remote execution.

## When to Use
Use for `/rehearse-actions` when an authorized local host has Docker and `act`.

## When Not to Use
Do not claim a local rehearsal is a GitHub-hosted Actions run. Do not inject secrets without explicit authorization.

## Inputs
Local repository/workflow target, event, permitted inputs/secrets, and expected outcome.

## Deliverables
Local rehearsal evidence or `BLOCKED` / `NOT_INVOKED`.

## Workflow
1. Verify Docker and `act`.
2. Resolve the workflow/event.
3. Run bounded rehearsal.
4. Capture local output/exit status.
5. Label evidence as local rehearsal.

## Validation
Supported function:
- `/github:local-actions:rehearse`

## Pitfalls
Local environment parity with GitHub-hosted runners is not guaranteed.

## Resources
See `references/contract.md` and `assets/request.json`.
