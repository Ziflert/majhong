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

    /// <summary>What a click inside the book asks for: a page back, a page on, or one named page.</summary>
    public enum RulesClickKind
    {
        None = 0,
        Previous = 1,
        Next = 2,
        Page = 3
    }

    /// <summary>The answer of <see cref="RulesPanel.TryPick"/>: the kind of click and the page it names.</summary>
    public readonly struct RulesClick
    {
        public RulesClick(RulesClickKind kind, int page)
        {
            Kind = kind;
            Page = page;
        }

        public RulesClickKind Kind { get; }
        public int Page { get; }
        public bool Picked => Kind != RulesClickKind.None;

        public static RulesClick Missed => new(RulesClickKind.None, -1);
    }

    /// <summary>
    /// The rulebook inside the game: a flat panel hanging in front of the camera with one page of
    /// <see cref="RulesBook"/> at a time. The pages are the Russian text from the rules translation,
    /// so the player never has to leave the table to look a term up.
    ///
    /// The book has a table of contents as its first page (У-5): the fifteen pages of the translation
    /// and the fan table of 3.8.1, each with its page number in front. A row opens its page with a
    /// click, the two arrow plates at the bottom of the page turn it back and on, and the counter
    /// between them says where the reader is. The reader keeps his page between openings, so Home is
    /// what brings the contents back. Arrows on the page exist because a player at the table may not
    /// own a keyboard he can see — the hot keys stay as the second way.
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

        /// <summary>Index of the contents page: the first page of the book, and where Home goes.</summary>
        public const int ContentsPage = 0;

        /// <summary>The table of contents is two columns of rows, one row per page of the book.</summary>
        private const int ContentsColumns = 2;
        private const float ContentsMargin = 0.020f;
        private const float RowHeight = 0.016f;
        private const float RowStep = 0.0195f;

        private const float ArrowWidth = 0.048f;
        private const float ArrowHeight = 0.016f;
        private const float BottomLine = 0.014f;

        private static readonly Color PanelColour = new(0.06f, 0.08f, 0.10f, 0.96f);
        private static readonly Color ButtonColour = new(0.13f, 0.16f, 0.19f);
        private static readonly Color ButtonCaptionColour = new(0.84f, 0.86f, 0.83f);
        private static readonly Color CounterColour = new(0.72f, 0.78f, 0.72f);

        /// <summary>One clickable plate of the bottom line: the page back or the page on.</summary>
        private sealed class ArrowPlate
        {
            public GameObject Root = null!;
            public Renderer Face = null!;
            public TextMesh Label = null!;
            public BoxCollider Box = null!;
        }

        /// <summary>One line of the table of contents: it stands for the page it names.</summary>
        private sealed class ContentsRow
        {
            public GameObject Root = null!;
            public Renderer Face = null!;
            public TextMesh Label = null!;
            public BoxCollider Box = null!;
            public int Page;
        }

        private readonly List<RulesSection> pages = new();
        private readonly List<ContentsRow> rows = new();
        private Transform cameraTransform = null!;

        private TextMesh title = null!;
        private TextMesh body = null!;
        private TextMesh footer = null!;
        private TextMesh counter = null!;
        private Renderer background = null!;
        private ArrowPlate previous = null!;
        private ArrowPlate next = null!;

        public bool IsOpen { get; private set; }

        /// <summary>Zero-based index of the page on screen.</summary>
        public int Page { get; private set; }

        public int PageCount => pages.Count;

        /// <summary>True while the table of contents is the page on screen.</summary>
        public bool IsContents => Page == ContentsPage;

        public string CurrentTitle => Page >= 0 && Page < pages.Count ? pages[Page].Title : string.Empty;
        public string CurrentBody => Page >= 0 && Page < pages.Count ? pages[Page].Body : string.Empty;

        public TextMesh Body => body;

        /// <summary>The dark page behind the text; the PlayMode test checks that it fits the frame.</summary>
        public Renderer Backdrop => background;
        public TextMesh Title => title;
        public TextMesh Footer => footer;

        /// <summary>The "page N of M" line between the two arrow plates.</summary>
        public TextMesh Counter => counter;

        /// <summary>The plate that turns the page back; a click anywhere on it is <see cref="RulesClickKind.Previous"/>.</summary>
        public GameObject PreviousButton => previous.Root;

        /// <summary>The plate that turns the page on.</summary>
        public GameObject NextButton => next.Root;

        /// <summary>How many rows the table of contents has; one per page of the book but the contents.</summary>
        public int RowCount => rows.Count;

        /// <summary>Caption of one row of the contents, as it is drawn; the tests read it instead of the screen.</summary>
        public string RowText(int index) => index >= 0 && index < rows.Count ? rows[index].Label.text : string.Empty;

        /// <summary>False while the book is on a page: the rows are put away instead of being drawn under it.</summary>
        public bool RowsShown(int index) => index >= 0 && index < rows.Count && rows[index].Root.activeSelf;

        /// <summary>Titles of the pages, for tests and for the table of contents.</summary>
        public IReadOnlyList<string> Titles => pages.Select(page => page.Title).ToList();

        public static RulesPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("RulesPanel");
            root.transform.SetParent(cameraTransform, false);
            // Centred on the frame: a vertical offset here pushed the bottom corners of the page out
            // of the game view even when the page itself was small enough.
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<RulesPanel>();
            panel.pages.Add(ContentsSection());
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
                0.0013f, TextAnchor.LowerCenter, CounterColour);
            panel.counter = CreateText(root.transform, "counter", Vector3.zero,
                0.0015f, TextAnchor.LowerCenter, CounterColour);

            panel.previous = CreateArrow(root.transform, "previous", "◀");
            panel.next = CreateArrow(root.transform, "next", "▶");
            panel.CreateContentsRows(root.transform);
            panel.Layout();

            root.SetActive(false);
            return panel;
        }

        /// <summary>The first page of the book: what the other sixteen pages are about.</summary>
        private static RulesSection ContentsSection() => new("contents", Loc.Get("ui.rulesContents"),
            new[] { Loc.Get("ui.rulesContentsHint") }, string.Empty);

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
        /// A plate with a caption and its own box, the same mechanism the action bar and the menu use:
        /// the box is what the ray hits, the cube is what the player sees.
        /// </summary>
        private static ArrowPlate CreateArrow(Transform parent, string name, string caption)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(ArrowWidth, ArrowHeight, ActionButton.Depth);
            Object.Destroy(faceObject.GetComponent<Collider>());

            var face = faceObject.GetComponent<Renderer>();
            face.material = new Material(Shader.Find("Unlit/Color"));
            face.material.color = ButtonColour;

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(ArrowWidth, ArrowHeight, ActionButton.Depth);

            var label = CreateText(root.transform, "caption", Vector3.zero, 0.0013f,
                TextAnchor.MiddleCenter, ButtonCaptionColour);
            label.text = caption;
            label.transform.localPosition = new Vector3(0f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);

            return new ArrowPlate { Root = root, Face = face, Label = label, Box = box };
        }

        /// <summary>One clickable line per page of the book, the contents page itself left out.</summary>
        private void CreateContentsRows(Transform parent)
        {
            for (var page = ContentsPage + 1; page < pages.Count; page++)
            {
                var root = new GameObject($"contents-{page}");
                root.transform.SetParent(parent, false);

                var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                faceObject.name = "face";
                faceObject.transform.SetParent(root.transform, false);
                faceObject.transform.localScale = new Vector3(0.2f, RowHeight, ActionButton.Depth);
                Object.Destroy(faceObject.GetComponent<Collider>());

                var face = faceObject.GetComponent<Renderer>();
                face.material = new Material(Shader.Find("Unlit/Color"));
                face.material.color = ButtonColour;

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.2f, RowHeight, ActionButton.Depth);

                var label = CreateText(root.transform, "label", Vector3.zero, 0.0012f,
                    TextAnchor.MiddleLeft, ButtonCaptionColour);
                label.transform.localPosition =
                    new Vector3(-0.1f + 0.004f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
                // The number is the page the row opens, the same number the counter shows.
                label.text = $"{page + 1}. {pages[page].Title}";

                rows.Add(new ContentsRow { Root = root, Face = face, Label = label, Box = box, Page = page });
            }
        }

        /// <summary>
        /// Fits the page to the frame and lays its texts out again. Called on creation, on every
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
            body.transform.localPosition = new Vector3(-width * 0.5f + 0.02f, height * 0.5f - 0.044f, -0.0009f);
            footer.transform.localPosition = new Vector3(0f, -height * 0.5f + 0.032f, -0.0009f);
            counter.transform.localPosition = new Vector3(0f, -height * 0.5f + BottomLine, -0.0009f);

            var arrowX = width * 0.5f - ArrowWidth * 0.5f - 0.012f;
            PlaceArrow(previous, -arrowX, -height * 0.5f + BottomLine);
            PlaceArrow(next, arrowX, -height * 0.5f + BottomLine);

            LayoutContents(width, height);
        }

        private static void PlaceArrow(ArrowPlate plate, float x, float y)
        {
            plate.Box.transform.localPosition = new Vector3(x, y, 0f);
            plate.Face.transform.localScale = new Vector3(ArrowWidth, ArrowHeight, ActionButton.Depth);
            plate.Label.transform.localPosition = new Vector3(0f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
        }

        /// <summary>
        /// The contents page as two columns of rows, so seventeen entries fit a page that is only
        /// half a metre in front of the camera. The row boxes are what a click picks.
        /// </summary>
        private void LayoutContents(float width, float height)
        {
            if (rows.Count == 0) return;

            var columnWidth = (width - ContentsMargin * 2f - 0.010f) / ContentsColumns;
            var perColumn = (rows.Count + ContentsColumns - 1) / ContentsColumns;
            var top = height * 0.5f - 0.078f;

            for (var index = 0; index < rows.Count; index++)
            {
                var column = index / perColumn;
                var line = index % perColumn;
                var centreX = -width * 0.5f + ContentsMargin + columnWidth * (column + 0.5f)
                              + (column > 0 ? 0.010f : 0f);
                var y = top - line * RowStep;

                var row = rows[index];
                row.Box.transform.localPosition = new Vector3(centreX, y, 0f);
                row.Box.size = new Vector3(columnWidth, RowHeight, ActionButton.Depth);
                row.Face.transform.localScale = new Vector3(columnWidth, RowHeight, ActionButton.Depth);
                row.Label.transform.localPosition =
                    new Vector3(-columnWidth * 0.5f + 0.004f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
            }
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

        /// <summary>Opens one page by its zero-based index; a click on a row of the contents uses this.</summary>
        public void GoToPage(int page)
        {
            if (pages.Count == 0) return;
            Page = Mathf.Clamp(page, 0, pages.Count - 1);
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
            // The reference of a page is the rule it explains; the contents page explains nothing, so
            // it carries the keys instead of a number nobody could look up.
            footer.text = page.Reference.Length > 0
                ? Loc.Format("ui.rulesLine", page.Reference)
                : Loc.Get("ui.rulesHint");
            counter.text = Loc.Format("ui.rulesPage", Page + 1, pages.Count);

            // The rows of the contents are hidden, not destroyed: the same book has to come back to
            // them every time the reader asks for the table of contents.
            foreach (var row in rows) row.Root.SetActive(IsContents);
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
        /// Home and End jump to the two ends of the book, so the contents is one key away from any page.
        /// </summary>
        public RulesKey HandleKeys()
        {
            if (!IsOpen) return RulesKey.None;

            if (Input.GetKeyDown(KeyCode.Home))
            {
                GoToPage(ContentsPage);
                return RulesKey.Turned;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                GoToPage(pages.Count - 1);
                return RulesKey.Turned;
            }

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

        /// <summary>
        /// Turns a ray from the camera into what the reader asked for: the plate back, the plate on,
        /// or the page named by the row of the contents under the cursor. A miss is a miss — the book
        /// stays open, because a click that touches nothing must not close it.
        /// </summary>
        public RulesClick TryPick(Ray ray)
        {
            if (!IsOpen) return RulesClick.Missed;

            var hits = Physics.RaycastAll(ray, 5f);
            var nearest = float.MaxValue;
            var answer = RulesClick.Missed;

            foreach (var hit in hits)
            {
                if (hit.distance >= nearest) continue;

                if (hit.collider == previous.Box)
                {
                    nearest = hit.distance;
                    answer = new RulesClick(RulesClickKind.Previous, Page);
                    continue;
                }

                if (hit.collider == next.Box)
                {
                    nearest = hit.distance;
                    answer = new RulesClick(RulesClickKind.Next, Page);
                    continue;
                }

                var row = rows.FirstOrDefault(candidate => candidate.Box == hit.collider);
                if (row != null && row.Root.activeSelf)
                {
                    nearest = hit.distance;
                    answer = new RulesClick(RulesClickKind.Page, row.Page);
                }
            }

            return answer;
        }

        /// <summary>World position of a row of the contents; a test aims its ray at it.</summary>
        public Vector3 RowPosition(int index) =>
            index >= 0 && index < rows.Count ? rows[index].Box.transform.position : Vector3.zero;

        /// <summary>World position of one of the two plates of the bottom line, for a test's ray.</summary>
        public Vector3 ButtonPosition(bool forward) => forward ? next.Box.transform.position : previous.Box.transform.position;
    }
}
