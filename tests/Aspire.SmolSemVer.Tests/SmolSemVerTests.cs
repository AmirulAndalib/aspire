// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

// Alias the original Semver package types so we can compare side-by-side.
using OrigSemVersion = Semver.SemVersion;
using OrigSemVersionStyles = Semver.SemVersionStyles;

namespace Aspire.SmolSemVer.Tests;

/// <summary>
/// Tests for <see cref="SemVersion"/> parsing with <see cref="SemVersionStyles.Strict"/> mode.
/// </summary>
public class StrictParsingTests
{
    [Theory]
    [InlineData("0.0.0", 0, 0, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("10.20.30", 10, 20, 30)]
    [InlineData("999.999.999", 999, 999, 999)]
    [InlineData("0.0.4", 0, 0, 4)]
    public void Parse_ValidStrictVersions(string input, int major, int minor, int patch)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Strict);

        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
        Assert.False(v.IsPrerelease);
        Assert.Equal(string.Empty, v.Prerelease);
        Assert.Equal(string.Empty, v.Metadata);
    }

    [Theory]
    [InlineData("1.0.0-alpha", "alpha")]
    [InlineData("1.0.0-alpha.1", "alpha.1")]
    [InlineData("1.0.0-0.3.7", "0.3.7")]
    [InlineData("1.0.0-x.7.z.92", "x.7.z.92")]
    [InlineData("1.0.0-alpha-beta", "alpha-beta")]
    [InlineData("1.0.0-beta.11", "beta.11")]
    [InlineData("1.0.0-rc.1", "rc.1")]
    [InlineData("10.0.100-preview.1.25463.5", "preview.1.25463.5")]
    public void Parse_WithPrerelease(string input, string expectedPrerelease)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Strict);

        Assert.True(v.IsPrerelease);
        Assert.Equal(expectedPrerelease, v.Prerelease);
    }

    [Theory]
    [InlineData("1.0.0+build", "", "build")]
    [InlineData("1.0.0+20130313144700", "", "20130313144700")]
    [InlineData("1.0.0+exp.sha.5114f85", "", "exp.sha.5114f85")]
    [InlineData("1.0.0-beta+exp.sha.5114f85", "beta", "exp.sha.5114f85")]
    [InlineData("1.0.0-alpha.1+001", "alpha.1", "001")]
    public void Parse_WithMetadata(string input, string expectedPrerelease, string expectedMetadata)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Strict);

        Assert.Equal(expectedPrerelease, v.Prerelease);
        Assert.Equal(expectedMetadata, v.Metadata);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("1.0.0-01")]       // Leading zero in numeric prerelease identifier
    [InlineData("1.0.0-")]         // Trailing hyphen (empty prerelease)
    [InlineData("1.0.0+")]         // Trailing plus (empty metadata)
    [InlineData("1.0.0- ")]        // Space in prerelease
    [InlineData("1.0.0-alpha..1")] // Empty prerelease identifier
    [InlineData("1.0.0+build..1")] // Empty metadata identifier
    [InlineData("a.b.c")]          // Non-numeric version parts
    [InlineData("-1.0.0")]         // Negative number
    [InlineData("1.0.0.0")]        // Four parts
    [InlineData("1.0.0 ")]         // Trailing space
    [InlineData(" 1.0.0")]         // Leading space
    public void Parse_Strict_RejectsInvalidVersions(string input)
    {
        Assert.False(SemVersion.TryParse(input, SemVersionStyles.Strict, out _));
        Assert.Throws<FormatException>(() => SemVersion.Parse(input, SemVersionStyles.Strict));
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SemVersion.Parse(null!, SemVersionStyles.Strict));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.False(SemVersion.TryParse(null, SemVersionStyles.Strict, out var v));
        Assert.Null(v);
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalse()
    {
        Assert.False(SemVersion.TryParse("", SemVersionStyles.Strict, out var v));
        Assert.Null(v);
    }
}

