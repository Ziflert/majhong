using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The font every <see cref="TextMesh"/> of the interface uses.
    ///
    /// The interface is Russian, and a legacy TextMesh with the built-in font shows Cyrillic as blank
    /// squares or nothing at all. So the font is picked once, in order of preference:
    /// a font shipped under <c>Resources/Fonts/ui</c>, then a system font that actually has Cyrillic
    /// glyphs. If nothing fits, the built-in font is kept and English stays readable.
    /// </summary>
    public static class UiFont
    {
        private static Font? cached;
        private static bool resolved;

        /// <summary>System fonts tried in order; all of them carry Cyrillic on Windows and macOS.</summary>
        private static readonly string[] Candidates = { "Segoe UI", "Arial", "Tahoma", "Verdana", "DejaVu Sans", "Liberation Sans" };

        /// <summary>The font to use, or null when the built-in font is the only option.</summary>
        public static Font? Get()
        {
            if (resolved) return cached;
            resolved = true;

            var shipped = Resources.Load<Font>("Fonts/ui");
            if (shipped != null && HasCyrillic(shipped))
            {
                cached = shipped;
                return cached;
            }

            foreach (var name in Candidates)
            {
                var font = Font.CreateDynamicFontFromOSFont(name, 48);
                if (font == null || !HasCyrillic(font)) continue;
                cached = font;
                return cached;
            }

            cached = Font.CreateDynamicFontFromOSFont(Candidates, 48);
            return cached;
        }

        /// <summary>Applies the interface font to a text mesh, keeping the built-in one as a fallback.</summary>
        public static void Assign(TextMesh mesh)
        {
            var font = Get();
            if (font == null || mesh == null) return;
            mesh.font = font;
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer != null && font.material != null) renderer.sharedMaterial = font.material;
        }

        /// <summary>True when the font can draw Russian text.</summary>
        public static bool HasCyrillic(Font font)
        {
            if (font == null) return false;
            return font.HasCharacter('А') && font.HasCharacter('я') && font.HasCharacter('Ё');
        }

        /// <summary>Forces a fresh lookup; used by tests that swap the font.</summary>
        public static void Reset()
        {
            cached = null;
            resolved = false;
        }
    }
}
