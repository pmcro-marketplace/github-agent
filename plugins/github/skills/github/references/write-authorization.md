# Native authenticated writes (v0.5.0)

The official GitHub REST 2026-03-10 API supports:

- POST /orgs/pmcro-marketplace/repos: 201 and repository ID; repository
  creation or administration write permission; explicit visibility.
- POST /repos/{owner}/{repo}/actions/workflows/{workflow_id}/dispatches:
  200 and workflow_run_id; Actions write permission. Workflow must be
  dispatchable; GitHub requires the trigger on the default branch.

References:
- https://docs.github.com/en/rest/repos/repos#create-an-organization-repository
- https://docs.github.com/en/rest/actions/workflows#create-a-workflow-dispatch-event
- https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch

## Operator gate

Set PMCRO_GITHUB_TOKEN in the host's secret environment using the approved
credential provider. Do not put the credential in a request, approval,
repository, UI or trail. Set PMCRO_GITHUB_APPROVAL_DIR to a private directory
owned by the host operator (not writable by remote agents). A local bearer
PMCRO_MAF_TOKEN protects A2A and AG-UI.

Create one request file containing the exact desired operation and a fresh
UUID in approvalId, for example:

```json
{"capability":"/github:change-management:create-repository","repository":"pmcro-marketplace/example-agent","visibility":"private","approvalId":"00000000-0000-4000-8000-000000000001"}
```

For dispatch:

```json
{"capability":"/github:actions:dispatch","repository":"pmcro-marketplace/github-agent","workflowId":427262368,"ref":"main","approvalId":"00000000-0000-4000-8000-000000000002"}
```

These examples are illustrative; do not execute them without confirming the
exact target. Once the operator has reviewed the exact bytes, run:

```sh
python plugins/github/skills/github/scripts/invoke_write.py approve request.json --approval-dir "$PMCRO_GITHUB_APPROVAL_DIR" --approve
python plugins/github/skills/github/scripts/invoke_write.py invoke request.json --port PORT
```

The approval is limited to the exact request bytes, expires in ten minutes,
and is consumed atomically before any POST. Never remove a claim to retry.
If the outcome is uncertain, independently inspect the repository or run
before authorizing a new request. Default visibility is never inferred.
GitHub may refuse the request based on organization or repository policy.

Plan validates target, approval and provider preflight. Make sends exactly
one POST. Check performs independent GET verification. Reflect accepts only
verified identity; a dispatched run still requires a separate terminal
status, job, log and artifact check before asserting workflow success.

## Test boundary

The isolated .NET HTTP fixture tests exercise POST payloads and verification
without contacting GitHub. The public native A2A/AG-UI test exercises actual
MAF workflow and GitHub reads, with both missing-credential writes rejected.
Live authenticated writes require a separate authorized host credential.