/// <summary>
/// Tests for <see cref="SemVersion"/> parsing with <see cref="SemVersionStyles.Any"/> mode.
/// </summary>
public class LenientParsingTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("V1.2.3", 1, 2, 3)]
    [InlineData("v0.0.0", 0, 0, 0)]
    public void Parse_Any_AllowsVPrefix(string input, int major, int minor, int patch)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Any);

        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
    }

    [Theory]
    [InlineData("01.2.3", 1, 2, 3)]
    [InlineData("1.02.3", 1, 2, 3)]
    [InlineData("1.2.03", 1, 2, 3)]
    [InlineData("001.002.003", 1, 2, 3)]
    public void Parse_Any_AllowsLeadingZeros(string input, int major, int minor, int patch)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Any);

        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
    }

    [Theory]
    [InlineData("1", 1, 0, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("v1", 1, 0, 0)]
    [InlineData("v1.2", 1, 2, 0)]
    public void Parse_Any_AllowsMissingParts(string input, int major, int minor, int patch)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Any);

        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
    }

    [Theory]
    [InlineData("v1.2.3-alpha+build")]
    [InlineData("01.02.03-beta.1")]
    [InlineData("1-alpha")]
    [InlineData("1.2-rc.1")]
    public void Parse_Any_AllowsCombinedLenience(string input)
    {
        Assert.True(SemVersion.TryParse(input, SemVersionStyles.Any, out var v));
        Assert.NotNull(v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("v")]
    [InlineData("1.2.3.4")]
    [InlineData("..")]
    public void Parse_Any_StillRejectsGarbage(string input)
    {
        Assert.False(SemVersion.TryParse(input, SemVersionStyles.Any, out _));
    }
}

/// <summary>
/// Tests for <see cref="SemVersion.ToString()"/> round-trip formatting.
/// </summary>
public class ToStringTests
{
    [Theory]
    [InlineData("1.2.3")]
    [InlineData("0.0.0")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0+build")]
    [InlineData("1.0.0-beta+exp.sha.5114f85")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x.7.z.92")]
    public void ToString_RoundTrips(string input)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Strict);
        Assert.Equal(input, v.ToString());
    }

    [Fact]
    public void ToString_FromConstructor()
    {
        var v = new SemVersion(1, 2, 3, "beta.1", "build.42");
        Assert.Equal("1.2.3-beta.1+build.42", v.ToString());
    }

    [Fact]
    public void ToString_StableFromConstructor()
    {
        var v = new SemVersion(1, 0, 0);
        Assert.Equal("1.0.0", v.ToString());
    }
}

