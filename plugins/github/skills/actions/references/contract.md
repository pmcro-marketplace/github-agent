# GitHub Actions Contract

Functions:
- `inspect` -> `/github:actions:inspect`
- `dispatch` -> `/github:actions:dispatch`
- `verify-run` -> `/github:actions:verify-run`

Workflow configuration is not run evidence.

Native dispatch requires a numeric workflow ID, branch ref, credential and
exact host approval. The 2026-03-10 REST contract returns a run ID. Verify
workflow ID, event, branch and SHA independently. A verified queued run is
not a successful completed run. Custom inputs are not yet bound.
