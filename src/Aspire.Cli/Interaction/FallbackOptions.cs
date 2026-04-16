// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Aspire.Cli.Interaction;

/// <summary>
/// Provides CLI argument fallback for interactive prompts.
/// When a prompt method receives this, it first checks the parse result for an explicitly provided value
/// before prompting interactively. In non-interactive mode, it uses the default value or displays an
/// actionable error naming the required option/argument.
/// </summary>
internal sealed class FallbackOptions<T>
{
    private readonly Func<ParseResult, (bool WasProvided, T? Value)> _resolver;

    internal FallbackOptions(
        ParseResult parseResult,
        string symbolDisplayName,
        Func<ParseResult, (bool WasProvided, T? Value)> resolver,
        T? defaultValue,
        bool hasDefaultValue)
    {
        ParseResult = parseResult;
        SymbolDisplayName = symbolDisplayName;
        _resolver = resolver;
        DefaultValue = defaultValue;
        HasDefaultValue = hasDefaultValue;
    }

    /// <summary>
    /// Gets the parse result from the current command invocation.
    /// </summary>
    public ParseResult ParseResult { get; }

    /// <summary>
    /// Gets the display name of the CLI symbol, formatted for error messages
    /// (e.g. "'--publisher'" for options, "'&lt;integration&gt;'" for arguments).
    /// </summary>
    public string SymbolDisplayName { get; }

    /// <summary>
    /// Gets the default value to use when non-interactive and the symbol was not provided.
    /// </summary>
    public T? DefaultValue { get; }

    /// <summary>
    /// Gets whether a default value was explicitly configured.
    /// </summary>
    public bool HasDefaultValue { get; }

    /// <summary>
    /// Resolves the value from the parse result. Returns whether the symbol was explicitly
    /// provided by the user and the resolved value.
    /// </summary>
    public (bool WasProvided, T? Value) Resolve() => _resolver(ParseResult);
}

/// <summary>
/// Factory methods for creating <see cref="FallbackOptions{T}"/> instances.
/// </summary>
internal static class FallbackOptions
{
    public static FallbackOptions<T> Create<T>(ParseResult parseResult, Option<T> option) =>
        new(parseResult, FormatOptionName(option), BuildOptionResolver(option), default, hasDefaultValue: false);

    public static FallbackOptions<T> Create<T>(ParseResult parseResult, Option<T> option, T defaultValue) =>
        new(parseResult, FormatOptionName(option), BuildOptionResolver(option), defaultValue, hasDefaultValue: true);

    public static FallbackOptions<T> Create<T>(ParseResult parseResult, Argument<T> argument) =>
        new(parseResult, FormatArgumentName(argument), BuildArgumentResolver(argument), default, hasDefaultValue: false);

    public static FallbackOptions<T> Create<T>(ParseResult parseResult, Argument<T> argument, T defaultValue) =>
        new(parseResult, FormatArgumentName(argument), BuildArgumentResolver(argument), defaultValue, hasDefaultValue: true);

    private static string FormatOptionName<T>(Option<T> option) => $"'--{option.Name}'";

    private static string FormatArgumentName<T>(Argument<T> argument) => $"'<{argument.Name}>'";

    private static Func<ParseResult, (bool, T?)> BuildOptionResolver<T>(Option<T> option) =>
        parseResult =>
        {
            var result = parseResult.GetResult(option);
            if (result is not null && !result.Implicit)
            {
                return (true, parseResult.GetValue(option));
            }

            return (false, default);
        };

    private static Func<ParseResult, (bool, T?)> BuildArgumentResolver<T>(Argument<T> argument) =>
        parseResult =>
        {
            var result = parseResult.GetResult(argument);
            if (result is not null && !result.Implicit)
            {
                return (true, parseResult.GetValue(argument));
            }

            return (false, default);
        };
}