/// <summary>
/// Tests for SemVer 2.0.0 precedence ordering.
/// </summary>
public class PrecedenceTests
{
    /// <summary>
    /// SemVer 2.0.0 §11 examples: 1.0.0-alpha &lt; 1.0.0-alpha.1 &lt; 1.0.0-alpha.beta
    /// &lt; 1.0.0-beta &lt; 1.0.0-beta.2 &lt; 1.0.0-beta.11 &lt; 1.0.0-rc.1 &lt; 1.0.0
    /// </summary>
    [Fact]
    public void Precedence_MatchesSemVerSpec_Section11()
    {
        var versions = new[]
        {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0"
        };

        for (var i = 0; i < versions.Length - 1; i++)
        {
            var lower = SemVersion.Parse(versions[i], SemVersionStyles.Strict);
            var higher = SemVersion.Parse(versions[i + 1], SemVersionStyles.Strict);

            Assert.True(lower.IsOlderThan(higher), $"{versions[i]} should be older than {versions[i + 1]}");
            Assert.True(higher.IsNewerThan(lower), $"{versions[i + 1]} should be newer than {versions[i]}");
            Assert.False(lower.HasSamePrecedenceAs(higher));
        }
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("1.0.0", "1.1.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0-alpha", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-1", "1.0.0-2")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]  // Numeric < alphanumeric
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")] // Fewer fields < more fields
    public void Precedence_OlderVersionIsOlder(string older, string newer)
    {
        var v1 = SemVersion.Parse(older, SemVersionStyles.Strict);
        var v2 = SemVersion.Parse(newer, SemVersionStyles.Strict);

        Assert.True(v1.IsOlderThan(v2));
        Assert.True(v2.IsNewerThan(v1));
        Assert.False(v1.IsNewerThan(v2));
        Assert.False(v2.IsOlderThan(v1));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1")]
    public void Precedence_EqualVersionsHaveSamePrecedence(string a, string b)
    {
        var v1 = SemVersion.Parse(a, SemVersionStyles.Strict);
        var v2 = SemVersion.Parse(b, SemVersionStyles.Strict);

        Assert.True(v1.HasSamePrecedenceAs(v2));
        Assert.True(v1.IsAtLeast(v2));
        Assert.False(v1.IsNewerThan(v2));
        Assert.False(v1.IsOlderThan(v2));
    }

    [Fact]
    public void Precedence_BuildMetadata_IsIgnored()
    {
        var v1 = SemVersion.Parse("1.0.0+build.1", SemVersionStyles.Strict);
        var v2 = SemVersion.Parse("1.0.0+build.2", SemVersionStyles.Strict);

        Assert.True(v1.HasSamePrecedenceAs(v2));
    }

    [Fact]
    public void Precedence_PrereleaseHasLowerPrecedenceThanRelease()
    {
        var prerelease = SemVersion.Parse("1.0.0-alpha", SemVersionStyles.Strict);
        var release = SemVersion.Parse("1.0.0", SemVersionStyles.Strict);

        Assert.True(prerelease.IsOlderThan(release));
        Assert.True(release.IsNewerThan(prerelease));
    }

    [Theory]
    [InlineData("1.0.0-1", "1.0.0-2")]
    [InlineData("1.0.0-2", "1.0.0-10")]
    [InlineData("1.0.0-0", "1.0.0-1")]
    public void Precedence_NumericIdentifiersComparedAsIntegers(string older, string newer)
    {
        var v1 = SemVersion.Parse(older, SemVersionStyles.Strict);
        var v2 = SemVersion.Parse(newer, SemVersionStyles.Strict);

        Assert.True(v1.IsOlderThan(v2));
    }

    [Theory]
    [InlineData("1.0.0-a", "1.0.0-b")]
    [InlineData("1.0.0-A", "1.0.0-a")]  // Uppercase < lowercase (ordinal)
    [InlineData("1.0.0-aaa", "1.0.0-b")]
    public void Precedence_AlphanumericIdentifiersComparedLexically(string older, string newer)
    {
        var v1 = SemVersion.Parse(older, SemVersionStyles.Strict);
        var v2 = SemVersion.Parse(newer, SemVersionStyles.Strict);

        Assert.True(v1.IsOlderThan(v2));
    }
}

/// <summary>
/// Tests for the explicit comparison methods: IsNewerThan, IsOlderThan, IsAtLeast, HasSamePrecedenceAs.
/// </summary>
public class ExplicitComparisonMethodTests
{
    [Fact]
    public void IsAtLeast_WhenEqual_ReturnsTrue()
    {
        var v = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);
        var min = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);

        Assert.True(v.IsAtLeast(min));
    }

    [Fact]
    public void IsAtLeast_WhenNewer_ReturnsTrue()
    {
        var v = SemVersion.Parse("3.0.0", SemVersionStyles.Strict);
        var min = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);

        Assert.True(v.IsAtLeast(min));
    }

    [Fact]
    public void IsAtLeast_WhenOlder_ReturnsFalse()
    {
        var v = SemVersion.Parse("1.0.0", SemVersionStyles.Strict);
        var min = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);

        Assert.False(v.IsAtLeast(min));
    }

    [Fact]
    public void IsNewerThan_WhenStrictlyNewer_ReturnsTrue()
    {
        var v = SemVersion.Parse("2.0.1", SemVersionStyles.Strict);
        var other = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);

        Assert.True(v.IsNewerThan(other));
    }

    [Fact]
    public void IsNewerThan_WhenEqual_ReturnsFalse()
    {
        var v = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);
        var other = SemVersion.Parse("2.0.0", SemVersionStyles.Strict);

        Assert.False(v.IsNewerThan(other));
    }

    [Fact]
    public void IsOlderThan_AppHostBelowMinimum()
    {
        // Real-world pattern from AppHostHelper.cs
        var aspireVersion = SemVersion.Parse("9.0.0", SemVersionStyles.Strict);
        var minimumVersion = SemVersion.Parse("9.2.0", SemVersionStyles.Strict);

        Assert.True(aspireVersion.IsOlderThan(minimumVersion));
    }

    [Fact]
    public void IsAtLeast_SdkMeetsMinimum()
    {
        // Real-world pattern from DotNetSdkInstaller.cs
        var installed = SemVersion.Parse("10.0.200", SemVersionStyles.Strict);
        var required = SemVersion.Parse("10.0.100", SemVersionStyles.Strict);

        Assert.True(installed.IsAtLeast(required));
    }

    [Fact]
    public void IsNewerThan_FindsHighestVersion()
    {
        // Real-world pattern: finding highest version in a list
        var versions = new[] { "1.0.0", "3.0.0", "2.0.0", "2.5.0" };

        SemVersion? highest = null;
        foreach (var vStr in versions)
        {
            var v = SemVersion.Parse(vStr, SemVersionStyles.Strict);
            if (highest is null || v.IsNewerThan(highest))
            {
                highest = v;
            }
        }

        Assert.Equal("3.0.0", highest!.ToString());
    }
}

