---
name: sync
description: Synchronize authorized source to an existing GitHub repository and verify the remote commit.
---
# sync

## Purpose
Push authorized source while preserving authored-source/generated-output boundaries and proving the remote result.

## When to Use
Use for `/sync` when an existing GitHub repository should receive authorized source.

## When Not to Use
Do not silently overwrite unrelated remote work or treat a local commit as proof of remote synchronization.

## Inputs
Source location/content, target repository/ref, allowed paths, expected remote state, and authorization.

## Deliverables
Verified remote synchronization or `BLOCKED` / `NOT_INVOKED`.

## Workflow
1. Inspect target repository/ref.
2. Determine the authorized source and changed paths.
3. Apply the narrowest GitHub write mechanism.
4. Verify the remote commit SHA and changed files.

## Validation
Supported function:
- `/github:sync:sync`

A PASS requires remote GitHub evidence.

## Pitfalls
Generated outputs are not authoritative when a separately authored source of truth exists.

## Resources
See `references/contract.md` and `assets/request.json`.


## Script reference

Scripts are not loaded into context. An agent runs one only when a step needs it, and you can run the same command by hand from this skill's folder.

| Script | Purpose | Manual run |
|---|---|---|
| `scripts/validate.py` | Structural self-check: verifies the skill's required files (and request.json contents where applicable). Takes no arguments; prints `PASS: <skill>` and exits 0, or prints `FAIL: ...` and exits 1. | `python scripts/validate.py` |
