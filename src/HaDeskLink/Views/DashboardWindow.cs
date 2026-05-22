// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaWebView;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HaDeskLink.Views;

/// <summary>
/// Embedded HA Dashboard using WebView.Avalonia (WKWebView) with external_auth API.
/// Auto-logs in using the Long-Lived Access Token from config.
/// Includes rate-limiting and IP-ban prevention via AuthGuard.
/// </summary>
public class DashboardWindow : Window
{
    private readonly string _haUrl;
    private readonly string _token;
    private readonly AuthGuard _authGuard;
    private WebView? _webView;
    private TextBlock? _errorLabel;
    private StackPanel? _loadingPanel;
    private Border? _mainPanel;

    public DashboardWindow(string haUrl, string token)
    {
        _haUrl = haUrl.TrimEnd('/');
        _token = token;
        _authGuard = new AuthGuard();

        Title = "HA DeskLink - Dashboard";
        Width = 1200;
        Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46));

        BuildContent();
        Loaded += OnLoaded;
    }

    private void BuildContent()
    {
        _errorLabel = new TextBlock
        {
            Text = "",
            Foreground = Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            IsVisible = false
        };

        _loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "🏠 Dashboard wird geladen…",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = "Verbinde mit Home Assistant…",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

        var retryBtn = new Button
        {
            Name = "BtnRetry",
            Content = "🔄 Erneut versuchen",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(255, 15, 52, 96)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        retryBtn.Click += OnRetry;

        var browserBtn = new Button
        {
            Name = "BtnOpenBrowser",
            Content = "🔗 Im Browser öffnen",
            Background = Brushes.Transparent,
            Foreground = Brushes.Gray,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        browserBtn.Click += OnOpenBrowser;

        _mainPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46)),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = "🌐 Embedded Dashboard", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                    _loadingPanel,
                    _errorLabel,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10, Children = { retryBtn, browserBtn } }
                }
            }
        };

        Content = _mainPanel;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await InitializeWebView();
    }

    private async Task InitializeWebView()
    {
        if (_authGuard.IsBlocked)
        {
            ShowError(_authGuard.BlockMessage);
            return;
        }

        try
        {
            _webView = new WebView
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var dashboardUrl = $"{_haUrl}?external_auth=1";
            _webView.Url = dashboardUrl;

            _webView.NavigationCompleted += OnNavigationCompleted;

            _mainPanel!.Child = _webView;
        }
        catch (Exception ex)
        {
            _authGuard.RecordFailure(ex.Message);
            if (_authGuard.IsBlocked)
                ShowError(_authGuard.BlockMessage);
            else
                ShowError($"Fehler beim Laden: {ex.Message}");
        }
    }

    private async void OnNavigationCompleted(object? sender, WebViewEventArgs e)
    {
        if (_webView == null || _authGuard.IsBlocked) return;

        try
        {
            var js = BuildExternalAuthScript();
            await Task.Delay(500);
            _webView.EvaluateJavaScript(js);
        }
        catch (Exception ex)
        {
            _authGuard.RecordFailure($"Auth inject failed: {ex.Message}");
        }
    }

    private string BuildExternalAuthScript()
    {
        var escapedToken = _token.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
        var expiresIn = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

        return $"""
        (function() {{
            if (window._externalAuthInjected) return;
            window._externalAuthInjected = true;

            const TOKEN = '{escapedToken}';
            const EXPIRES = {expiresIn};

            window.externalApp = {{
                getExternalAuth: function(callback, force) {{
                    try {{
                        callback({{
                            access_token: TOKEN,
                            expires_in: 900,
                            refresh_token: TOKEN,
                            token_type: 'Bearer'
                        }});
                    }} catch(e) {{
                        console.error('[HA DeskLink] getExternalAuth error:', e);
                    }}
                }},
                saveExternalAuth: function(data, callback) {{
                    try {{ if (callback) callback(); }} catch(e) {{}}
                }},
                revokeExternalAuth: function(callback) {{
                    try {{ if (callback) callback(); }} catch(e) {{}}
                    if (window.close) window.close();
                }}
            }};

            console.log('[HA DeskLink] externalAuth interface injected successfully');
        }})();
        """;
    }

    private void ShowError(string message)
    {
        if (_loadingPanel != null) _loadingPanel.IsVisible = false;
        if (_errorLabel != null)
        {
            _errorLabel.Text = message;
            _errorLabel.IsVisible = true;
        }
        var btnRetry = this.FindControl<Button>("BtnRetry");
        if (btnRetry != null && !_authGuard.IsHardBlocked)
            btnRetry.IsVisible = true;
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        _authGuard.Reset();
        if (_errorLabel != null) _errorLabel.IsVisible = false;
        if (_loadingPanel != null) _loadingPanel.IsVisible = true;
        var btnRetry = this.FindControl<Button>("BtnRetry");
        if (btnRetry != null) btnRetry.IsVisible = false;

        _ = InitializeWebView();
    }

    private void OnOpenBrowser(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_haUrl) { UseShellExecute = true });
        }
        catch { }
        Close();
    }
}