/// <summary>
/// Tests for <see cref="SemVersion.PrecedenceComparer"/> used in LINQ ordering.
/// </summary>
public class PrecedenceComparerTests
{
    [Fact]
    public void PrecedenceComparer_SortsCorrectly()
    {
        var input = new[] { "3.0.0", "1.0.0", "2.0.0-beta", "2.0.0", "1.0.0-alpha" };
        var parsed = input.Select(s => SemVersion.Parse(s, SemVersionStyles.Strict)).ToList();

        var sorted = parsed.OrderBy(v => v, SemVersion.PrecedenceComparer).Select(v => v.ToString()).ToArray();

        Assert.Equal(["1.0.0-alpha", "1.0.0", "2.0.0-beta", "2.0.0", "3.0.0"], sorted);
    }

    [Fact]
    public void PrecedenceComparer_OrderByDescending_GivesNewestFirst()
    {
        var input = new[] { "1.0.0", "3.0.0", "2.0.0" };
        var parsed = input.Select(s => SemVersion.Parse(s, SemVersionStyles.Strict)).ToList();

        var sorted = parsed.OrderByDescending(v => v, SemVersion.PrecedenceComparer).Select(v => v.ToString()).ToArray();

        Assert.Equal(["3.0.0", "2.0.0", "1.0.0"], sorted);
    }

