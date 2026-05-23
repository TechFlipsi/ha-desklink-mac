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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HaDeskLink.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace HaDeskLink;

public class DeskLinkApp : Application
{
    private HaWebSocketClient? _ws;
    private System.Threading.Timer? _sensorTimer;
    private System.Threading.Timer? _mediaTimer;
    public static DeskLinkApp? Instance { get; private set; }
    private HaApiClient? _api;
    private MqttClient? _mqttClient;

    public HaApiClient GetApiClient() => _api ?? throw new InvalidOperationException("API not initialized");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Instance = this;
        var config = Program.GlobalConfig!;
        var api = Program.GlobalApi!;
        _api = api;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(config, api);

            // Start WebSocket
            StartWebSocket(config, api);

            // Start MQTT if enabled
            if (config.MqttEnabled && !string.IsNullOrEmpty(config.MqttBroker))
            {
                _ = StartMqttAsync(config);
            }

            // Start sensor timer
            _sensorTimer = new System.Threading.Timer(async _ =>
            {
                try { await UpdateSensors(api, config); }
                catch (Exception ex) { System.Console.WriteLine($"[SensorTimer] Error: {ex.Message}"); }
            }, null,
                System.TimeSpan.Zero, System.TimeSpan.FromSeconds(config.SensorInterval));

            // Start media state timer (MQTT only)
            _mediaTimer = new System.Threading.Timer(async _ =>
            {
                try { await UpdateMediaState(); }
                catch { }
            }, null,
                System.TimeSpan.FromSeconds(2), System.TimeSpan.FromSeconds(5));

            // Handle graceful shutdown — send pc_status = "off" + disconnect MQTT
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.ShutdownRequested += async (s, e) =>
                {
                    try
                    {
                        if (_api != null)
                        {
                            var pcOff = new SensorData("pc_status", "PC Status", "off", "", "connectivity", "mdi:desktop-classic")
                            {
                                SensorKind = SensorType.BinarySensor,
                                EntityCategory = null
                            };
                            await _api.UpdateSensorStatesAsync(new List<SensorData> { pcOff });
                        }
                    }
                    catch { }

                    // Disconnect MQTT gracefully
                    if (_mqttClient != null)
                    {
                        try { _mqttClient.Dispose(); }
                        catch { }
                    }
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartMqttAsync(Config config)
    {
        try
        {
            var regPath = Path.Combine(Config.GetConfigDir(), "registration.json");
            string deviceId = "";
            if (File.Exists(regPath))
            {
                var reg = JsonDocument.Parse(File.ReadAllText(regPath));
                deviceId = reg.RootElement.GetProperty("webhook_id").GetString() ?? "";
            }

            var configDir = Config.GetConfigDir();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "4.3.0";

            _mqttClient = new MqttClient(
                config.MqttBroker,
                config.MqttPort,
                config.MqttUsername,
                config.MqttPassword,
                config.MqttUseSsl,
                configDir,
                version,
                cmd => { try { _ = CommandHandler.ExecuteAsync(cmd); } catch { } }
            );

            await _mqttClient.ConnectAsync();

            // Publish discovery for all sensors + media player
            var sensors = SensorManager.GetAllSensors();
            await _mqttClient.PublishDiscoveryAsync(sensors);

            // Publish initial states
            await _mqttClient.PublishSensorStatesAsync(sensors);

            System.Console.WriteLine("[MQTT] Connected and discovery published");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[MQTT] Failed to connect: {ex.Message}");
            _mqttClient = null;
        }
    }

    private void StartWebSocket(Config config, HaApiClient api)
    {
        if (string.IsNullOrEmpty(config.HaToken))
        {
            System.Console.WriteLine("[HA DeskLink] FEHLER: Token konnte nicht geladen werden. Bitte App neu einrichten.");
            return;
        }

        try
        {
            var regPath = Path.Combine(Config.GetConfigDir(), "registration.json");
            var registration = System.Text.Json.JsonDocument.Parse(File.ReadAllText(regPath));
            var webhookId = registration.RootElement.GetProperty("webhook_id").GetString() ?? "";

            _ws = new HaWebSocketClient(
                config.HaUrl,
                config.HaToken,
                webhookId,
                null,
                cmd => { try { _ = CommandHandler.ExecuteAsync(cmd).ContinueWith(t => { if (t.IsFaulted) System.Console.WriteLine($"[CommandHandler] Error: {t.Exception?.InnerException?.Message}"); }, TaskScheduler.Default); } catch { } },
                verifySsl: config.VerifySsl
            );

            // Pass WS to MainWindow for retry button
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt && dt.MainWindow is Views.MainWindow mw)
                mw.SetWebSocketClient(_ws);

            _ = Task.Run(async () =>
            {
                try { await _ws.ConnectAsync(); }
                catch (System.Exception ex) { System.Console.WriteLine($"WebSocket-Fehler: {ex.Message}"); }
            });
        }
        catch (System.Exception ex) { System.Console.WriteLine($"WebSocket-Start fehlgeschlagen: {ex.Message}"); }
    }

    private async Task UpdateSensors(HaApiClient api, Config config)
    {
        try
        {
            var sensors = SensorManager.GetAllSensors();

            // Always send via WebSocket (mobile_app webhook) for redundancy
            await api.UpdateSensorStatesAsync(sensors);

            // Also publish via MQTT if connected
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                await _mqttClient.PublishSensorStatesAsync(sensors);
            }
        }
        catch { }
    }

    private async Task UpdateMediaState()
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
            return;

        try
        {
            var mediaState = MediaPlayer.GetCurrentMediaState();
            await _mqttClient.PublishMediaStateAsync(
                mediaState.State,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    mediaState.Title,
                    mediaState.Artist,
                    mediaState.Album,
                    mediaState.Source,
                    mediaState.Volume,
                    mediaState.Muted
                })
            );
        }
        catch { }
    }
}