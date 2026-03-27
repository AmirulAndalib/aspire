# Pipeline Generation for Aspire

## Status

**Stage:** Spike / Proof of Concept
**Authors:** Aspire Team
**Date:** 2025

## Summary

This document describes the architecture, API primitives, and approach for generating CI/CD pipeline definitions (e.g., GitHub Actions workflows, Azure DevOps pipelines) from an Aspire application model. The core idea is that developers can declare pipeline structure in their AppHost code and Aspire generates the corresponding workflow YAML files, with each step mapped to CI/CD jobs that invoke `aspire deploy --continue` to execute the subset of pipeline steps appropriate for that job.

## Motivation

Today, `aspire publish`, `aspire deploy`, and `aspire do [step]` execute pipeline steps locally in a single process. This works well for developer inner-loop, but production deployments typically need:

- **CI/CD integration** — Steps should run in GitHub Actions jobs, Azure DevOps stages, etc.
- **Parallelism** — Independent steps (e.g., building multiple services) should run on separate agents.
- **State management** — Intermediate artifacts must flow between jobs.
- **Auditability** — The workflow YAML is version-controlled alongside the app code.

Pipeline generation bridges this gap: developers define workflow structure in C#, and `aspire pipeline init` emits the workflow files.

## Architecture Overview

```text
┌──────────────────────────────────────┐
│            AppHost Code              │
│                                      │
│  var wf = builder                    │
│    .AddGitHubActionsWorkflow("ci");  │
│  var build = wf.AddJob("build");     │
│  var deploy = wf.AddJob("deploy");   │
│                                      │
│  builder.Pipeline.AddStep(           │
│    "build-app", ...,                 │
│    scheduledBy: build);              │
│  builder.Pipeline.AddStep(           │
│    "deploy-app", ...,                │
│    scheduledBy: deploy);             │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│      Scheduling Resolver             │
│                                      │
│  • Maps steps → jobs                 │
│  • Projects step DAG onto job graph  │
│  • Validates no cycles               │
│  • Computes `needs:` dependencies    │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│      YAML Generator (future)         │
│                                      │
│  • Emits .github/workflows/*.yml     │
│  • Includes state upload/download    │
│  • Each job runs `aspire do --cont.` │
└──────────────────────────────────────┘
```

## Core Abstractions

### `IPipelineEnvironment`

A marker interface extending `IResource` that identifies a resource as a pipeline execution environment. This follows the same pattern as `IComputeEnvironmentResource` in the hosting model.

```csharp
[Experimental("ASPIREPIPELINES001")]
public interface IPipelineEnvironment : IResource
{
}
```

Pipeline environments are added to the application model like any other resource. The system resolves the active environment at runtime by checking annotations.

### `PipelineEnvironmentCheckAnnotation`

An annotation applied to `IPipelineEnvironment` resources that determines whether the environment is relevant for the current invocation. This follows the existing annotation-based pattern used by `ComputeEnvironmentAnnotation` and `DeploymentTargetAnnotation`.

```csharp
[Experimental("ASPIREPIPELINES001")]
public class PipelineEnvironmentCheckAnnotation(
    Func<PipelineEnvironmentCheckContext, Task<bool>> checkAsync) : IResourceAnnotation
{
    public Func<PipelineEnvironmentCheckContext, Task<bool>> CheckAsync { get; } = checkAsync;
}
```

For example, a GitHub Actions environment would check for the `GITHUB_ACTIONS` environment variable.

### Environment Resolution

`DistributedApplicationPipeline.GetEnvironmentAsync()` resolves the active environment:

1. Scan the application model for all `IPipelineEnvironment` resources.
2. For each, invoke its `PipelineEnvironmentCheckAnnotation.CheckAsync()`.
3. If exactly one passes → return it.
4. If none pass → return `LocalPipelineEnvironment` (internal fallback).
5. If multiple pass → throw `InvalidOperationException`.

### `IPipelineStepTarget`

An interface that pipeline job objects implement. It provides the link between a pipeline step and the CI/CD construct (job, stage, etc.) it should run within.

```csharp
[Experimental("ASPIREPIPELINES001")]
public interface IPipelineStepTarget
{
    string Id { get; }
    IPipelineEnvironment Environment { get; }
}
```

### `PipelineStep.ScheduledBy`

The `PipelineStep` class gains a `ScheduledBy` property:

```csharp
public IPipelineStepTarget? ScheduledBy { get; set; }
```

When set, the step is intended to execute within the context of a specific job. When null, the step is assigned to a default target (first declared job, or a synthetic "default" job if none declared).

### `IDistributedApplicationPipeline.AddStep()` — Extended

The `AddStep` method gains a `scheduledBy` parameter:

```csharp
void AddStep(string name, Func<PipelineStepContext, Task> action,
             object? dependsOn = null, object? requiredBy = null,
             IPipelineStepTarget? scheduledBy = null);
```

## GitHub Actions Implementation

### `GitHubActionsWorkflowResource`

