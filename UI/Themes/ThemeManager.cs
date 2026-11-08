using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mahjong.UI.Themes
{
    /// <summary>
    /// Central theme manager. Responsible for loading, applying color and font themes,
    /// custom color schemes, preview, and saving settings.
    /// </summary>
    public sealed class ThemeManager : MonoBehaviour
    {
        private static ThemeManager _instance;

        /// <summary>Global theme manager instance.</summary>
        public static ThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ThemeManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ThemeManager>();
                }
                return _instance;
            }
        }

        public event Action<ColorTheme> OnThemeChanged;
        public event Action<ColorTheme> OnCustomSchemeApplied;
        public event Action<ColorTheme> OnCustomSchemeReset;

        public ColorTheme ActiveTheme { get; private set; }
        public ColorTheme BaseTheme { get; private set; }
        public FontTheme FontTheme { get; private set; }
        public GameConfig Config { get; private set; }
        public bool CustomSchemeActive { get; private set; }

        private readonly List<ColorTheme> _presets = new()
        {
            ColorTheme.DarkJade, ColorTheme.CreamPaper, ColorTheme.WoodClassic,
            ColorTheme.AmberSunrise, ColorTheme.IcyRiver,
        };

        private readonly string[] _presetNames =
        {
            "DarkJade", "CreamPaper", "WoodClassic", "AmberSunrise", "IcyRiver",
        };

        private ColorTheme _previewTheme;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            LoadConfig();
            InitializeThemes();
            ApplyTheme(BaseTheme);
        }

        private void OnApplicationQuit()
        {
            Config.Save();
        }

        private void LoadConfig()
        {
            Config = GameConfig.Load();

            if (!string.IsNullOrEmpty(Config.colorThemeGuid))
            {
                var loaded = Resources.Load<ColorTheme>(Config.colorThemeGuid);
                if (loaded != null)
                {
                    BaseTheme = Instantiate(loaded);
                }
            }

            if (!string.IsNullOrEmpty(Config.fontThemeGuid))
            {
                var loaded = Resources.Load<FontTheme>(Config.fontThemeGuid);
                if (loaded != null)
                {
                    FontTheme = Instantiate(loaded);
                }
            }

            CustomSchemeActive = !string.IsNullOrEmpty(Config.customColorsHex);
        }

        private void InitializeThemes()
        {
            if (BaseTheme == null)
            {
                BaseTheme = ColorTheme.DarkJade;
            }

            if (FontTheme == null)
            {
                FontTheme = FontTheme.CreateDefault();
            }

            if (CustomSchemeActive && !string.IsNullOrEmpty(Config.customColorsHex))
            {
                var custom = Instantiate(BaseTheme);
                if (custom.FromHex(Config.customColorsHex))
                {
                    ActiveTheme = custom;
                }
                else
                {
                    Debug.LogWarning("Corrupt custom scheme; falling back to base theme.");
                    CustomSchemeActive = false;
                    ActiveTheme = BaseTheme;
                }
            }
            else
            {
                ActiveTheme = BaseTheme;
            }
        }

        public bool SetPreset(int index)
        {
            if (index < 0 || index >= _presets.Count) return false;

            BaseTheme = Instantiate(_presets[index]);
            CustomSchemeActive = false;
            ActiveTheme = BaseTheme;

            Config.colorThemeGuid = string.Empty;
            Config.Save();

            OnThemeChanged?.Invoke(ActiveTheme);
            return true;
        }

        public bool SetPresetByName(string name)
        {
            var index = Array.IndexOf(_presetNames, name);
            return SetPreset(index);
        }

        public void SetTheme(ColorTheme theme)
        {
            if (theme == null) return;

            BaseTheme = Instantiate(theme);
            CustomSchemeActive = false;
            ActiveTheme = BaseTheme;

            Config.colorThemeGuid = string.Empty;
            Config.Save();

            OnThemeChanged?.Invoke(ActiveTheme);
        }

        public void ApplyCustomScheme(ColorTheme custom)
        {
            if (custom == null) return;

            CustomSchemeActive = true;
            ActiveTheme = Instantiate(custom);

            Config.customColorsHex = ActiveTheme.ToHex();
            Config.Save();

            OnCustomSchemeApplied?.Invoke(ActiveTheme);
        }

        public void ResetCustomScheme()
        {
            if (!CustomSchemeActive) return;

            CustomSchemeActive = false;
            ActiveTheme = BaseTheme;

            Config.customColorsHex = string.Empty;
            Config.Save();

            OnCustomSchemeReset?.Invoke(BaseTheme);
        }

        public (string Name, string Description)[] GetPresetNames()
        {
            var descriptions = new[]
            {
                "Deep emerald with jade accents - default theme",
                "Light cream palette for bright screens",
                "Warm wood tones of classic mahjong",
                "Warm amber palette with gold accents",
                "Cold icy palette with blue accents",
            };

            var result = new (string, string)[_presetNames.Length];
            for (var i = 0; i < _presetNames.Length; i++)
            {
                result[i] = (_presetNames[i], descriptions[i]);
            }

            return result;
        }

        public string GetCurrentThemeDescription()
        {
            if (CustomSchemeActive)
            {
                return "Custom scheme";
            }

            for (var i = 0; i < _presetNames.Length; i++)
            {
                if (BaseTheme.name == _presets[i].name)
                {
                    return _presetNames[i];
                }
            }

            return BaseTheme.name;
        }

        public bool IsCustom() => CustomSchemeActive;

        public void PreviewTheme(ColorTheme theme)
        {
            if (theme == null) return;
            _previewTheme = Instantiate(theme);
        }

        public void ApplyPreview()
        {
            if (_previewTheme == null) return;

            if (CustomSchemeActive)
            {
                ApplyCustomScheme(_previewTheme);
            }
            else
            {
                SetTheme(_previewTheme);
            }

            _previewTheme = null;
        }

        public void CancelPreview()
        {
            _previewTheme = null;
        }

        public void ApplyFont<T>(T component, string kind) where T : UnityEngine.Component
        {
            if (FontTheme == null) return;

            var font = kind switch
            {
                "Display" => FontTheme.display,
                "Section" => FontTheme.display,
                "Body" => FontTheme.body,
                "Numbers" => FontTheme.numbers,
                "Button" => FontTheme.body,
                _ => FontTheme.body,
            };

            var size = kind switch
            {
                "Display" => FontTheme.displaySize,
                "Section" => FontTheme.sectionSize,
                "Body" => FontTheme.bodySize,
                "Numbers" => FontTheme.numbersSize,
                "Button" => FontTheme.buttonSize,
                _ => FontTheme.bodySize,
            };

            SetFontAndSize(component, font, size);
        }

        private void SetFontAndSize<T>(T component, Font font, float size) where T : UnityEngine.Component
        {
            if (component is TextMeshProUGUI textMeshUGUI)
            {
                textMeshUGUI.font = font;
                textMeshUGUI.fontSize = (int)size;
                return;
            }

            if (component is Text text)
            {
                text.font = font;
                text.fontSize = (int)size;
            }
        }

        public void ApplyColor<T>(T component, string role) where T : UnityEngine.Component
        {
            if (ActiveTheme == null) return;

            var color = GetColorFromRole(role);
            ApplyColorToComponent(component, color);
        }

        private Color GetColorFromRole(string role)
        {
            var field = typeof(ColorTheme).GetField(role,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return (Color)field.GetValue(ActiveTheme);
            }

            return ActiveTheme.textMain;
        }

        private void ApplyColorToComponent<T>(T component, Color color) where T : UnityEngine.Component
        {
            if (component is Image image)
            {
                image.color = color;
                return;
            }

            if (component is Text text)
            {
                text.color = color;
            }

            if (component is TextMeshProUGUI textMeshUGUI)
            {
                textMeshUGUI.color = color;
            }
        }
    }
}
