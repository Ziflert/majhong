using System;
using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>What a click on the main interface asks the table to do.</summary>
    public enum HudCommand
    {
        None = 0,
        Pause = 1,
        Rules = 2,
        Review = 3,
        Save = 4,
        Hints = 5,
        Combos = 6
    }

    /// <summary>
    /// The main game interface, built to the approved visual reference
    /// (<c>examples/Эскизы UI Маджонг.html</c>, frame 1 for PC and frame 5 for a phone in landscape).
    ///
    /// The reference is a 1280 × 720 sheet, so the numbers below are the reference's own design pixels
    /// and <see cref="units"/> converts them into camera space. One implementation therefore stays
    /// honest against the mock-up: a 38 px badge is 38 design pixels at any resolution and any aspect,
    /// because the sheet is scaled by the height of the frame, the way CSS scales it.
    ///
    /// What the reference draws, and what this class draws with it:
    ///   • three quiet badges on the top bar — the hand with the rules profile, the wall, the clock;
    ///   • the dice toast under the clock with the two lines of the first throw, dismissible by a click;
    ///   • four player plates: the seat opposite on top, the two wings at the sides, the human at the
    ///     bottom-left in gold with the dealer badge and the turn clock;
    ///   • a single hint bar above the hand, where the eye already is;
    ///   • the two bottom panels of the reference: the game keys on the left, the two learn buttons on
    ///     the right — plates with a caption, a glyph and the hot key in the corner.
    ///
    /// The class reads state only. Every command still belongs to <see cref="MahjongGame"/>, which is
    /// what <see cref="TryPick"/> reports.
    /// </summary>
    public sealed class ReferenceHud : MonoBehaviour
    {
        /// <summary>Plane the interface hangs on, half a metre in front of the camera.</summary>
        private const float Depth = 0.5f;

        /// <summary>Height of the design sheet the numbers below are measured on.</summary>
        private const float DesignHeight = 720f;

        // ------------------------------------------------------------------ design numbers (CSS px)

        private const float MarginX = 24f;
        private const float MarginTop = 16f;
        private const float BadgeHeight = 38f;
        private const float BadgePadding = 16f;
        private const float BadgeGap = 8f;

        // Fixed top-row slots. Text is measured for typography, but never determines a module's
        // position or plate width: long localized labels must not push the next badge during orbit/zoom.
        private const float TopHandWidth = 350f;
        private const float TopRulesWidth = 270f;
        private const float TopWallWidth = 190f;
        private const float TopRulesLeft = 370f;
        private const float TopWallLeft = 650f;

        private const float PlateWidth = 200f;
        private const float PlateHeight = 52f;
        private const float SelfPlateWidth = 236f;
        private const float SelfPlateHeight = 56f;
        private const float CircleSize = 32f;
        private const float SelfCircleSize = 36f;
        private const float DealerSize = 24f;

        private const float TopPlateTop = 62f;
        private const float SidePlateTop = 300f;
        private const float SelfPlateBottom = 92f;

        private const float HintBottom = 86f;
        private const float HintHeight = 44f;
        private const float HintMaxWidth = 500f;

        private const float ButtonBottom = 24f;
        private const float ButtonHeight = 56f;
        private const float ButtonGap = 10f;
        // Keep every actionable chip at the desktop minimum from the UI brief (120 × 56 px).
        // The former 78 px key chips made captions and hot-key badges collide at 1280 px.
        private const float KeyButtonWidth = 120f;
        private const float HintsButtonWidth = 132f;
        private const float CombosButtonWidth = 144f;

        // The reference keeps the player's hand pinned to the bottom of the screen. These are real
        // TileObjects duplicated from the table state, but parented to the camera HUD plane.
        private const float HandTileWidth = 29f;
        private const float HandTileHeight = 39f;
        private const float HandTileGap = 2f;
        private const float HandBottom = 18f;
        private const float HandTileScale = 0.54f;

        private const float ToastTop = 64f;
        private const float ToastWidth = 310f;
        private const float ToastHeight = 58f;

        /// <summary>World height of a glyph per one unit of <see cref="TextMesh.characterSize"/>.</summary>
        private const float GlyphPerSize = 5f;
        private const float IconCaptionScale = 0.72f;
        private const float IconKeyScale = 0.66f;

        /// <summary>Advance of one glyph in <see cref="TextMesh.characterSize"/> units; a measuring stick.</summary>
        private const float GlyphAdvance = 4.2f;

        private sealed class Caption
        {
            public TextMesh Mesh = null!;
            public float FontPx;
            public float Width;
        }

        private sealed class Chip
        {
            public UiPlate Plate = null!;
            public TextMesh Label = null!;
            public TextMesh Key = null!;
            public BoxCollider Box = null!;
            public HudCommand Command;
            public float WidthPx;
            public bool Enabled = true;

            /// <summary>The glyph of the button: plates and the offset each one takes inside the chip.</summary>
            public readonly List<(UiPlate Plate, Vector4 Box)> Icon = new();
        }

        private sealed class SeatPlate
        {
            public UiPlate Plate = null!;
            public UiPlate Rim = null!;
            public UiPlate Circle = null!;
            public UiPlate Dealer = null!;
            public TextMesh Wind = null!;
            public TextMesh Name = null!;
            public TextMesh Score = null!;
            public TextMesh DealerLetter = null!;
            public TextMesh Seconds = null!;
        }

        /// <summary>World units per design pixel of the reference sheet.</summary>
        private float units = 0.000425f;

        private Caption hand = null!;
        private UiPlate handPlate = null!;

        private Caption rules = null!;
        private UiPlate rulesPlate = null!;
        private UiPlate rulesDot = null!;
        private TextMesh rulesMark = null!;

        private Caption wall = null!;
        private Caption wallCount = null!;
        private UiPlate wallPlate = null!;
        private readonly List<UiPlate> wallIcon = new();

        private Caption clock = null!;
        private Caption clockTotal = null!;
        private UiPlate clockPlate = null!;
        private UiPlate clockDial = null!;
        private UiPlate clockHand = null!;

        private UiPlate toastPlate = null!;
        private Caption toastTitle = null!;
        private Caption toastDetail = null!;
        private TextMesh toastClose = null!;
        private BoxCollider toastBox = null!;
        private float toastUntil;

        private Caption potential = null!;
        private Caption hint = null!;
        private UiPlate hintPlate = null!;

        private UiPlate guidePlate = null!;
        private Caption guide = null!;
        private bool guideVisible;

        private readonly SeatPlate[] seats = new SeatPlate[4];
        private readonly List<Chip> chips = new();
        private readonly List<UiPlate> plates = new();
        private readonly List<TileObject> handTiles = new();
        private string handFingerprint = string.Empty;

        private int styleVersion = -1;
        private GameState? lastState;
        private bool hintVisible = true;
        private HudCommand hovered = HudCommand.None;

        // ------------------------------------------------------------------------ public read-outs

        /// <summary>The hand badge: round and hand number.</summary>
        public TextMesh Header => hand.Mesh;

        /// <summary>The wall caption, without its highlighted count.</summary>
        public TextMesh WallCounter => wall.Mesh;

        /// <summary>Tiles left in the wall; the reference draws this number in gold.</summary>
        public TextMesh WallCount => wallCount.Mesh;

        /// <summary>The elapsed time of the session clock.</summary>
        public TextMesh ClockText => clock.Mesh;

        /// <summary>The single-line prompt above the hand.</summary>
        public TextMesh Hint => hint.Mesh;

        /// <summary>The dice toast; empty while it is closed.</summary>
        public TextMesh Notice => toastTitle.Mesh;

        /// <summary>Every plate of this interface; a test reads their skins instead of colours.</summary>
        public IReadOnlyList<UiPlate> Plates => plates;

        /// <summary>The buttons of the two bottom panels, in the order they are drawn.</summary>
        public IReadOnlyList<UiPlate> Buttons
        {
            get
            {
                var list = new List<UiPlate>(chips.Count);
                foreach (var chip in chips) list.Add(chip.Plate);
                return list;
            }
        }

        public TextMesh PlayerName(int seat) => seats[seat].Name;
        public TextMesh PlayerScore(int seat) => seats[seat].Score;

        /// <summary>Whether the hint bar is drawn; the "Hints" button turns it off.</summary>
        public bool HintsVisible => hintVisible;

        /// <summary>Whether the little guide to chow, pung and kong is open.</summary>
        public bool GuideVisible => guideVisible;

        /// <summary>Turns the guide on and off; the "Melds" button of the bottom panel does it.</summary>
        public void ToggleGuide() => guideVisible = !guideVisible;

        public bool IsOpen => gameObject.activeInHierarchy;

        /// <summary>Returns the face of a pinned hand tile under the pointer, if any.</summary>
        public bool TryPickPinnedTile(Ray ray, out TileType type)
        {
            type = default;
            foreach (var hit in Physics.RaycastAll(ray, Depth * 2f))
            {
                var tile = hit.collider == null ? null : hit.collider.GetComponentInParent<TileObject>();
                if (tile == null || !handTiles.Contains(tile) || !tile.IsFaceUp) continue;
                type = tile.Type;
                return true;
            }
            return false;
        }

        /// <summary>The caption of a button, as it is drawn; a test reads it instead of the screen.</summary>
        public string ButtonText(HudCommand command)
        {
            foreach (var chip in chips)
                if (chip.Command == command) return chip.Label.text;
            return string.Empty;
        }

        /// <summary>The hot key drawn in the corner of a button.</summary>
        public string ButtonKey(HudCommand command)
        {
            foreach (var chip in chips)
                if (chip.Command == command) return chip.Key.text;
            return string.Empty;
        }

        // --------------------------------------------------------------------------- construction

        public static ReferenceHud Create(Transform camera)
        {
            var root = new GameObject("ReferenceHud");
            root.transform.SetParent(camera, false);
            root.transform.localPosition = new Vector3(0f, 0f, Depth);
            root.transform.localRotation = Quaternion.identity;

            var hud = root.AddComponent<ReferenceHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            handPlate = AddPlate("hand-badge", UiSkin.Card);
            hand = CreateText("hand-badge-text", 17f, TextAlignment.Left, UiRole.TextMain);

            rulesPlate = AddPlate("rules-badge", UiSkin.Card);
            rulesDot = AddPlate("rules-dot", UiSkin.Ring);
            rulesMark = CreateText("rules-mark", 11f, TextAlignment.Center, UiRole.Accent).Mesh;
            rulesMark.text = "i";
            rules = CreateText("rules-badge-text", 15f, TextAlignment.Left, UiRole.TextMuted);

            wallPlate = AddPlate("wall-badge", UiSkin.Card);
            wall = CreateText("wall-badge-text", 17f, TextAlignment.Left, UiRole.TextMain);
            wallCount = CreateText("wall-badge-count", 17f, TextAlignment.Left, UiRole.Accent);
            for (var row = 0; row < 3; row++) wallIcon.Add(AddPlate($"wall-icon-{row}", UiSkin.Row));

            clockPlate = AddPlate("clock-badge", UiSkin.Card);
            clockDial = AddPlate("clock-dial", UiSkin.Ring);
            clockHand = AddPlate("clock-hand", UiSkin.Accent);
            clock = CreateText("clock-badge-text", 17f, TextAlignment.Left, UiRole.TextMain);
            clockTotal = CreateText("clock-badge-total", 15f, TextAlignment.Left, UiRole.TextMuted);

            // The toast: two lines of the first throw, and a cross that closes it.
            toastPlate = AddPlate("toast", UiSkin.Card);
            toastTitle = CreateText("toast-title", 15f, TextAlignment.Left, UiRole.TextMain);
            toastDetail = CreateText("toast-detail", 13f, TextAlignment.Left, UiRole.TextMuted);
            toastClose = CreateText("toast-close", 15f, TextAlignment.Center, UiRole.TextMuted).Mesh;
            toastClose.text = "×";
            toastBox = toastPlate.gameObject.AddComponent<BoxCollider>();
            toastBox.size = Vector3.one;

            potential = CreateText("potential", 12f, TextAlignment.Left, UiRole.Accent);
            potential.Mesh.gameObject.SetActive(false);

            for (var seat = 0; seat < seats.Length; seat++)
            {
                seats[seat] = new SeatPlate
                {
                    Plate = AddPlate($"seat-{seat}", UiSkin.Card),
                    Rim = AddPlate($"seat-{seat}-rim", UiSkin.Outline),
                    Circle = AddPlate($"seat-{seat}-wind", UiSkin.Circle),
                    Dealer = AddPlate($"seat-{seat}-dealer", UiSkin.Circle),
                    Wind = CreateText($"seat-{seat}-wind-text", 16f, TextAlignment.Center, UiRole.Accent).Mesh,
                    Name = CreateText($"seat-{seat}-name", 15f, TextAlignment.Left, UiRole.TextMain).Mesh,
                    Score = CreateText($"seat-{seat}-score", 13f, TextAlignment.Left, UiRole.TextMuted).Mesh,
                    DealerLetter = CreateText($"seat-{seat}-dealer-text", 12f, TextAlignment.Center,
                        UiRole.Background).Mesh,
                    Seconds = CreateText($"seat-{seat}-seconds", 13f, TextAlignment.Center, UiRole.Accent).Mesh
                };
            }

            hintPlate = AddPlate("hint-plate", UiSkin.Card);
            hint = CreateText("hint", 19f, TextAlignment.Center, UiRole.Accent);

            guidePlate = AddPlate("meld-guide", UiSkin.Card);
            guide = CreateText("meld-guide-text", 13f, TextAlignment.Left, UiRole.TextMain);

            // The persistent bottom bar contains only the four actions from the desktop brief.
            // Keep the learning controls visible, but compact: they are useful during a hand and
            // their former large captions made the lower-right corner feel crowded.
            AddChip(HudCommand.Rules, KeyButtonWidth, "hud.btn.rules", "R");
            AddChip(HudCommand.Pause, KeyButtonWidth, "hud.btn.pause", "Esc");
            AddChip(HudCommand.Review, KeyButtonWidth, "hud.btn.review", "V");
            AddChip(HudCommand.Save, KeyButtonWidth, "hud.btn.save", "F5");
            AddChip(HudCommand.Hints, HintsButtonWidth, "hud.hints", string.Empty);
            AddChip(HudCommand.Combos, CombosButtonWidth, "hud.meldGuide", string.Empty);
        }

        private void AddChip(HudCommand command, float widthPx, string caption, string key)
        {
            var chip = new Chip
            {
                Plate = AddPlate($"button-{command}", UiSkin.Button),
                Label = CreateText($"button-{command}-text", 15f, TextAlignment.Center, UiRole.TextMain).Mesh,
                Key = CreateText($"button-{command}-key", 11f, TextAlignment.Right, UiRole.Accent).Mesh,
                Command = command,
                WidthPx = widthPx
            };
            chip.Label.text = Loc.Get(caption);
            chip.Key.text = key;
            chip.Box = chip.Plate.gameObject.AddComponent<BoxCollider>();
            chip.Box.size = Vector3.one;
            AddIcon(chip);
            chips.Add(chip);
        }

        /// <summary>
        /// The glyph of a button, drawn from the same plates as everything else: the reference draws a
        /// small line icon over the caption of a key and beside the caption of the two learn buttons.
        /// Two or three plates are enough for a book, a pause, an eye, a floppy, a bulb and the two
        /// layers of the meld guide.
        /// </summary>
        private void AddIcon(Chip chip)
        {
            void Part(UiSkin skin, float widthPx, float heightPx, float dxPx, float dyPx)
            {
                var plate = AddPlate($"button-{chip.Command}-icon-{chip.Icon.Count}", skin);
                plate.SetSize(widthPx * units, heightPx * units);
                chip.Icon.Add((plate, new Vector4(widthPx, heightPx, dxPx, dyPx)));
            }

            switch (chip.Command)
            {
                case HudCommand.Rules:
                    Part(UiSkin.Outline, 15f, 17f, 0f, 0f);
                    Part(UiSkin.Accent, 2f, 13f, -4.5f, 0f);
                    break;
                case HudCommand.Pause:
                    Part(UiSkin.Accent, 3f, 15f, -3f, 0f);
                    Part(UiSkin.Accent, 3f, 15f, 3f, 0f);
                    break;
                case HudCommand.Review:
                    Part(UiSkin.Outline, 18f, 12f, 0f, 0f);
                    Part(UiSkin.Accent, 5f, 5f, 0f, 0f);
                    break;
                case HudCommand.Save:
                    Part(UiSkin.Outline, 16f, 16f, 0f, 0f);
                    Part(UiSkin.Accent, 8f, 5f, 0f, 4f);
                    break;
                case HudCommand.Hints:
                    Part(UiSkin.Outline, 13f, 13f, 0f, 2f);
                    Part(UiSkin.Accent, 5f, 2.5f, 0f, -6.5f);
                    break;
                case HudCommand.Combos:
                    Part(UiSkin.Outline, 17f, 8f, 0f, 4f);
                    Part(UiSkin.Outline, 17f, 8f, 0f, -5f);
                    break;
            }
        }

        private UiPlate AddPlate(string name, UiSkin skin)
        {
            var plate = UiPlate.Create(transform, skin, name);
            plates.Add(plate);
            return plate;
        }

        private Caption CreateText(string name, float fontPx, TextAlignment alignment, UiRole role)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            host.transform.localRotation = Quaternion.identity;

            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = alignment == TextAlignment.Center ? TextAnchor.MiddleCenter
                : alignment == TextAlignment.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            mesh.alignment = alignment;
            mesh.fontSize = 64;
            mesh.characterSize = fontPx;
            mesh.color = UiStyle.Of(role);
            mesh.text = string.Empty;
            UiFont.Assign(mesh);

            // Text is transparent like the plates it stands on, and a plate and its caption are a
            // millimetre apart: without an explicit order the two swap places from frame to frame.
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer != null) renderer.sortingOrder = 100;

            return new Caption { Mesh = mesh, FontPx = fontPx };
        }

        // -------------------------------------------------------------------------------- picking

        /// <summary>True when the ray landed on the toast: the caller closes it.</summary>
        public bool TryDismiss(Ray ray)
        {
            if (!IsOpen || !toastPlate.gameObject.activeSelf) return false;
            foreach (var hit in Physics.RaycastAll(ray, Depth * 2f))
                if (hit.collider != null && hit.collider.gameObject == toastPlate.gameObject)
                {
                    toastUntil = 0f;
                    return true;
                }
            return false;
        }

        /// <summary>Turns a click into the command of the button it landed on.</summary>
        public HudCommand TryPick(Ray ray)
        {
            if (!IsOpen) return HudCommand.None;
            var nearest = float.MaxValue;
            var picked = HudCommand.None;
            foreach (var hit in Physics.RaycastAll(ray, Depth * 2f))
            {
                if (hit.collider == null || hit.distance >= nearest) continue;
                foreach (var chip in chips)
                {
                    if (hit.collider.gameObject != chip.Plate.gameObject) continue;
                    nearest = hit.distance;
                    picked = chip.Enabled ? chip.Command : HudCommand.None;
                    break;
                }
            }
            return picked;
        }

        /// <summary>Lights the button under the cursor the way a menu row lights up.</summary>
        public void Hover(Ray ray)
        {
            if (!IsOpen) return;
            hovered = HudCommand.None;
            foreach (var hit in Physics.RaycastAll(ray, Depth * 2f))
                foreach (var chip in chips)
                    if (hit.collider != null && hit.collider.gameObject == chip.Plate.gameObject)
                        hovered = chip.Command;
        }

        // -------------------------------------------------------------------------------- drawing

        public void Refresh(Camera camera, GameSession session, GameSettings settings, int humanSeat,
            bool humanTurn, PlayClock clockState, string note,
            HandPotentialMode potentialMode = HandPotentialMode.Off)
        {
            var state = session.State;
            if (state == null) return;

            var halfHeight = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Depth;
            units = halfHeight * 2f / DesignHeight * UiStyle.Scale;
            // HUD coordinates belong to the camera frame, not to the table plane. This keeps the top
            // bar and the bottom controls fixed while the player orbits or zooms the table camera.
            transform.localPosition = new Vector3(0f, 0f, Depth);
            transform.localRotation = Quaternion.identity;
            var halfWidth = halfHeight * camera.aspect;

            if (!ReferenceEquals(state, lastState))
            {
                lastState = state;
                toastUntil = 0f;
            }

            if (styleVersion != UiStyle.Revision) Repaint();

            var left = -halfWidth + MarginX * units;
            var right = halfWidth - MarginX * units;
            var top = halfHeight - MarginTop * units;
            var rowY = top - BadgeHeight * 0.5f * units;

            // Top badges use fixed screen slots. The previous layout placed the rules badge after the
            // measured hand badge while the wall badge was independently centred; long Russian labels
            // therefore collided as soon as the camera/aspect changed. These are camera-space slots,
            // so orbiting the table cannot move one module through another.
            var topLeft = -halfWidth + MarginX * units;
            // The top row is a screen-space grid. Unlike the old measured-flow layout, these slots do
            // not depend on the width of localized text and therefore stay put when the camera or
            // perspective changes.
            DrawHandBadge(session, topLeft, rowY, units);
            DrawRulesBadge(session, settings, topLeft + TopRulesLeft * units, rowY, units);
            DrawWallBadge(state, topLeft + TopWallLeft * units, rowY, units);
            DrawClockBadge(session, right, rowY);
            DrawHint(state, humanSeat, humanTurn, clockState, halfHeight);
            DrawPinnedHand(state, humanSeat, halfWidth, halfHeight, units);
            DrawPlayers(state, session, humanSeat, halfHeight, left, right, clockState, humanTurn);
            DrawPotential(session, humanSeat, potentialMode, left, top);
            DrawToast(state, note, right, top);
            DrawGuide(session, halfHeight, right);
            DrawButtons(state, halfHeight, left, right);

            // Colliders are picking targets: the plates have just moved.
            Physics.SyncTransforms();
        }

        /// <summary>Repaints every plate when the palette or the composition changes.</summary>
        private void Repaint()
        {
            styleVersion = UiStyle.Revision;
            foreach (var tile in handTiles)
                if (tile != null) tile.Repaint();
            foreach (var plate in plates) plate.SetSkin(plate.Skin);
            foreach (var chip in chips)
            {
                chip.Plate.SetSkin(chip.Enabled ? UiSkin.Button : UiSkin.ButtonDisabled);
                chip.Label.color = UiStyle.Of(UiRole.TextMain);
                chip.Key.color = UiStyle.Of(UiRole.Accent);
            }
            foreach (var seat in seats)
            {
                seat.Circle.SetSkin(UiSkin.Circle);
                seat.Rim.SetSkin(UiSkin.Outline);
                seat.Dealer.SetSkin(UiSkin.Circle);
            }
            foreach (var bar in wallIcon) bar.SetSkin(UiSkin.Row);
            rulesDot.SetSkin(UiSkin.Ring);
            rulesMark.color = UiStyle.Of(UiRole.Accent);
            clockDial.SetSkin(UiSkin.Ring);
            clockHand.SetSkin(UiSkin.Accent);
            toastClose.color = UiStyle.Of(UiRole.TextMuted);
        }

        /// <summary>Draws the hand badge; returns the right edge it ended on.</summary>
        private float DrawHandBadge(GameSession session, float left, float y, float slotUnits)
        {
            SetText(hand, Loc.Format("hud.handRound", session.HandsPlayed + 1, GameSession.TotalHands,
                Text.SeatName(session.RoundWind)), UiRole.TextMain);
            Fit(hand, TopHandWidth * slotUnits - 32f * slotUnits);
            var width = TopHandWidth * slotUnits;
            handPlate.SetSize(width, BadgeHeight * units);
            handPlate.SetPosition(left + width * 0.5f, y, 0.001f);
            // The caption is centred in its badge: a measuring stick is never exact, and a symmetric
            // inset hides the difference instead of leaving a gap on one side only.
            Put(hand, left + (width - hand.Width) * 0.5f, y);
            return left + width;
        }

        /// <summary>
        /// The rules profile as a quiet badge next to the hand, the way the reference puts it: the name
        /// of the profile and a gold "i" that says the table may be running a house rule.
        /// </summary>
        private void DrawRulesBadge(GameSession session, GameSettings settings, float left, float y, float slotUnits)
        {
            var houseRules = !SessionLengthSettings.FollowsRules(session.TimeLimit)
                || !ClockPaceSettings.FollowsRules(settings.ClockPace);
            var label = Text.VariantName(session.Variant)
                + (houseRules ? " · " + Loc.Get("hud.houseBadge") : string.Empty);
            SetText(rules, label, UiRole.TextMuted);
            Fit(rules, TopRulesWidth * slotUnits - 48f * slotUnits);

            var icon = 16f * slotUnits;
            var width = TopRulesWidth * slotUnits;
            rulesPlate.SetSize(width, BadgeHeight * units);
            rulesPlate.SetPosition(left + width * 0.5f, y, 0.001f);

            var dotX = left + 12f * units + icon * 0.5f;
            rulesDot.SetSize(icon, icon);
            rulesDot.SetPosition(dotX, y, 0.0005f);
            rulesMark.characterSize = SizeOf(11f);
            rulesMark.transform.localPosition = new Vector3(dotX, y, -0.001f);

            Put(rules, left + 12f * units + icon + 8f * units, y);
            rules.Mesh.characterSize = SizeOf(13f);
        }

        private void DrawWallBadge(GameState state, float center, float y, float slotUnits)
        {
            SetText(wall, Loc.Get("hud.wallCaption"), UiRole.TextMain);
            SetText(wallCount, state.Wall.Remaining.ToString(), UiRole.Accent);
            var iconWidth = 20f;
            var width = TopWallWidth * slotUnits;
            var groupWidth = width - BadgePadding * 2f * units;
            var left = -width * 0.5f;

            wallPlate.SetSize(width, BadgeHeight * units);
            wallPlate.SetPosition(center, y, 0.001f);

            var iconX = center - width * 0.5f + 16f * units;
            for (var row = 0; row < wallIcon.Count; row++)
            {
                var barWidth = (12f + row * 3f) * units;
                wallIcon[row].SetSize(barWidth, 3f * units);
                wallIcon[row].SetPosition(iconX + barWidth * 0.5f, y + (row - 1) * 5f * units, 0.0005f);
            }

            var textX = iconX + (iconWidth + BadgeGap) * units;
            Put(wall, textX, y);
            Put(wallCount, textX + wall.Width + 4f * units, y);
        }

        private void DrawClockBadge(GameSession session, float right, float y)
        {
            SetText(clock, Clock(session.Elapsed), UiRole.TextMain);
            SetText(clockTotal, session.HasTimeLimit
                ? "/ " + Clock(session.TimeLimit)
                : Loc.Get("hud.noClock"), UiRole.TextMuted);

            var icon = 18f;
            var groupWidth = icon * units + 10f * units + clock.Width + 4f * units + clockTotal.Width;
            var width = groupWidth + BadgePadding * 2f * units;
            var left = right - width;

            clockPlate.SetSize(width, BadgeHeight * units);
            clockPlate.SetPosition(left + width * 0.5f, y, 0.001f);

            var dialX = left + BadgePadding * units + icon * 0.5f * units;
            clockDial.SetSize(icon * units, icon * units);
            clockDial.SetPosition(dialX, y, 0.0005f);
            clockHand.SetSize(1.6f * units, 5f * units);
            clockHand.SetPosition(dialX, y + 2.2f * units, 0.0004f);

            var textX = dialX + icon * 0.5f * units + 10f * units;
            Put(clock, textX, y);
            Put(clockTotal, textX + clock.Width + 4f * units, y);
        }

        private void DrawHint(GameState state, int humanSeat, bool humanTurn, PlayClock clockState,
            float halfHeight)
        {
            var prompt = humanTurn
                ? Loc.Get("hud.yourTurn")
                : Loc.Format("hud.turnName", PlayerDisplayName(state, state.CurrentPlayer, humanSeat));
            if (clockState.IsRunning)
                prompt += " · " + (clockState.IsLimited
                    ? Loc.Format("hud.secondsShort", clockState.SecondsLeft)
                    : Loc.Get("hud.noLimit"));
            if (state.IsWinForbidden(humanSeat))
                prompt += " · " + Loc.Format("hud.huForbidden", state.WinForbiddenReason(humanSeat));

            var shown = hintVisible;
            hintPlate.gameObject.SetActive(shown);
            hint.Mesh.gameObject.SetActive(shown);
            if (!shown) return;

            SetText(hint, prompt, humanTurn ? UiRole.Accent : UiRole.TextMain);
            var width = Mathf.Min(HintMaxWidth * units, hint.Width + 36f * units);
            var y = -halfHeight + (HintBottom + HintHeight * 0.5f) * units;
            hintPlate.SetSize(width, HintHeight * units);
            hintPlate.SetPosition(0f, y, 0.001f);
            hint.Mesh.transform.localPosition = new Vector3(0f, y, -0.001f);
        }

        private void DrawPlayers(GameState state, GameSession session, int humanSeat, float halfHeight,
            float left, float right, PlayClock clockState, bool humanTurn)
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var plate = seats[seat];
                // Where a seat sits is relative to the human: 0 is the near corner, 2 the far one.
                var view = (seat - humanSeat + 4) % 4;
                var self = view == 0;
                var width = (self ? SelfPlateWidth : PlateWidth) * units;
                var height = (self ? SelfPlateHeight : PlateHeight) * units;
                var circle = (self ? SelfCircleSize : CircleSize) * units;

                var x = view switch
                {
                    0 => left + width * 0.5f,
                    1 => left + width * 0.5f,
                    2 => 0f,
                    _ => right - width * 0.5f
                };
                var y = view switch
                {
                    0 => -halfHeight + SelfPlateBottom * units + height * 0.5f,
                    2 => halfHeight - TopPlateTop * units - height * 0.5f,
                    _ => halfHeight - SidePlateTop * units - height * 0.5f
                };

                var active = seat == state.CurrentPlayer;
                plate.Plate.SetSkin(self || active ? UiSkin.Panel : UiSkin.Card);
                plate.Plate.SetSize(width, height);
                plate.Plate.SetPosition(x, y, 0.001f);

                // A gold rim on the plate that is moving: the reference's answer to "whose turn is it".
                var rim = active ? 2f : 0f;
                plate.Rim.gameObject.SetActive(active);
                if (active)
                {
                    plate.Rim.SetSize(width + rim * units * 2f, height + rim * units * 2f);
                    plate.Rim.SetPosition(x, y, 0.0015f);
                }

                var circleX = x - width * 0.5f + 12f * units + circle * 0.5f;
                plate.Circle.SetSize(circle, circle);
                plate.Circle.SetPosition(circleX, y, 0.0005f);
                plate.Circle.SetSkin(self ? UiSkin.Circle : UiSkin.Circle);

                // The human's token is the reference's turn clock: a gold rim with the seconds inside.
                var showClock = self && clockState.IsRunning && clockState.IsLimited;
                plate.Seconds.gameObject.SetActive(showClock);
                if (showClock)
                {
                    plate.Seconds.text = clockState.SecondsLeft.ToString();
                    plate.Seconds.characterSize = SizeOf(13f);
                    plate.Seconds.color = UiStyle.Of(UiRole.Accent);
                    plate.Seconds.transform.localPosition = new Vector3(circleX, y, -0.001f);
                }
                else
                {
                    plate.Wind.gameObject.SetActive(true);
                    plate.Wind.text = Text.SeatWindLetter(state.SeatWindOf(seat));
                    plate.Wind.characterSize = SizeOf(self ? 17f : 16f);
                    plate.Wind.transform.localPosition = new Vector3(circleX, y, -0.001f);
                }
                plate.Wind.gameObject.SetActive(!showClock);

                var textX = circleX + circle * 0.5f + 10f * units;

                plate.Name.text = self
                    ? Loc.Format("hud.youSeat", Text.SeatName(state.SeatWindOf(seat)))
                    : PlayerDisplayName(state, seat, humanSeat);
                plate.Name.characterSize = SizeOf(15f);
                plate.Name.color = UiStyle.Of(active ? UiRole.Accent : UiRole.TextMain);
                plate.Name.transform.localPosition = new Vector3(textX, y + 8f * units, -0.001f);

                plate.Score.text = Loc.Format("hud.playerScore", session.Scores[seat]);
                plate.Score.characterSize = SizeOf(13f);
                plate.Score.color = UiStyle.Of(UiRole.TextMuted);
                plate.Score.transform.localPosition = new Vector3(textX, y - 9f * units, -0.001f);

                // The dealer carries the gold "D" of the reference, on the far side of the plate.
                var dealer = seat == session.Dealer;
                plate.Dealer.gameObject.SetActive(dealer);
                plate.DealerLetter.gameObject.SetActive(dealer);
                if (dealer)
                {
                    var dealerX = x + width * 0.5f - 12f * units - DealerSize * 0.5f * units;
                    plate.Dealer.SetSize(DealerSize * units, DealerSize * units);
                    plate.Dealer.SetPosition(dealerX, y, 0.0004f);
                    plate.DealerLetter.text = Loc.Get("hud.dealerLetter");
                    plate.DealerLetter.characterSize = SizeOf(12f);
                    plate.DealerLetter.color = UiStyle.Of(UiRole.Background);
                    plate.DealerLetter.transform.localPosition = new Vector3(dealerX, y, -0.001f);
                }
            }
        }

        private void DrawPotential(GameSession session, int humanSeat, HandPotentialMode mode, float left,
            float top)
        {
            var shown = mode != HandPotentialMode.Off && session.Variant.Mode == ScoringMode.Fans;
            potential.Mesh.gameObject.SetActive(shown);
            if (!shown) return;

            var line = HandPotential.Evaluate(session.State!, humanSeat);
            SetText(potential, mode == HandPotentialMode.BestAchievable
                ? Loc.Format("hud.potentialMaxOnly", line.MaximumPoints)
                : Loc.Format("hud.potentialForecast", line.ExpectedPoints, line.LiveCopies), UiRole.Accent);
            potential.Mesh.transform.localPosition =
                new Vector3(left + 4f * units, top - BadgeHeight * units - 12f * units, -0.001f);
        }

        /// <summary>
        /// The two learn buttons' card: what a chow, a pung and a kong are, and what they are worth in
        /// the profile on the table. It stands above the buttons, where it covers no tile.
        /// </summary>
        private void DrawGuide(GameSession session, float halfHeight, float right)
        {
            guidePlate.gameObject.SetActive(guideVisible);
            guide.Mesh.gameObject.SetActive(guideVisible);
            if (!guideVisible) return;

            var text = Loc.Get("hud.meldDefinitions") + "\n\n"
                + Loc.Get(session.Variant.Mode == ScoringMode.ClassicPoints
                    ? "hud.meldClassic" : "hud.meldSports");
            SetText(guide, text, UiRole.TextMain);

            var lines = 0;
            foreach (var letter in text) if (letter == '\n') lines++;
            lines++;
            var lineHeight = guide.Mesh.characterSize * GlyphPerSize * 1.25f;
            var width = Mathf.Min(360f * units, guide.Width + 28f * units);
            var height = lines * lineHeight + 24f * units;
            var y = -halfHeight + (ButtonBottom + ButtonHeight + 14f) * units + height * 0.5f;
            guidePlate.SetSize(width, height);
            guidePlate.SetPosition(right - width * 0.5f, y, 0.001f);
            guide.Mesh.transform.localPosition =
                new Vector3(right - width + 14f * units, y + height * 0.5f - 12f * units, -0.001f);
        }

        /// <summary>
        /// The dice of the deal, under the clock: two lines, the way the reference writes them, and a
        /// cross. The toast is a note of the table, so a click anywhere on it closes it.
        /// </summary>
        private void DrawToast(GameState state, string note, float right, float top)
        {
            var shown = false;
            var title = note;
            var detail = string.Empty;
            if (title.Length == 0 && Time.unscaledTime < toastUntil)
            {
                title = Loc.Format("hud.diceThrow", state.DealerRoll.ToString());
                detail = Loc.Format("hud.breakAfter", state.BreakStacks);
                shown = true;
            }
            else if (title.Length > 0)
            {
                shown = true;
            }

            toastPlate.gameObject.SetActive(shown);
            toastTitle.Mesh.gameObject.SetActive(shown);
            toastDetail.Mesh.gameObject.SetActive(shown);
            toastClose.gameObject.SetActive(shown);
            if (!shown) return;

            if (toastTitle.Mesh.text != title) toastTitle.Mesh.text = title;
            toastTitle.Mesh.characterSize = SizeOf(15f);
            toastTitle.Mesh.color = UiStyle.Of(UiRole.TextMain);
            if (toastDetail.Mesh.text != detail) toastDetail.Mesh.text = detail;
            toastDetail.Mesh.characterSize = SizeOf(13f);
            toastDetail.Mesh.color = UiStyle.Of(UiRole.TextMuted);
            toastClose.characterSize = SizeOf(15f);
            toastClose.color = UiStyle.Of(UiRole.TextMuted);

            var width = ToastWidth * units;
            var height = ToastHeight * units;
            var y = top - (ToastTop + ToastHeight * 0.5f) * units;
            toastPlate.SetSize(width, height);
            toastPlate.SetPosition(right - width * 0.5f, y, 0.001f);

            var textLeft = right - width + 12f * units;
            toastTitle.Mesh.transform.localPosition = new Vector3(textLeft, y + 9f * units, -0.001f);
            toastDetail.Mesh.transform.localPosition = new Vector3(textLeft, y - 9f * units, -0.001f);
            toastClose.transform.localPosition =
                new Vector3(right - 14f * units, y + height * 0.5f - 12f * units, -0.001f);

            var box = toastPlate.GetComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.01f);
        }

        /// <summary>
        /// The four persistent desktop actions. Hints and the meld guide are transient overlays, not
        /// permanent controls, so this strip never occupies the player's hand area.
        /// </summary>
        private void DrawButtons(GameState state, float halfHeight, float left, float right)
        {
            var y = -halfHeight + (ButtonBottom + ButtonHeight * 0.5f) * units;
            var height = ButtonHeight * units;

            // Saving is only possible while a hand is on the table: the reference draws the key disabled.
            foreach (var chip in chips)
                if (chip.Command == HudCommand.Save) chip.Enabled = state.Phase != GamePhase.HandFinished;

            var x = left;
            foreach (var chip in chips)
            {
                if (chip.Command is HudCommand.Hints or HudCommand.Combos) continue;
                x = PlaceChip(chip, x, y, height);
                x += ButtonGap * units;
            }

            var x2 = right;
            for (var index = chips.Count - 1; index >= 0; index--)
            {
                var chip = chips[index];
                if (chip.Command is not (HudCommand.Hints or HudCommand.Combos)) continue;
                x2 = PlaceChipRight(chip, x2, y, height);
                x2 -= ButtonGap * units;
            }

            foreach (var chip in chips)
            {
                if (chip.Command == HudCommand.Hints) chip.Enabled = hintVisible;
                chip.Plate.SetSkin(!chip.Enabled ? UiSkin.ButtonDisabled
                    : chip.Command == hovered ? UiSkin.ButtonHover : UiSkin.Button);
                chip.Label.color = UiStyle.Of(chip.Enabled ? UiRole.TextMain : UiRole.TextMuted);
                chip.Key.color = UiStyle.Of(chip.Enabled ? UiRole.Accent : UiRole.TextMuted);
            }
        }

        private float PlaceChip(Chip chip, float left, float y, float height)
        {
            var width = chip.WidthPx * units;
            chip.Plate.SetSize(width, height);
            chip.Plate.SetPosition(left + width * 0.5f, y, 0.0005f);
            var box = chip.Plate.GetComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.01f);

            chip.Label.characterSize = SizeOf(12f * IconCaptionScale);
            Show(chip.Label, true);
            chip.Label.transform.localPosition = new Vector3(left + width * 0.5f, y - 12f * units, -0.001f);
            chip.Label.color = UiStyle.Of(chip.Enabled ? UiRole.TextMain : UiRole.TextMuted);
            PlaceIcon(chip, left + width * 0.5f, y + 9f * units);

            chip.Key.characterSize = SizeOf(11f * IconKeyScale);
            Show(chip.Key, chip.Key.text.Length > 0);
            chip.Key.color = UiStyle.Of(chip.Enabled ? UiRole.Accent : UiRole.TextMuted);
            chip.Key.transform.localPosition =
                new Vector3(left + width - 6f * units, y + height * 0.5f - 9f * units, -0.001f);
            return left + width;
        }

        private float PlaceChipRight(Chip chip, float rightEdge, float y, float height)
        {
            var width = chip.WidthPx * units;
            var left = rightEdge - width;
            // The buttons of the right panel are wide and carry no hot key: their caption sits in the
            // middle of what the icon leaves of the plate, exactly as the reference draws them.
            chip.Label.characterSize = SizeOf(15f);
            Show(chip.Label, true);
            chip.Label.transform.localPosition =
                new Vector3(left + width * 0.5f + 12f * units, y, -0.001f);
            chip.Label.color = UiStyle.Of(chip.Enabled ? UiRole.TextMain : UiRole.TextMuted);
            Show(chip.Key, false);
            PlaceIcon(chip, left + 24f * units, y);

            chip.Plate.SetSize(width, height);
            chip.Plate.SetPosition(left + width * 0.5f, y, 0.0005f);
            var box = chip.Plate.GetComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.01f);
            return left;
        }

        /// <summary>Draws the glyph of a button around the point the caller has chosen for it.</summary>
        private void PlaceIcon(Chip chip, float x, float y)
        {
            foreach (var part in chip.Icon)
            {
                var box = part.Box;
                part.Plate.SetSize(box.x * units, box.y * units);
                part.Plate.SetPosition(x + box.z * units, y + box.w * units, 0.0002f);
            }
        }

        /// <summary>Turns the hint bar on and off; the button of the same name follows.</summary>
        public void ToggleHints() => hintVisible = !hintVisible;

        /// <summary>Shows the dice toast again for a few seconds, e.g. after a new deal.</summary>
        public void AnnounceDice(float seconds) => toastUntil = Time.unscaledTime + seconds;

        private void DrawPinnedHand(GameState state, int humanSeat, float halfWidth, float halfHeight, float screenUnits)
        {
            var handState = state.Players[humanSeat];
            var sorted = new List<Tile>(handState.Concealed);
            sorted.Sort((a, b) =>
            {
                var suit = a.Type.Suit.CompareTo(b.Type.Suit);
                return suit != 0 ? suit : a.Type.Rank.CompareTo(b.Type.Rank);
            });
            var fingerprint = string.Join(",", sorted.ConvertAll(tile => tile.InstanceId));
            if (fingerprint == handFingerprint && handTiles.Count == sorted.Count) return;

            foreach (var tile in handTiles)
                if (tile != null) Destroy(tile.gameObject);
            handTiles.Clear();
            handFingerprint = fingerprint;

            if (sorted.Count == 0) return;
            var spacing = (HandTileWidth + HandTileGap) * screenUnits;
            var total = sorted.Count * spacing - HandTileGap * screenUnits;
            var start = -total * 0.5f + (HandTileWidth * screenUnits) * 0.5f;
            var y = -halfHeight + (HandBottom + HandTileHeight * 0.5f) * screenUnits;
            for (var i = 0; i < sorted.Count; i++)
            {
                var tile = TileObject.Create(transform, $"pinned-hand-{i}");
                tile.SetHudTile(sorted[i].Type,
                    new Vector3(start + i * spacing, y, -0.01f), HandTileScale);
                handTiles.Add(tile);
            }
            Physics.SyncTransforms();
        }

        // -------------------------------------------------------------------------------- helpers

        /// <summary>How many lines of a plate are drawn; a TextMesh is hidden through its renderer.</summary>
        private static void Show(TextMesh mesh, bool shown)
        {
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = shown;
        }

        /// <summary>Writes a caption and remembers the width it asks for, in world units.</summary>
        private void SetText(Caption text, string value, UiRole role)
        {
            text.Mesh.characterSize = SizeOf(text.FontPx);
            text.Mesh.color = UiStyle.Of(role);
            if (text.Mesh.text != value) text.Mesh.text = value;
            text.Width = Measure(text.Mesh, value);
        }

        /// <summary>
        /// The width of a drawn line. The font is measured from the mesh it produced instead of being
        /// guessed from the number of letters: Russian and English lines differ, and a badge that is
        /// fifty pixels too wide is the first thing a reader notices.
        /// </summary>
        private static float Measure(TextMesh mesh, string value)
        {
            if (value.Length == 0) return 0f;
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer != null)
            {
                var width = renderer.bounds.size.x;
                if (width > 0f) return width;
            }
            return value.Length * mesh.characterSize * GlyphAdvance;
        }

        /// <summary>Prevents a localized caption from escaping its fixed screen slot.</summary>
        private static void Fit(Caption text, float maximum)
        {
            if (text.Width <= maximum || text.Width <= 0f) return;
            text.Mesh.characterSize *= maximum / text.Width;
            text.Width = Measure(text.Mesh, text.Mesh.text);
        }

        private void Put(Caption text, float left, float y) =>
            text.Mesh.transform.localPosition = new Vector3(left, y, -0.001f);

        /// <summary>A design size in pixels as the character size a <see cref="TextMesh"/> wants.</summary>
        private float SizeOf(float px) => px * units * UiStyle.TextScale / GlyphPerSize;

        public static string PlayerDisplayName(GameState state, int seat, int humanSeat) =>
            seat == humanSeat ? Loc.Get("hud.you") : Text.SeatName(state.SeatWindOf(seat));

        private static string Clock(TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }
}
