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
using System.IO;
using System.Threading.Tasks;

namespace HaDeskLink;

public class DeskLinkApp : Application
{
    private HaWebSocketClient? _ws;
    private System.Threading.Timer? _sensorTimer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var config = Program.GlobalConfig!;
        var api = Program.GlobalApi!;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(config, api);

            // Start WebSocket
            StartWebSocket(config, api);

            // Start sensor timer
            _sensorTimer = new System.Threading.Timer(async _ => await UpdateSensors(api), null,
                System.TimeSpan.Zero, System.TimeSpan.FromSeconds(config.SensorInterval));
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void StartWebSocket(Config config, HaApiClient api)
    {
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
                cmd => CommandHandler.Execute(cmd)
            );

            _ = Task.Run(async () =>
            {
                try { await _ws.ConnectAsync(); }
                catch (System.Exception ex) { System.Console.WriteLine($"WebSocket-Fehler: {ex.Message}"); }
            });
        }
        catch (System.Exception ex) { System.Console.WriteLine($"WebSocket-Start fehlgeschlagen: {ex.Message}"); }
    }

    private async Task UpdateSensors(HaApiClient api)
    {
        try
        {
            var sensors = SensorManager.GetAllSensors();
            await api.UpdateSensorStatesAsync(sensors);
        }
        catch { }
    }
}