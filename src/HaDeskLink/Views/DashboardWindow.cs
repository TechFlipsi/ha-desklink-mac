// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HaDeskLink.Views;

/// <summary>
/// Embedded HA Dashboard using Avalonia NativeWebView with external_auth.
/// Auto-logs in using the Long-Lived Access Token from config.
/// Includes rate-limiting and IP-ban prevention.
/// </summary>
public class DashboardWindow : Window
{
    private readonly string _haUrl;
    private readonly string _token;
    private readonly AuthGuard _authGuard;

    private object? _webView;
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

        BuildContent();
        Loaded += OnLoaded;
    }

    private void BuildContent()
    {
        _errorLabel = new TextBlock
        {
            Text = "",
            Foreground = Brushes.Red,
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
                    Text = "🏠 Dashboard wird geladen...",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = "Verbinde mit Home Assistant...",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

        var fallbackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "🌐 Embedded Dashboard",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                _loadingPanel,
                _errorLabel,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 10,
                    Children =
                    {
                        new Button { Name = "BtnRetry", Content = "🔄 Erneut versuchen", IsVisible = false },
                        new Button { Name = "BtnOpenBrowser", Content = "🔗 Im Browser öffnen" }
                    }
                }
            }
        };

        _mainPanel = new Border { Background = Brushes.White, Child = fallbackPanel };
        Content = _mainPanel;

        var btnRetry = this.FindControl<Button>("BtnRetry");
        if (btnRetry != null) btnRetry.Click += OnRetry;

        var btnBrowser = this.FindControl<Button>("BtnOpenBrowser");
        if (btnBrowser != null) btnBrowser.Click += OnOpenBrowser;
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
            var webViewType = TryResolveWebViewType();

            if (webViewType == null)
            {
                ShowFallback("WebView nicht verfügbar – wird im Browser geöffnet.", canRetry: false);
                await Task.Delay(1500);
                OpenInBrowser();
                return;
            }

            _webView = Activator.CreateInstance(webViewType);
            if (_webView == null)
            {
                ShowFallback("WebView konnte nicht erstellt werden.", canRetry: false);
                return;
            }

            var dashboardUrl = $"{_haUrl}?external_auth=1";
            await NavigateWebView(_webView, dashboardUrl);
            HookNavigationComplete(_webView);
            _mainPanel!.Child = _webView as Control;
        }
        catch (Exception ex)
        {
            _authGuard.RecordFailure(ex.Message);
            if (_authGuard.IsBlocked)
                ShowError(_authGuard.BlockMessage);
            else
                ShowFallback($"Fehler beim Laden: {ex.Message}", canRetry: true);
        }
    }

    private Type? TryResolveWebViewType()
    {
        try { var t = Type.GetType("Avalonia.Controls.NativeWebView, Avalonia.Controls.WebView"); if (t != null) return t; } catch { }
        try { var a = System.Reflection.Assembly.Load("Avalonia.Controls.WebView"); var t = a?.GetType("Avalonia.Controls.NativeWebView"); if (t != null) return t; } catch { }
        try { var t = Type.GetType("WebView.Avalonia.WebView, WebView.Avalonia"); if (t != null) return t; } catch { }
        return null;
    }

    private void SetWebViewProperty(object obj, string propertyName, object value)
    {
        try { obj.GetType().GetProperty(propertyName)?.SetValue(obj, value); } catch { }
    }

    private async Task NavigateWebView(object webView, string url)
    {
        try
        {
            var m = webView.GetType().GetMethod("NavigateAsync");
            if (m != null) { var t = m.Invoke(webView, new object[] { url }) as Task; if (t != null) await t; return; }
            var m2 = webView.GetType().GetMethod("Navigate");
            if (m2 != null) { m2.Invoke(webView, new object[] { url }); return; }
            var p = webView.GetType().GetProperty("Source");
            if (p != null) { p.SetValue(webView, new Uri(url)); }
        } catch { }
    }

    private void HookNavigationComplete(object webView)
    {
        try
        {
            var evt = webView.GetType().GetEvent("NavigationCompleted")
                ?? webView.GetType().GetEvent("NavigateCompleted")
                ?? webView.GetType().GetEvent("PageLoaded");
            if (evt != null)
            {
                var d = Delegate.CreateDelegate(evt.EventHandlerType!, this, nameof(OnNavigationCompleted));
                evt.AddEventHandler(webView, d);
            }
            else
            {
                _ = InjectExternalAuthDelayed(webView, 3000);
            }
        }
        catch { _ = InjectExternalAuthDelayed(webView, 3000); }
    }

    private void OnNavigationCompleted(object? sender, EventArgs e) { _ = InjectExternalAuth(_webView!); }

    private async Task InjectExternalAuthDelayed(object webView, int delayMs)
    {
        await Task.Delay(delayMs);
        await InjectExternalAuth(webView);
    }

    private async Task InjectExternalAuth(object webView)
    {
        if (_authGuard.IsBlocked) return;
        try
        {
            var js = BuildExternalAuthScript();
            var m = webView.GetType().GetMethod("ExecuteScriptAsync");
            if (m != null) { var t = m.Invoke(webView, new object[] { js }) as Task<string>; if (t != null) await t; return; }
            var m2 = webView.GetType().GetMethod("ExecuteJavaScript");
            if (m2 != null) { m2.Invoke(webView, new object[] { js }); }
        }
        catch (Exception ex) { _authGuard.RecordFailure($"Auth inject failed: {ex.Message}"); }
    }

    private string BuildExternalAuthScript()
    {
        var escapedToken = _token.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
        return $$"""
        (function() {
            if (window._externalAuthInjected) return;
            window._externalAuthInjected = true;
            const TOKEN = '{{escapedToken}}';
            window.externalApp = {
                getExternalAuth: function(callback, force) {
                    try { callback({ access_token: TOKEN, expires_in: 900, refresh_token: TOKEN, token_type: 'Bearer' }); }
                    catch(e) { console.error('externalApp.getExternalAuth error:', e); }
                },
                saveExternalAuth: function(data, callback) { try { if (callback) callback(); } catch(e) {} },
                revokeExternalAuth: function(callback) { try { if (callback) callback(); } catch(e) {} if (window.close) window.close(); }
            };
            console.log('[HA DeskLink] externalAuth interface injected');
        })();
        """;
    }

    private void ShowError(string message)
    {
        if (_loadingPanel != null) _loadingPanel.IsVisible = false;
        if (_errorLabel != null) { _errorLabel.Text = message; _errorLabel.IsVisible = true; }
        var btnRetry = this.FindControl<Button>("BtnRetry");
        if (btnRetry != null && !_authGuard.IsHardBlocked) btnRetry.IsVisible = true;
    }

    private void ShowFallback(string message, bool canRetry)
    {
        if (_loadingPanel != null) _loadingPanel.IsVisible = false;
        if (_errorLabel != null) { _errorLabel.Text = message; _errorLabel.IsVisible = true; _errorLabel.Foreground = Brushes.Orange; }
        var btnRetry = this.FindControl<Button>("BtnRetry");
        if (btnRetry != null) btnRetry.IsVisible = canRetry;
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

    private void OnOpenBrowser(object? sender, RoutedEventArgs e) { OpenInBrowser(); }

    private void OpenInBrowser()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl) { UseShellExecute = true }); } catch { }
        Close();
    }
}

