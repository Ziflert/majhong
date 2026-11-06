using System;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>Read-only world-space presentation. Commands, clocks and state still belong to MahjongGame.</summary>
    public sealed class TableHud : MonoBehaviour
    {
        private const float Depth = 0.5f;
        private sealed class Card
        {
            public TextMesh Label;
            public UiPlate Plate;
            public float Size;
        }

        private Card header = null!;
        private Card wall = null!;
        private Card time = null!;
        private Card rules = null!;
        private Card hint = null!;
        private Card notice = null!;
        private Card debug = null!;
        private Card meldGuide = null!;
        private ControlTile hintsButton = null!;
        private ControlTile guideButton = null!;
        private bool hintsVisible = true;
        private bool guideVisible;
        public bool HintsVisible => hintsVisible;
        public bool MeldGuideVisible => guideVisible;
        public TextMesh MeldGuide => meldGuide.Label;

        public void ToggleHints() => hintsVisible = !hintsVisible;
        public void ToggleMeldGuide() => guideVisible = !guideVisible;

        /// <summary>UI clicks are consumed before any table pick, including during the opening.</summary>
        public bool HandleClick(Ray ray)
        {
            foreach (var hit in Physics.RaycastAll(ray, 1f))
            {
                var tile = hit.collider.GetComponentInParent<ControlTile>();
                if (tile == hintsButton) { ToggleHints(); return true; }
                if (tile == guideButton) { ToggleMeldGuide(); return true; }
            }
            return false;
        }
        private readonly Card[] players = new Card[4];
        private GameState lastState;
        private float toastUntil;
        private bool debugOpen;
        private int styleVersion = -1;

        public TextMesh Header => header.Label;
        public TextMesh WallCounter => wall.Label;
        public TextMesh Hint => hint.Label;
        public TextMesh Notice => notice.Label;
        public bool DebugOpen => debugOpen;
        public TextMesh DebugText => debug.Label;
        public TextMesh PlayerLabel(int seat) => players[seat].Label;

        public static TableHud Create(Transform camera)
        {
            var host = new GameObject("TableHud");
            host.transform.SetParent(camera, false);
            var hud = host.AddComponent<TableHud>();
            hud.header = hud.CreateCard("TopBar", TextAnchor.UpperLeft, 0.00135f);
            hud.wall = hud.CreateCard("WallCounter", TextAnchor.UpperCenter, 0.00135f);
            hud.time = hud.CreateCard("SessionClock", TextAnchor.UpperRight, 0.00135f);
            hud.rules = hud.CreateCard("RuleBadge", TextAnchor.UpperLeft, 0.0011f);
            hud.hint = hud.CreateCard("HintBar", TextAnchor.MiddleCenter, 0.00115f);
            hud.notice = hud.CreateCard("Notice", TextAnchor.UpperCenter, 0.0013f);
            hud.debug = hud.CreateCard("DebugPanel", TextAnchor.UpperLeft, 0.0011f);
            for (var i = 0; i < hud.players.Length; i++)
                hud.players[i] = hud.CreateCard($"PlayerPlate-{i}", TextAnchor.MiddleCenter, 0.00125f);
            hud.meldGuide = hud.CreateCard("MeldGuide", TextAnchor.UpperLeft, 0.00115f);
            hud.meldGuide.Label.gameObject.SetActive(false);
            hud.hintsButton = ControlTile.Create(host.transform, Loc.Get("hud.hints"), KeyCode.None, ControlType.Hints);
            hud.guideButton = ControlTile.Create(host.transform, Loc.Get("hud.meldGuide"), KeyCode.None, ControlType.MeldGuide);
            hud.SetDebug(false);
            return hud;
        }

        private Card CreateCard(string name, TextAnchor anchor, float size)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left
                : anchor == TextAnchor.UpperRight ? TextAlignment.Right : TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size * UiStyle.Scale;
            mesh.color = UiStyle.Of(UiRole.TextMain);
            UiFont.Assign(mesh);
            return new Card { Label = mesh, Plate = UiPlate.Create(host.transform, UiSkin.Card, "backdrop"), Size = size };
        }

        public void ToggleDebug() => SetDebug(!debugOpen);
        public void SetDebug(bool value)
        {
            debugOpen = value;
            debug.Label.gameObject.SetActive(value);
        }

        public static string PlayerDisplayName(GameState state, int seat, int humanSeat) =>
            seat == humanSeat ? Loc.Get("hud.you") : Text.SeatName(state.SeatWindOf(seat));

        public void DismissNotice() => toastUntil = 0f;

        public void Refresh(Camera camera, GameSession session, GameSettings settings, int humanSeat,
            bool humanTurn, PlayClock clock, string note, string diagnostic)
        {
            var state = session.State;
            if (state == null) return;
            UpdateButton(hintsButton, "hud.hints", hintsVisible);
            UpdateButton(guideButton, "hud.meldGuide", guideVisible);
            if (!ReferenceEquals(state, lastState))
            {
                lastState = state;
                toastUntil = Time.unscaledTime + 4f;
            }
            if (styleVersion != UiStyle.Revision)
            {
                styleVersion = UiStyle.Revision;
                foreach (var card in GetComponentsInChildren<UiPlate>(true)) card.SetSkin(UiSkin.Card);
            }
            var halfHeight = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Depth;
            var halfWidth = halfHeight * camera.aspect;
            var left = -halfWidth + 0.014f;
            var top = halfHeight - 0.012f;
            var right = halfWidth - 0.014f;
            PlaceButton(hintsButton, right - 0.071f * UiStyle.Scale, -halfHeight + 0.019f * UiStyle.Scale);
            PlaceButton(guideButton, right - 0.024f * UiStyle.Scale, -halfHeight + 0.019f * UiStyle.Scale);
            hint.Label.gameObject.SetActive(hintsVisible);
            meldGuide.Label.gameObject.SetActive(guideVisible);
            Set(meldGuide, Loc.Get("hud.meldDefinitions") + "\n\n"
                + Loc.Get(session.Variant.Mode == ScoringMode.ClassicPoints
                    ? "hud.meldClassic" : "hud.meldSports"),
                right - 0.185f * Mathf.Min(UiStyle.Scale, 1f), top - 0.07f, UiRole.TextMain);

            Set(header, Loc.Format("hud.handRound", session.HandsPlayed + 1, GameSession.TotalHands,
                Text.SeatName(session.RoundWind)), left, top, UiRole.TextMain);
            Set(wall, Loc.Format("hud.wall", state.Wall.Remaining), 0f, top, UiRole.TextMain);
            Set(time, Loc.Format("hud.clock", Clock(session.Elapsed), session.HasTimeLimit
                ? Clock(session.TimeLimit) : Loc.Get("hud.noClock")), right, top,
                session.IsTimeUp || session.IsFinalQuarterHour ? UiRole.Critical : UiRole.TextMain);
            var houseRules = !SessionLengthSettings.FollowsRules(session.TimeLimit)
                || !ClockPaceSettings.FollowsRules(settings.ClockPace);
            Set(rules, Text.VariantName(session.Variant)
                + (houseRules ? " · " + Loc.Get("hud.houseBadge") : string.Empty),
                left, top - 0.018f * UiStyle.Scale, UiRole.TextMuted);

            var prompt = humanTurn ? Loc.Get("hud.yourTurn")
                : Loc.Format("hud.turnName", PlayerDisplayName(state, state.CurrentPlayer, humanSeat));
            if (clock.IsRunning) prompt += "\n" + (clock.IsLimited
                ? Loc.Format("hud.seconds", Text.ClockLabel(clock.Window), clock.SecondsLeft)
                : Loc.Format("hud.secondsNone", Text.ClockLabel(clock.Window)));
            if (state.IsWinForbidden(humanSeat))
                prompt += "\n" + Loc.Format("hud.huForbidden", state.WinForbiddenReason(humanSeat));
            // Keep the hint above the hand and below the table center; the previous position
            // made the two-line clock prompt cover the player's tiles on 1080p.
            Set(hint, prompt, 0f, -halfHeight + 0.105f, humanTurn ? UiRole.Accent : UiRole.TextMain);

            for (var seat = 0; seat < players.Length; seat++)
            {
                var label = PlayerDisplayName(state, seat, humanSeat)
                    + (seat == state.Dealer ? " ◆" : string.Empty)
                    + "\n" + Text.SeatName(state.SeatWindOf(seat)) + " · "
                    + Loc.Format("hud.playerScore", session.Scores[seat]);
                // Project the existing seat locations onto the HUD plane, keeping text upright and
                // sized independently of the orbit. Clamp it inside the frame at camera extremes.
                var point = camera.WorldToViewportPoint(TableLayout.PlatePosition(seat) + Vector3.up * 0.06f);
                var x = Mathf.Clamp((point.x * 2f - 1f) * halfWidth, left + 0.045f, right - 0.045f);
                var y = Mathf.Clamp((point.y * 2f - 1f) * halfHeight,
                    -halfHeight + 0.095f, top - 0.065f);
                var active = seat == state.CurrentPlayer;
                Set(players[seat], (active ? "▸ " : string.Empty) + label, x, y,
                    active ? UiRole.Accent : UiRole.TextMain);
                players[seat].Plate.SetSkin(active ? UiSkin.RowSelected : UiSkin.Card);
            }

            var toast = note;
            if (toast.Length == 0 && Time.unscaledTime < toastUntil)
                toast = Loc.Format("hud.dice", state.DealerRoll.ToString(),
                    Text.SeatName(state.SeatWindOf(state.DiceThrower)), state.BreakStacks);
            notice.Label.gameObject.SetActive(toast.Length > 0);
            Set(notice, toast, 0f, top - 0.045f, UiRole.TextMain);
            if (notice.Label.gameObject.activeSelf && Input.GetMouseButtonDown(0))
            {
                var mouse = camera.ScreenToViewportPoint(Input.mousePosition);
                // HUD-only dismissal; never consumes a table command or changes game state.
                if (mouse.y > 0.72f) DismissNotice();
            }
            debug.Label.gameObject.SetActive(debugOpen);
            Set(debug, diagnostic, left, top - 0.078f, UiRole.TextMuted);

            // Fit after the current frame's TextMesh rebuild (in LateUpdate on the following frame).
            foreach (var card in new[] { header, wall, time, rules, hint, notice, debug, meldGuide }) Fit(card, camera);
            foreach (var card in players) Fit(card, camera);
        }

        private static void Set(Card card, string text, float x, float y, UiRole colour)
        {
            if (card.Label.text != text) card.Label.text = text;
            card.Label.characterSize = card.Size * UiStyle.Scale;
            card.Label.color = UiStyle.Of(colour);
            card.Label.transform.localPosition = new Vector3(x, y, Depth);
        }

        private static void Fit(Card card, Camera camera)
        {
            if (!card.Label.gameObject.activeInHierarchy) return;
            var mesh = card.Label.GetComponent<MeshRenderer>();
            if (mesh == null) return;
            var bounds = mesh.localBounds;
            // Camera-independent local bounds keep the plate stable while orbiting.
            var size = bounds.size;
            card.Plate.SetSize(size.x + 0.014f, size.y + 0.009f);
            card.Plate.SetPosition(bounds.center.x, bounds.center.y, 0.006f);
            var halfHeight = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Depth;
            var available = halfHeight * camera.aspect * 2f - 0.03f;
            var fit = Mathf.Min(1f, available / Mathf.Max(0.001f, size.x + 0.014f));
            card.Label.transform.localScale = Vector3.one * fit;
        }

        private static void PlaceButton(ControlTile button, float x, float y)
        {
            button.transform.localPosition = new Vector3(x, y, Depth - 0.01f);
            button.transform.localScale = Vector3.one * (0.68f * UiStyle.Scale);
        }

        private static void UpdateButton(ControlTile button, string key, bool active)
        {
            button.SetCaption(Loc.Get(key), active ? "−" : "+");
        }

        private static string Clock(TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }
}
