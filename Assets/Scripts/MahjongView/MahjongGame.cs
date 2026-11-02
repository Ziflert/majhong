using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// Prototype table: builds the scene from primitives, runs a session of one human
    /// against three heuristic bots and translates mouse clicks into engine actions.
    ///
    /// Drop this component on an empty GameObject (or let the bootstrap create it) and press Play.
    /// </summary>
    public sealed class MahjongGame : MonoBehaviour
    {
        [SerializeField] private int humanSeat;
        [SerializeField] private float botDelay = 0.45f;
        [SerializeField] private int randomSeed = 12345;

        /// <summary>
        /// The HUD line. Exposed so tests and tools can read the state of the table without a screen;
        /// the mesh itself is parented to the camera and is not part of the game object hierarchy.
        /// </summary>
        public TextMesh Status => status;

        /// <summary>The bar of legal actions offered to the human. Exposed for tests and tools.</summary>
        public ActionBar Bar => bar;

        /// <summary>The choices currently offered to the human; closed when it is not his decision.</summary>
        public ActionPrompt Prompt { get; private set; } = ActionPrompt.Closed;

        /// <summary>Tiles rest on the table top, which sits at y = -0.005.</summary>
        private const float TileY = 0.01f;

        private readonly Dictionary<int, TileObject> handTiles = new();
        private readonly List<TileObject> discardTiles = new();
        private readonly List<TileObject> meldTiles = new();
        private readonly List<TileObject> opponentTiles = new();

        private GameSession session = null!;
        private IMahjongBot[] bots = null!;
        private TextMesh status = null!;
        private ActionBar bar = null!;
        private Transform table = null!;
        private Transform board = null!;
        private float nextBotAction;
        private float nextHandAt;
        private bool waitingForNextHand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<MahjongGame>() != null) return;
            var host = new GameObject("Mahjong");
            host.AddComponent<MahjongGame>();
        }

        private void Start()
        {
            var random = new System.Random(randomSeed);
            bots = new IMahjongBot[]
            {
                new HeuristicBot(random.Next()),
                new HeuristicBot(random.Next()),
                new HeuristicBot(random.Next()),
                new HeuristicBot(random.Next())
            };

            BuildScene();
            session = new GameSession(SessionTimeLimit());
            StartHand(random.Next());
        }

        /// <summary>
        /// Rule 3.5.6 gives a session 150 minutes. The prototype can shorten that with
        /// MAHJONG_SESSION_MINUTES so the time-up path can be seen in a capture run.
        /// </summary>
        private static System.TimeSpan? SessionTimeLimit()
        {
            var raw = System.Environment.GetEnvironmentVariable("MAHJONG_SESSION_MINUTES");
            // Invariant culture: "0.2" must mean a fifth of a minute even under a Russian locale.
            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes) && minutes > 0
                ? System.TimeSpan.FromMinutes(minutes)
                : null;
        }

        private void BuildScene()
        {
            // The scene is generated code-first, so the camera is configured even when the open
            // scene already has one (a fresh untitled scene ships with its own Main Camera).
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 1.15f, -1.45f);
            camera.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            camera.fieldOfView = 34f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

            var lightObject = new GameObject("Sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var tableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableObject.name = "Table";
            tableObject.transform.position = new Vector3(0f, -0.02f, 0.08f);
            tableObject.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            tableObject.GetComponent<Renderer>().material.color = new Color(0.09f, 0.32f, 0.20f);
            table = tableObject.transform;

            // The table cube is scaled non-uniformly; tiles live in an unscaled container so they
            // keep the size given by TileObject instead of inheriting the table's squash.
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(transform, false);
            board = boardObject.transform;

            var statusObject = new GameObject("Status");
            // HUD text follows the camera: anchored to the table it leaves the frame as soon as the
            // camera is pitched down onto the board.
            statusObject.transform.SetParent(camera.transform, false);
            statusObject.transform.localPosition = new Vector3(0f, 0.13f, 0.5f);
            statusObject.transform.localRotation = Quaternion.identity;
            status = statusObject.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperCenter;
            status.alignment = TextAlignment.Center;
            status.fontSize = 64;
            status.characterSize = 0.0016f;
            status.color = Color.white;

            bar = ActionBar.Create(camera.transform);
        }

        private void StartHand(int seed)
        {
            waitingForNextHand = false;
            ClosePrompt();
            session.StartNextHand(seed);
            ClearViews();
            Rebuild();
        }

        private void Update()
        {
            if (session.State == null) return;

            // Rule 3.5.6: the session clock runs during play, not during the pause between hands.
            if (!session.IsFinished && session.State.Phase != GamePhase.HandFinished)
                session.AdvanceClock(System.TimeSpan.FromSeconds(Time.deltaTime));

            if (session.IsTimeUp && session.State.Phase != GamePhase.HandFinished && !session.EndedByTimeLimit)
            {
                ClosePrompt();
                session.AbandonRunningHand();
                waitingForNextHand = false;
                status.text = "Time is up: the hand in progress scores nothing\n" + session.Describe();
                return;
            }

            // The table is closed for good: keep the final table on screen.
            if (session.EndedByTimeLimit) return;

            if (session.State.Phase == GamePhase.HandFinished)
            {
                if (!waitingForNextHand)
                {
                    waitingForNextHand = true;
                    nextHandAt = Time.time + 3f;
                    ClosePrompt();
                    session.CompleteHand();
                    status.text = DescribeResult();
                }
                else if (Time.time >= nextHandAt)
                {
                    if (session.IsFinished) status.text = "Session complete\n" + session.Describe();
                    else StartHand(new System.Random().Next());
                }
                return;
            }

            switch (session.State.Phase)
            {
                case GamePhase.AwaitingClaims:
                case GamePhase.AwaitingKongRobbery:
                    HandleClaims();
                    break;
                case GamePhase.AwaitingDraw:
                case GamePhase.AwaitingDiscard:
                    if (session.State.CurrentPlayer == humanSeat) HandleHumanTurn();
                    else HandleBotTurn();
                    break;
            }
        }

        // --------------------------------------------------------------- turns

        private void HandleHumanTurn()
        {
            var state = session.State!;
            var options = state.GetLegalActions(humanSeat);

            var draw = options.FirstOrDefault(option => option.Kind == ActionKind.Draw);
            if (draw != null)
            {
                ClosePrompt();
                state.Apply(draw);
                AfterAction();
                return;
            }

            HighlightHand(true);
            ShowPrompt(ActionPrompt.FromLegalActions(options));

            // H stays as a shortcut for the decision that a tile click cannot express
            if (Input.GetKeyDown(KeyCode.H) && Prompt.TrySelectKind(ActionKind.Win, out var hu))
            {
                Play(hu);
                return;
            }

            if (TryReadSelection(out var chosen))
            {
                Play(chosen);
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            var picked = PickTileUnderMouse();
            if (!picked.HasValue) return;

            var discard = options.FirstOrDefault(option =>
                option.Kind == ActionKind.Discard && option.Tile?.InstanceId == picked.Value);
            if (discard == null) return;

            Play(discard);
        }

        private void HandleBotTurn()
        {
            if (Time.time < nextBotAction) return;
            nextBotAction = Time.time + botDelay;

            var state = session.State!;
            var player = state.CurrentPlayer;
            var options = state.GetLegalActions(player);
            if (options.Count == 0) return;

            state.Apply(bots[player].Choose(state, player, options));
            AfterAction();
        }

        /// <summary>
        /// Open claim window: the bots answer on their own timer while the human picks from the bar.
        /// The engine keeps the window open as long as somebody still holds a real claim, so a human
        /// who can Pung, Kong or Hu is never passed for by the UI.
        /// </summary>
        private void HandleClaims()
        {
            var state = session.State!;
            var phase = state.Phase;

            RunBotResponses(state);

            if (state.Phase != phase)
            {
                ClosePrompt();
                AfterAction();
                return;
            }

            ShowPrompt(ActionPrompt.FromLegalActions(state.GetLegalActions(humanSeat)));
            if (!Prompt.IsOpen) return;

            if (TryReadSelection(out var chosen)) Play(chosen);
        }

        /// <summary>Bots answer the claim window one by one; the human is left to the action bar.</summary>
        private void RunBotResponses(GameState state)
        {
            if (Time.time < nextBotAction) return;
            nextBotAction = Time.time + botDelay;

            var phase = state.Phase;
            for (var player = 0; player < 4; player++)
            {
                if (player == humanSeat) continue;

                var options = state.GetLegalActions(player);
                if (options.Count == 0) continue;

                if (phase == GamePhase.AwaitingKongRobbery && options.All(o => o.Kind != ActionKind.Win))
                    continue;

                state.Apply(bots[player].Choose(state, player, options));
                if (state.Phase != phase) break;
            }
        }

        // -------------------------------------------------------------- prompt

        private void ShowPrompt(ActionPrompt prompt)
        {
            Prompt = prompt;
            bar.Show(prompt);
            if (!prompt.IsOpen) return;

            var hovered = bar.Hover(MouseRay());
            bar.SetHint(hovered?.Hint ?? DefaultHint(prompt));
        }

        private void ClosePrompt()
        {
            Prompt = ActionPrompt.Closed;
            bar.Hide();
        }

        private static string DefaultHint(ActionPrompt prompt) =>
            prompt.Count == 1
                ? prompt.Options[0].Hint
                : $"Press 1-{prompt.Count} or click a button";

        private void Play(LegalAction action)
        {
            ClosePrompt();
            session.State!.Apply(action);
            AfterAction();
        }

        /// <summary>Reads a number key or a click on the bar; false when the bar was not used.</summary>
        private bool TryReadSelection(out LegalAction action)
        {
            action = null!;
            if (!Prompt.IsOpen) return false;

            for (var i = 0; i < Prompt.Count && i < ActionPrompt.MaxOptions; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    return Prompt.TrySelect(Prompt.Options[i].Number, out action);

            if (!Input.GetMouseButtonDown(0)) return false;
            return bar.TryPick(MouseRay(), out action);
        }

        private static Ray MouseRay()
        {
            var camera = Camera.main;
            return camera == null ? default : camera.ScreenPointToRay(Input.mousePosition);
        }

        private void AfterAction()
        {
            if (session.State!.Phase == GamePhase.HandFinished)
            {
                waitingForNextHand = false;
                return;
            }
            Rebuild();
        }

        // ---------------------------------------------------------------- view

        private void ClearViews()
        {
            foreach (var tile in handTiles.Values) Destroy(tile.gameObject);
            handTiles.Clear();
            foreach (var tile in discardTiles) Destroy(tile.gameObject);
            discardTiles.Clear();
            foreach (var tile in meldTiles) Destroy(tile.gameObject);
            meldTiles.Clear();
            foreach (var tile in opponentTiles) Destroy(tile.gameObject);
            opponentTiles.Clear();
        }

        private void HighlightHand(bool on)
        {
            foreach (var tile in handTiles.Values) tile.SetHighlight(on);
        }

        private void Rebuild()
        {
            ClearViews();
            var state = session.State!;
            var hand = state.Players[humanSeat];

            var sorted = hand.Concealed.OrderBy(t => t.Type.Suit).ThenBy(t => t.Type.Rank).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var tile = TileObject.Create(board, "hand");
                tile.Bind(sorted[i].Type);
                tile.SetPosition(new Vector3(-0.40f + i * 0.062f, TileY, -0.52f), 0f);
                handTiles[sorted[i].InstanceId] = tile;
            }

            for (var i = 0; i < hand.Melds.Count; i++)
            {
                var meld = hand.Melds[i];
                for (var k = 0; k < meld.Tiles.Count; k++)
                {
                    var tile = TileObject.Create(board, "meld");
                    tile.Bind(meld.Tiles[k].Type);
                    tile.SetPosition(new Vector3(-0.40f + k * 0.062f, TileY, -0.41f - i * 0.078f), 0f);
                    meldTiles.Add(tile);
                }
            }

            for (var player = 0; player < 4; player++)
            {
                var discards = state.Discards.Where(d => d.Player == player).Select(d => d.Tile).ToList();
                for (var i = 0; i < discards.Count; i++)
                {
                    var tile = TileObject.Create(board, "discard");
                    tile.Bind(discards[i].Type);
                    var (origin, rowDirection, rowStep) = DiscardOrigin(player);
                    tile.SetPosition(origin + rowDirection * (i % 6) + rowStep * (i / 6), player * 90f);
                    discardTiles.Add(tile);
                }

                if (player == humanSeat) continue;
                var count = state.Players[player].Concealed.Count;
                for (var i = 0; i < count; i++)
                {
                    var tile = TileObject.Create(board, "opponent");
                    tile.BindBack();
                    var (origin, direction) = OpponentRow(player);
                    tile.SetPosition(origin + direction * i, player * 90f);
                    opponentTiles.Add(tile);
                }
            }

            HighlightHand(state.CurrentPlayer == humanSeat && state.Phase == GamePhase.AwaitingDiscard);
            status.text = DescribeState();
        }

        /// <summary>Discard blocks sit in the four quadrants of the table centre, one per player.</summary>
        private (Vector3 Origin, Vector3 Direction, Vector3 RowStep) DiscardOrigin(int player)
        {
            return player switch
            {
                0 => (new Vector3(-0.19f, TileY, -0.24f), new Vector3(0.062f, 0f, 0f), new Vector3(0f, 0f, 0.078f)),
                1 => (new Vector3(0.30f, TileY, -0.19f), new Vector3(0f, 0f, 0.062f), new Vector3(0.078f, 0f, 0f)),
                2 => (new Vector3(-0.19f, TileY, 0.30f), new Vector3(0.062f, 0f, 0f), new Vector3(0f, 0f, -0.078f)),
                _ => (new Vector3(-0.30f, TileY, -0.19f), new Vector3(0f, 0f, 0.062f), new Vector3(-0.078f, 0f, 0f))
            };
        }

        private (Vector3 Origin, Vector3 Direction) OpponentRow(int player)
        {
            return player switch
            {
                1 => (new Vector3(0.60f, TileY, -0.32f), new Vector3(0f, 0f, 0.062f)),
                2 => (new Vector3(-0.40f, TileY, 0.70f), new Vector3(0.062f, 0f, 0f)),
                _ => (new Vector3(-0.60f, TileY, -0.32f), new Vector3(0f, 0f, 0.062f))
            };
        }

        private int? PickTileUnderMouse()
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5f)) return null;

            // the collider sits on the tile root, but the body cube can still be hit while its
            // own collider is being destroyed, so look the component up the hierarchy
            var tile = hit.collider.GetComponentInParent<TileObject>();
            if (tile == null) return null;

            foreach (var entry in handTiles)
                if (entry.Value == tile) return entry.Key;
            return null;
        }

        // ---------------------------------------------------------------- text

        private string DescribeState()
        {
            var state = session.State!;
            var wind = state.SeatWindOf(humanSeat);
            var lines = new List<string>
            {
                $"Hand {session.HandsPlayed + 1}/{GameSession.TotalHands}  Round {session.RoundWind}  Your seat {wind}",
                state.Phase == GamePhase.AwaitingDiscard && state.CurrentPlayer == humanSeat
                    ? "Your turn: click a tile to discard, 1-N for the action bar, H for Hu"
                    : $"Waiting for player {state.CurrentPlayer}",
                $"Wall: {state.Wall.Remaining}",
                $"Points: {string.Join("  ", session.Scores.Select((score, player) => $"P{player}={score}"))}",
                $"Clock: {Clock(session.Elapsed)} / {Clock(session.TimeLimit)}"
                + (session.IsTimeUp ? "  time is up" : session.IsFinalQuarterHour ? "  last quarter of an hour" : string.Empty)
            };

            if (state.IsWinForbidden(humanSeat))
                lines.Add($"Hu is forbidden this hand: {state.WinForbiddenReason(humanSeat)}");

            return string.Join("\n", lines);
        }

        private static string Clock(System.TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";

        private string DescribeResult()
        {
            if (session.LastSettlement == null) return "Hand finished";
            var settlement = session.LastSettlement;
            if (settlement.WinnerPlayer < 0) return "Draw: the wall is empty";
            return $"Player {settlement.WinnerPlayer} wins\n{settlement.Explanation}\nPaid: {settlement.TotalPaid}";
        }
    }
}
