// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Cli.Projects;

internal static class AppHostEnvironmentDefaults
{
    private const string EnvironmentArgumentName = "--environment";
    private const string EnvironmentArgumentAlias = "-e";

    internal const string AspireEnvironmentVariableName = "ASPIRE_ENVIRONMENT";
    internal const string DotNetEnvironmentVariableName = "DOTNET_ENVIRONMENT";
    internal const string AspNetCoreEnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";
    internal const string DevelopmentEnvironmentName = "Development";
    internal const string ProductionEnvironmentName = "Production";

    internal static bool IsEnvironmentVariableName(string variableName) =>
        variableName is DotNetEnvironmentVariableName or AspNetCoreEnvironmentVariableName or AspireEnvironmentVariableName;

    internal static void ApplyEffectiveEnvironment(
        IDictionary<string, string> environmentVariables,
        string? defaultEnvironment = null,
        IReadOnlyDictionary<string, string?>? inheritedEnvironmentVariables = null,
        string[]? args = null)
    {
        if (TryResolveEnvironment(environmentVariables, inheritedEnvironmentVariables, args, out var environment))
        {
            environmentVariables[DotNetEnvironmentVariableName] = environment;
            environmentVariables[AspNetCoreEnvironmentVariableName] = environment;
        }
        else if (defaultEnvironment is not null)
        {
            environmentVariables[DotNetEnvironmentVariableName] = defaultEnvironment;
            environmentVariables[AspNetCoreEnvironmentVariableName] = defaultEnvironment;
        }
    }

    private static bool TryResolveEnvironment(
        IDictionary<string, string> environmentVariables,
        IReadOnlyDictionary<string, string?>? inheritedEnvironmentVariables,
        string[]? args,
        out string environment)
    {
        if (TryGetRequestedEnvironment(args, out environment) ||
            TryGetEnvironmentValue(environmentVariables, DotNetEnvironmentVariableName, out environment) ||
            TryGetInheritedEnvironmentValue(inheritedEnvironmentVariables, DotNetEnvironmentVariableName, out environment) ||
            TryGetEnvironmentValue(environmentVariables, AspireEnvironmentVariableName, out environment) ||
            TryGetInheritedEnvironmentValue(inheritedEnvironmentVariables, AspireEnvironmentVariableName, out environment))
        {
            return true;
        }

        environment = null!;
        return false;
    }

    private static bool TryGetRequestedEnvironment(string[]? args, out string environment)
    {
        if (args is not null)
        {
            for (var i = args.Length - 1; i >= 0; i--)
            {
                if (TryGetRequestedEnvironment(args, i, out environment))
                {
                    return true;
                }
            }
        }

        environment = null!;
        return false;
    }

    private static bool TryGetRequestedEnvironment(string[] args, int index, out string environment)
    {
        var argument = args[index];

        if (argument.StartsWith(EnvironmentArgumentName + "=", StringComparison.Ordinal))
        {
            return TryGetEnvironmentArgumentValue(argument[(EnvironmentArgumentName.Length + 1)..], out environment);
        }

        if (argument.StartsWith(EnvironmentArgumentAlias + "=", StringComparison.Ordinal))
        {
            return TryGetEnvironmentArgumentValue(argument[(EnvironmentArgumentAlias.Length + 1)..], out environment);
        }

        if (argument is EnvironmentArgumentName or EnvironmentArgumentAlias)
        {
            if (index + 1 < args.Length)
            {
                return TryGetEnvironmentArgumentValue(args[index + 1], out environment);
            }
        }

        environment = null!;
        return false;
    }

    private static bool TryGetEnvironmentArgumentValue(string value, out string environment)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment = value;
            return true;
        }

        environment = null!;
        return false;
    }

    private static bool TryGetEnvironmentValue(
        IDictionary<string, string> environmentVariables,
        string variableName,
        out string environment)
    {
        if (environmentVariables.TryGetValue(variableName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            environment = value;
            return true;
        }

        environment = null!;
        return false;
    }

    private static bool TryGetInheritedEnvironmentValue(
        IReadOnlyDictionary<string, string?>? inheritedEnvironmentVariables,
        string variableName,
        out string environment)
    {
        if (inheritedEnvironmentVariables is not null)
        {
            if (inheritedEnvironmentVariables.TryGetValue(variableName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                environment = value;
                return true;
            }
        }
        else
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                environment = value;
                return true;
            }
        }

        environment = null!;
        return false;
    }
}
