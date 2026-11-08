using System.Collections.Generic;
using Mahjong.Controls;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The kinds of flat surface the interface is built from. A panel never picks a colour: it picks a
    /// skin, and the skin is a pair of palette roles — a fill and a hairline border — baked for the
    /// palette that is in effect.
    /// </summary>
    public enum UiSkin
    {
        /// <summary>A large sheet under the whole screen: surface with a gold hairline.</summary>
        Panel = 0,

        /// <summary>A sheet without the gold line, for cards that stand inside a panel.</summary>
        Card = 1,

        /// <summary>One row of a list, at rest.</summary>
        Row = 2,

        /// <summary>The row under the cursor: the fitted colour with a gold line.</summary>
        RowSelected = 3,

        /// <summary>A row for something that is not built yet: the surface, quieter.</summary>
        RowMuted = 4,

        /// <summary>An on-screen button at rest.</summary>
        Button = 5,

        /// <summary>An on-screen button under the pointer.</summary>
        ButtonHover = 6,

        /// <summary>An on-screen button that cannot be pressed right now.</summary>
        ButtonDisabled = 7,

        /// <summary>A solid accent bar: a rule under a title, a progress line.</summary>
        Accent = 8,

        /// <summary>Dimming of the table behind a modal screen.</summary>
        Scrim = 9,

        /// <summary>A soft drop shadow; the only skin that is not a palette colour.</summary>
        Shadow = 10,

        /// <summary>A filled disc: the wind tokens and the round badges of the reference.</summary>
        Circle = 11,

        /// <summary>A thin gold ring, drawn over a disc: the rim of a token, the track of a timer.</summary>
        Ring = 12,

        /// <summary>
        /// An empty rounded outline: the gold rim of a plate that is in play. Unlike <see cref="Ring"/>
        /// it keeps the corners of a rectangle, so a wide plate is outlined, not turned into an ellipse.
        /// </summary>
        Outline = 13
    }

    /// <summary>
    /// The baked surfaces of the interface: one small texture per skin and palette, drawn once and
    /// reused by every panel.
    ///
    /// A rounded rectangle cannot be drawn by a stretched quad — a 25:1 row would turn its corners into
    /// ellipses — so each skin is a 9-slice texture: the corners keep their own pixels, the middle
    /// stretches. That is what makes a row, a button and a panel read as the same shape at three sizes.
    /// </summary>
    public static class UiSkins
    {
        /// <summary>Side of a baked skin texture.</summary>
        public const int TextureSize = 64;

        /// <summary>How much of the texture a corner owns; the rest stretches.</summary>
        public const int Margin = 22;

        /// <summary>Corner radius of a skin, in texture pixels (a length, see <see cref="Corner"/>).</summary>
        private const float Radius = 18f;

        /// <summary>Thickness of the hairline border, in texture pixels.</summary>
        private const float Border = 2f;

        /// <summary>Width of the soft edge of a shadow, in texture pixels.</summary>
        private const float Feather = 7f;

        /// <summary>
        /// How big a corner is on screen, in world units before the interface size is applied. It stays
        /// the same whatever the plate is: corner radius is a property of the look, not of the panel.
        /// </summary>
        public static float Corner => 0.0075f * UiStyle.Profile.Corner;

        private static readonly Dictionary<UiSkin, Texture2D> textures = new();
        private static readonly Dictionary<UiSkin, Material> materials = new();

        /// <summary>Throws the baked skins away; called when the palette or the size changes.</summary>
        public static void Forget()
        {
            foreach (var texture in textures.Values)
                if (texture != null) Object.Destroy(texture);
            foreach (var material in materials.Values)
                if (material != null) Object.Destroy(material);

            textures.Clear();
            materials.Clear();
        }

        /// <summary>The baked texture of a skin in the palette that is in effect.</summary>
        public static Texture2D Texture(UiSkin skin)
        {
            if (textures.TryGetValue(skin, out var cached) && cached != null) return cached;
            var texture = Bake(skin);
            textures[skin] = texture;
            return texture;
        }

        /// <summary>
        /// The material of a skin. Materials are shared between plates of the same skin: a screen of
        /// twenty rows is twenty meshes over one texture, not twenty copies of it.
        /// </summary>
        public static Material Material(UiSkin skin)
        {
            if (materials.TryGetValue(skin, out var cached) && cached != null) return cached;

            var shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { mainTexture = Texture(skin) };
            materials[skin] = material;
            return material;
        }

        /// <summary>The fill and the border of a skin, in the palette that is in effect.</summary>
        internal static (Color Fill, Color Border) Colours(UiSkin skin) => skin switch
        {
            UiSkin.Panel => (UiStyle.Of(UiRole.Surface, 0.96f), UiStyle.Of(UiRole.Accent, 0.28f)),
            UiSkin.Card => (UiStyle.Of(UiRole.Surface), UiStyle.Of(UiRole.Border, 0.55f)),
            UiSkin.Row => (UiStyle.Of(UiRole.SurfaceHigh), UiStyle.Of(UiRole.Border, 0.30f)),
            UiSkin.RowSelected => (UiStyle.Of(UiRole.Primary), UiStyle.Of(UiRole.Accent, 0.85f)),
            UiSkin.RowMuted => (UiStyle.Of(UiRole.Surface, 0.75f), UiStyle.Of(UiRole.Border, 0.22f)),
            UiSkin.Button => (UiStyle.Of(UiRole.SurfaceHigh, 0.97f), UiStyle.Of(UiRole.Border, 0.85f)),
            UiSkin.ButtonHover => (UiStyle.Of(UiRole.Primary), UiStyle.Of(UiRole.Accent, 0.90f)),
            UiSkin.ButtonDisabled => (UiStyle.Sink(UiStyle.Of(UiRole.SurfaceHigh), 0.45f),
                UiStyle.Of(UiRole.Border, 0.25f)),
            UiSkin.Accent => (UiStyle.Of(UiRole.Accent), Color.clear),
            UiSkin.Scrim => (UiStyle.Of(UiRole.Background, 0.55f), Color.clear),
            UiSkin.Circle => (UiStyle.Of(UiRole.SurfaceHigh), UiStyle.Of(UiRole.Border, 0.90f)),
            UiSkin.Ring => (UiStyle.Of(UiRole.Accent), Color.clear),
            UiSkin.Outline => (Color.clear, UiStyle.Of(UiRole.Accent, 0.95f)),
            _ => (new Color(0f, 0f, 0f, 0.55f * UiStyle.Profile.Shadow), Color.clear)
        };

        /// <summary>
        /// Draws one skin: a rounded rectangle of the fill, a hairline of the border along its edge and,
        /// for the shadow, a soft edge instead of a line. The edge is antialiased over one pixel, so a
        /// corner reads as a curve and not as a staircase.
        /// </summary>
        /// <summary>True when the skin is a disc: it keeps its shape instead of being sliced.</summary>
        public static bool IsRadial(UiSkin skin) => skin is UiSkin.Circle or UiSkin.Ring;

        private static Texture2D Bake(UiSkin skin)
        {
            var (fill, border) = Colours(skin);
            var soft = skin == UiSkin.Shadow;

            var size = TextureSize;

            // A disc and a ring are the same distance field without the rounding: the corner radius of
            // the rectangle becomes the radius of the circle, so the wind tokens read as coins.
            if (IsRadial(skin))
            {
                var disc = new Color32[size * size];
                var centre = size * 0.5f;
                var radius = centre - 0.5f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x + 0.5f - centre;
                        var dy = y + 0.5f - centre;
                        var distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                        Color colour;
                        if (skin == UiSkin.Ring)
                        {
                            // A ring is the band between the rim and a slightly smaller circle.
                            var inner = -Mathf.Max(2.5f, radius * 0.16f);
                            var band = Mathf.Min(Mathf.Clamp01(0.5f - distance), Mathf.Clamp01(distance - inner + 0.5f));
                            colour = fill;
                            colour.a *= band;
                        }
                        else
                        {
                            var coverage = Mathf.Clamp01(0.5f - distance);
                            colour = fill;
                            if (border.a > 0f && distance > -Border)
                                colour = Color.Lerp(fill, border, Mathf.Clamp01(distance + Border + 0.5f));
                            colour.a *= coverage;
                        }

                        disc[y * size + x] = colour;
                    }
                }

                return Upload(skin, disc);
            }
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            var box = half - 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Distance to the rounded rectangle: negative inside, positive outside, in pixels.
                    var px = Mathf.Abs(x + 0.5f - half);
                    var py = Mathf.Abs(y + 0.5f - half);
                    var qx = Mathf.Max(px - (box - Radius), 0f);
                    var qy = Mathf.Max(py - (box - Radius), 0f);
                    var distance = Mathf.Sqrt(qx * qx + qy * qy) - Radius;

                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (soft)
                        coverage = Mathf.Clamp01(0.5f - distance / (Feather * 2f));

                    var colour = fill;
                    if (border.a > 0f && !soft && distance > -Border)
                    {
                        // The hairline keeps its own alpha where the fill is translucent, and an
                        // outline is nothing but the hairline: its fill is fully transparent.
                        var edge = Mathf.Clamp01(distance + Border + 0.5f);
                        colour = Color.Lerp(fill, border, edge);
                    }

                    colour.a *= coverage;
                    pixels[y * size + x] = colour;
                }
            }

            return Upload(skin, pixels);
        }

        private static Texture2D Upload(UiSkin skin, Color32[] pixels)
        {
            var size = TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"ui-skin-{skin}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }

    /// <summary>
    /// A flat surface of the interface: a rounded plate of a given skin and size, drawn as a 9-slice
    /// mesh so its corners keep the radius the palette asks for at any width.
    ///
    /// Plates replace the bare cubes and quads the prototype used to colour by hand: a panel, a row, a
    /// button and a shadow are the same object with a different skin, which is why they line up.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class UiPlate : MonoBehaviour
    {
        private const int Grid = 4;

        private Mesh mesh = null!;
        private MeshRenderer meshRenderer = null!;
        private UiSkin skin;
        private float width;
        private float height;

        public UiSkin Skin => skin;
        public Renderer Renderer => meshRenderer;
        public float Width => width;
        public float Height => height;

        /// <summary>A plate of a skin, sized to nothing until <see cref="SetSize"/> is called.</summary>
        public static UiPlate Create(Transform parent, UiSkin skin, string name = "plate")
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localRotation = Quaternion.identity;
            var plate = host.AddComponent<UiPlate>();
            plate.SetSkin(skin);
            return plate;
        }

        private void Awake()
        {
            if (mesh != null) return;
            meshRenderer = GetComponent<MeshRenderer>();
            mesh = new Mesh { name = "ui-plate", hideFlags = HideFlags.HideAndDontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>Repaints the plate in another skin of the current palette.</summary>
        public void SetSkin(UiSkin next)
        {
            skin = next;
            if (meshRenderer == null) Awake();
            meshRenderer.sharedMaterial = UiSkins.Material(skin);
        }

        /// <summary>Sizes the plate; the corners keep their radius, the middle stretches.</summary>
        public void SetSize(float newWidth, float newHeight)
        {
            if (Mathf.Approximately(width, newWidth) && Mathf.Approximately(height, newHeight)) return;
            width = newWidth;
            height = newHeight;
            Rebuild();
        }

        /// <summary>Places the plate on a camera-facing plane; the caller owns the rest of the transform.</summary>
        public void SetPosition(float x, float y, float z) =>
            transform.localPosition = new Vector3(x, y, z);

        private void Rebuild()
        {
            if (mesh == null) Awake();

            // A disc is one quad with the whole texture on it: slicing a circle would bend its rim.
            if (UiSkins.IsRadial(skin))
            {
                var quadSize = Mathf.Min(width, height);
                mesh.Clear();
                mesh.vertices = new[]
                {
                    new Vector3(-quadSize * 0.5f, -quadSize * 0.5f, 0f),
                    new Vector3(quadSize * 0.5f, -quadSize * 0.5f, 0f),
                    new Vector3(-quadSize * 0.5f, quadSize * 0.5f, 0f),
                    new Vector3(quadSize * 0.5f, quadSize * 0.5f, 0f)
                };
                mesh.uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
                };
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                mesh.RecalculateBounds();
                return;
            }

            var corner = Mathf.Min(UiStyle.S(UiSkins.Corner), Mathf.Min(width, height) * 0.5f);
            var xs = new[] { -width * 0.5f, -width * 0.5f + corner, width * 0.5f - corner, width * 0.5f };
            var ys = new[] { -height * 0.5f, -height * 0.5f + corner, height * 0.5f - corner, height * 0.5f };
            var margin = UiSkins.Margin / (float)UiSkins.TextureSize;
            var us = new[] { 0f, margin, 1f - margin, 1f };

            var vertices = new Vector3[Grid * Grid];
            var uvs = new Vector2[Grid * Grid];
            for (var y = 0; y < Grid; y++)
            {
                for (var x = 0; x < Grid; x++)
                {
                    vertices[y * Grid + x] = new Vector3(xs[x], ys[y], 0f);
                    uvs[y * Grid + x] = new Vector2(us[x], us[y]);
                }
            }

            var triangles = new List<int>(3 * 2 * 9);
            for (var y = 0; y < Grid - 1; y++)
            {
                for (var x = 0; x < Grid - 1; x++)
                {
                    var a = y * Grid + x;
                    var b = a + 1;
                    var c = a + Grid;
                    var d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
        }
    }
}
