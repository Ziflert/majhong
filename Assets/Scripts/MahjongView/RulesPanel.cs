using System.Collections.Generic;
using System.Linq;
using Mahjong.Knowledge;
using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>What a key did to the rulebook: nothing, one page turned, or the book should close.</summary>
    public enum RulesKey
    {
        None = 0,
        Turned = 1,
        Close = 2
    }

    /// <summary>
    /// The rulebook inside the game: a flat panel hanging in front of the camera with one page of
    /// <see cref="RulesBook"/> at a time. The pages are the Russian text from the rules translation,
    /// so the player never has to leave the table to look a term up.
    ///
    /// The last page is the fan catalogue of 3.8.1, generated from <see cref="Mahjong.Rules.Fans"/>.
    /// </summary>
    public sealed class RulesPanel : MonoBehaviour
    {
        /// <summary>Distance in front of the camera; the action bar uses the same plane.</summary>
        private const float PanelZ = 0.5f;

        /// <summary>
        /// Largest panel that fits the frame half a metre out. The frame there is about 0.61 x 0.31 in
        /// a wide game view but only 0.41 x 0.31 in a 4:3 window, so the size is measured from the
        /// camera instead of being fixed: a fixed 0.62 x 0.36 panel hung off the screen on all four
        /// sides. The PlayMode test checks that every corner of the page lands inside the frame.
        /// </summary>
        private const float MaxPanelWidth = 0.56f;
        private const float MaxPanelHeight = 0.28f;

        /// <summary>Share of the frame the page is allowed to take, so it never touches the edge.</summary>
        private const float FrameShare = 0.92f;

        /// <summary>Width of one body line in characters; the pages are authored in long lines.</summary>
        private const int BodyWidth = 70;

        private static readonly Color PanelColour = new(0.06f, 0.08f, 0.10f, 0.96f);

        private readonly List<RulesSection> pages = new();
        private Transform cameraTransform = null!;

        private TextMesh title = null!;
        private TextMesh body = null!;
        private TextMesh footer = null!;
        private Renderer background = null!;

        public bool IsOpen { get; private set; }

        /// <summary>Zero-based index of the page on screen.</summary>
        public int Page { get; private set; }

        public int PageCount => pages.Count;

        public string CurrentTitle => Page >= 0 && Page < pages.Count ? pages[Page].Title : string.Empty;
        public string CurrentBody => Page >= 0 && Page < pages.Count ? pages[Page].Body : string.Empty;

        public TextMesh Body => body;

        /// <summary>The dark page behind the text; the PlayMode test checks that it fits the frame.</summary>
        public Renderer Backdrop => background;
        public TextMesh Title => title;
        public TextMesh Footer => footer;

        public static RulesPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("RulesPanel");
            root.transform.SetParent(cameraTransform, false);
            // Centred on the frame: a vertical offset here pushed the bottom corners of the page out
            // of the game view even when the page itself was small enough.
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<RulesPanel>();
            panel.pages.AddRange(RulesBook.Sections);
            panel.pages.Add(RulesBook.FanSection());

            panel.cameraTransform = cameraTransform;
            panel.background = CreateBackground(root.transform);
            panel.title = CreateText(root.transform, "title", Vector3.zero,
                0.0022f, TextAnchor.UpperCenter, new Color(0.98f, 0.94f, 0.80f));
            // UpperLeft anchors the text at its own top-left corner, so the anchor point is the left
            // margin of the page and not its middle: a body at x = 0 started in the centre and ran
            // off the right edge of the screen.
            panel.body = CreateText(root.transform, "body", Vector3.zero,
                0.0013f, TextAnchor.UpperLeft, new Color(0.92f, 0.93f, 0.95f));
            panel.footer = CreateText(root.transform, "footer", Vector3.zero,
                0.0015f, TextAnchor.LowerCenter, new Color(0.72f, 0.78f, 0.72f));
            panel.Layout();

            root.SetActive(false);
            return panel;
        }

        private static Renderer CreateBackground(Transform parent)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "backdrop";
            quad.transform.SetParent(parent, false);
            // The quad mesh faces -Z and the camera looks along +Z, so at identity the panel is
            // paper-thin the right way round. Turning it round (as the first version did) showed the
            // culled back face, which is why the panel had no backdrop at all.
            quad.transform.localRotation = Quaternion.identity;
            Object.Destroy(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = PanelColour;
            return renderer;
        }

        private static TextMesh CreateText(Transform parent, string name, Vector3 position, float size,
            TextAnchor anchor, Color colour)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = position;
            host.transform.localRotation = Quaternion.identity;

            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = colour;
            mesh.richText = false;
            UiFont.Assign(mesh);
            return mesh;
        }

        /// <summary>
        /// Fits the page to the frame and lays its three texts out again. Called on creation, on every
        /// open and on every frame the page is visible, so a resized game view relayouts the book
        /// instead of cropping it.
        /// </summary>
        public void Layout()
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            var halfHeight = camera != null
                ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * PanelZ
                : MaxPanelHeight * 0.5f;
            var halfWidth = camera != null ? halfHeight * camera.aspect : MaxPanelWidth * 0.5f;

            var width = Mathf.Min(MaxPanelWidth, halfWidth * 2f * FrameShare);
            var height = Mathf.Min(MaxPanelHeight, halfHeight * 2f * FrameShare);

            background.transform.localScale = new Vector3(width, height, 1f);
            title.transform.localPosition = new Vector3(0f, height * 0.5f - 0.020f, -0.0009f);
            body.transform.localPosition = new Vector3(-width * 0.5f + 0.02f, height * 0.5f - 0.048f, -0.0009f);
            footer.transform.localPosition = new Vector3(0f, -height * 0.5f + 0.014f, -0.0009f);
        }

        /// <summary>The game view can be resized at any moment; the page follows it.</summary>
        private void Update()
        {
            if (IsOpen) Layout();
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            IsOpen = open;
            gameObject.SetActive(open);
            if (open) Refresh();
        }

        public void Close() => SetOpen(false);

        /// <summary>Turns a page; wraps around so the arrows never get stuck at an end.</summary>
        public void Turn(int delta)
        {
            if (pages.Count == 0) return;
            Page = ((Page + delta) % pages.Count + pages.Count) % pages.Count;
            Refresh();
        }

        public void GoTo(string id)
        {
            var index = pages.FindIndex(page => page.Id == id);
            if (index < 0) return;
            Page = index;
            Refresh();
        }

        private void Refresh()
        {
            if (pages.Count == 0) return;

            var page = pages[Page];
            title.text = page.Title;
            body.text = Wrap(page.Body);
            footer.text = Loc.Format("ui.rulesLine", page.Reference, Page + 1, pages.Count);
        }

        /// <summary>
        /// The book is authored in lines of roughly a hundred characters, which is already narrower
        /// than the panel; this only shortens a hand-written line that ran long.
        /// </summary>
        private static string Wrap(string text)
        {
            var lines = new List<string>();
            foreach (var source in text.Split('\n'))
                lines.AddRange(Split(source, BodyWidth));
            return string.Join("\n", lines);
        }

        private static IEnumerable<string> Split(string line, int width)
        {
            if (line.Length <= width)
            {
                yield return line;
                yield break;
            }

            var rest = line;
            while (rest.Length > width)
            {
                var cut = rest.LastIndexOf(' ', width, width);
                if (cut <= 0) cut = width;
                yield return rest.Substring(0, cut);
                rest = rest.Substring(cut).TrimStart();
            }

            if (rest.Length > 0) yield return rest;
        }

        /// <summary>
        /// Reads the panel keys. "Used" and "close" are different answers: an arrow turns a page, and
        /// the panel must stay open afterwards — the old single boolean read every key as "close",
        /// so a page turned with an arrow also shut the book and the next R opened a later page (У-5).
        /// </summary>
        public RulesKey HandleKeys()
        {
            if (!IsOpen) return RulesKey.None;

            // Both pairs of arrows turn pages: a player who reads a book reaches for either one.
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.A))
            {
                Turn(-1);
                return RulesKey.Turned;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.D))
            {
                Turn(1);
                return RulesKey.Turned;
            }

            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.R)
                ? RulesKey.Close
                : RulesKey.None;
        }

        /// <summary>Titles of the pages, for tests and for a table of contents later.</summary>
        public IReadOnlyList<string> Titles => pages.Select(page => page.Title).ToList();
    }
}
