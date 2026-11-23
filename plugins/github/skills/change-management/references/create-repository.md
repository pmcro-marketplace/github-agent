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
2. If it already exists, stop the creation request; use the read capability to verify and reuse the existing repository.
3. Prefer a connected GitHub provider create-repository function when available.
4. Otherwise, invoke the native approved REST binding (or authenticated `gh repo create` on an authorized machine). The exact request requires an explicit visibility and separate host approval.
5. Verify with GitHub `get repository`.
6. Return repository URL, visibility, permissions, and provider evidence.

Organization creation must fail closed if the authenticated identity lacks organization creation permission.
