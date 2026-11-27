---
name: repository-discovery
description: Read authoritative GitHub repository state before mutation.
---
# repository-discovery

## Purpose
Inspect repositories, branches, files, commits, releases, issues, pull requests, and workflow definitions without mutating them.

## When to Use
Use for `/inspect`, `/list-repositories`, or `/get-repository`, and before governed mutations when current state matters.

## When Not to Use
Do not create repositories or mutate repository state.

## Inputs
Repository owner/name or other exact GitHub target plus optional ref/path/query.

## Deliverables
Authoritative GitHub observations with URLs/IDs/SHAs when available.

## Workflow
1. Resolve the exact GitHub target.
2. Invoke the connected GitHub read provider.
3. Preserve source refs and identifiers.
4. Return evidence for downstream mutation/checking.

## Validation
Supported functions:
- `/github:repository-discovery:inspect`
- `/github:repository-discovery:list-repositories`
- `/github:repository-discovery:get-repository`

## Pitfalls
A 404 can mean missing or inaccessible; organization-level evidence may be needed to disambiguate.

## Resources
See `references/contract.md` and `assets/request.json`.


## Script reference

Scripts are not loaded into context. An agent runs one only when a step needs it, and you can run the same command by hand from this skill's folder.

| Script | Purpose | Manual run |
|---|---|---|
| `scripts/validate.py` | Structural self-check: verifies the skill's required files (and request.json contents where applicable). Takes no arguments; prints `PASS: <skill>` and exits 0, or prints `FAIL: ...` and exits 1. | `python scripts/validate.py` |
