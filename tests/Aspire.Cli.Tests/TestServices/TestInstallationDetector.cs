// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Utils;

namespace Aspire.Cli.Tests.TestServices;

internal sealed class TestInstallationDetector : IInstallationDetector
{
    public InstallationInfo InstallationInfo { get; set; } = new(IsDotNetTool: false, SelfUpdateDisabled: false, UpdateInstructions: null);

    public InstallationInfo GetInstallationInfo() => InstallationInfo;
}
