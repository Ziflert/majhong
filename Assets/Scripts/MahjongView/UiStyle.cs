using System.Collections.Generic;
using Mahjong.Controls;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The one place where a role of the interface becomes a Unity colour.
    ///
    /// The palettes themselves live in the rules assembly (<see cref="UiThemes"/>) and are checked
    /// headless; the view only converts, scales and draws. Every panel asks
    /// <see cref="Of(UiRole)"/> instead of writing <c>new Color(0.9f, …)</c>, so switching the theme
    /// repaints the whole interface without touching a layout number, and no screen can keep a colour
    /// from a palette that is no longer in effect.
    /// </summary>
    public static class UiStyle
    {
        private static UiTheme theme = UiThemes.Default;
        private static UiProfile profile = UiProfiles.Default;
        private static float scale = 1f;
        private static float textScale = 1f;
        private static int revision;

        /// <summary>The palette in effect.</summary>
        public static UiTheme Theme => theme;

        /// <summary>The composition profile in effect.</summary>
        public static UiProfile Profile => profile;

        /// <summary>The size factor in effect; layout numbers are written for 100%.</summary>
        public static float Scale => scale * profile.Density;
        public static float TextScale => textScale;

        /// <summary>Goes up whenever the look changes, so a panel can tell it has to repaint itself.</summary>
        public static int Revision => revision;

        /// <summary>Puts a palette and a size in effect; every panel reads them on its next repaint.</summary>
        public static void Apply(UiTheme? next, double uiScale)
        {
            Apply(UiProfiles.Default, next, uiScale);
        }

        public static void Apply(UiProfile? nextProfile, UiTheme? next, double uiScale)
        {
            var palette = next ?? UiThemes.Default;
            var nextProfileValue = nextProfile ?? UiProfiles.Default;
            var factor = (float)UiThemeSettings.ClampScale(uiScale);
            if (palette.Id == theme.Id && nextProfileValue.Id == profile.Id && Mathf.Abs(factor - scale) < 0.0001f) return;

            theme = palette;
            profile = nextProfileValue;
            scale = factor;
            revision++;
            UiSkins.Forget(); // the baked skins belong to the palette that was in effect
        }

        /// <summary>Puts the look of these settings in effect.</summary>
        public static void Apply(GameSettings settings)
        {
            Apply(settings.Profile, settings.Theme, settings.UiScale);
            SetTextScale((float)settings.FontSize);
        }

        public static void SetTextScale(float value)
        {
            var next = Mathf.Clamp(value, 0.85f, 1.15f);
            if (Mathf.Abs(textScale - next) < 0.0001f) return;
            textScale = next;
            revision++;
        }

        /// <summary>The colour of a role in the palette in effect.</summary>
        public static Color Of(UiRole role) => ToColor(theme.Of(role));

        /// <summary>The colour of a role with its own transparency, for plates that sit on the table.</summary>
        public static Color Of(UiRole role, float alpha)
        {
            var colour = Of(role);
            colour.a = Mathf.Clamp01(alpha);
            return colour;
        }

        /// <summary>A palette colour as Unity sees it.</summary>
        public static Color ToColor(UiColour colour) =>
            new(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1f);

        /// <summary>A length scaled for the interface size the player chose.</summary>
        public static float S(float value) => value * scale;

        /// <summary>A composition-specific panel length, kept separate from text scaling.</summary>
        public static float Panel(float value) => value * profile.PanelWidth;

        /// <summary>A composition-specific vertical length.</summary>
        public static float PanelHeight(float value) => value * profile.PanelHeight;

        /// <summary>Spacing between rows in a modal list.</summary>
        public static float Row(float value) => value * profile.RowSpacing;

        /// <summary>Spacing between action buttons.</summary>
        public static float ButtonGap(float value) => value * profile.ButtonSpacing;

        /// <summary>Mixes two colours, 0 keeping <paramref name="from"/> and 1 giving <paramref name="to"/>.</summary>
        public static Color Mix(Color from, Color to, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(
                Mathf.Lerp(from.r, to.r, t),
                Mathf.Lerp(from.g, to.g, t),
                Mathf.Lerp(from.b, to.b, t),
                Mathf.Lerp(from.a, to.a, t));
        }

        /// <summary>Lifts a colour toward its palette's text colour, for a hover state that stays readable.</summary>
        public static Color Lift(Color colour, float amount) => Mix(colour, Of(UiRole.TextMain), amount);

        /// <summary>Darkens a colour toward the background, for a pressed or disabled state.</summary>
        public static Color Sink(Color colour, float amount) => Mix(colour, Of(UiRole.Background), amount);
    }
}