    [Fact]
    public void PrecedenceComparer_HandlesNulls()
    {
        var result = SemVersion.PrecedenceComparer.Compare(null, SemVersion.Parse("1.0.0"));
        Assert.True(result < 0);

        result = SemVersion.PrecedenceComparer.Compare(SemVersion.Parse("1.0.0"), null);
        Assert.True(result > 0);

        result = SemVersion.PrecedenceComparer.Compare(null, null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void PrecedenceComparer_RealWorldPackageSelection()
    {
        // Simulates the pattern used in AddCommand, InitCommand, etc.
        var packageVersions = new[] { "9.0.0", "9.1.0-preview.1", "9.1.0", "9.2.0-rc.1", "9.2.0" };

        var latest = packageVersions
            .Select(v => SemVersion.Parse(v, SemVersionStyles.Strict))
            .OrderByDescending(v => v, SemVersion.PrecedenceComparer)
            .First();

        Assert.Equal("9.2.0", latest.ToString());
    }

    [Fact]
    public void PrecedenceComparer_FilterAndSort_StableOnly()
    {
        // Simulates PackageChannel.cs filtering for stable-only
        var packageVersions = new[] { "9.0.0", "9.1.0-preview.1", "9.1.0", "9.2.0-rc.1", "9.2.0" };

        var latestStable = packageVersions
            .Select(v => SemVersion.Parse(v, SemVersionStyles.Strict))
            .Where(v => !v.IsPrerelease)
            .OrderByDescending(v => v, SemVersion.PrecedenceComparer)
            .First();

        Assert.Equal("9.2.0", latestStable.ToString());
    }
}

/// <summary>
/// Tests for <see cref="SemVersion"/> equality and hashing.
/// </summary>
public class EqualityTests
{
    [Fact]
    public void Equals_SameVersion_ReturnsTrue()
    {
        var v1 = SemVersion.Parse("1.2.3-alpha+build", SemVersionStyles.Strict);
        var v2 = SemVersion.Parse("1.2.3-alpha+build", SemVersionStyles.Strict);

        Assert.Equal(v1, v2);
        Assert.True(v1 == v2);
        Assert.False(v1 != v2);
    }

    [Fact]
    public void Equals_DifferentMetadata_AreNotEqual()
    {
        // Equality includes metadata (unlike precedence).
        var v1 = SemVersion.Parse("1.0.0+build.1", SemVersionStyles.Strict);
        var v2 = SemVersion.Parse("1.0.0+build.2", SemVersionStyles.Strict);

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void Equals_DifferentVersions_AreNotEqual()
    {
        var v1 = SemVersion.Parse("1.0.0", SemVersionStyles.Strict);
        var v2 = SemVersion.Parse("1.0.1", SemVersionStyles.Strict);

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void GetHashCode_SameVersion_SameHash()
    {
        var v1 = SemVersion.Parse("1.2.3-alpha+build", SemVersionStyles.Strict);
        var v2 = SemVersion.Parse("1.2.3-alpha+build", SemVersionStyles.Strict);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var v = SemVersion.Parse("1.0.0", SemVersionStyles.Strict);
        Assert.False(v.Equals(null));
        Assert.False(v == null);
        Assert.True(v != null);
    }

    [Fact]
    public void Operators_NullOnBothSides()
    {
        SemVersion? a = null;
        SemVersion? b = null;
        Assert.True(a == b);
        Assert.False(a != b);
    }
}

/// <summary>
/// Tests for <see cref="SemVersion.IsPrerelease"/> property.
/// </summary>
public class IsPrereleaseTests
{
    [Theory]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0-alpha", true)]
    [InlineData("1.0.0-0", true)]
    [InlineData("1.0.0-alpha.1", true)]
    [InlineData("1.0.0+build", false)]          // Metadata only → stable
    [InlineData("1.0.0-rc.1+build", true)]      // Prerelease + metadata → prerelease
    [InlineData("10.0.100-preview.1.25463.5", true)]
    public void IsPrerelease_CorrectlyIdentifies(string input, bool expected)
    {
        var v = SemVersion.Parse(input, SemVersionStyles.Strict);
        Assert.Equal(expected, v.IsPrerelease);
    }
}

/// <summary>
/// Tests for the <see cref="SemVersion(int, int, int, string, string)"/> constructor.
/// </summary>
public class ConstructorTests
{
    [Fact]
    public void Constructor_DefaultsToStable()
    {
        var v = new SemVersion(1, 2, 3);

        Assert.Equal(1, v.Major);
        Assert.Equal(2, v.Minor);
        Assert.Equal(3, v.Patch);
        Assert.False(v.IsPrerelease);
        Assert.Equal(string.Empty, v.Prerelease);
        Assert.Equal(string.Empty, v.Metadata);
    }

    [Fact]
    public void Constructor_MinorAndPatchDefaultToZero()
    {
        var v = new SemVersion(5);

        Assert.Equal(5, v.Major);
        Assert.Equal(0, v.Minor);
        Assert.Equal(0, v.Patch);
    }

    [Fact]
    public void Constructor_WithAllParts()
    {
        var v = new SemVersion(1, 2, 3, "beta.1", "build.42");

        Assert.Equal(1, v.Major);
        Assert.Equal(2, v.Minor);
        Assert.Equal(3, v.Patch);
        Assert.Equal("beta.1", v.Prerelease);
        Assert.Equal("build.42", v.Metadata);
        Assert.True(v.IsPrerelease);
    }
}

/// <summary>
/// Edge case and boundary tests.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Parse_LargeVersionNumbers()
    {
        var v = SemVersion.Parse("2147483647.2147483647.2147483647", SemVersionStyles.Strict);

        Assert.Equal(int.MaxValue, v.Major);
        Assert.Equal(int.MaxValue, v.Minor);
        Assert.Equal(int.MaxValue, v.Patch);
    }

    [Fact]
    public void Parse_OverflowVersionNumber_Fails()
    {
        // int.MaxValue + 1
        Assert.False(SemVersion.TryParse("2147483648.0.0", SemVersionStyles.Strict, out _));
    }

    [Fact]
    public void Parse_HyphenInPrerelease()
    {
        var v = SemVersion.Parse("1.0.0-alpha-beta-gamma", SemVersionStyles.Strict);

        Assert.Equal("alpha-beta-gamma", v.Prerelease);
    }

    [Fact]
    public void Parse_NumericOnlyPrerelease()
    {
        var v = SemVersion.Parse("1.0.0-0", SemVersionStyles.Strict);

        Assert.Equal("0", v.Prerelease);
        Assert.True(v.IsPrerelease);
    }

    [Fact]
    public void Parse_ZeroPrerelease_IsValid()
    {
        // "0" is valid (no leading zeros issue since it's just "0")
        var v = SemVersion.Parse("1.0.0-0", SemVersionStyles.Strict);
        Assert.Equal("0", v.Prerelease);
    }

    [Theory]
    [InlineData("1.0.0-alpha.0beta")]    // Mixed alpha-numeric identifier
    [InlineData("1.0.0-alpha.0")]        // 0 in dotted prerelease is fine
    public void Parse_Strict_MixedPrereleaseIdentifiers(string input)
    {
        Assert.True(SemVersion.TryParse(input, SemVersionStyles.Strict, out var v));
        Assert.NotNull(v);
    }

    [Fact]
    public void TryParse_DefaultStyle_UsesStrict()
    {
        // The overload without styles should use Strict
        Assert.True(SemVersion.TryParse("1.2.3", out var v));
        Assert.NotNull(v);

        // v-prefix should fail with default (Strict)
        Assert.False(SemVersion.TryParse("v1.2.3", out _));
    }

    [Fact]
    public void Parse_LongPrerelease()
    {
        var prerelease = "alpha.1.2.3.4.5.6.7.8.9.10";
        var v = SemVersion.Parse($"1.0.0-{prerelease}", SemVersionStyles.Strict);
        Assert.Equal(prerelease, v.Prerelease);
    }
}

/// <summary>
/// Compatibility tests: validates SmolSemVer produces identical results to the reference Semver NuGet package.
/// </summary>
public class CompatibilityWithOriginalSemverTests
{
    /// <summary>
    /// Versions used by the Aspire codebase in real scenarios.
    /// </summary>
    public static TheoryData<string> RealWorldVersions => new()
    {
        "9.0.0",
        "9.1.0",
        "9.2.0",
        "9.2.0-preview.1",
        "10.0.0",
        "10.0.100-preview.1.25463.5",
        "1.0.0-alpha",
        "1.0.0-alpha.1",
        "1.0.0-beta",
        "1.0.0-beta.2",
        "1.0.0-beta.11",
        "1.0.0-rc.1",
        "1.0.0",
        "0.1.1",
        "3.0.0",
        "13.0.0",
        "13.1.0-preview.25206.3",
    };

    [Theory]
    [MemberData(nameof(RealWorldVersions))]
    public void Parse_MatchesOriginal_StrictMode(string input)
    {
        var smol = SemVersion.Parse(input, SemVersionStyles.Strict);
        var orig = OrigSemVersion.Parse(input, OrigSemVersionStyles.Strict);

        Assert.Equal(orig.Major, smol.Major);
        Assert.Equal(orig.Minor, smol.Minor);
        Assert.Equal(orig.Patch, smol.Patch);
        Assert.Equal(orig.IsPrerelease, smol.IsPrerelease);
        Assert.Equal(orig.Prerelease, smol.Prerelease);
        Assert.Equal(orig.ToString(), smol.ToString());
    }

    public static TheoryData<string> LenientVersions => new()
    {
        "v1.2.3",
        "V1.0.0-alpha",
        "01.02.03",
        "1",
        "1.2",
        "v1.2",
    };

    [Theory]
    [MemberData(nameof(LenientVersions))]
    public void Parse_MatchesOriginal_AnyMode(string input)
    {
        var smol = SemVersion.Parse(input, SemVersionStyles.Any);
        var orig = OrigSemVersion.Parse(input, OrigSemVersionStyles.Any);

        Assert.Equal(orig.Major, smol.Major);
        Assert.Equal(orig.Minor, smol.Minor);
        Assert.Equal(orig.Patch, smol.Patch);
        Assert.Equal(orig.IsPrerelease, smol.IsPrerelease);
    }

    [Fact]
    public void Precedence_MatchesOriginal_ForAllPairs()
    {
        var versionStrings = new[]
        {
            "0.0.1",
            "0.1.0",
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
            "1.0.1",
            "1.1.0",
            "2.0.0",
            "9.2.0-preview.1",
            "9.2.0",
            "10.0.100-preview.1.25463.5",
            "10.0.100",
        };

        var smolVersions = versionStrings.Select(s => SemVersion.Parse(s, SemVersionStyles.Strict)).ToArray();
        var origVersions = versionStrings.Select(s => OrigSemVersion.Parse(s, OrigSemVersionStyles.Strict)).ToArray();

        for (var i = 0; i < versionStrings.Length; i++)
        {
            for (var j = 0; j < versionStrings.Length; j++)
            {
                var smolCmp = Math.Sign(smolVersions[i].CompareTo(smolVersions[j]));
                var origCmp = Math.Sign(OrigSemVersion.ComparePrecedence(origVersions[i], origVersions[j]));

                Assert.True(origCmp == smolCmp,
                    $"Precedence mismatch for {versionStrings[i]} vs {versionStrings[j]}: " +
                    $"SmolSemVer={smolCmp}, Original={origCmp}");
            }
        }
    }

    [Fact]
    public void Sorting_MatchesOriginal()
    {
        var input = new[]
        {
            "3.0.0", "1.0.0-alpha", "2.0.0-beta", "1.0.0",
            "2.0.0", "1.0.0-alpha.1", "1.0.0-beta"
        };

        var smolSorted = input
            .Select(s => SemVersion.Parse(s, SemVersionStyles.Strict))
            .OrderBy(v => v, SemVersion.PrecedenceComparer)
            .Select(v => v.ToString())
            .ToArray();

        var origSorted = input
            .Select(s => OrigSemVersion.Parse(s, OrigSemVersionStyles.Strict))
            .OrderBy(v => v, OrigSemVersion.PrecedenceComparer)
            .Select(v => v.ToString())
            .ToArray();

        Assert.Equal(origSorted, smolSorted);
    }

    [Theory]
    [MemberData(nameof(RealWorldVersions))]
    public void IsPrerelease_MatchesOriginal(string input)
    {
        var smol = SemVersion.Parse(input, SemVersionStyles.Strict);
        var orig = OrigSemVersion.Parse(input, OrigSemVersionStyles.Strict);

        Assert.Equal(orig.IsPrerelease, smol.IsPrerelease);
    }

    public static TheoryData<string> InvalidVersions => new()
    {
        "",
        "abc",
        "1.0.0-01",     // Leading zero in numeric prerelease
        "01.0.0",        // Leading zero in strict
        "1.0.0-",        // Trailing hyphen
        "1.0.0+",        // Trailing plus
        "1.0.0.0",       // Four parts
        "v1.0.0",        // v-prefix in strict
    };

    [Theory]
    [MemberData(nameof(InvalidVersions))]
    public void TryParse_RejectsInvalid_MatchesOriginal(string input)
    {
        var smolResult = SemVersion.TryParse(input, SemVersionStyles.Strict, out _);
        var origResult = OrigSemVersion.TryParse(input, OrigSemVersionStyles.Strict, out _);

        Assert.True(origResult == smolResult,
            $"Rejection mismatch for '{input}': SmolSemVer={smolResult}, Original={origResult}");
    }
}

/// <summary>
/// Tests for individual <see cref="SemVersionStyles"/> flags.
/// </summary>
public class StylesFlagTests
{
    [Fact]
    public void AllowLeadingV_Only()
    {
        // v-prefix should work
        Assert.True(SemVersion.TryParse("v1.2.3", SemVersionStyles.AllowLeadingV, out _));

        // But leading zeros should not
        Assert.False(SemVersion.TryParse("01.2.3", SemVersionStyles.AllowLeadingV, out _));

        // Missing parts should not
        Assert.False(SemVersion.TryParse("1.2", SemVersionStyles.AllowLeadingV, out _));
    }

    [Fact]
    public void AllowLeadingZeros_Only()
    {
        // Leading zeros should work
        Assert.True(SemVersion.TryParse("01.02.03", SemVersionStyles.AllowLeadingZeros, out _));

        // But v-prefix should not
        Assert.False(SemVersion.TryParse("v1.2.3", SemVersionStyles.AllowLeadingZeros, out _));

        // Missing parts should not
        Assert.False(SemVersion.TryParse("1.2", SemVersionStyles.AllowLeadingZeros, out _));
    }

    [Fact]
    public void AllowMissingParts_Only()
    {
        // Missing parts should work
        Assert.True(SemVersion.TryParse("1", SemVersionStyles.AllowMissingParts, out _));
        Assert.True(SemVersion.TryParse("1.2", SemVersionStyles.AllowMissingParts, out _));

        // But v-prefix should not
        Assert.False(SemVersion.TryParse("v1.2.3", SemVersionStyles.AllowMissingParts, out _));

        // Leading zeros should not
        Assert.False(SemVersion.TryParse("01.2.3", SemVersionStyles.AllowMissingParts, out _));
    }
}