A `Resource` + `IPipelineEnvironment` that represents a GitHub Actions workflow file.

```csharp
var workflow = builder.AddGitHubActionsWorkflow("deploy");
var buildJob = workflow.AddJob("build");
var deployJob = workflow.AddJob("deploy");
```

- `WorkflowFileName` → `"deploy.yml"`
- `Jobs` → ordered list of `GitHubActionsJob`

### `GitHubActionsJob`

Implements `IPipelineStepTarget`. Properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | `string` | (required) | Job identifier in the YAML |
| `DisplayName` | `string?` | `null` | Human-readable `name:` in YAML |
| `RunsOn` | `string` | `"ubuntu-latest"` | Runner label |
| `DependsOnJobs` | `IReadOnlyList<string>` | `[]` | Explicit job-level `needs:` |

Jobs can declare explicit dependencies:

```csharp
deployJob.DependsOn(buildJob); // Explicit job dependency
```

### Scheduling Resolver

The scheduling resolver is the core algorithm that projects the step DAG onto the job dependency graph. Given a set of pipeline steps (some with `ScheduledBy` set), it:

1. **Assigns steps to jobs** — Steps with `ScheduledBy` use that job; unassigned steps go to a default job.
2. **Projects step dependencies onto job dependencies** — If step A (on job X) depends on step B (on job Y), then job X needs job Y.
3. **Merges explicit job dependencies** — Any `DependsOn` calls on jobs are included.
4. **Validates the job graph is a DAG** — Uses three-state DFS cycle detection.
5. **Groups steps per job** — For YAML generation.

#### Default Job Selection

- No jobs declared → creates synthetic `"default"` job
- One job → uses it as default
- Multiple jobs → uses the first declared job

#### Error Cases

| Scenario | Error |
|----------|-------|
| Step scheduled on job from different workflow | `SchedulingValidationException` |
| Step assignments create circular job deps | `SchedulingValidationException` with cycle path |
| Explicit job deps create cycle | `SchedulingValidationException` |

### Example: End-to-End

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add application resources
var api = builder.AddProject<Projects.Api>("api");
var web = builder.AddProject<Projects.Web>("web");

// Define CI/CD workflow
var workflow = builder.AddGitHubActionsWorkflow("deploy");
var publishJob = workflow.AddJob("publish");
var deployJob = workflow.AddJob("deploy");

// Pipeline steps with scheduling
builder.Pipeline.AddStep("build-images", BuildImagesAsync,
    scheduledBy: publishJob);
builder.Pipeline.AddStep("push-images", PushImagesAsync,
    dependsOn: "build-images",
    scheduledBy: publishJob);
builder.Pipeline.AddStep("deploy-infra", DeployInfraAsync,
    dependsOn: "push-images",
    scheduledBy: deployJob);
builder.Pipeline.AddStep("deploy-apps", DeployAppsAsync,
    dependsOn: "deploy-infra",
    scheduledBy: deployJob);
```

The resolver computes:

- **`publish` job**: `build-images` → `push-images` (no `needs:`)
- **`deploy` job**: `deploy-infra` → `deploy-apps` (`needs: publish`)

## Generated Workflow Structure (Future)

The YAML generator (not yet implemented) would produce:

```yaml
name: deploy
on:
  workflow_dispatch:

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
      - name: Install Aspire CLI
        run: dotnet tool install -g aspire
      - name: Run pipeline steps
        run: aspire do --continue --job publish
      - name: Upload state
        uses: actions/upload-artifact@v4
        with:
          name: aspire-state-publish
          path: .aspire/state/

  deploy:
    runs-on: ubuntu-latest
    needs: [publish]
    steps:
      - uses: actions/checkout@v4
      - name: Download state
        uses: actions/download-artifact@v4
        with:
          name: aspire-state-publish
          path: .aspire/state/
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
      - name: Install Aspire CLI
        run: dotnet tool install -g aspire
      - name: Run pipeline steps
        run: aspire do --continue --job deploy
