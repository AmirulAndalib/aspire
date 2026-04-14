// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Utils;

namespace Aspire.Cli.Tests.Utils;

public class MissingJavaScriptToolWarningTests
{
    [Theory]
    [InlineData("npm is not installed or not found in PATH. Please install Node.js and try again.")]
    [InlineData("npx is not installed or not found in PATH. Please install Node.js and try again.")]
    [InlineData("bun is not installed or not found in PATH. Please install Bun and try again.")]
    [InlineData("yarn is not installed or not found in PATH. Please install Yarn and try again.")]
    [InlineData("pnpm is not installed or not found in PATH. Please install pnpm and try again.")]
    public void IsMatch_WhenJavaScriptToolIsMissing_ReturnsTrue(string message)
    {
        var lines = new[]
        {
            (OutputLineStream.StdErr, message)
        };

        Assert.True(MissingJavaScriptToolWarning.IsMatch(lines));
    }

    [Fact]
    public void IsMatch_WhenOutputIsUnrelated_ReturnsFalse()
    {
        var lines = new[]
        {
            (OutputLineStream.StdErr, "npm ERR! code E401"),
            (OutputLineStream.StdOut, "Installing packages...")
        };

        Assert.False(MissingJavaScriptToolWarning.IsMatch(lines));
    }
}
