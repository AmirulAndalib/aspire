// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

/// <summary>
/// Thread-safe singleton implementation of <see cref="INotificationService"/>.
/// Notifications persist across browser refreshes since this is registered as a singleton.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly List<NotificationItem> _notifications = [];
    private readonly object _lock = new();
    private int _unreadCount;

    public int UnreadCount
    {
        get
        {
            lock (_lock)
            {
                return _unreadCount;
            }
        }
    }

    public event Action? OnChange;

    public IReadOnlyList<NotificationItem> GetNotifications()
    {
        lock (_lock)
        {
            // Return a snapshot in reverse order (newest first).
            var snapshot = new List<NotificationItem>(_notifications.Count);
            for (var i = _notifications.Count - 1; i >= 0; i--)
            {
                snapshot.Add(_notifications[i]);
            }
            return snapshot;
        }
    }

    public void AddNotification(NotificationItem item)
    {
        lock (_lock)
        {
            _notifications.Add(item);
            _unreadCount++;
        }

        OnChange?.Invoke();
    }

    public void UpdateNotification(string id)
    {
        // The caller already mutated the item in-place. We just verify it exists and notify.
        bool found;
        lock (_lock)
        {
            found = _notifications.Exists(n => n.Id == id);
        }

        if (found)
        {
            OnChange?.Invoke();
        }
    }

    public void RemoveNotification(string id)
    {
        bool removed;
        lock (_lock)
        {
            removed = _notifications.RemoveAll(n => n.Id == id) > 0;
            if (removed && _unreadCount > 0)
            {
                _unreadCount--;
            }
        }

        if (removed)
        {
            OnChange?.Invoke();
        }
    }

    public void MarkAllAsRead()
    {
        lock (_lock)
        {
            _unreadCount = 0;
        }

        OnChange?.Invoke();
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _notifications.Clear();
            _unreadCount = 0;
        }

        OnChange?.Invoke();
    }
}
