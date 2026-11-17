# Create Repository

Canonical capability:

`/github:change-management:create-repository`

Required inputs:
- owner or organization
- repository name
- visibility
- optional description
- optional initialization behavior

Procedure:
1. Check exact repository existence.
2. If it already exists, return the existing repository rather than duplicating it.
3. Prefer a connected GitHub provider create-repository function when available.
4. Otherwise, on an authorized machine with authenticated `gh`, use `gh repo create`.
5. Verify with GitHub `get repository`.
6. Return repository URL, visibility, permissions, and provider evidence.

Organization creation must fail closed if the authenticated identity lacks organization creation permission.
