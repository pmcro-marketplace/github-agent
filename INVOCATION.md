# Invocation Contract

Human surface:

```text
@github-agent /<function> [arguments]
```

Internal identity:

```text
/github:<skill>:<function>
```

## Repository discovery

```text
@github-agent /inspect
  -> /github:repository-discovery:inspect

@github-agent /list-repositories
  -> /github:repository-discovery:list-repositories

@github-agent /get-repository
  -> /github:repository-discovery:get-repository
```

## Repository and change management

```text
@github-agent /create-repository
  -> /github:change-management:create-repository

@github-agent /create-branch
  -> /github:change-management:create-branch

@github-agent /create-file
  -> /github:change-management:create-file

@github-agent /update-file
  -> /github:change-management:update-file

@github-agent /create-issue
  -> /github:change-management:create-issue

@github-agent /create-pull-request
  -> /github:change-management:create-pull-request

@github-agent /review-pull-request
  -> /github:change-management:review-pull-request

@github-agent /merge-pull-request
  -> /github:change-management:merge-pull-request

@github-agent /create-release
  -> /github:change-management:create-release
```

## GitHub Actions

```text
@github-agent /inspect-actions
  -> /github:actions:inspect

@github-agent /dispatch
  -> /github:actions:dispatch

@github-agent /verify-run
  -> /github:actions:verify-run

@github-agent /rehearse-actions
  -> /github:local-actions:rehearse
```

## Synchronization

```text
@github-agent /sync
  -> /github:sync:sync
```

## Provider law

```text
RESOLVE
-> EMIT exact function
-> connected GitHub provider if available
-> otherwise authenticated `gh` when authorized and available
-> VERIFY GitHub state
-> EVIDENCE
```

Hook Agent may surround applicable execution only when a real lifecycle provider exists.

Emission is not invocation.
A local `git` commit is not proof of a remote GitHub mutation.
A workflow file is not proof of an Actions run.
