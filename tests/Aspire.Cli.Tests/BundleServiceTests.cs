// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Bundles;
using Aspire.Cli.Tests.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Cli.Tests;

public class BundleServiceTests
{
    [Fact]
    public void IsBundle_ReturnsFalse_WhenNoEmbeddedResource()
    {
        // Test assembly has no embedded bundle.tar.gz resource — verify via OpenPayload
        Assert.Null(BundleService.OpenPayload());
    }

    [Fact]
    public void OpenPayload_ReturnsNull_WhenNoEmbeddedResource()
    {
        Assert.Null(BundleService.OpenPayload());
    }

    [Fact]
    public void VersionMarker_WriteAndRead_Roundtrips()
    {
        var tempDir = Directory.CreateTempSubdirectory("aspire-test");
        try
        {
            BundleService.WriteVersionMarker(tempDir.FullName, "13.2.0-dev");

            var readVersion = BundleService.ReadVersionMarker(tempDir.FullName);
            Assert.Equal("13.2.0-dev", readVersion);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void VersionMarker_ReturnsNull_WhenMissing()
    {
        var tempDir = Directory.CreateTempSubdirectory("aspire-test");
        try
        {
            var readVersion = BundleService.ReadVersionMarker(tempDir.FullName);
            Assert.Null(readVersion);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetDefaultExtractDir_ReturnsAspireDir_ForStandardLayout()
    {
        // Create a temp directory simulating {home}/.aspire/bin/aspire
        var fakeHome = Directory.CreateTempSubdirectory("aspire-test-home");
        try
        {
            var aspireDir = Path.Combine(fakeHome.FullName, ".aspire");
            var binDir = Path.Combine(aspireDir, "bin");
            Directory.CreateDirectory(binDir);
            var processPath = Path.Combine(binDir, "aspire");

            var context = CreateContext(fakeHome.FullName);
            var service = CreateBundleService(context);

            var result = service.GetDefaultExtractDir(processPath);
            Assert.Equal(aspireDir, result);
        }
        finally
        {
            fakeHome.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetDefaultExtractDir_FallsBackToAspireDir_ForNonStandardLayout()
    {
        var fakeHome = Directory.CreateTempSubdirectory("aspire-test-home");
        try
        {
            var expectedAspireDir = Path.Combine(fakeHome.FullName, ".aspire");
            var context = CreateContext(fakeHome.FullName);
            var service = CreateBundleService(context);

            // Simulate Homebrew/Winget paths that are NOT under the aspire directory
            Assert.Equal(expectedAspireDir, service.GetDefaultExtractDir("/usr/local/bin/aspire"));
            Assert.Equal(expectedAspireDir, service.GetDefaultExtractDir("/opt/homebrew/bin/aspire"));

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(expectedAspireDir, service.GetDefaultExtractDir(@"C:\Program Files\WinGet\Links\aspire.exe"));
            }
        }
        finally
        {
            fakeHome.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetDefaultExtractDir_FallsBackToAspireDir_ForCustomInstallLocation()
    {
        var fakeHome = Directory.CreateTempSubdirectory("aspire-test-home");
        try
        {
            var expectedAspireDir = Path.Combine(fakeHome.FullName, ".aspire");
            var context = CreateContext(fakeHome.FullName);
            var service = CreateBundleService(context);

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(expectedAspireDir, service.GetDefaultExtractDir(@"D:\tools\aspire\bin\aspire.exe"));
            }
            else
            {
                Assert.Equal(expectedAspireDir, service.GetDefaultExtractDir("/opt/aspire/bin/aspire"));
            }
        }
        finally
        {
            fakeHome.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetCurrentVersion_ReturnsNonNull()
    {
        var version = BundleService.GetCurrentVersion();
        Assert.NotNull(version);
        Assert.NotEqual("unknown", version);
    }

    private static CliExecutionContext CreateContext(string homeDir)
    {
        var aspireDir = Path.Combine(homeDir, ".aspire");
        return new CliExecutionContext(
            new DirectoryInfo("."),
            new DirectoryInfo(Path.Combine(aspireDir, "hives")),
            new DirectoryInfo(Path.Combine(aspireDir, "cache")),
            new DirectoryInfo(Path.Combine(aspireDir, "sdks")),
            new DirectoryInfo(Path.Combine(aspireDir, "logs")),
            "test.log",
            homeDirectory: new DirectoryInfo(homeDir));
    }

    private static BundleService CreateBundleService(CliExecutionContext context)
    {
        return new BundleService(
            new NullLayoutDiscovery(),
            context,
            NullLogger<BundleService>.Instance);
    }
}
