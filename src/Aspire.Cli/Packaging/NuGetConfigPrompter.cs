// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Interaction;
using Aspire.Cli.Resources;
using Aspire.Cli.Utils;

namespace Aspire.Cli.Packaging;

/// <summary>
/// Handles prompting users to create or update NuGet.config files for explicit package channels.
/// </summary>
internal class NuGetConfigPrompter
{
    private readonly IInteractionService _interactionService;
    private readonly ICliHostEnvironment _hostEnvironment;

    public NuGetConfigPrompter(IInteractionService interactionService, ICliHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        _interactionService = interactionService;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// Prompts to create or update a NuGet.config for explicit channels.
    /// Always prompts the user before creating or updating the file.
    /// </summary>
    /// <param name="targetDirectory">The directory where the NuGet.config should be created or updated.</param>
    /// <param name="channel">The package channel providing mapping information.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    public async Task PromptToCreateOrUpdateAsync(DirectoryInfo targetDirectory, PackageChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetDirectory);
        ArgumentNullException.ThrowIfNull(channel);

        if (channel.Type is not PackageChannelType.Explicit)
        {
            return;
        }

        var mappings = channel.Mappings;
        if (mappings is null || mappings.Length == 0)
        {
            return;
        }

        var hasConfigInTargetDir = NuGetConfigMerger.TryFindNuGetConfigInDirectory(targetDirectory, out var nugetConfigFile);
        var hasMissingSources = hasConfigInTargetDir && NuGetConfigMerger.HasMissingSources(targetDirectory, channel);

        if (!hasConfigInTargetDir)
        {
            bool shouldCreate;

            if (!_hostEnvironment.SupportsInteractiveInput)
            {
                shouldCreate = true;
            }
            else
            {
                var choice = await _interactionService.PromptForSelectionAsync(
                    TemplatingStrings.CreateNugetConfigConfirmation,
                    [TemplatingStrings.Yes, TemplatingStrings.No],
                    c => c,
                    cancellationToken: cancellationToken);
                shouldCreate = string.Equals(choice, TemplatingStrings.Yes, StringComparisons.CliInputOrOutput);
            }

            if (shouldCreate)
            {
                await NuGetConfigMerger.CreateOrUpdateAsync(targetDirectory, channel, cancellationToken: cancellationToken);
                _interactionService.DisplayMessage(KnownEmojis.Package, TemplatingStrings.NuGetConfigCreatedConfirmationMessage);
            }
        }
        else if (hasMissingSources)
        {
            bool shouldUpdate;

            if (!_hostEnvironment.SupportsInteractiveInput)
            {
                shouldUpdate = true;
            }
            else
            {
                var updateChoice = await _interactionService.PromptForSelectionAsync(
                    TemplatingStrings.UpdateNuGetConfigConfirmation,
                    [TemplatingStrings.Yes, TemplatingStrings.No],
                    c => c,
                    cancellationToken: cancellationToken);
                shouldUpdate = string.Equals(updateChoice, TemplatingStrings.Yes, StringComparisons.CliInputOrOutput);
            }

            if (shouldUpdate)
            {
                await NuGetConfigMerger.CreateOrUpdateAsync(targetDirectory, channel, cancellationToken: cancellationToken);
                _interactionService.DisplayMessage(KnownEmojis.Package, TemplatingStrings.NuGetConfigUpdatedConfirmationMessage);
            }
        }
    }

    /// <summary>
    /// Creates or updates a NuGet.config for explicit channels without prompting.
    /// This is used when creating projects in subdirectories where the behavior is expected.
    /// </summary>
    /// <param name="targetDirectory">The directory where the NuGet.config should be created or updated.</param>
    /// <param name="channel">The package channel providing mapping information.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    public async Task CreateOrUpdateWithoutPromptAsync(DirectoryInfo targetDirectory, PackageChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetDirectory);
        ArgumentNullException.ThrowIfNull(channel);

        if (channel.Type is not PackageChannelType.Explicit)
        {
            return;
        }

        var mappings = channel.Mappings;
        if (mappings is null || mappings.Length == 0)
        {
            return;
        }

        await NuGetConfigMerger.CreateOrUpdateAsync(targetDirectory, channel, cancellationToken: cancellationToken);
        _interactionService.DisplayMessage(KnownEmojis.Package, TemplatingStrings.NuGetConfigCreatedOrUpdatedConfirmationMessage);
    }
}
