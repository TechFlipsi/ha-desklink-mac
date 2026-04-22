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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// Application configuration persisted as JSON.
/// HA Token is encrypted with macOS Keychain via security command.
/// Falls back to AES-GCM with machine-keyed encryption if Keychain unavailable.
/// </summary>
public class Config
{
    private static readonly string AppName = "HA_DeskLink";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public string HaUrl { get; set; } = "";
    public string HaToken { get; set; } = "";
    public bool VerifySsl { get; set; } = false;
    public int SensorInterval { get; set; } = 30;
    public string UpdateChannel { get; set; } = "stable";
    public string Language { get; set; } = "de";
    /// <summary>
    /// Encrypted HA token. When set, HaToken is cleared.
    /// </summary>
    public string? HaTokenEncrypted { get; set; }

    private string ConfigPath => Path.Combine(ConfigDir, "config.json");

    /// <summary>
    /// Store token in macOS Keychain (most secure option).
    /// </summary>
    private static bool TryKeychainStore(string token)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "security",
                Arguments = $"add-generic-password -a ha-desklink -s ha-desklink-token -w {EscapeShellArg(token)} -U",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Read token from macOS Keychain.
    /// </summary>
    private static string? TryKeychainRead()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "security",
                Arguments = "find-generic-password -a ha-desklink -s ha-desklink-token -w",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    private static string EscapeShellArg(string arg)
    {
        return "'" + arg.Replace("'", "'\\''") + "'";
    }

    /// <summary>
    /// AES-GCM fallback encryption (if Keychain unavailable).
    /// </summary>
    private static byte[] GetOrCreateKey()
    {
        var keyPath = Path.Combine(ConfigDir, ".key");
        if (File.Exists(keyPath))
            return Convert.FromBase64String(File.ReadAllText(keyPath).Trim());

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(keyPath, Convert.ToBase64String(key));
        try { System.Diagnostics.Process.Start("chmod", $"600 {keyPath}")?.WaitForExit(2000); } catch { }
        return key;
    }

    private static string EncryptAesGcm(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var key = GetOrCreateKey();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = new AesGcm(key);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    private static string DecryptAesGcm(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return "";
        try
        {
            var key = GetOrCreateKey();
            var combined = Convert.FromBase64String(encryptedText);
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            if (combined.Length < nonceSize + tagSize) return "";

            var nonce = new byte[nonceSize];
            var tag = new byte[tagSize];
            var ciphertext = new byte[combined.Length - nonceSize - tagSize];
            Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(combined, nonceSize + tagSize, ciphertext, 0, ciphertext.Length);

            using var aes = new AesGcm(key);
            var plainBytes = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch { return ""; }
    }

    public static Config Load()
    {
        Directory.CreateDirectory(ConfigDir);
        var path = Path.Combine(ConfigDir, "config.json");
        Config config;

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        else
        {
            config = new Config();
        }

        // Try Keychain first (most secure)
        var keychainToken = TryKeychainRead();

        // Migration: if HaTokenEncrypted is empty but HaToken has a value
        if (string.IsNullOrEmpty(config.HaTokenEncrypted) && !string.IsNullOrEmpty(config.HaToken))
        {
            // Try Keychain first
            if (TryKeychainStore(config.HaToken))
            {
                config.HaTokenEncrypted = "keychain";
            }
            else
            {
                config.HaTokenEncrypted = EncryptAesGcm(config.HaToken);
            }
            config.HaToken = "";
            config.Save();
            config.HaToken = TryKeychainRead() ?? DecryptAesGcm(config.HaTokenEncrypted);
        }
        else if (!string.IsNullOrEmpty(config.HaTokenEncrypted))
        {
            if (config.HaTokenEncrypted == "keychain")
            {
                config.HaToken = keychainToken ?? "";
            }
            else
            {
                config.HaToken = DecryptAesGcm(config.HaTokenEncrypted);
            }
        }

        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);

        if (!string.IsNullOrEmpty(HaToken))
        {
            if (TryKeychainStore(HaToken))
            {
                HaTokenEncrypted = "keychain";
            }
            else
            {
                HaTokenEncrypted = EncryptAesGcm(HaToken);
            }
        }

        var saveConfig = new Config
        {
            HaUrl = HaUrl,
            HaToken = "", // NEVER save plaintext token
            VerifySsl = VerifySsl,
            SensorInterval = SensorInterval,
            UpdateChannel = UpdateChannel,
            Language = Language,
            HaTokenEncrypted = HaTokenEncrypted
        };

        var json = JsonSerializer.Serialize(saveConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string GetConfigDir() => ConfigDir;
}