---
name: actions
description: Inspect, dispatch, diagnose, and verify remote GitHub Actions workflows.
---
# actions

## Purpose
Operate remote GitHub Actions while distinguishing workflow configuration from actual run evidence.

## When to Use
Use for `/inspect-actions`, `/dispatch`, or `/verify-run`.

## When Not to Use
Do not treat a workflow YAML file as proof a workflow ran. Do not use this subagent for local `act` rehearsal.

## Inputs
Exact repository, workflow/run selector, ref and inputs when dispatching, authorization, and acceptance criteria.

## Deliverables
Workflow/run evidence or `BLOCKED` / `NOT_INVOKED`.

## Workflow
1. Resolve repository and workflow/run.
2. Invoke the connected GitHub Actions provider.
3. For dispatch, capture the provider result and then query run state.
4. Verify run conclusion from GitHub evidence.

## Validation
Supported functions:
- `/github:actions:inspect`
- `/github:actions:dispatch`
- `/github:actions:verify-run`

## Pitfalls
Configured is not dispatched. Dispatched is not successful.

## Resources
See `references/contract.md` and `assets/request.json`.


## Script reference

Scripts are not loaded into context. An agent runs one only when a step needs it, and you can run the same command by hand from this skill's folder.

| Script | Purpose | Manual run |
|---|---|---|
| `scripts/validate.py` | Structural self-check: verifies the skill's required files (and request.json contents where applicable). Takes no arguments; prints `PASS: <skill>` and exits 0, or prints `FAIL: ...` and exits 1. | `python scripts/validate.py` |
