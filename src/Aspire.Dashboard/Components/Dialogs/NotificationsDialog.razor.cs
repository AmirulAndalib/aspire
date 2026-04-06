// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Components.Dialogs;

public partial class NotificationsDialog : IDialogContentComponent, IDisposable
{
    private IReadOnlyList<NotificationItem> _notifications = [];

    [Inject]
    public required INotificationService NotificationService { get; init; }

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [CascadingParameter]
    public FluentDialog Dialog { get; set; } = default!;

    protected override void OnInitialized()
    {
        NotificationService.MarkAllAsRead();
        _notifications = NotificationService.GetNotifications();
        NotificationService.OnChange += HandleNotificationsChanged;
    }

    private void HandleNotificationsChanged()
    {
        InvokeAsync(() =>
        {
            _notifications = NotificationService.GetNotifications();
            StateHasChanged();
        });
    }

    private void DismissAll()
    {
        NotificationService.ClearAll();
    }

    public void Dispose()
    {
        NotificationService.OnChange -= HandleNotificationsChanged;
    }
}
