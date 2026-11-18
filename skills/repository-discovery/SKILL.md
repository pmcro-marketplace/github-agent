---
name: repository-discovery
description: Read authoritative GitHub repository state before mutation. USE FOR read-only repository, branch, file, commit, release, issue, pull request, or workflow discovery. DO NOT USE FOR mutation, Actions dispatch, local rehearsal, or synchronization; use change-management, actions, local-actions, or sync.
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
See `references/contract.md` and `assets/request.json`, and `scripts/validate.py`.