/// <summary>
/// Protects against HA IP-bans by rate-limiting authentication attempts.
/// Rules: Max 3 soft → Max 5 hard block. Exponential backoff. Auto-reset on success.
/// </summary>
public class AuthGuard
{
    private int _failedAttempts;
    private DateTime _blockedUntil = DateTime.MinValue;
    private bool _hardBlocked;
    private string _lastError = "";

    public const int MaxSoftAttempts = 3;
    public const int MaxHardAttempts = 5;

    public int CurrentBackoffSeconds => _failedAttempts switch { 0 => 0, 1 => 5, 2 => 30, 3 => 120, _ => 300 };

    public bool IsBlocked
    {
        get
        {
            if (_hardBlocked) return true;
            if (_failedAttempts >= MaxSoftAttempts && DateTime.UtcNow < _blockedUntil) return true;
            return false;
        }
    }

    public bool IsHardBlocked => _hardBlocked;

    public string BlockMessage
    {
        get
        {
            if (_hardBlocked)
                return $"⚠️ Authentifizierung blockiert — zu viele fehlgeschlagene Versuche.\n\nLetzter Fehler: {_lastError}\n\nAus Sicherheitsgründen (HA IP-Ban-Schutz) wurden die Login-Versuche gestoppt.\nBitte überprüfe deinen Token und starte HA DeskLink neu.";
            if (_failedAttempts >= MaxSoftAttempts)
            {
                var remaining = (_blockedUntil - DateTime.UtcNow);
                if (remaining > TimeSpan.Zero)
                    return $"⚠️ Zu viele Login-Versuche — warte {remaining:hh\\:mm\\:ss} vor erneutem Versuch.\n\nLetzter Fehler: {_lastError}\n\nDies schützt vor HA IP-Bans bei ungültigen Token.";
            }
            return $"⚠️ Authentifizierung fehlgeschlagen ({_failedAttempts}/{MaxHardAttempts}).\n{_lastError}";
        }
    }

    public void RecordFailure(string error)
    {
        _failedAttempts++;
        _lastError = error;
        if (_failedAttempts >= MaxHardAttempts) { _hardBlocked = true; _blockedUntil = DateTime.MaxValue; }
        else if (_failedAttempts >= MaxSoftAttempts) { _blockedUntil = DateTime.UtcNow.AddSeconds(CurrentBackoffSeconds); }
    }

    public void RecordSuccess() { _failedAttempts = 0; _hardBlocked = false; _blockedUntil = DateTime.MinValue; _lastError = ""; }
    public void Reset() { _failedAttempts = 0; _hardBlocked = false; _blockedUntil = DateTime.MinValue; _lastError = ""; }

    public static bool ValidateTokenFormat(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (token.Length < 20) return false;
        if (token.Contains(' ') && !token.StartsWith("ey")) return false;
        return true;
    }
}