using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Replay;
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

        /// <summary>Strength of the three bots: 1 tiles only, 2 reads danger, 3 also values waits.</summary>
        [SerializeField] [Range(1, 3)] private int botLevel = 3;

        /// <summary>
        /// The HUD line. Exposed so tests and tools can read the state of the table without a screen;
        /// the mesh itself is parented to the camera and is not part of the game object hierarchy.
        /// </summary>
        public TextMesh Status => status;

        /// <summary>The bar of legal actions offered to the human. Exposed for tests and tools.</summary>
        public ActionBar Bar => bar;

        /// <summary>The Russian rulebook that opens with R. Exposed for tests and tools.</summary>
        public RulesPanel Rules => rules;

        /// <summary>The free-play table that opens with F. Exposed for tests and tools.</summary>
        public FreePlayTable FreePlay => free;

        /// <summary>The review of a saved game, while V has it open. Exposed for tests and tools.</summary>
        public ReplayCursor Replay => replay;

        /// <summary>True while the review of a saved game holds the table.</summary>
        public bool IsReplayOpen => replayOpen;

        /// <summary>The move log of this session: this is what a save file is made of.</summary>
        public SessionLog MoveLog => moveLog;

        /// <summary>File the session was last written to; empty until the first save.</summary>
        public string LastSavePath => sessionSavePath ?? string.Empty;

        /// <summary>True while the human is to choose a discard: the game is waiting for him.</summary>
        public bool IsYourTurn =>
            session?.State != null && session.State.Phase == GamePhase.AwaitingDiscard
            && session.State.CurrentPlayer == humanSeat;

        /// <summary>Tiles left in the wall; the HUD prints it and the tests read it.</summary>
        public int WallRemaining => session?.State?.Wall.Remaining ?? 0;

        /// <summary>Language the interface is drawn in.</summary>
        public Language UiLanguage => Loc.Current;

        /// <summary>The choices currently offered to the human; closed when it is not his decision.</summary>
        public ActionPrompt Prompt { get; private set; } = ActionPrompt.Closed;

        /// <summary>The hand clock of rules 3.5.7.6, 3.6.4 and 3.6.7. Exposed for tests and tools.</summary>
        public PlayClock HandClock => clock;

        /// <summary>How many tiles lie in the discard piles; a timed-out turn adds one.</summary>
        public int DiscardTileCount => session?.State?.Discards.Count ?? 0;

        /// <summary>Tile objects on the scored table: the human hand, the melds, the piles and the backs.</summary>
        public int TableTileCount => tilesById.Count;

        /// <summary>Scored tiles actually drawn right now; free play hides them all.</summary>
        public int VisibleTableTileCount =>
            tilesById.Values.Count(tile => tile != null && tile.gameObject.activeSelf);

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

        /// <summary>Corner of the HUD in camera space: the text hangs half a metre away.</summary>
        private const float HudZ = 0.5f;
        private const float HudLeft = -0.295f;
        private const float HudTop = 0.145f;

        /// <summary>Seat plates stand just above the felt, clear of the tiles behind them.</summary>
        private const float SeatPlateY = 0.02f;

        /// <summary>Size of the dark plate under the HUD, in camera space at half a metre.</summary>
        private const float HudPlateWidth = 0.30f;
        private const float HudPlateHeight = 0.115f;

        /// <summary>How much plate sticks out around the text, so the glyphs do not touch the edge.</summary>
        private const float HudPlateMargin = 0.006f;

        /// <summary>Nearly opaque: the HUD is read against a busy table, not against the sky.</summary>
        private static readonly Color HudPlateColour = new(0.05f, 0.07f, 0.09f, 0.88f);

        /// <summary>
        /// Puts the HUD in the top-left corner of the frame and sizes its plate to it. The corner is
        /// measured from the camera, so a narrow game view does not push the text off the screen.
        /// </summary>
        private void LayoutHud(Camera camera)
        {
            if (status == null) return;

            var halfHeight = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * HudZ;
            var halfWidth = halfHeight * camera.aspect;

            var left = -halfWidth + 0.012f;
            var top = halfHeight - 0.010f;
            status.transform.localPosition = new Vector3(left, top, HudZ);

            if (hudPlate == null) return;
            FitHudPlate(camera);
        }

        /// <summary>
        /// Sizes the dark plate to the text that is actually there. A fixed plate is either a black
        /// half-screen or too narrow for the longest line (the standings table at the end of a session
        /// is much wider than the turn line), so the text mesh is measured instead.
        /// </summary>
        private void FitHudPlate(Camera camera)
        {
            var bounds = status!.GetComponent<MeshRenderer>().bounds;
            if (bounds.size.sqrMagnitude <= 0f) return;

            var min = camera.transform.InverseTransformPoint(bounds.min);
            var max = camera.transform.InverseTransformPoint(bounds.max);
            var width = max.x - min.x;
            var height = max.y - min.y;
            if (width <= 0f || height <= 0f) return;

            hudPlate.localScale = new Vector3(width + HudPlateMargin * 2f, height + HudPlateMargin, 1f);
            hudPlate.localPosition = new Vector3(
                (min.x + max.x) * 0.5f,
                (min.y + max.y) * 0.5f,
                HudZ + 0.02f);
        }

        /// <summary>Index of the fresh tile being placed in the current rebuild; reset by <see cref="Rebuild"/>.</summary>
        private int dealIndex;

        /// <summary>Whether the scored tiles are on screen; free play hides them.</summary>
        private bool tilesVisible = true;

        /// <summary>The well in the middle of the table; free play covers it with its own grid.</summary>
        private GameObject well = null!;

        /// <summary>Dark plate under the HUD; resized with the frame.</summary>
        private Transform hudPlate = null!;

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
        private RulesPanel rules = null!;
        private FreePlayTable free = null!;
        private Transform table = null!;
        private Transform board = null!;
        private readonly TextMesh[] seatPlates = new TextMesh[4];
        private readonly PlayClock clock = new();

        /// <summary>Every accepted move of the session, in order; a save file is this log as text.</summary>
        private readonly SessionLog moveLog = new();

        private ReplayCursor replay = null!;
        private bool replayOpen;
        private string sessionSavePath;

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
            // The interface language comes from MAHJONG_LANG and defaults to Russian; everything below
            // builds its captions from Loc, so it has to be set before the scene is generated.
            Loc.Current = Loc.PreferredLanguage;

            var random = new System.Random(randomSeed);
            var level = BotDifficulty();
            bots = new IMahjongBot[]
            {
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level)
            };

            BuildScene();
            session = new GameSession(SessionTimeLimit());
            StartHand(random.Next());
        }

        /// <summary>
        /// Bot strength as a setting: the inspector value, overridable by MAHJONG_BOT_LEVEL
        /// so a capture run can compare the levels without touching the scene.
        /// </summary>
        private BotLevel BotDifficulty()
        {
            var raw = System.Environment.GetEnvironmentVariable("MAHJONG_BOT_LEVEL");
            var chosen = int.TryParse(raw, out var parsed) ? parsed : botLevel;
            return chosen switch
            {
                1 => BotLevel.Fast,
                2 => BotLevel.Cautious,
                _ => BotLevel.Strategic
            };
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

            // The eye sits far enough back that the whole table fits with a margin above it: the top
            // of the screen belongs to the HUD, and the far hand must not disappear behind that text.
            var eye = new Vector3(0f, 1.78f, -1.70f);
            var lookAt = new Vector3(0f, 0f, 0.08f);
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - eye, Vector3.up);
            camera.fieldOfView = 34f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

            var lightObject = new GameObject("Sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // The table is a dark wooden frame with a felt inset, so the playing surface reads as a
            // table and not as a green slab: the frame is the first "Table" object, the felt sits on
            // top of it and the tiles rest on the felt.
            var tableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableObject.name = "Table";
            tableObject.transform.position = new Vector3(0f, -0.045f, 0.08f);
            tableObject.transform.localScale = new Vector3(1.47f, 0.05f, 1.47f);
            tableObject.GetComponent<Renderer>().material.color = new Color(0.20f, 0.13f, 0.08f);
            table = tableObject.transform;

            var feltObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            feltObject.name = "Felt";
            feltObject.transform.SetParent(table, false);
            feltObject.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            feltObject.transform.localScale = new Vector3(0.925f, 0.25f, 0.925f);
            feltObject.GetComponent<Renderer>().material.color = new Color(0.09f, 0.32f, 0.20f);

            // A slightly lighter square in the middle marks the well where the discards go.
            var wellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wellObject.name = "Well";
            wellObject.transform.SetParent(table, false);
            wellObject.transform.localPosition = new Vector3(0f, 0.83f, 0f);
            wellObject.transform.localScale = new Vector3(0.52f, 0.03f, 0.52f);
            wellObject.GetComponent<Renderer>().material.color = new Color(0.07f, 0.26f, 0.16f);
            well = wellObject;

            // The table cube is scaled non-uniformly; tiles live in an unscaled container so they
            // keep the size given by TileObject instead of inheriting the table's squash.
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(transform, false);
            board = boardObject.transform;

            var statusObject = new GameObject("Status");
            // HUD text follows the camera: anchored to the table it leaves the frame as soon as the
            // camera is pitched down onto the board. It sits in the top-left corner, on its own dark
            // plate, so it never covers the far hand and stays readable over a light table.
            statusObject.transform.SetParent(camera.transform, false);
            statusObject.transform.localPosition = new Vector3(HudLeft, HudTop, HudZ);
            statusObject.transform.localRotation = Quaternion.identity;
            status = statusObject.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperLeft;
            status.alignment = TextAlignment.Left;
            status.fontSize = 64;
            // Small enough that the whole block sits above the table in the top-left corner and
            // large enough to read at 720p, where one line is about eleven pixels high.
            status.characterSize = 0.00125f;
            status.color = Color.white;
            UiFont.Assign(status);
            BuildHudPlate(camera.transform);

            bar = ActionBar.Create(camera.transform);
            rules = RulesPanel.Create(camera.transform);
            free = FreePlayTable.Create(board, randomSeed);

            BuildSeatPlates(camera.transform);
        }

        /// <summary>
        /// Four small plates with the wind of each seat, one per side, just outside that side's row of
        /// tiles. They are billboards turned towards the camera every frame: a plate lying flat on the
        /// felt was three pixels tall and unreadable, and one turned to its own player reads upside
        /// down from the only screen the game has.
        /// </summary>
        private void BuildSeatPlates(Transform cameraTransform)
        {
            var sides = new[]
            {
                new Vector3(0f, SeatPlateY, -0.63f),
                new Vector3(0.665f, SeatPlateY, -0.42f),
                new Vector3(0f, SeatPlateY, 0.72f),
                new Vector3(-0.665f, SeatPlateY, -0.42f)
            };

            for (var seat = 0; seat < 4; seat++)
            {
                var host = new GameObject($"seat-{seat}");
                host.transform.SetParent(board, false);
                host.transform.localPosition = sides[seat];

                var mesh = host.AddComponent<TextMesh>();
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.fontSize = 64;
                mesh.characterSize = 0.0075f;
                mesh.color = new Color(0.86f, 0.88f, 0.82f);
                UiFont.Assign(mesh);
                seatPlates[seat] = mesh;
            }

            FaceSeatPlates(cameraTransform);
        }

        /// <summary>Points every seat plate at the camera; a TextMesh reads when its back faces us.</summary>
        private void FaceSeatPlates(Transform cameraTransform)
        {
            var eye = cameraTransform.position;
            foreach (var plate in seatPlates)
            {
                if (plate == null) continue;
                var away = plate.transform.position - eye;
                if (away.sqrMagnitude < 1e-6f) continue;
                plate.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        /// <summary>
        /// Dark plate behind the HUD text. The interface font is a system font, so the text has no
        /// outline of its own: over the light felt and the pale tile backs it needs a backdrop. The
        /// plate is a quad turned to face the camera, the same trick the rulebook uses.
        /// </summary>
        private void BuildHudPlate(Transform cameraTransform)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "StatusPlate";
            quad.transform.SetParent(cameraTransform, false);
            // The quad mesh faces -Z and the camera looks along +Z: identity is the readable side.
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localPosition = new Vector3(HudLeft + HudPlateWidth * 0.5f,
                HudTop - HudPlateHeight * 0.5f, HudZ + 0.02f);
            quad.transform.localScale = new Vector3(HudPlateWidth, HudPlateHeight, 1f);
            Destroy(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = HudPlateColour;
            hudPlate = quad.transform;
            LayoutHud(cameraTransform.GetComponent<Camera>());
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
            // The hand is logged from the deal that produced it, so a save replays from seeds alone.
            var dealt = session.State!;
            moveLog.BeginHand(dealt.Dealer, seed, dealt.RoundWind);
            BotTable.UpdateMatchContext(session, bots);
            announcedMelds.Clear();
            ClearViews();
            Rebuild();
        }

        private void Update()
        {
            if (session.State == null) return;

            FaceSeatPlates(Camera.main != null ? Camera.main.transform : transform);
            SyncInterfaceVisibility();

            // The rulebook and the free table own the screen: while either is open the hand is
            // paused, so a player can read a rule or move tiles without losing his turn.
            if (HandleInterfaceKeys()) return;

            // Rule 3.5.6: the session clock runs during play, not during the pause between hands.
            if (!session.IsFinished && session.State.Phase != GamePhase.HandFinished)
                session.AdvanceClock(System.TimeSpan.FromSeconds(Time.deltaTime));

            if (session.IsTimeUp && session.State.Phase != GamePhase.HandFinished && !session.EndedByTimeLimit)
            {
                ClosePrompt();
                StopClock();
                session.AbandonRunningHand();
                waitingForNextHand = false;
                status.text = Loc.Get("session.timeUp") + "\n" + Text.Standings(session.Standings(), humanSeat);
                SaveReplay();
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
                    SaveReplay();
                }
                else if (Time.time >= nextHandAt)
                {
                    if (session.IsFinished)
                        status.text = Loc.Get("session.complete") + "\n" + Text.Standings(session.Standings(), humanSeat);
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

        // ----------------------------------------------------------- interface

        /// <summary>
        /// Rules panel, free play and the language switch. Returns true when the interface consumed
        /// this frame, which pauses the hand: reading a rule must not cost a player his turn.
        /// </summary>
        private bool HandleInterfaceKeys()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                ToggleLanguage();
                return false;
            }

            if (rules.IsOpen)
            {
                if (rules.HandleKeys()) rules.Close();
                return true;
            }

            // The review of a saved game pauses the hand the same way the rulebook does.
            if (replayOpen)
            {
                HandleReplayKeys();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ClosePrompt();
                StopClock();
                rules.SetOpen(true);
                return true;
            }

            if (free.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
                {
                    CloseFreePlay();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Tab)) free.NextSeat();
                if (Input.GetKeyDown(KeyCode.Backspace)) free.Reset();

                if (Input.GetMouseButtonDown(0)) free.TryClick(MouseRay(), flip: false);
                else if (Input.GetMouseButtonDown(1)) free.TryClick(MouseRay(), flip: true);

                var freeText = free.StatusText();
                if (status.text != freeText) status.text = freeText;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                SaveReplay();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                OpenReplay();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                OpenFreePlay();
                return true;
            }

            return false;
        }

        /// <summary>Switches the interface language; every caption is rebuilt from the catalogue.</summary>
        public Language ToggleLanguage()
        {
            Loc.Toggle();
            bar.Hide();
            if (replayOpen) status.text = DescribeReplay();
            else if (free.IsOpen) status.text = free.StatusText();
            else RefreshStatus();
            return Loc.Current;
        }

        /// <summary>Opens free play: the whole set on the table, movable by any seat.</summary>
        public void OpenFreePlay()
        {
            ClosePrompt();
            StopClock();
            free.Open();
            HighlightHand(false);
            SyncInterfaceVisibility();
            status.text = free.StatusText();
        }

        /// <summary>Leaves free play and returns to the scored table.</summary>
        public void CloseFreePlay()
        {
            free.Close();
            SyncInterfaceVisibility();
            status.text = DescribeState();
        }

        /// <summary>
        /// Keeps whatever the interface is showing consistent with what is on the screen, every frame.
        /// It is done from the state of the two panels rather than from the key handler, so a panel
        /// opened by a tool or a test behaves exactly like one opened by the player.
        /// </summary>
        private void SyncInterfaceVisibility()
        {
            var rulesOpen = rules.IsOpen;
            var freeOpen = free.IsOpen;

            // The page covers the middle of the screen; a HUD drawn over it reads as two texts in
            // the same place.
            var hudWanted = !rulesOpen;
            if (status != null && status.gameObject.activeSelf != hudWanted)
                status.gameObject.SetActive(hudWanted);

            // Wind plates belong to the scored table.
            var platesWanted = !rulesOpen && !freeOpen;
            foreach (var plate in seatPlates)
                if (plate != null && plate.gameObject.activeSelf != platesWanted)
                    plate.gameObject.SetActive(platesWanted);

            // The scored hand keeps lying on the table otherwise, and the free set looks like it was
            // dealt into a live hand. The tiles are hidden, not destroyed: the hand comes back as it was.
            var tilesWanted = !freeOpen;
            if (tilesVisible != tilesWanted)
            {
                SetScoredTilesVisible(tilesWanted);
                tilesVisible = tilesWanted;
            }

            if (well != null && well.activeSelf != tilesWanted) well.SetActive(tilesWanted);

            var camera = Camera.main;
            if (camera != null) LayoutHud(camera);
        }

        /// <summary>Shows or hides the scored tiles without forgetting their views.</summary>
        private void SetScoredTilesVisible(bool visible)
        {
            foreach (var tile in tilesById.Values)
                if (tile != null) tile.gameObject.SetActive(visible);
        }

        /// <summary>
        /// The HUD and the four wind plates belong to the scored table: a page that covers the screen
        /// would be drawn into by both, so the rulebook hides them.
        /// </summary>
        private void SetTableLabelsVisible(bool visible)
        {
            if (status != null) status.gameObject.SetActive(visible);
            SetSeatPlatesVisible(visible);
        }

        /// <summary>Wind plates of the four seats; free play has its own seat line in the HUD.</summary>
        private void SetSeatPlatesVisible(bool visible)
        {
            foreach (var plate in seatPlates)
                if (plate != null) plate.gameObject.SetActive(visible);
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
                ApplyMove(humanSeat, draw);
                AfterAction();
                return;
            }

            HighlightHand(true);
            UpdateHandHover();
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

            ApplyMove(player, bots[player].Choose(state, player, options));
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

                ApplyMove(player, bots[player].Choose(state, player, options));
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
                : Loc.Format("hint.bar", prompt.Count);

        private void Play(LegalAction action)
        {
            ClosePrompt();
            ApplyMove(humanSeat, action);
            if (action.Kind == ActionKind.Discard) humanDiscardedThisHand = true;
            AfterAction();
        }

        /// <summary>
        /// The only place an engine action is applied: the state changes and the move enters the log,
        /// so a session cannot quietly play a move the save file does not know about.
        /// </summary>
        private void ApplyMove(int seat, LegalAction action)
        {
            session.State!.Apply(action);
            moveLog.Record(seat, action);
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

        // ------------------------------------------------------- save and review

        /// <summary>
        /// Writes the move log of the session to a file and returns its path. Called after every
        /// finished hand, on F5 and on quit, so an interrupted session is still reviewable.
        /// </summary>
        public string SaveReplay()
        {
            if (moveLog.IsEmpty) return string.Empty;
            try
            {
                sessionSavePath ??= ReplaySavePath();
                moveLog.Save(sessionSavePath);
                return sessionSavePath;
            }
            catch (System.Exception failure)
                when (failure is IOException or System.UnauthorizedAccessException)
            {
                Debug.LogWarning($"[replay] the session could not be saved: {failure.Message}");
                return string.Empty;
            }
        }

        /// <summary>Where a save goes: MAHJONG_SAVE, else a timestamped file in the save directory.</summary>
        private static string ReplaySavePath()
        {
            var forced = System.Environment.GetEnvironmentVariable("MAHJONG_SAVE");
            if (!string.IsNullOrWhiteSpace(forced)) return forced;
            var name = $"replay-{System.DateTime.Now:yyyyMMdd-HHmmss}.mahjong";
            return Path.Combine(SaveDirectory(), name);
        }

        /// <summary>Saves live next to the player data; MAHJONG_REPLAY_DIR moves them for a capture run.</summary>
        private static string SaveDirectory()
        {
            var directory = System.Environment.GetEnvironmentVariable("MAHJONG_REPLAY_DIR");
            return string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Application.persistentDataPath, "saves")
                : directory;
        }

        /// <summary>The most recently written save, or an empty path when there is none.</summary>
        private static string NewestSave()
        {
            try
            {
                var directory = SaveDirectory();
                if (!Directory.Exists(directory)) return string.Empty;
                return Directory.GetFiles(directory, "*.mahjong")
                           .OrderByDescending(File.GetLastWriteTimeUtc)
                           .FirstOrDefault() ?? string.Empty;
            }
            catch (System.Exception failure)
                when (failure is IOException or System.UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Opens the review: the file named by MAHJONG_REPLAY, otherwise the newest save, and when
        /// nothing has been saved yet — the game that is running right now.
        /// </summary>
        public bool OpenReplay()
        {
            if (!TryReplaySource(out var log, out var problem) || log.IsEmpty)
            {
                status.text = problem.Length > 0 ? problem : Loc.Get("replay.nothing");
                return false;
            }

            ClosePrompt();
            StopClock();
            replay = new ReplayCursor(log);
            replayOpen = true;
            RenderReplay();
            return true;
        }

        /// <summary>Returns to the live table exactly where the hand was paused.</summary>
        public void CloseReplay()
        {
            if (!replayOpen) return;
            replayOpen = false;
            Rebuild();
        }

        /// <summary>Steps the review by whole moves: +1 forward, -1 back; false when it did not move.</summary>
        public bool ReplayStep(int moves)
        {
            if (!replayOpen) return false;
            var moved = false;
            for (var i = 0; i < System.Math.Abs(moves); i++)
            {
                if (!(moves > 0 ? replay.Next() : replay.Back())) break;
                moved = true;
            }

            if (moved) RenderReplay();
            return moved;
        }

        /// <summary>Steps the review by hands: +1 to the next deal, -1 to the previous one.</summary>
        public bool ReplayHand(int hands)
        {
            if (!replayOpen) return false;
            var moved = false;
            for (var i = 0; i < System.Math.Abs(hands); i++)
            {
                if (!(hands > 0 ? replay.NextHand() : replay.PreviousHand())) break;
                moved = true;
            }

            if (moved) RenderReplay();
            return moved;
        }

        public bool ReplayToStart()
        {
            if (!replayOpen) return false;
            replay.ToStart();
            RenderReplay();
            return true;
        }

        public bool ReplayToEnd()
        {
            if (!replayOpen) return false;
            replay.ToEnd();
            RenderReplay();
            return true;
        }

        /// <summary>Keys of the review; the step methods above are what a test drives instead.</summary>
        private void HandleReplayKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.V))
            {
                CloseReplay();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow)) ReplayStep(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) ReplayStep(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) ReplayHand(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) ReplayHand(-1);
            else if (Input.GetKeyDown(KeyCode.End)) ReplayToEnd();
            else if (Input.GetKeyDown(KeyCode.Home)) ReplayToStart();
        }

        private void RenderReplay()
        {
            RebuildFrom(replay.State);
            status.text = DescribeReplay();
        }

        /// <summary>The HUD of the review: where the cursor stands and which move it just passed.</summary>
        private string DescribeReplay()
        {
            var state = replay.State;
            var last = replay.LastStep;
            return string.Join("\n",
                Loc.Format("replay.title", replay.PositionText),
                last == null
                    ? Loc.Get("replay.deal")
                    : Loc.Format("replay.move", DescribeSavedMove(last)),
                Loc.Format("hud.wall", state.Wall.Remaining),
                Loc.Get("replay.keys"));
        }

        /// <summary>A saved move in the words of the interface language.</summary>
        private static string DescribeSavedMove(ReplayCursor.SavedMove move)
        {
            var tiles = string.Join(" ", move.Tiles);
            var what = move.Kind switch
            {
                ActionKind.Draw => Loc.Get("action.draw"),
                ActionKind.Discard => Loc.Format("action.discard", tiles),
                ActionKind.Chow => Loc.Format("action.chow", tiles),
                ActionKind.Pung => Loc.Format("action.pung", tiles),
                ActionKind.OpenKong or ActionKind.AddedKong or ActionKind.ClosedKong =>
                    Loc.Format("action.kong", tiles),
                ActionKind.Win => Loc.Get("action.win"),
                ActionKind.Pass => Loc.Get("action.pass"),
                _ => move.Kind.ToString()
            };

            return $"P{move.Seat + 1} {what}".TrimEnd();
        }

        /// <summary>The log to review and, if it could not be read, why.</summary>
        private bool TryReplaySource(out SessionLog log, out string problem)
        {
            log = null!;
            problem = string.Empty;

            var forced = System.Environment.GetEnvironmentVariable("MAHJONG_REPLAY");
            var path = string.IsNullOrWhiteSpace(forced) ? NewestSave() : forced;
            if (path.Length == 0)
            {
                // Nothing saved yet: the game in progress is the game to review.
                log = moveLog;
                return true;
            }

            try
            {
                log = SessionLog.Load(path);
                return true;
            }
            catch (System.Exception failure) when (failure is IOException or System.FormatException
                                                   or System.UnauthorizedAccessException)
            {
                problem = Loc.Format("replay.unreadable", failure.Message);
                return false;
            }
        }

        /// <summary>The session is saved on the way out: a game interrupted mid-hand is still reviewable.</summary>
        private void OnApplicationQuit() => SaveReplay();

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
            RebuildFrom(session.State!);
            status.text = DescribeState();
        }

        /// <summary>
        /// Lays the table out from any engine state. The live hand passes its own state; the review of
        /// a saved game passes the state the replay cursor is standing on, which is why the review
        /// needs no renderer of its own.
        /// </summary>
        private void RebuildFrom(GameState state)
        {
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
            RefreshSeatPlates(state);

            // autoSyncTransforms is off in this project: without a sync the colliders of the hand stay
            // where the tiles were before this rebuild, and a click on a tile is resolved at its old
            // place.
            Physics.SyncTransforms();
        }

        /// <summary>Names the four seats by their wind; the dealer changes every hand.</summary>
        private void RefreshSeatPlates(GameState state)
        {
            for (var seat = 0; seat < 4; seat++)
            {
                if (seatPlates[seat] == null) continue;
                var wind = Text.SeatName(state.SeatWindOf(seat));
                seatPlates[seat].text = seat == humanSeat ? wind + " ▲" : wind;
                seatPlates[seat].color = seat == humanSeat
                    ? new Color(0.98f, 0.94f, 0.72f)
                    : new Color(0.80f, 0.83f, 0.78f);
            }
        }

        /// <summary>
        /// Raises the hand tile under the cursor. It is only a hint for the player: the click itself
        /// is still resolved by the engine action list, never by the hover.
        /// </summary>
        private void UpdateHandHover()
        {
            if (handTiles.Count == 0) return;

            var hovered = PickTileUnderMouse();
            foreach (var entry in handTiles)
                if (entry.Value != null) entry.Value.SetHovered(hovered.HasValue && entry.Key == hovered.Value);
        }

        /// <summary>
        /// Rebuilds the HUD only when its text changes: the countdown makes it change once a second,
        /// which is cheap, but rebuilding it every frame would allocate a string per frame for nothing.
        /// </summary>
        /// <summary>
        /// A TextMesh rebuilds its mesh when the text changes, and the renderer bounds only follow on
        /// the next frame: measuring the plate here keeps it one frame behind the text instead of never.
        /// </summary>
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null && status != null && status.gameObject.activeSelf) FitHudPlate(camera);
        }

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
                2 => (new Vector3(-0.40f, TileY, 0.60f), new Vector3(0.062f, 0f, 0f)),
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
                Loc.Format("hud.header", session.HandsPlayed + 1, GameSession.TotalHands,
                    Text.SeatName(session.RoundWind), Text.SeatName(wind)),
                IsYourTurn ? Loc.Get("hud.yourTurn") : Loc.Format("hud.waiting", state.CurrentPlayer + 1),
                Loc.Format("hud.wall", state.Wall.Remaining),
                Loc.Format("hud.points", string.Join("  ",
                    session.Scores.Select((score, player) => $"P{player + 1}={score}"))),
                Loc.Format("hud.clock", Clock(session.Elapsed), Clock(session.TimeLimit))
                + (session.IsTimeUp ? "  " + Loc.Get("hud.timeUp")
                    : session.IsFinalQuarterHour ? "  " + Loc.Get("hud.lastQuarter") : string.Empty)
            };

            if (IsYourTurn) lines.Add(Loc.Get("hud.keys"));

            if (clock.IsRunning)
                lines.Add(Loc.Format("hud.seconds", Text.ClockLabel(clock.Window), clock.SecondsLeft));

            if (state.IsWinForbidden(humanSeat))
                lines.Add(Loc.Format("hud.huForbidden", state.WinForbiddenReason(humanSeat)));

            return string.Join("\n", lines);
        }

        private static string Clock(System.TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";

        private string DescribeResult()
        {
            if (session.LastSettlement == null) return Loc.Get("result.handFinished");
            var settlement = session.LastSettlement;
            if (settlement.WinnerPlayer < 0) return Loc.Get("result.draw");

            var winner = settlement.WinnerPlayer == humanSeat
                ? Loc.Get("result.youWin")
                : Loc.Format("result.playerWins", settlement.WinnerPlayer + 1);

            var result = session.State!.Result;
            var breakdown = result != null && result.Score.IsValidWin
                ? Text.ScoreSummary(result)
                : settlement.Explanation;

            return $"{winner}\n{breakdown}\n{Loc.Format("result.paid", settlement.TotalPaid)}";
        }
    }
}
