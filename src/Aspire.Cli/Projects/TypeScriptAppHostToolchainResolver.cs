// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Cli.Utils;
using Aspire.TypeSystem;

namespace Aspire.Cli.Projects;

internal enum TypeScriptAppHostToolchain
{
    Npm,
    Bun,
    Yarn,
    Pnpm
}

internal static class TypeScriptAppHostToolchainResolver
{
    private const string PackageJsonFileName = "package.json";
    private const string BunLockFileName = "bun.lock";
    private const string BunBinaryLockFileName = "bun.lockb";
    private const string YarnLockFileName = "yarn.lock";
    private const string YarnConfigFileName = ".yarnrc.yml";
    private const string YarnDirectoryName = ".yarn";
    private const string PnpmLockFileName = "pnpm-lock.yaml";

    public static bool IsTypeScriptLanguage(LanguageInfo? language)
    {
        return language is not null &&
            (language.LanguageId.Value.Equals(KnownLanguageId.TypeScript, StringComparison.OrdinalIgnoreCase) ||
             language.LanguageId.Value.Equals(KnownLanguageId.TypeScriptAlias, StringComparison.OrdinalIgnoreCase));
    }

    public static TypeScriptAppHostToolchain Resolve(DirectoryInfo appHostDirectory)
    {
        if (TryGetToolchainFromPackageJson(appHostDirectory, out var configuredToolchain))
        {
            return configuredToolchain;
        }

        if (File.Exists(Path.Combine(appHostDirectory.FullName, BunLockFileName)) ||
            File.Exists(Path.Combine(appHostDirectory.FullName, BunBinaryLockFileName)))
        {
            return TypeScriptAppHostToolchain.Bun;
        }

        if (File.Exists(Path.Combine(appHostDirectory.FullName, PnpmLockFileName)))
        {
            return TypeScriptAppHostToolchain.Pnpm;
        }

        if (File.Exists(Path.Combine(appHostDirectory.FullName, YarnLockFileName)) ||
            File.Exists(Path.Combine(appHostDirectory.FullName, YarnConfigFileName)) ||
            Directory.Exists(Path.Combine(appHostDirectory.FullName, YarnDirectoryName)))
        {
            return TypeScriptAppHostToolchain.Yarn;
        }

        return TypeScriptAppHostToolchain.Npm;
    }

    public static string[] GetRequiredCommands(TypeScriptAppHostToolchain toolchain)
    {
        return toolchain switch
        {
            TypeScriptAppHostToolchain.Npm => ["npm", "npx"],
            _ => [GetCommandName(toolchain)]
        };
    }

    public static string GetCommandName(TypeScriptAppHostToolchain toolchain)
    {
        return toolchain switch
        {
            TypeScriptAppHostToolchain.Npm => "npm",
            TypeScriptAppHostToolchain.Bun => "bun",
            TypeScriptAppHostToolchain.Yarn => "yarn",
            TypeScriptAppHostToolchain.Pnpm => "pnpm",
            _ => throw new ArgumentOutOfRangeException(nameof(toolchain), toolchain, null)
        };
    }

    public static string GetDisplayName(TypeScriptAppHostToolchain toolchain)
    {
        return toolchain switch
        {
            TypeScriptAppHostToolchain.Npm => "Node.js",
            TypeScriptAppHostToolchain.Bun => "Bun",
            TypeScriptAppHostToolchain.Yarn => "Yarn",
            TypeScriptAppHostToolchain.Pnpm => "pnpm",
            _ => throw new ArgumentOutOfRangeException(nameof(toolchain), toolchain, null)
        };
    }

    public static RuntimeSpec ApplyToRuntimeSpec(RuntimeSpec baseRuntimeSpec, TypeScriptAppHostToolchain toolchain)
    {
        if (toolchain == TypeScriptAppHostToolchain.Npm)
        {
            return baseRuntimeSpec;
        }

        var tsConfigFileName = GetTsConfigFileName(baseRuntimeSpec);

        return new RuntimeSpec
        {
            Language = baseRuntimeSpec.Language,
            DisplayName = $"TypeScript ({GetDisplayName(toolchain)})",
            CodeGenLanguage = baseRuntimeSpec.CodeGenLanguage,
            DetectionPatterns = baseRuntimeSpec.DetectionPatterns,
            Initialize = baseRuntimeSpec.Initialize,
            InstallDependencies = CreateInstallCommand(toolchain),
            Execute = CreateExecuteCommand(toolchain, tsConfigFileName),
            WatchExecute = CreateWatchCommand(toolchain, tsConfigFileName),
            PublishExecute = baseRuntimeSpec.PublishExecute,
            ExtensionLaunchCapability = baseRuntimeSpec.ExtensionLaunchCapability,
            MigrationFiles = baseRuntimeSpec.MigrationFiles
        };
    }

