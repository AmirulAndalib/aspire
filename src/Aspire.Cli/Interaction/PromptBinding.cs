// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Aspire.Cli.Interaction;

/// <summary>
/// Binds a CLI option or argument to an interactive prompt.
/// When a prompt method receives this, it first checks the parse result for an explicitly provided value
/// before prompting interactively. In non-interactive mode, it uses the default value or displays an
/// actionable error naming the required option/argument.
/// </summary>
internal sealed class PromptBinding<T>
{
    private readonly Func<ParseResult, (bool WasProvided, T? Value)> _resolver;

    internal PromptBinding(
        ParseResult parseResult,
        string symbolDisplayName,
        Func<ParseResult, (bool WasProvided, T? Value)> resolver,
        T? defaultValue)
    {
        ParseResult = parseResult;
        SymbolDisplayName = symbolDisplayName;
        _resolver = resolver;
        DefaultValue = defaultValue;
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
    /// Resolves the value from the parse result. Returns whether the symbol was explicitly
    /// provided by the user and the resolved value.
    /// </summary>
    public (bool WasProvided, T? Value) Resolve() => _resolver(ParseResult);

    /// <summary>
    /// Creates a copy of this binding with a different default value.
    /// Useful when the default is computed later than the binding is created.
    /// </summary>
    public PromptBinding<T> WithDefault(T? newDefault) =>
        new(ParseResult, SymbolDisplayName, _resolver, newDefault);
}

/// <summary>
/// Factory methods for creating <see cref="PromptBinding{T}"/> instances.
/// </summary>
internal static class PromptBinding
{
    public static (bool WasProvided, T? OptionValue, T? DefaultValue) Resolve<T>(PromptBinding<T>? binding)
    {
        if (binding == null)
        {
            return default;
        }

        var (wasProvided, optionValue) = binding.Resolve();
        return (wasProvided, optionValue, binding.DefaultValue);
    }

    public static PromptBinding<T> Create<T>(ParseResult parseResult, Option<T> option) =>
        new(parseResult, FormatOptionName(option), BuildOptionResolver(option), default);

    public static PromptBinding<T> Create<T>(ParseResult parseResult, Option<T> option, T defaultValue) =>
        new(parseResult, FormatOptionName(option), BuildOptionResolver(option), defaultValue);

    public static PromptBinding<T> Create<T>(ParseResult parseResult, Argument<T> argument) =>
        new(parseResult, FormatArgumentName(argument), BuildArgumentResolver(argument), default);

    public static PromptBinding<T> Create<T>(ParseResult parseResult, Argument<T> argument, T defaultValue) =>
        new(parseResult, FormatArgumentName(argument), BuildArgumentResolver(argument), defaultValue);

    /// <summary>
    /// Creates a <see cref="PromptBinding{T}"/> with only a default value and no CLI symbol binding.
    /// Use when there is no corresponding CLI option or argument for the prompt.
    /// </summary>
    public static PromptBinding<T> CreateDefault<T>(T defaultValue) =>
        new(null!, string.Empty, static _ => (false, default), defaultValue);

    /// <summary>
    /// Creates a <see cref="PromptBinding{T}"/> for a <c>bool?</c> option that maps to Yes/No selection choices.
    /// When the option is explicitly provided, resolves to <paramref name="trueValue"/> or <paramref name="falseValue"/>.
    /// When not provided in non-interactive mode, defaults to <paramref name="falseValue"/>.
    /// </summary>
    public static PromptBinding<string?> CreateBoolAsSelection(ParseResult parseResult, Option<bool?> option, string trueValue, string falseValue) =>
        new(parseResult, FormatOptionName(option), BuildBoolAsSelectionResolver(option, trueValue, falseValue), falseValue);

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

    private static Func<ParseResult, (bool, string?)> BuildBoolAsSelectionResolver(Option<bool?> option, string trueValue, string falseValue) =>
        parseResult =>
        {
            var result = parseResult.GetResult(option);
            if (result is not null && !result.Implicit)
            {
                var value = parseResult.GetValue(option);
                return (true, value == true ? trueValue : falseValue);
            }

            return (false, default);
        };
}
