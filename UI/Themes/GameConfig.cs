using System;
using System.IO;
using UnityEngine;

namespace Mahjong.UI.Themes
{
    /// <summary>
    /// Global game settings persisted to disk as JSON in Application.persistentDataPath.
    /// Stores the current theme GUID, font theme GUID, interface scale, audio volumes,
    /// key bindings, accessibility options and table texture choice.
    /// </summary>
    [Serializable]
    public sealed class GameConfig
    {
        /// <summary>Config format version for forward migrations.</summary>
        public const string ConfigVersion = "ui-config-v1";

        public string colorThemeGuid = "";
        public string fontThemeGuid = "";
        public float interfaceScale = 1.0f;
        public string language = "ru";

        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public float voiceVolume = 0.6f;
        public bool musicEnabled = true;
        public bool sfxEnabled = true;
        public bool effectsEnabled = true;
        public bool animationsEnabled = true;

        /// <summary>Key -> action name, serialized via JsonUtility as a list.</summary>
        public KeyBindingList keyBindings = new();

        public string colorBlindness = "none";
        public bool largeText = false;
        public bool subtitlesEnabled = false;

        /// <summary>Custom color scheme as hex string (ColorTheme.ToHex format).</summary>
        public string customColorsHex = "";

        /// <summary>Rules profile: sports / classic144 / classic136.</summary>
        public string rulesVariant = "sports";

        /// <summary>Table texture: felt_dark / felt_red / bamboo / marble / wood / grid.</summary>
        public string tableTexture = "felt_dark";

        // ------------------------------------------------------------- paths

        private static string ConfigPath =>
            Path.Combine(Application.persistentDataPath, "majhong-config.json");

        private static string BackupPath =>
            Path.Combine(Application.persistentDataPath, "majhong-config.json.bak");

        // ------------------------------------------------------------- load / save

        /// <summary>Load config from disk. Falls back to defaults on corruption.</summary>
        public static GameConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    var cfg = new GameConfig();
                    cfg.Save();
                    return cfg;
                }

                var json = File.ReadAllText(ConfigPath);
                var cfg2 = JsonUtility.FromJson<GameConfig>(json);
                if (cfg2 == null)
                {
                    return CreateDefault();
                }

                return cfg2;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Config load failed: {ex.Message}");
                return CreateDefault();
            }
        }

        /// <summary>Save config to disk with a backup.</summary>
        public void Save()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    File.Copy(ConfigPath, BackupPath, overwrite: true);
                }

                var json = JsonUtility.ToJson(this, prettyPrint: true);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Config save failed: {ex.Message}");
            }
        }

        /// <summary>Restore the previous config from backup.</summary>
        public static bool RestoreBackup()
        {
            try
            {
                if (!File.Exists(BackupPath)) return false;
                File.Copy(BackupPath, ConfigPath, overwrite: true);
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------- key bindings

        /// <summary>Get the action bound to a key, or null.</summary>
        public string GetBinding(string key)
        {
            foreach (var kb in keyBindings.entries)
            {
                if (kb.key == key) return kb.action;
            }
            return null;
        }

        /// <summary>Bind a key to an action. Replaces any previous binding for that key.</summary>
        public void SetBinding(string key, string action)
        {
            foreach (var kb in keyBindings.entries)
            {
                if (kb.key == key)
                {
                    kb.action = action;
                    return;
                }
            }
            keyBindings.entries.Add(new KeyBinding { key = key, action = action });
        }

        /// <summary>Remove all bindings for an action.</summary>
        public void UnsetBinding(string action)
        {
            keyBindings.entries.RemoveAll(kb => kb.action == action);
        }

        // ------------------------------------------------------------- defaults

        /// <summary>Create a default config instance.</summary>
        public static GameConfig CreateDefault()
        {
            return new GameConfig
            {
                interfaceScale = 1.0f,
                language = "ru",
                musicVolume = 0.7f,
                sfxVolume = 0.8f,
                voiceVolume = 0.6f,
                musicEnabled = true,
                sfxEnabled = true,
                effectsEnabled = true,
                animationsEnabled = true,
                keyBindings = new KeyBindingList(),
                colorBlindness = "none",
                largeText = false,
                subtitlesEnabled = false,
                rulesVariant = "sports",
                tableTexture = "felt_dark",
            };
        }

        /// <summary>Reset all settings to defaults and save.</summary>
        public void ResetToDefaults()
        {
            var d = CreateDefault();
            colorThemeGuid = d.colorThemeGuid;
            fontThemeGuid = d.fontThemeGuid;
            interfaceScale = d.interfaceScale;
            language = d.language;
            musicVolume = d.musicVolume;
            sfxVolume = d.sfxVolume;
            voiceVolume = d.voiceVolume;
            musicEnabled = d.musicEnabled;
            sfxEnabled = d.sfxEnabled;
            effectsEnabled = d.effectsEnabled;
            animationsEnabled = d.animationsEnabled;
            keyBindings = d.keyBindings;
            colorBlindness = d.colorBlindness;
            largeText = d.largeText;
            subtitlesEnabled = d.subtitlesEnabled;
            customColorsHex = d.customColorsHex;
            rulesVariant = d.rulesVariant;
            tableTexture = d.tableTexture;
            Save();
        }
    }

    /// <summary>A single key binding entry.</summary>
    [Serializable]
    public sealed class KeyBinding
    {
        public string key = "";
        public string action = "";
    }

    /// <summary>Serializable list wrapper for JsonUtility.</summary>
    [Serializable]
    public sealed class KeyBindingList
    {
        public System.Collections.Generic.List<KeyBinding> entries = new();
    }
}
