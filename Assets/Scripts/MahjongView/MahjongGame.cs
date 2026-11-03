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

        /// <summary>The hand clock of rules 3.5.7.6, 3.6.4 and 3.6.7. Exposed for tests and tools.</summary>
        public PlayClock HandClock => clock;

        /// <summary>How many tiles lie in the discard piles; a timed-out turn adds one.</summary>
        public int DiscardTileCount => session?.State?.Discards.Count ?? 0;

        /// <summary>Phase of the hand that is being played. Exposed for tests and tools.</summary>
        public GamePhase Phase => session?.State?.Phase ?? GamePhase.NotStarted;

        /// <summary>Seat that has to act, or the discarder while a claim window is open.</summary>
        public int CurrentSeat => session?.State?.CurrentPlayer ?? -1;

        /// <summary>
        /// Concealed tiles of the three opponents: exactly the tiles the view has to draw face down.
        /// Exposed so a test can compare the engine with the table instead of hard-coding 13.
        /// </summary>
        public int OpponentConcealedTileCount
        {
            get
            {
                if (session?.State == null) return 0;
                var total = 0;
                for (var player = 0; player < 4; player++)
                {
                    if (player == humanSeat) continue;
                    total += session.State.Players[player].Concealed.Count;
                }
                return total;
            }
        }

        /// <summary>Tiles rest on the table top, which sits at y = -0.005.</summary>
        private const float TileY = 0.01f;

        /// <summary>Extra start time per freshly created tile, so an opening hand is dealt in order.</summary>
        private const float DealStagger = 0.014f;

        /// <summary>Ceiling for the deal stagger: a wall of new tiles must not crawl in forever.</summary>
        private const float MaxDealDelay = 0.35f;

        /// <summary>Index of the fresh tile being placed in the current rebuild; reset by <see cref="Rebuild"/>.</summary>
        private int dealIndex;

        /// <summary>Meld signatures already shown, so a kong hops only once when it is declared.</summary>
        private readonly HashSet<string> announcedMelds = new();

        /// <summary>One view per engine tile, keyed by instance id: identity is what makes slides possible.</summary>
        private readonly Dictionary<int, TileObject> tilesById = new();

        /// <summary>Tiles of the human hand, keyed the same way; this is what a click can pick.</summary>
        private readonly Dictionary<int, TileObject> handTiles = new();

        private GameSession session = null!;
        private IMahjongBot[] bots = null!;
        private TextMesh status = null!;
        private ActionBar bar = null!;
        private Transform table = null!;
        private Transform board = null!;
        private readonly PlayClock clock = new();

        private float nextBotAction;
        private float nextHandAt;
        private bool waitingForNextHand;

        /// <summary>Bumped on every engine action; a new value means a new decision and a new clock window.</summary>
        private long stateVersion;

        private string clockKey = string.Empty;
        private bool humanDiscardedThisHand;
        private bool resultPresented;

        private long claimCheckVersion = -1;
        private bool humanHasRealClaim;

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
            humanDiscardedThisHand = false;
            resultPresented = false;
            stateVersion++;
            claimCheckVersion = -1;
            clock.Stop();
            clockKey = string.Empty;
            ClosePrompt();
            session.StartNextHand(seed);
            announcedMelds.Clear();
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
                StopClock();
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
                    StopClock();
                    session.CompleteHand();
                    PresentResult();
                }
                else if (Time.time >= nextHandAt)
                {
                    if (session.IsFinished) status.text = "Session complete\n" + session.Describe();
                    else StartHand(new System.Random().Next());
                }
                return;
            }

            // Rules 3.5.7.6, 3.6.4 and 3.6.7: the human is on a clock for every decision.
            SyncClock();
            TickClock(System.TimeSpan.FromSeconds(Time.deltaTime));
            if (session.State.Phase == GamePhase.HandFinished) return;
            RefreshStatus();

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
            if (action.Kind == ActionKind.Discard) humanDiscardedThisHand = true;
            AfterAction();
        }

        // ----------------------------------------------------------------- clock

        /// <summary>
        /// Keeps the hand clock in step with the table: a new decision opens a window, no decision
        /// closes it. The key changes only when the engine state changes, so the countdown is not
        /// restarted by calling this every frame.
        /// </summary>
        private void SyncClock()
        {
            var state = session.State!;
            var key = DecisionKey(state);
            if (key == clockKey) return;
            clockKey = key;

            if (key.Length == 0)
            {
                clock.Stop();
                return;
            }

            if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                clock.Restart(ClockWindow.Claim);
            else
                // Rule 3.5.7.6 gives twenty seconds to arrange the tiles; the human gets them for his
                // first discard of the hand, so a fresh hand is not rushed.
                clock.Restart(humanDiscardedThisHand ? ClockWindow.Discard : ClockWindow.Arrange);
        }

        /// <summary>Names the current decision; empty when the human is not to act.</summary>
        private string DecisionKey(GameState state)
        {
            if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                return HumanHasRealClaim(state) ? $"claim:{stateVersion}" : string.Empty;

            if (state.Phase == GamePhase.AwaitingDiscard && state.CurrentPlayer == humanSeat)
                return $"turn:{stateVersion}";

            return string.Empty;
        }

        /// <summary>
        /// True when the human has something to decide in the open claim window. A window where he can
        /// only pass is not a decision — the engine closes it by itself — so it must not put him on the
        /// clock either. Evaluated once per engine action, because the win check scores the whole hand.
        /// </summary>
        private bool HumanHasRealClaim(GameState state)
        {
            if (claimCheckVersion == stateVersion) return humanHasRealClaim;

            claimCheckVersion = stateVersion;
            humanHasRealClaim = state.GetLegalActions(humanSeat).Any(action =>
                action.Kind is not (ActionKind.Pass or ActionKind.Draw or ActionKind.Discard));
            return humanHasRealClaim;
        }

        private void TickClock(System.TimeSpan delta) => AdvanceClock(delta.TotalSeconds);

        /// <summary>
        /// Ticks the hand clock and plays the forced move when a window runs out: a claim becomes a
        /// pass and a turn becomes a discard (see <see cref="ClockExpiry"/>). Public so a test or a
        /// capture run can jump over the wait instead of sleeping for ten seconds.
        /// </summary>
        public void AdvanceClock(double seconds)
        {
            if (session.State == null || !clock.IsRunning) return;
            if (!clock.Tick(System.TimeSpan.FromSeconds(seconds))) return;

            var window = clock.Window;
            StopClock();

            var state = session.State!;
            var options = state.GetLegalActions(humanSeat);
            var forced = ClockExpiry.Choose(state, humanSeat, options,
                window == ClockWindow.Claim ? null : bots[humanSeat]);
            if (forced == null)
            {
                Debug.LogWarning($"[clock] the {window} window ran out with nothing to play.");
                return;
            }

            Debug.Log($"[clock] the {window} window ran out, playing {forced}");
            Play(forced);
        }

        private void StopClock()
        {
            clock.Stop();
            clockKey = string.Empty;
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
            stateVersion++;
            if (session.State!.Phase == GamePhase.HandFinished)
            {
                waitingForNextHand = false;
                return;
            }
            Rebuild();
        }

        /// <summary>Shows the settled winning hand and gives its tiles a clear result-state highlight.</summary>
        private void PresentResult()
        {
            if (resultPresented) return;
            resultPresented = true;
            Rebuild();
            var result = session.State!.Result;
            if (result != null)
            {
                foreach (var tile in tilesById.Values)
                    if (result.Score.IsValidWin && result.PlayerIndex == humanSeat && tile.IsFaceUp)
                        tile.SetVictoryHighlight();
            }
            status.text = DescribeResult();
        }

        // ---------------------------------------------------------------- view

        private void ClearViews()
        {
            foreach (var tile in tilesById.Values)
                if (tile != null) Destroy(tile.gameObject);
            tilesById.Clear();
            handTiles.Clear();
        }

        /// <summary>
        /// Highlights the hand when it is the human turn. Every face-up tile is reset first: a tile that
        /// left the hand has to lose the highlight instead of carrying it into the discard pile.
        /// </summary>
        private void HighlightHand(bool on)
        {
            foreach (var tile in tilesById.Values)
            {
                if (!tile.IsFaceUp) continue;
                tile.SetHighlight(on && handTiles.ContainsValue(tile));
            }
        }

        /// <summary>
        /// Lays the table out from the engine state. Tiles are not rebuilt: each one keeps its view
        /// object by instance id, so a tile that moves from the hand to the discard pile or into a meld
        /// slides there on its own (see <see cref="TileObject.Update"/>). Tiles that left the game are
        /// destroyed, new ones come out of the wall and glide into place.
        /// </summary>
        private void Rebuild()
        {
            var state = session.State!;
            var hand = state.Players[humanSeat];
            var used = new HashSet<int>();
            handTiles.Clear();
            dealIndex = 0;

            var sorted = hand.Concealed.OrderBy(t => t.Type.Suit).ThenBy(t => t.Type.Rank).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var view = Place(sorted[i], used, "hand", new Vector3(-0.40f + i * 0.062f, TileY, -0.52f), 0f);
                handTiles[sorted[i].InstanceId] = view;
            }

            // Melds get their own horizontal slots. Previously every meld started at the same x
            // coordinate, so the second declared combination slid on top of the first one. A claimed
            // tile is turned sideways: this is both the conventional mahjong marking and an explicit
            // visual phase for an open claim. A closed kong shows its outer tiles face down and the
            // middle pair face up, which is how a concealed kong is marked on a real table.
            for (var i = 0; i < hand.Melds.Count; i++)
            {
                var meld = hand.Melds[i];
                var originX = -0.57f + i * 0.285f;
                for (var k = 0; k < meld.Tiles.Count; k++)
                {
                    var tile = meld.Tiles[k];
                    var faceUp = meld.Kind != MeldKind.ClosedKong || (k == 1 || k == 2);
                    var sideways = IsMeldMarker(meld, k) ? 90f : 0f;
                    Place(tile, used, "meld", new Vector3(originX + k * 0.062f, TileY, -0.41f), sideways, faceUp);
                }
            }

            AnnounceKongs(hand.Melds);

            for (var player = 0; player < 4; player++)
            {
                var discards = state.Discards.Where(d => d.Player == player).Select(d => d.Tile).ToList();
                for (var i = 0; i < discards.Count; i++)
                {
                    var (origin, rowDirection, rowStep) = DiscardOrigin(player);
                    Place(discards[i], used, "discard",
                        origin + rowDirection * (i % 6) + rowStep * (i / 6), player * 90f);
                }

                if (player == humanSeat) continue;
                var concealed = state.Players[player].Concealed;
                var (row, direction) = OpponentRow(player);
                for (var i = 0; i < concealed.Count; i++)
                    Place(concealed[i], used, "opponent", row + direction * i, player * 90f, faceUp: false);
            }

            foreach (var stale in tilesById.Keys.Where(id => !used.Contains(id)).ToList())
            {
                Destroy(tilesById[stale].gameObject);
                tilesById.Remove(stale);
            }

            HighlightHand(state.CurrentPlayer == humanSeat && state.Phase == GamePhase.AwaitingDiscard);
            status.text = DescribeState();
        }

        /// <summary>
        /// Rebuilds the HUD only when its text changes: the countdown makes it change once a second,
        /// which is cheap, but rebuilding it every frame would allocate a string per frame for nothing.
        /// </summary>
        private void RefreshStatus()
        {
            var text = DescribeState();
            if (text != status.text) status.text = text;
        }

        /// <summary>
        /// A declared kong gets a visible beat of its own: its tiles hop once they have landed in the
        /// meld slot. The engine says nothing about "this is new", so a meld is remembered by the
        /// instance ids of its tiles; anything not seen before is the declaration of this very action.
        /// </summary>
        private void AnnounceKongs(IReadOnlyList<Meld> melds)
        {
            foreach (var meld in melds)
            {
                var key = $"{meld.Kind}:" + string.Join(",", meld.Tiles.Select(t => t.InstanceId));
                if (!announcedMelds.Add(key)) continue;
                if (!meld.IsKong) continue;

                foreach (var tile in meld.Tiles)
                {
                    if (tilesById.TryGetValue(tile.InstanceId, out var view) && view != null)
                        view.Pulse(height: 0.045f, duration: 0.26f, delay: view.LandingDelay);
                }
            }
        }

        /// <summary>
        /// Places one engine tile on the table, reusing its view object. A tile seen for the first time
        /// appears at the wall and slides to its place, which is the dealing animation as well.
        /// </summary>
        private static bool IsMeldMarker(Meld meld, int tileIndex)
        {
            // For a discard claim, the claimed tile is the last tile in the engine's meld list.
            // Added kongs likewise mark the fourth tile; a closed kong has no exposed marker.
            if (meld.Kind == MeldKind.ClosedKong) return false;
            if (meld.Kind == MeldKind.AddedKong) return tileIndex == meld.Tiles.Count - 1;
            return meld.ClaimedFromPlayer.HasValue && tileIndex == meld.Tiles.Count - 1;
        }

        private TileObject Place(Tile tile, HashSet<int> used, string name, Vector3 position, float rotationY,
            bool faceUp = true)
        {
            var isNew = false;
            if (!tilesById.TryGetValue(tile.InstanceId, out var view) || view == null)
            {
                view = TileObject.Create(board, name);
                // Spread the spawn points a little, otherwise a fresh hand starts as one cube.
                var slot = tilesById.Count % 7;
                view.SpawnAt(new Vector3(-0.18f + slot * 0.06f, TileY, 0.52f));
                tilesById[tile.InstanceId] = view;
                isNew = true;
            }

            view.gameObject.name = name;
            if (faceUp) view.SetFace(tile.Type);
            else view.SetBack();

            // A whole opening hand appears in one frame. Giving every fresh tile a slightly later
            // start turns that into a dealt sequence; a single replacement tile (index 0 here) starts
            // at once, and the stagger is capped so a wall of new tiles does not crawl.
            var delay = isNew ? Mathf.Min(dealIndex++ * DealStagger, MaxDealDelay) : 0f;
            view.SetTarget(position, rotationY, delay);
            used.Add(tile.InstanceId);
            return view;
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

            if (clock.IsRunning)
                lines.Add($"{ClockLabel(clock.Window)}: {clock.SecondsLeft} s");

            if (state.IsWinForbidden(humanSeat))
                lines.Add($"Hu is forbidden this hand: {state.WinForbiddenReason(humanSeat)}");

            return string.Join("\n", lines);
        }

        private static string Clock(System.TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";

        private static string ClockLabel(ClockWindow window) => window switch
        {
            ClockWindow.Arrange => "Arrange your tiles",
            ClockWindow.Discard => "Time to discard",
            ClockWindow.Claim => "Time to claim",
            _ => "Time"
        };

        private string DescribeResult()
        {
            if (session.LastSettlement == null) return "Hand finished";
            var settlement = session.LastSettlement;
            if (settlement.WinnerPlayer < 0) return "Draw: the wall is empty";
            var winner = settlement.WinnerPlayer == humanSeat ? "You win" : $"Player {settlement.WinnerPlayer} wins";
            return $"{winner}\n{settlement.Explanation}\nPaid: {settlement.TotalPaid}";
        }
    }
}
