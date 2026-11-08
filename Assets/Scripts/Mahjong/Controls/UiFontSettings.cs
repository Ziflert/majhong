using System;
using System.Collections.Generic;

namespace Mahjong.Controls
{
    /// <summary>Font families and text-size presets exposed by the interface settings.</summary>
    public static class UiFontSettings
    {
        public const string Default = "segoe";
        public static readonly IReadOnlyList<string> Families = new[] { "segoe", "arial", "tahoma" };
        public static readonly double[] Sizes = { 0.85d, 1d, 1.15d };

        public static string StepFamily(string current, int delta)
        {
            var index = FamilyIndex(current);
            return Families[Math.Clamp(index + Math.Sign(delta), 0, Families.Count - 1)];
        }

        public static double StepSize(double current, int delta)
        {
            var value = double.IsNaN(current) ? 1d : current;
            var index = 0;
            var distance = double.MaxValue;
            for (var i = 0; i < Sizes.Length; i++)
                if (Math.Abs(Sizes[i] - value) < distance) { index = i; distance = Math.Abs(Sizes[i] - value); }
            return Sizes[Math.Clamp(index + Math.Sign(delta), 0, Sizes.Length - 1)];
        }

        public static string FamilyName(string family) => family?.ToLowerInvariant() switch
        {
            "arial" => "Arial",
            "tahoma" => "Tahoma",
            _ => "Segoe UI"
        };

        public static string SizeText(double size) => $"{(int)Math.Round(StepSize(size, 0) * 100d)}%";

        private static int FamilyIndex(string family)
        {
            for (var i = 0; i < Families.Count; i++)
                if (string.Equals(Families[i], family, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        public static string? TryParseFamily(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim().ToLowerInvariant();
            foreach (var family in Families)
                if (text == family || text == FamilyName(family).ToLowerInvariant()) return family;
            return null;
        }

        public static double? TryParseSize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim().TrimEnd('%');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return null;
            if (parsed > 2d) parsed /= 100d;
            return StepSize(parsed, 0);
        }
    }
}
