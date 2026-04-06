// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

/// <summary>
/// Represents the intent/severity of a notification.
/// </summary>
public enum NotificationIntent
{
    Progress,
    Success,
    Error,
    Info
}

/// <summary>
/// A mutable notification item that can transition from one state to another
/// (e.g., Progress to Success/Error when a command completes).
/// </summary>
public sealed class NotificationItem
{
    public string Id { get; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public NotificationIntent Intent { get; set; }

    public string? ResourceName { get; set; }

    public string? CommandName { get; set; }
}
