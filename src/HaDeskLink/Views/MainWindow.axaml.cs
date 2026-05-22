// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HaDeskLink.Views;

/// <summary>
/// Main window with modern dark UI, embedded HA Dashboard, Quick Actions.
/// Uses external_auth API for auto-login with Long-Lived Access Token.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Config _config;
    private readonly HaApiClient _api;
    private HaWebSocketClient? _ws;
    private TextBlock? _statusText;
    private TextBlock? _connectionLabel;
    private Button? _retryButton;

    // ── Color Palette ──────────────────────────────────────────
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46));
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromArgb(255, 22, 33, 62));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.FromArgb(255, 15, 52, 96));
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(255, 233, 69, 96));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));
    private static readonly IBrush GrayBrush = new SolidColorBrush(Color.FromArgb(255, 140, 140, 160));

    public MainWindow(Config config, HaApiClient api)
    {
        _config = config;
        _api = api;

        Title = "HA DeskLink macOS";
        Width = 560;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BgBrush;

        var root = new StackPanel { Margin = new Thickness(0), Spacing = 0 };

        // ── Accent bar top ──────────────────────────────────────
        root.Children.Add(new Border { Height = 4, Background = AccentBrush });

        // ── Header ──────────────────────────────────────────────
        root.Children.Add(new Border
        {
            Background = PanelBrush,
            Padding = new Thickness(20, 18, 20, 14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "🏠 HA DeskLink macOS", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                    new TextBlock { Text = $"v{GetVersion()} • Home Assistant Companion", FontSize = 12, Foreground = GrayBrush }
                }
            }
        });

        // ── Connection Status Card ─────────────────────────────
        var isConnected = !string.IsNullOrEmpty(config.HaUrl);
        _connectionLabel = new TextBlock
        {
            Text = isConnected ? $"✓ Verbunden: {config.HaUrl}" : "⚠️ Nicht verbunden",
            FontSize = 13,
            Foreground = isConnected ? SuccessBrush : HighlightBrush
        };

        root.Children.Add(new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(16, 12, 16, 0),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "📊 Status", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Brushes.White },
                    _connectionLabel,
                    new TextBlock { Text = "📡 Sensoren werden automatisch an HA gesendet", FontSize = 12, Foreground = GrayBrush }
                }
            }
        });

        // ── Dashboard Button ───────────────────────────────────
        var dashboardBtn = new Button
        {
            Content = "🌐 Dashboard öffnen",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(255, 66, 133, 244)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16, 12, 16, 0)
        };
        dashboardBtn.Click += OnOpenDashboard;
        root.Children.Add(dashboardBtn);

        // ── Quick Actions Button ───────────────────────────────
        var quickActionsBtn = new Button
        {
            Content = "⚡ Quick Actions",
            FontSize = 14,
            Background = AccentBrush,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16, 8, 16, 0)
        };
        quickActionsBtn.Click += OnQuickActions;
        root.Children.Add(quickActionsBtn);

        // ── Commands Info Card ──────────────────────────────────
        root.Children.Add(new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(16, 12, 16, 0),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "🖥️ Befehle", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Brushes.White },
                    new TextBlock { Text = "shutdown • restart • sleep • lock • mute", FontSize = 12, Foreground = GrayBrush },
                    new TextBlock { Text = "volume_up/down • monitor_off/on • brightness:N", FontSize = 12, Foreground = GrayBrush }
                }
            }
        });

        // ── Footer ──────────────────────────────────────────────
        root.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16,
            Margin = new Thickness(0, 16, 0, 8),
            Children =
            {
                new Button { Content = "💬 Discord", FontSize = 12, Background = AccentBrush, Foreground = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6) },
                new Button { Content = "📦 GitHub", FontSize = 12, Background = AccentBrush, Foreground = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6) },
            }
        });

        // Wire footer buttons
        var footerBtns = root.Children.OfType<StackPanel>().LastOrDefault();
        if (footerBtns != null)
        {
            var btns = footerBtns.Children.OfType<Button>().ToList();
            if (btns.Count >= 1) btns[0].Click += (s, e) => OpenUrl("https://discord.gg/7G2SqpXpsC");
            if (btns.Count >= 2) btns[1].Click += (s, e) => OpenUrl("https://github.com/TechFlipsi/ha-desklink-mac");
        }

        // ── Status + Retry (bottom) ────────────────────────────
        _statusText = new TextBlock
        {
            Text = "",
            FontSize = 13,
            Foreground = HighlightBrush,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16, 8, 16, 0)
        };
        root.Children.Add(_statusText);

        _retryButton = new Button
        {
            Content = "🔄 Erneut verbinden",
            FontSize = 13,
            Background = HighlightBrush,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 8)
        };
        _retryButton.Click += OnRetryClicked;
        root.Children.Add(_retryButton);

        Content = root;
    }

    public void SetWebSocketClient(HaWebSocketClient ws) { _ws = ws; CheckLoginBlock(); }

    public void CheckLoginBlock()
    {
        if (_ws != null && _ws.IsBlocked)
        {
            _statusText!.Text = "⛔ Login fehlgeschlagen. Token ungültig.\nBitte überprüfe deinen Home Assistant Token.";
            _retryButton!.IsVisible = true;
        }
        else
        {
            _statusText!.Text = "";
            _retryButton!.IsVisible = false;
        }
    }

    private async void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        if (_ws == null) return;
        _ws.ResetLoginBlock();
        _statusText!.Text = "🔄 Verbindung wird erneut versucht...";
        _statusText.Foreground = GrayBrush;
        _retryButton!.IsVisible = false;
        _ = Task.Run(async () =>
        {
            try { await _ws.ConnectAsync(); }
            catch (Exception ex) { Console.WriteLine($"WebSocket-Fehler: {ex.Message}"); }
        });
        await Task.Delay(5000);
        CheckLoginBlock();
    }

    private void OnOpenDashboard(object? sender, RoutedEventArgs e)
    {
        // macOS: Open in default browser (no embedded WebView available for Avalonia 11.2)
        try { Process.Start(new ProcessStartInfo { FileName = _config.HaUrl, UseShellExecute = true }); }
        catch (Exception ex) { Console.WriteLine($"Fehler beim Öffnen: {ex.Message}"); }
    }

    private static string GetVersion()
    {
        try
        {
            var vfile = System.IO.Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (System.IO.File.Exists(vfile)) return System.IO.File.ReadAllText(vfile).Trim();
        }
        catch { }
        return "3.1.0";
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private async void OnQuickActions(object? sender, RoutedEventArgs e)
    {
        var actions = LoadQuickActions(_config);

        var panel = new StackPanel { Margin = new Thickness(0), Spacing = 0 };

        // ── Accent bar ─────────────────────────────────────────
        panel.Children.Add(new Border { Height = 4, Background = AccentBrush });

        // ── Header ─────────────────────────────────────────────
        panel.Children.Add(new Border
        {
            Background = PanelBrush,
            Padding = new Thickness(20, 16, 20, 12),
            Child = new TextBlock { Text = "⚡ Quick Actions", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White }
        });

        if (actions.Count == 0)
        {
            panel.Children.Add(new Border
            {
                Background = BgBrush,
                Padding = new Thickness(20, 30),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "📭", FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "Keine Quick Actions konfiguriert", FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "In config.json QuickActions hinzufügen:\n{ \"entityId\": \"light.wohnzimmer\", \"name\": \"Wohnzimmer\" }", Foreground = GrayBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center },
                        new Button { Content = "OK", Background = AccentBrush, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(24, 8), HorizontalAlignment = HorizontalAlignment.Center }
                    }
                }
            });
        }
        else
        {
            var actionPanel = new StackPanel { Margin = new Thickness(12, 8, 12, 8), Spacing = 6 };

            foreach (var action in actions)
            {
                var entityDot = new Border
                {
                    Width = 8, Height = 8,
                    Background = HighlightBrush,
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(entityDot, 0);

                var nameText = new TextBlock
                {
                    Text = action.Name,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameText, 1);

                var toggleBtn = new Button
                {
                    Content = "⏻",
                    FontSize = 16,
                    Background = AccentBrush, Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 4),
                    Tag = action
                };
                Grid.SetColumn(toggleBtn, 2);

                var card = new Border
                {
                    Background = PanelBrush,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10),
                    Margin = new Thickness(0, 2),
                    Child = new Grid
                    {
                        ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto"),
                        Children = { entityDot, nameText, toggleBtn }
                    }
                };

                toggleBtn.Click += async (s, args) =>
                {
                    var b = s as Button;
                    var a = b?.Tag as QuickActionItem;
                    if (a == null) return;
                    b!.Background = HighlightBrush;
                    b.Content = "⏳";
                    try { await _api.ToggleEntityAsync(a.EntityId); b.Content = "✓"; b.Background = SuccessBrush; }
                    catch { b.Content = "✗"; b.Background = HighlightBrush; }
                    await Task.Delay(1200);
                    b.Content = "⏻";
                    b.Background = AccentBrush;
                };
                actionPanel.Children.Add(card);
            }

            panel.Children.Add(new Border { Background = BgBrush, Child = actionPanel });
        }

        var dialog = new Window
        {
            Title = "Quick Actions",
            Width = 420,
            Height = actions.Count == 0 ? 250 : Math.Max(180, 60 + actions.Count * 62),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = BgBrush,
            Content = panel
        };

        var closeBtns = panel.Children.OfType<Border>()
            .SelectMany(b => (b.Child as StackPanel)?.Children.OfType<Button>() ?? Enumerable.Empty<Button>());
        foreach (var cb in closeBtns) cb.Click += (s, a) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static List<QuickActionItem> LoadQuickActions(Config config)
    {
        var result = new List<QuickActionItem>();
        try
        {
            var arr = System.Text.Json.JsonDocument.Parse(config.QuickActions).RootElement;
            foreach (var item in arr.EnumerateArray())
            {
                var entityId = item.TryGetProperty("entityId", out var eid) ? eid.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? entityId : entityId;
                if (!string.IsNullOrEmpty(entityId))
                    result.Add(new QuickActionItem(entityId, name));
            }
        }
        catch { }
        return result;
    }
}

public class QuickActionItem
{
    public string EntityId { get; }
    public string Name { get; }
    public QuickActionItem(string entityId, string name) { EntityId = entityId; Name = name; }
}