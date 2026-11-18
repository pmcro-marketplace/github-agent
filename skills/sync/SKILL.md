---
name: sync
description: Synchronize authorized source to an existing GitHub repository and verify the remote commit. USE FOR pushing authorized source to an existing repository/ref and proving the resulting remote commit. DO NOT USE FOR repository creation, general mutations, discovery-only work, or Actions operation; use change-management, repository-discovery, or actions.
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
See `references/contract.md` and `assets/request.json`, and `scripts/validate.py`.

