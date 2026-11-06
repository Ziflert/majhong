using System;
using System.Collections.Generic;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>Composition of the interface, independent from its colour palette.</summary>
    public sealed class UiProfile
    {
        public UiProfile(string id, string nameRu, string nameEn, float density, float corner, float shadow,
            float panelWidth, float panelHeight, float rowSpacing, float buttonSpacing)
        {
            Id = id; NameRu = nameRu; NameEn = nameEn; Density = density; Corner = corner; Shadow = shadow;
            PanelWidth = panelWidth; PanelHeight = panelHeight; RowSpacing = rowSpacing; ButtonSpacing = buttonSpacing;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        /// <summary>Relative breathing room of rows and panels.</summary>
        public float Density { get; }
        /// <summary>Relative corner radius used by the plate skin.</summary>
        public float Corner { get; }
        /// <summary>Opacity of the profile's soft shadow.</summary>
        public float Shadow { get; }
        /// <summary>Relative width of modal sheets, without changing the camera or their frame limit.</summary>
        public float PanelWidth { get; }
        /// <summary>Relative height of modal sheets.</summary>
        public float PanelHeight { get; }
        /// <summary>Relative distance between rows in modal lists.</summary>
        public float RowSpacing { get; }
        /// <summary>Relative air between buttons in an action bar.</summary>
        public float ButtonSpacing { get; }
        public string Name => Loc.Current == Language.Ru ? NameRu : NameEn;
    }

    public static class UiProfiles
    {
        public static readonly UiProfile Minimal = new("minimal", "Минималистичный", "Minimal", 1f, 1f, 0.55f,
            1f, 1f, 1f, 1f);
        public static readonly UiProfile Studio = new("studio", "Studio", "Studio", 1.12f, 1.45f, 0.72f,
            1.08f, 1.08f, 1.10f, 1.18f);
        public static readonly IReadOnlyList<UiProfile> All = new[] { Minimal, Studio };
        public static UiProfile Default => Minimal;

        public static UiProfile Find(string? id)
        {
            foreach (var profile in All)
                if (string.Equals(profile.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) return profile;
            return Default;
        }

        public static UiProfile Step(UiProfile current, int delta)
        {
            if (delta == 0) return current ?? Default;
            var index = 0;
            for (var i = 0; i < All.Count; i++) if (All[i].Id == current?.Id) index = i;
            return All[Math.Clamp(index + Math.Sign(delta), 0, All.Count - 1)];
        }

        public static UiProfile? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var value = text.Trim();
            foreach (var profile in All)
                if (profile.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    profile.NameRu.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    profile.NameEn.Equals(value, StringComparison.OrdinalIgnoreCase)) return profile;
            return null;
        }
    }
}
