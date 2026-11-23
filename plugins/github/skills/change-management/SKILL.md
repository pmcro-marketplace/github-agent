---
name: change-management
description: Perform governed GitHub repository and change mutations, including repository creation, branches, files, issues, pull requests, reviews, merges, and releases.
---
# change-management

## Purpose
Mutate one explicitly resolved GitHub target and return provider-backed evidence.

## When to Use
Use for `/create-repository`, `/create-branch`, `/create-file`, `/update-file`, `/create-issue`, `/create-pull-request`, `/review-pull-request`, `/merge-pull-request`, or `/create-release`.

## When Not to Use
Do not mutate an ambiguous target. Do not use local git state as proof of remote GitHub state.

## Inputs
Exact GitHub target, function-specific inputs, authorization context, expected current state, and acceptance criteria.

## Deliverables
One concrete GitHub mutation with remote evidence, or `BLOCKED` / `NOT_INVOKED`.

## Workflow
1. Inspect authoritative target state first when needed.
2. Resolve the concrete function.
3. Emit the exact function identity.
4. Prefer the connected GitHub provider if it exposes that mutation.
5. If the provider does not expose the mutation, use authenticated `gh` on an authorized machine when available.
6. Verify the resulting GitHub state through the connected provider when possible.
7. Return IDs, URLs, SHAs, or explicit provider errors as evidence.

## Validation
Supported functions:
- `/github:change-management:create-repository`
- `/github:change-management:create-branch`
- `/github:change-management:create-file`
- `/github:change-management:update-file`
- `/github:change-management:create-issue`
- `/github:change-management:create-pull-request`
- `/github:change-management:review-pull-request`
- `/github:change-management:merge-pull-request`
- `/github:change-management:create-release`

`create-repository` implementation order:
1. connected GitHub provider create-repository mutation, if exposed;
2. native approved GitHub REST POST or authenticated `gh repo create`, if authorized and available;
3. otherwise `BLOCKED` / `NOT_INVOKED`.

## Pitfalls
GitHub authentication is not organization-admin authorization. Verify organization creation permission before claiming success.

## Resources
See `references/contract.md`, `references/create-repository.md`, and `assets/request.json`.
