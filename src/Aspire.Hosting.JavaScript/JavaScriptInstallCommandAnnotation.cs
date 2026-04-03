// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.JavaScript;

/// <summary>
/// Represents the annotation for the JavaScript package manager's install command.
/// </summary>
/// <param name="args">
/// The command line arguments for the JavaScript package manager's install command.
/// This includes the command itself (i.e. "install").
/// </param>
public sealed class JavaScriptInstallCommandAnnotation(string[] args) : IResourceAnnotation
{
    /// <summary>
    /// Gets the command-line arguments supplied to the JavaScript package manager.
    /// </summary>
    public string[] Args { get; } = args;

    /// <summary>
    /// Gets or sets the arguments for installing production-only dependencies (excluding devDependencies).
    /// Each package manager sets its own flags (e.g. npm uses <c>install --omit=dev</c>,
    /// yarn uses <c>install --production</c>, pnpm uses <c>install --prod</c>).
    /// </summary>
    public string? ProductionInstallArgs { get; init; }
}