    private static CommandSpec CreateInstallCommand(TypeScriptAppHostToolchain toolchain)
    {
        return new CommandSpec
        {
            Command = GetCommandName(toolchain),
            Args = ["install"]
        };
    }

    private static CommandSpec CreateExecuteCommand(TypeScriptAppHostToolchain toolchain, string tsConfigFileName)
    {
        return toolchain switch
        {
            TypeScriptAppHostToolchain.Bun => new CommandSpec
            {
                Command = "bun",
                Args = ["run", "{appHostFile}"]
            },
            TypeScriptAppHostToolchain.Yarn => new CommandSpec
            {
                Command = "yarn",
                Args = ["exec", "tsx", "--tsconfig", tsConfigFileName, "{appHostFile}"]
            },
            TypeScriptAppHostToolchain.Pnpm => new CommandSpec
            {
                Command = "pnpm",
                Args = ["exec", "tsx", "--tsconfig", tsConfigFileName, "{appHostFile}"]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(toolchain), toolchain, null)
        };
    }

    private static CommandSpec CreateWatchCommand(TypeScriptAppHostToolchain toolchain, string tsConfigFileName)
    {
        return toolchain switch
        {
            TypeScriptAppHostToolchain.Bun => new CommandSpec
            {
                Command = "bun",
                Args = ["--watch", "run", "{appHostFile}"]
            },
            TypeScriptAppHostToolchain.Yarn => new CommandSpec
            {
                Command = "yarn",
                Args =
                [
                    "exec",
                    "nodemon",
                    "--signal", "SIGTERM",
                    "--watch", ".",
                    "--ext", "ts",
                    "--ignore", "node_modules/",
                    "--ignore", ".modules/",
                    "--exec", $"yarn exec tsx --tsconfig {tsConfigFileName} {{appHostFile}}"
                ]
            },
            TypeScriptAppHostToolchain.Pnpm => new CommandSpec
            {
                Command = "pnpm",
                Args =
                [
                    "exec",
                    "nodemon",
                    "--signal", "SIGTERM",
                    "--watch", ".",
                    "--ext", "ts",
                    "--ignore", "node_modules/",
                    "--ignore", ".modules/",
                    "--exec", $"pnpm exec tsx --tsconfig {tsConfigFileName} {{appHostFile}}"
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(toolchain), toolchain, null)
        };
    }

    private static string GetTsConfigFileName(RuntimeSpec runtimeSpec)
    {
        var args = runtimeSpec.Execute.Args;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--tsconfig", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return "tsconfig.apphost.json";
    }

    private static bool TryGetToolchainFromPackageJson(DirectoryInfo appHostDirectory, out TypeScriptAppHostToolchain toolchain)
    {
        var packageJsonPath = Path.Combine(appHostDirectory.FullName, PackageJsonFileName);
        if (!File.Exists(packageJsonPath))
        {
            toolchain = default;
            return false;
        }

        try
        {
            var packageJson = JsonNode.Parse(File.ReadAllText(packageJsonPath), documentOptions: ConfigurationHelper.ParseOptions) as JsonObject;
            if (packageJson?["packageManager"] is not JsonValue packageManagerValue ||
                !packageManagerValue.TryGetValue<string>(out var packageManager) ||
                string.IsNullOrWhiteSpace(packageManager))
            {
                toolchain = default;
                return false;
            }

            var packageManagerName = packageManager.Split('@', 2)[0];
            return TryParseToolchain(packageManagerName, out toolchain);
        }
        catch (JsonException)
        {
            toolchain = default;
            return false;
        }
        catch (IOException)
        {
            toolchain = default;
            return false;
        }
    }

    private static bool TryParseToolchain(string packageManagerName, out TypeScriptAppHostToolchain toolchain)
    {
        switch (packageManagerName.ToLowerInvariant())
        {
            case "npm":
                toolchain = TypeScriptAppHostToolchain.Npm;
                return true;
            case "bun":
                toolchain = TypeScriptAppHostToolchain.Bun;
                return true;
            case "yarn":
                toolchain = TypeScriptAppHostToolchain.Yarn;
                return true;
            case "pnpm":
                toolchain = TypeScriptAppHostToolchain.Pnpm;
                return true;
            default:
                toolchain = default;
                return false;
        }
    }
}
