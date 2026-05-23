// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaDeskLink.Views;

/// <summary>
/// Modern floating notification toast for HA push notifications (v4.1.0).
/// Dark navy blue palette with accent color support.
/// Auto-dismisses after 8s, supports action buttons, pause-on-hover.
/// </summary>
public class NotificationPopup : Window
{
    private DispatcherTimer? _autoCloseTimer;
    private bool _isClosing;
    private bool _isHovering;

    // ── Color Palette (Windows v4.1.0 Modern Toast) ────────────
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46));        // #1A1A2E - window bg
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromArgb(255, 22, 33, 62));      // #16213E - card bg
    private static readonly IBrush AccentBlueBrush = new SolidColorBrush(Color.FromArgb(255, 66, 133, 244)); // #4285F4
    private static readonly IBrush AccentGreenBrush = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)); // #4CAF50
    private static readonly IBrush GrayBrush = new SolidColorBrush(Color.FromArgb(255, 140, 140, 160));
    private static readonly IBrush MessageBrush = new SolidColorBrush(Color.FromArgb(255, 200, 200, 215));

    private readonly IBrush _accentColor;

    public NotificationPopup(string title, string message, List<NotificationActionInfo>? actions = null, IBrush? accentColor = null)
    {
        _accentColor = accentColor ?? AccentBlueBrush;

        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = BgBrush;

        actions ??= new List<NotificationActionInfo>();

        // ── Accent bar (left edge) ─────────────────────────────
        var accentBar = new Border
        {
            Background = _accentColor,
            CornerRadius = new CornerRadius(12, 0, 0, 12)
        };
        Grid.SetColumn(accentBar, 0);

        // ── Content stack ──────────────────────────────────────
        var contentStack = new StackPanel { Margin = new Thickness(16, 14, 14, 14), Spacing = 8 };
        Grid.SetColumn(contentStack, 1);
        BuildContentChildren(contentStack, title, message, actions);

        // ── Card with accent bar ──────────────────────────────
        var card = new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(8),
            Child = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("4,*"),
                Children = { accentBar, contentStack }
            }
        };

        Content = card;

        // ── Auto-close timer (8 seconds) ──────────────────────
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _autoCloseTimer.Tick += OnAutoCloseTick;
        _autoCloseTimer.Start();

        // ── Pause timer on hover, restart on leave ────────────
        PointerEntered += OnPointerEnter;
        PointerExited += OnPointerLeave;
        Closing += OnWindowClosing;
    }

    private void BuildContentChildren(StackPanel stack, string title, string message, List<NotificationActionInfo> actions)
    {
        // ── Title row with close button ────────────────────────
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(titleText, 0);

        var closeBtn = new Button
        {
            Content = "✕",
            FontSize = 14,
            Background = Brushes.Transparent,
            Foreground = GrayBrush,
            Padding = new Thickness(4, 2),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            [ToolTip.TipProperty] = "Schließen"
        };
        Grid.SetColumn(closeBtn, 1);
        closeBtn.PointerEntered += (s, e) => { if (s is Button b) b.Foreground = Brushes.White; };
        closeBtn.PointerExited += (s, e) => { if (s is Button b) b.Foreground = GrayBrush; };
        closeBtn.Click += (s, e) => CloseAnimated();

        var headerGrid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto") };
        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(closeBtn);
        stack.Children.Add(headerGrid);

        // ── Message body ───────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = MessageBrush,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 5
        });

        // ── Timestamp ─────────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            FontSize = 11,
            Foreground = GrayBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        });

        // ── Action buttons ────────────────────────────────────
        if (actions.Count > 0)
        {
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            foreach (var action in actions)
            {
                var btn = BuildActionButton(action);
                btnPanel.Children.Add(btn);
            }
            stack.Children.Add(btnPanel);
        }
    }

    private Button BuildActionButton(NotificationActionInfo action)
    {
        var defaultBg = _accentColor;
        var hoverBg = DarkenBrush(_accentColor, 0.2);

        var btn = new Button
        {
            Content = action.Title,
            FontSize = 12,
            Background = defaultBg,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 6),
            Tag = action
        };

        btn.PointerEntered += (s, e) => { if (s is Button b) b.Background = hoverBg; };
        btn.PointerExited += (s, e) => { if (s is Button b) b.Background = defaultBg; };
        btn.Click += (s, e) =>
        {
            PauseTimer();
            action.OnAction?.Invoke();
            CloseAnimated();
        };

        return btn;
    }

    /// <summary>
    /// Create a slightly darker version of a SolidColorBrush for hover effect.
    /// </summary>
    private static IBrush DarkenBrush(IBrush brush, double amount)
    {
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SolidColorBrush(Color.FromArgb(
                c.A,
                (byte)Math.Max(0, c.R - (int)(c.R * amount)),
                (byte)Math.Max(0, c.G - (int)(c.G * amount)),
                (byte)Math.Max(0, c.B - (int)(c.B * amount))
            ));
        }
        return brush;
    }

    public void PositionTopRight(double offsetX = 20, double offsetY = 20)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.All.FirstOrDefault();
        if (screen != null)
        {
            var wa = screen.WorkingArea;
            Position = new PixelPoint(wa.X + wa.Width - (int)Width - (int)offsetX - 16, wa.Y + (int)offsetY);
        }
    }

    // ── Timer management ───────────────────────────────────────
    private void OnAutoCloseTick(object? sender, EventArgs e)
    {
        _autoCloseTimer?.Stop();
        CloseAnimated();
    }

    private void OnPointerEnter(object? sender, PointerEventArgs e)
    {
        _isHovering = true;
        PauseTimer();
    }

    private void OnPointerLeave(object? sender, PointerEventArgs e)
    {
        _isHovering = false;
        if (!_isClosing)
            ResumeTimer();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CleanupTimer();
    }

    private void PauseTimer()
    {
        _autoCloseTimer?.Stop();
    }

    private void ResumeTimer()
    {
        if (!_isClosing && _autoCloseTimer != null)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(8);
            _autoCloseTimer.Start();
        }
    }

    private void CleanupTimer()
    {
        if (_autoCloseTimer != null)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Tick -= OnAutoCloseTick;
            _autoCloseTimer = null;
        }
    }

    private void CloseAnimated()
    {
        if (_isClosing) return;
        _isClosing = true;
        CleanupTimer();
        Close();
    }

    // ── Static factory methods ─────────────────────────────────

    /// <summary>
    /// Show a standard notification toast with blue accent.
    /// </summary>
    public static NotificationPopup ShowNotification(string title, string message, List<NotificationActionInfo>? actions = null)
    {
        var popup = new NotificationPopup(title, message, actions);
        popup.Show();
        popup.PositionTopRight();
        return popup;
    }

    /// <summary>
    /// Show a connection toast with green accent (success/connected).
    /// </summary>
    public static NotificationPopup ShowConnectionToast(string title, string message, List<NotificationActionInfo>? actions = null)
    {
        var popup = new NotificationPopup(title, message, actions, accentColor: AccentGreenBrush);
        popup.Show();
        popup.PositionTopRight();
        return popup;
    }
}

public class NotificationActionInfo
{
    public string ActionKey { get; }
    public string Title { get; }
    public Action? OnAction { get; set; }
    public NotificationActionInfo(string actionKey, string title, Action? onAction = null)
    {
        ActionKey = actionKey;
        Title = title;
        OnAction = onAction;
    }
}
