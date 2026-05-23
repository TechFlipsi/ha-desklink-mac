
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
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HaDeskLink;

/// <summary>
/// Shared model for now-playing media state across all platforms.
/// </summary>
public class MediaState
{
    public string State { get; set; } = "idle";  // idle, playing, paused
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Source { get; set; }           // App name (Spotify, Chrome, etc.)
    public int? Volume { get; set; }              // 0-100
    public bool? Muted { get; set; }
}

/// <summary>
/// Detects now-playing media on macOS using AppleScript / osascript.
/// Queries Apple Music (Music.app), Spotify, and browser-based players.
/// 
/// ⚠️ COMMUNITY TEST VERSION - NOT TESTED BY THE DEVELOPER
/// Some features may not work correctly on all macOS versions.
/// </summary>
public static class MediaPlayer
{
    /// <summary>
    /// Get the current media playback state on macOS.
    /// Tries multiple sources: Music.app, Spotify, browsers via nowplaying-cli.
    /// </summary>
    public static MediaState GetCurrentMediaState()
    {
        // Strategy 1: Apple Music (Music.app) — most reliable on macOS
        try
        {
            var state = GetMusicAppState();
            if (state != null && state.State != "idle")
                return state;
        }
        catch { }

        // Strategy 2: Spotify (via AppleScript)
        try
        {
            var state = GetSpotifyState();
            if (state != null && state.State != "idle")
                return state;
        }
        catch { }

        // Strategy 3: Browser/other players via nowplaying-cli
        try
        {
            var state = GetNowPlayingCliState();
            if (state != null && state.State != "idle")
                return state;
        }
        catch { }

        return new MediaState { State = "idle" };
    }

    /// <summary>
    /// Query Apple Music (Music.app) via osascript.
    /// </summary>
    private static MediaState? GetMusicAppState()
    {
        try
        {
            // Check if Music.app is running
            var isRunning = RunOsascript("application \"Music\" is running");
            if (string.IsNullOrEmpty(isRunning) || !isRunning.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                return null;

            // Get player state
            var playerState = RunOsascript(
                "tell application \"Music\" to get player state as string");
            if (string.IsNullOrEmpty(playerState))
                return null;

            var state = new MediaState
            {
                Source = "Music"
            };

            // Map AppleScript player states
            state.State = playerState.Trim().ToLowerInvariant() switch
            {
                "playing" => "playing",
                "paused" => "paused",
                "stopped" => "idle",
                "fast forwarding" => "playing",
                "rewinding" => "playing",
                _ => "idle"
            };

            // Only get track info if playing or paused
            if (state.State == "idle")
                return state;

            // Get current track info
            state.Title = RunOsascript(
                "tell application \"Music\" to get name of current track");
            state.Artist = RunOsascript(
                "tell application \"Music\" to get artist of current track");
            state.Album = RunOsascript(
                "tell application \"Music\" to get album of current track");

            return state;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Query Spotify (spotify.app) via osascript.
    /// </summary>
    private static MediaState? GetSpotifyState()
    {
        try
        {
            // Check if Spotify is running
            var isRunning = RunOsascript("application \"Spotify\" is running");
            if (string.IsNullOrEmpty(isRunning) || !isRunning.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                return null;

            // Get player state
            var playerState = RunOsascript(
                "tell application \"Spotify\" to get player state as string");
            if (string.IsNullOrEmpty(playerState))
                return null;

            var state = new MediaState
            {
                Source = "Spotify"
            };

            state.State = playerState.Trim().ToLowerInvariant() switch
            {
                "playing" => "playing",
                "paused" => "paused",
                "stopped" => "idle",
                _ => "idle"
            };

            if (state.State == "idle")
                return state;

            // Get current track
            state.Title = RunOsascript(
                "tell application \"Spotify\" to get name of current track");
            state.Artist = RunOsascript(
                "tell application \"Spotify\" to get artist of current track");
            state.Album = RunOsascript(
                "tell application \"Spotify\" to get album of current track");

            return state;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Query browser-based players and other apps via nowplaying-cli.
    /// nowplaying-cli: https://github.com/nicegram/nowplaying-cli
    /// </summary>
    private static MediaState? GetNowPlayingCliState()
    {
        try
        {
            // Check if nowplaying-cli is available
            using var whichProc = Process.Start(new ProcessStartInfo("which", "nowplaying-cli")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var whichOutput = whichProc?.StandardOutput.ReadToEnd()?.Trim();
            whichProc?.WaitForExit(2000);
            if (string.IsNullOrEmpty(whichOutput))
                return null;

            var state = new MediaState();

            // Get title
            state.Title = RunProcessAndGetOutput("nowplaying-cli", "get title");
            if (string.IsNullOrWhiteSpace(state.Title))
                return null; // nothing playing

            // Get artist
            state.Artist = RunProcessAndGetOutput("nowplaying-cli", "get artist");

            // Get album
            state.Album = RunProcessAndGetOutput("nowplaying-cli", "get album");

            // Get playback state
            var playbackState = RunProcessAndGetOutput("nowplaying-cli", "get playbackState");
            state.State = playbackState?.ToLowerInvariant() switch
            {
                "playing" => "playing",
                "paused" => "paused",
                "stopped" => "idle",
                _ => "playing" // assume playing if we have a title
            };

            // Get source app
            state.Source = RunProcessAndGetOutput("nowplaying-cli", "get title")
                ?? "Browser";

            // Try to get the app name more specifically
            var appName = RunProcessAndGetOutput("nowplaying-cli", "get appBundleIdentifier");
            if (!string.IsNullOrEmpty(appName))
            {
                state.Source = appName switch
                {
                    "com.google.Chrome" => "Chrome",
                    "com.apple.Safari" => "Safari",
                    "org.mozilla.firefox" => "Firefox",
                    "com.microsoft.edgemac" => "Edge",
                    "com.brave.Browser" => "Brave",
                    "com.spotify.client" => "Spotify",
                    _ => appName.Contains('.') ? appName.Substring(appName.LastIndexOf('.') + 1) : appName
                };
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Run an osascript command and return the output.
    /// </summary>
    private static string? RunOsascript(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e '{script.Replace("'", "'\\''")}'",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Run a generic process and return its stdout.
    /// </summary>
    private static string? RunProcessAndGetOutput(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
