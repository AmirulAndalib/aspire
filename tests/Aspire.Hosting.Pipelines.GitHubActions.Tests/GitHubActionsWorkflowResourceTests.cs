// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

namespace Aspire.Hosting.Pipelines.GitHubActions.Tests;

[Trait("Partition", "4")]
public class GitHubActionsWorkflowResourceTests
{
    [Fact]
    public void WorkflowFileName_MatchesResourceName()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");

        Assert.Equal("deploy.yml", workflow.WorkflowFileName);
    }

    [Fact]
    public void AddJob_CreatesJobWithCorrectId()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        var job = workflow.AddJob("build");

        Assert.Equal("build", job.Id);
        Assert.Same(workflow, job.Workflow);
    }

    [Fact]
    public void AddJob_MultipleJobs_AllTracked()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        var build = workflow.AddJob("build");
        var test = workflow.AddJob("test");
        var deploy = workflow.AddJob("deploy");

        Assert.Equal(3, workflow.Jobs.Count);
        Assert.Same(build, workflow.Jobs[0]);
        Assert.Same(test, workflow.Jobs[1]);
        Assert.Same(deploy, workflow.Jobs[2]);
    }

    [Fact]
    public void AddJob_DuplicateId_Throws()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        workflow.AddJob("build");

        var ex = Assert.Throws<InvalidOperationException>(() => workflow.AddJob("build"));
        Assert.Contains("build", ex.Message);
    }

    [Fact]
    public void Job_DependsOn_ById()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        workflow.AddJob("build");
        var deploy = workflow.AddJob("deploy");

        deploy.DependsOn("build");

        Assert.Single(deploy.DependsOnJobs);
        Assert.Equal("build", deploy.DependsOnJobs[0]);
    }

    [Fact]
    public void Job_DependsOn_ByReference()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        var build = workflow.AddJob("build");
        var deploy = workflow.AddJob("deploy");

        deploy.DependsOn(build);

        Assert.Single(deploy.DependsOnJobs);
        Assert.Equal("build", deploy.DependsOnJobs[0]);
    }

    [Fact]
    public void Job_DefaultRunsOn_IsUbuntuLatest()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        var job = workflow.AddJob("build");

        Assert.Equal("ubuntu-latest", job.RunsOn);
    }

    [Fact]
    public void Job_IPipelineStepTarget_EnvironmentIsWorkflow()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");
        var job = workflow.AddJob("build");

        IPipelineStepTarget target = job;

        Assert.Same(workflow, target.Environment);
    }

    [Fact]
    public void Workflow_ImplementsIPipelineEnvironment()
    {
        var workflow = new GitHubActionsWorkflowResource("deploy");

        Assert.IsAssignableFrom<IPipelineEnvironment>(workflow);
    }
}