```

### `--continue` and `--job` Semantics (Future)

When `aspire do --continue --job <jobId>` is invoked:

1. The AppHost starts and builds the pipeline as usual.
2. It reads the job ID from the CLI argument.
3. It runs only the steps assigned to that job (per the scheduling resolver).
4. State from previous jobs is already available via downloaded artifacts.

## State Management (Future)

Inter-job state is managed through CI/CD artifacts:

- **State directory**: `.aspire/state/`
- **Upload**: Each job uploads its state after execution
- **Download**: Each job downloads state from its dependency jobs before execution
- **Content**: Serialized pipeline context, resource connection strings, provisioned resource metadata
- **Security**: No secrets in artifacts — secrets use CI/CD native secret management

## Extensibility

### Adding New CI/CD Providers

New providers implement:

1. A `Resource` + `IPipelineEnvironment` class (like `GitHubActionsWorkflowResource`)
2. A job/stage class implementing `IPipelineStepTarget` (like `GitHubActionsJob`)
3. A builder extension method (`AddAzureDevOpsPipeline(...)`, etc.)
4. A YAML/config generator specific to the provider

The scheduling resolver is **provider-agnostic** — it works with any `IPipelineStepTarget` implementation.

### Azure DevOps (Example Future Provider)

```csharp
var pipeline = builder.AddAzureDevOpsPipeline("deploy");
var buildStage = pipeline.AddStage("build");
var deployStage = pipeline.AddStage("deploy");
```

The `AzureDevOpsStage` would implement `IPipelineStepTarget` and the YAML generator would emit `azure-pipelines.yml`.

## Testing Strategy

### Unit Tests

The scheduling resolver has extensive unit tests covering:

| Test Case | Description |
|-----------|-------------|
| Two steps, two jobs | Basic cross-job dependency |
| Fan-out | One step depending on three across three jobs |
| Fan-in | Three steps depending on one setup step |
| Diamond | A→B, A→C, B→D, C→D across four jobs |
| Cycle detection | Circular job dependencies from step assignments |
| Default job | Unscheduled steps grouped into default job |
| Mixed scheduling | Some steps scheduled, some default |
| Single job | All steps on one job — no cross-job deps |
| No jobs declared | Synthetic default job created |
| Steps grouped | Correct grouping of steps per job |
| Explicit job deps | `DependsOn()` preserved in output |
| Cross-workflow | Step from different workflow → error |
| Explicit cycle | Direct job cycle → error |

Environment resolution tests cover:

| Test Case | Description |
|-----------|-------------|
| No environments | Falls back to `LocalPipelineEnvironment` |
| One passing env | Returns it |
| One failing env | Falls back to local |
| Two envs, one passes | Returns the passing one |
| Two envs, both pass | Throws ambiguity error |
| No check annotation | Treated as non-relevant |
| Late-added env | Detected after pipeline construction |

### Integration Tests (Future)

- End-to-end YAML generation and validation
- Round-trip: generate YAML → parse → verify structure
- CLI `aspire pipeline init` command execution

## Open Questions

1. **State serialization format** — JSON? Binary? How to handle large artifacts?
2. **Secret injection** — How do CI/CD secrets map to Aspire parameters?
3. **Multi-workflow** — Can an app model produce multiple workflow files? (Yes, via multiple `AddGitHubActionsWorkflow` calls — but what about environment resolution?)
4. **Conditional steps** — How do steps that only run on certain branches/events interact with scheduling?
5. **Custom runner labels** — Per-step runner requirements (e.g., GPU, Windows)?
6. **Caching** — Should generated workflows include caching for NuGet packages, Docker layers, etc.?

## Implementation Files

### Source

| File | Description |
|------|-------------|
| `src/Aspire.Hosting/Pipelines/IPipelineEnvironment.cs` | Marker interface |
| `src/Aspire.Hosting/Pipelines/IPipelineStepTarget.cs` | Scheduling target interface |
| `src/Aspire.Hosting/Pipelines/PipelineEnvironmentCheckAnnotation.cs` | Relevance check annotation |
| `src/Aspire.Hosting/Pipelines/PipelineEnvironmentCheckContext.cs` | Check context |
| `src/Aspire.Hosting/Pipelines/LocalPipelineEnvironment.cs` | Fallback environment |
| `src/Aspire.Hosting.Pipelines.GitHubActions/GitHubActionsWorkflowResource.cs` | Workflow resource |
| `src/Aspire.Hosting.Pipelines.GitHubActions/GitHubActionsJob.cs` | Job target |
| `src/Aspire.Hosting.Pipelines.GitHubActions/GitHubActionsWorkflowExtensions.cs` | Builder extension |
| `src/Aspire.Hosting.Pipelines.GitHubActions/SchedulingResolver.cs` | Step-to-job resolver |
| `src/Aspire.Hosting.Pipelines.GitHubActions/SchedulingValidationException.cs` | Validation errors |

### Tests

| File | Description |
|------|-------------|
| `tests/Aspire.Hosting.Tests/Pipelines/PipelineEnvironmentTests.cs` | Environment resolution tests |
| `tests/Aspire.Hosting.Pipelines.GitHubActions.Tests/GitHubActionsWorkflowResourceTests.cs` | Workflow model tests |
| `tests/Aspire.Hosting.Pipelines.GitHubActions.Tests/SchedulingResolverTests.cs` | Scheduling validation tests |

### Modified

| File | Change |
|------|--------|
| `src/Aspire.Hosting/Pipelines/PipelineStep.cs` | Added `ScheduledBy` property |
| `src/Aspire.Hosting/Pipelines/IDistributedApplicationPipeline.cs` | Added `scheduledBy` to `AddStep()`, added `GetEnvironmentAsync()` |
| `src/Aspire.Hosting/Pipelines/DistributedApplicationPipeline.cs` | Constructor takes model, implements `GetEnvironmentAsync()` |
| `src/Aspire.Hosting/DistributedApplicationBuilder.cs` | Pipeline initialized with model |
