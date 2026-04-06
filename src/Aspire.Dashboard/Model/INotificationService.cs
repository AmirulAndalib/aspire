// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

/// <summary>
/// Service for managing dashboard notifications. Implementations must be thread-safe
/// since the service is registered as a singleton and accessed from multiple Blazor circuits.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gets all notifications, newest first.
    /// </summary>
    IReadOnlyList<NotificationItem> GetNotifications();

    /// <summary>
    /// Gets the count of unread notifications (added since the panel was last opened).
    /// </summary>
    int UnreadCount { get; }

    /// <summary>
    /// Adds a new notification and raises <see cref="OnChange"/>.
    /// </summary>
    void AddNotification(NotificationItem item);

    /// <summary>
    /// Signals that an existing notification was mutated (e.g., Progress → Success) and raises <see cref="OnChange"/>.
    /// </summary>
    void UpdateNotification(string id);

    /// <summary>
    /// Removes a specific notification (e.g., for cancelled commands) and raises <see cref="OnChange"/>.
    /// </summary>
    void RemoveNotification(string id);

    /// <summary>
    /// Marks all current notifications as read, resetting <see cref="UnreadCount"/> to zero.
    /// </summary>
    void MarkAllAsRead();

    /// <summary>
    /// Clears all notifications and raises <see cref="OnChange"/>.
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Raised when notifications are added, updated, removed, or cleared.
    /// </summary>
    event Action? OnChange;
}
