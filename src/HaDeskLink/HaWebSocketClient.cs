// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HaDeskLink;

/// <summary>
/// Connects to Home Assistant via WebSocket to receive push notifications.
/// macOS version - no TrayIcon, uses console output instead.
/// </summary>
public class HaWebSocketClient : IDisposable
{
    private readonly string _haUrl;
    private readonly string _token;
    private readonly string _webhookId;
    private readonly Action<string>? _onCommand;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _msgId = 1;
    private bool _connected;

    public bool IsConnected => _connected;

    public HaWebSocketClient(string haUrl, string token, string webhookId, object? trayIcon, Action<string>? onCommand = null)
    {
        _haUrl = haUrl.TrimEnd('/');
        _token = token;
        _webhookId = webhookId;
        _onCommand = onCommand;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        var wsUrl = _haUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/api/websocket";

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                _ws.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;
                await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);

                var msg = await ReceiveMessage();
                if (msg == null || !msg.Contains("auth_required"))
                    throw new Exception("Expected auth_required from HA");

                await SendMessage(new { type = "auth", access_token = _token });

                msg = await ReceiveMessage();
                if (msg == null || !msg.Contains("auth_ok"))
                    throw new Exception("Auth failed");

                _connected = true;

                await SendMessage(new
                {
                    id = _msgId++,
                    type = "mobile_app/push_notification_channel",
                    webhook_id = _webhookId,
                    support_confirm = false
                });

                Console.WriteLine("✓ WebSocket verbunden mit Home Assistant");

                await ListenLoop();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _connected = false;
                Console.WriteLine($"WebSocket getrennt, reconnect in 5s... ({ex.Message})");
            }

            if (!_cts.Token.IsCancellationRequested)
            {
                try { await Task.Delay(5000, _cts.Token); } catch { break; }
            }
        }
        _connected = false;
    }

    private async Task<string?> ReceiveMessage()
    {
        if (_ws?.State != WebSocketState.Open) return null;
        var buffer = new byte[16384];
        var sb = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts?.Token ?? CancellationToken.None);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);
        return sb.ToString();
    }

    private async Task SendMessage(object data)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ListenLoop()
    {
        while (_ws?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
        {
            try
            {
                var msg = await ReceiveMessage();
                if (msg == null) break;
                ProcessMessage(msg);
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }
            catch { break; }
        }
    }

    private void ProcessMessage(string msg)
    {
        try
        {
            var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "event")
            {
                if (root.TryGetProperty("event", out var eventEl))
                {
                    string title = "HA DeskLink";
                    string message = "";
                    string? command = null;
                    List<NotificationAction>? actions = null;
                    string? commandOnAction = null;

                    if (eventEl.TryGetProperty("title", out var t))
                        title = t.GetString() ?? title;
                    if (eventEl.TryGetProperty("message", out var m))
                        message = m.GetString() ?? "";
                    if (eventEl.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("command", out var c))
                            command = c.GetString();
                        if (data.TryGetProperty("title", out var dt))
                            title = dt.GetString() ?? title;
                        if (data.TryGetProperty("message", out var dm))
                            message = dm.GetString() ?? message;
                        if (data.TryGetProperty("command_on_action", out var coa))
                            commandOnAction = coa.GetString();
                        if (data.TryGetProperty("actions", out var actionsArr))
                        {
                            actions = new List<NotificationAction>();
                            foreach (var a in actionsArr.EnumerateArray())
                            {
                                var act = a.GetProperty("action").GetString() ?? "";
                                var actTitle = a.TryGetProperty("title", out var at) ? at.GetString() ?? act : act;
                                var actCommand = a.TryGetProperty("command", out var ac) ? ac.GetString() : null;
                                actions.Add(new NotificationAction(act, actTitle, actCommand));
                            }
                        }
                    }

                    // Execute command if present (no action buttons)
                    if (!string.IsNullOrEmpty(command) && actions == null)
                    {
                        try { _onCommand?.Invoke(command!); }
                        catch { }
                        Console.WriteLine($"Befehl ausgeführt: {command}");
                    }

                    // Handle actionable notifications
                    if (actions != null && actions.Count > 0 && !string.IsNullOrEmpty(commandOnAction))
                    {
                        Console.WriteLine($"[Action] Auto-executing: {commandOnAction}");
                        try { _ = CommandHandler.ExecuteAsync(commandOnAction); }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        // Show macOS notification via osascript
                        try
                        {
                            var escapedTitle = title.Replace("\"", "\\\"");
                            var escapedMsg = message.Replace("\"", "\\\"");
                            var actionHint = actions != null && actions.Count > 0
                                ? $" | Aktionen: {string.Join(", ", actions.Select(a => a.Title))}"
                                : "";
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "osascript",
                                Arguments = $"-e 'display notification \"{escapedMsg}{actionHint}\" with title \"{escapedTitle}\"'",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            })?.WaitForExit(3000);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            if (_ws?.State == WebSocketState.Open)
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None).Wait(2000);
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _ws?.Dispose();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}

public class NotificationAction
{
    public string ActionKey { get; }
    public string Title { get; }
    public string? Command { get; }
    public NotificationAction(string actionKey, string title, string? command = null)
    {
        ActionKey = actionKey;
        Title = title;
        Command = command;
    }
}