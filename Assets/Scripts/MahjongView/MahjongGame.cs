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
        /// Whether the welcome screen takes the frame at launch (У-2). A capture run turns it off with
        /// MAHJONG_TITLE=0, and a play-mode test that needs a live table calls <see cref="SkipWelcome"/>
        /// before the first frame, because the table is built in Start.
        /// </summary>
        [SerializeField] private bool welcomeScreen = true;

        /// <summary>
        /// The HUD line. Exposed so tests and tools can read the state of the table without a screen;
        /// the mesh itself is parented to the camera and is not part of the game object hierarchy.
        /// </summary>
        public TextMesh Status => status;

        /// <summary>The wooden rim of the table; its colour is the palette's business (8.10).</summary>
        public Renderer TableSurface => tableRenderer;

        /// <summary>The cloth the tiles stand on; its colour is the palette's business (8.10).</summary>
        public Renderer Cloth => feltRenderer;

        /// <summary>The plate behind the HUD text; it is measured from the text the size setting draws.</summary>
        public Transform HudPlate => hudPlate;

        /// <summary>The bar of legal actions offered to the human. Exposed for tests and tools.</summary>
        public ActionBar Bar => bar;

        /// <summary>The Russian rulebook that opens with R. Exposed for tests and tools.</summary>
        public RulesPanel Rules => rules;

        /// <summary>The on-screen tray of hot-key buttons (У-6). Exposed for tests and tools.</summary>
        public ControlsPanel Controls => controls;

        /// <summary>The free-play table that opens with F. Exposed for tests and tools.</summary>
        public FreePlayTable FreePlay => free;

        /// <summary>The menu that opens with Esc: settings, saving, review. Exposed for tests and tools.</summary>
        public MenuPanel MenuPanel => menu;

        /// <summary>The menu model: rows, cursor, what a step and a pick mean. Exposed for tests.</summary>
        public GameMenu Menu => gameMenu;

        /// <summary>The end-of-hand screen (У-7). Exposed for tests and tools.</summary>
        public ResultPanel ResultPanel => resultPanel;

        /// <summary>The result screen model: what it says, how long it waits, when it is over.</summary>
        public ResultScreen Result => resultScreen;

        /// <summary>True while a finished hand is still on the screen and the next one has not been dealt.</summary>
        public bool IsResultOpen => resultScreen.IsOpen;

        /// <summary>Seat of the human; the result screen names the winner in this seat's words.</summary>
        public int HumanSeat => humanSeat;

        /// <summary>True while the menu holds the table and the hand is paused.</summary>
        public bool IsMenuOpen => menu != null && menu.IsOpen;

        /// <summary>Language, bot strength and bot speed as they are set right now.</summary>
        public GameSettings Settings => settings;

        /// <summary>The review of a saved game, while V has it open. Exposed for tests and tools.</summary>
        public ReplayCursor Replay => replay;

        /// <summary>True while the review of a saved game holds the table.</summary>
        public bool IsReplayOpen => replayOpen;

        /// <summary>The move log of this session: this is what a save file is made of.</summary>
        public SessionLog MoveLog => moveLog;

        /// <summary>File the session was last written to; empty until the first save.</summary>
        public string LastSavePath => sessionSavePath ?? string.Empty;

        /// <summary>True while the welcome screen holds the table: no hand has been dealt yet (У-2).</summary>
        public bool IsTitleOpen => titleOpen;

        /// <summary>The welcome screen model: its rows, its two pages and what a pick means.</summary>
        public TitleScreen Title => titleScreen;

        /// <summary>The welcome screen as it stands on the screen. Exposed for tests and tools.</summary>
        public TitlePanel TitlePanel => titlePanel;

        /// <summary>The session of the table: points, penalties, the deals and the hand being played.</summary>
        public GameSession Session => session;

        /// <summary>The sounds of the table (4.10). Exposed for tests and tools.</summary>
        public SoundPlayer Sound => sound;

        /// <summary>Game points of the four seats, as the score sheet has them. Exposed for tests.</summary>
        public IReadOnlyList<int> Scores => session?.Scores ?? new int[4];

        /// <summary>Hands already settled in this session. Exposed for tests and tools.</summary>
        public int HandsPlayed => session?.HandsPlayed ?? 0;

        /// <summary>True while the human is to choose a discard: the game is waiting for him.</summary>
        public bool IsYourTurn =>
            session?.State != null && session.State.Phase == GamePhase.AwaitingDiscard
            && session.State.CurrentPlayer == humanSeat;

        /// <summary>Tiles left in the wall; the HUD prints it and the tests read it.</summary>
        public int WallRemaining => session?.State?.Wall.Remaining ?? 0;

        /// <summary>The two dice on the table; the tests read what they show.</summary>
        public DiceTray Dice => dice;

        /// <summary>The opening of the current hand: dice throws, the break and the deal.</summary>
        public HandOpening Opening => opening;

        /// <summary>The marker that shows where the wall was broken; the tests read its place.</summary>
        public Transform BreakMarker => breakMarker;

        /// <summary>The live hand's state; the tests read the wall and the dice from it.</summary>
        public GameState State => session.State!;

        /// <summary>Language the interface is drawn in.</summary>
        public Language UiLanguage => Loc.Current;

        /// <summary>The choices currently offered to the human; closed when it is not his decision.</summary>
        public ActionPrompt Prompt { get; private set; } = ActionPrompt.Closed;

        /// <summary>The hand clock of rules 3.5.7.6, 3.6.4 and 3.6.7. Exposed for tests and tools.</summary>
        public PlayClock HandClock => clock;

        /// <summary>How many tiles lie in the discard piles; a timed-out turn adds one.</summary>
        public int DiscardTileCount => session?.State?.Discards.Count ?? 0;

        /// <summary>How many melds are open on the table, all four seats together (У-3).</summary>
        public int OpenMeldCount => session?.State?.Players.Sum(player => player.Melds.Count) ?? 0;

        /// <summary>How many melds the human has declared; the table shows them in its own row.</summary>
        public int HumanMeldCount => session?.State?.Players[humanSeat].Melds.Count ?? 0;

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

        /// <summary>Tiles rest on the table top; the wall and the hands all sit at this height.</summary>
        private const float TileY = TableLayout.TileY;

        /// <summary>Extra start time per tile of the opening deal, so the wall empties stack by stack.</summary>
        private const float DealStagger = 0.014f;

        /// <summary>A replacement tile waits for the deal to finish before it leaves the other end.</summary>
        private const float ReplacementDelay = 0.85f;

        /// <summary>Corner of the HUD in camera space: the text hangs half a metre away.</summary>
        private const float HudZ = 0.5f;
        private const float HudLeft = -0.295f;
        private const float HudTop = 0.145f;

        /// <summary>Seat plates stand just above the felt, clear of the tiles behind them.</summary>
        private const float SeatPlateY = 0.062f;

        /// <summary>Size of the dark plate under the HUD, in camera space at half a metre.</summary>
        private const float HudPlateWidth = 0.30f;
        private const float HudPlateHeight = 0.115f;

        /// <summary>How much plate sticks out around the text, so the glyphs do not touch the edge.</summary>
        private const float HudPlateMargin = 0.006f;

        /// <summary>Size of the HUD line at 100 %; the size of the interface multiplies it (8.10).</summary>
        private const float HudTextSize = 0.00125f;

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

        /// <summary>
        /// Delay each tile of the fresh wall waits before it leaves for a hand: its place in the
        /// deal, counted from the break. Cleared by every rebuild and refilled only for a new wall.
        /// </summary>
        private readonly Dictionary<int, float> dealDelays = new();

        /// <summary>The dice, the break and the deal as they are shown at the start of a hand.</summary>
        private HandOpening opening = HandOpening.Finished();

        /// <summary>Whether the scored tiles are on screen; free play hides them.</summary>
        private bool tilesVisible = true;

        /// <summary>The well in the middle of the table; free play covers it with its own grid.</summary>
        private GameObject well = null!;

        /// <summary>Dark plate under the HUD; resized with the frame.</summary>
        private Transform hudPlate = null!;

        /// <summary>Two dice in the middle of the table and the marker of the break in the wall.</summary>
        private DiceTray dice = null!;
        private Transform breakMarker = null!;

        /// <summary>Whether the dice belong on the table: a resumed hand is not thrown again.</summary>
        private bool diceVisible;

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

        /// <summary>True when the book was opened from the menu: closing it puts the menu back (У-5).</summary>
        private bool rulesFromMenu;
        private MenuPanel menu = null!;
        private ResultPanel resultPanel = null!;
        private readonly ResultScreen resultScreen = new();
        private FreePlayTable free = null!;
        private ControlsPanel controls = null!;

        /// <summary>The ears of the table: every knock, chime and rattle of a hand (4.10).</summary>
        private SoundPlayer sound = null!;

        /// <summary>Last mouse position the tray was hit-tested at: the tray is not re-picked every frame.</summary>
        private Vector3 lastTrayMouse = new(float.NaN, float.NaN, float.NaN);
        private Transform table = null!;

        // The surfaces of the table whose colour is the palette's business (8.10): the wooden rim, the
        // cloth, the well in the middle and the plate behind the HUD text.
        private Renderer tableRenderer = null!;
        private Renderer feltRenderer = null!;
        private Renderer wellRenderer = null!;
        private Renderer hudPlateRenderer = null!;
        private Transform board = null!;
        private readonly TextMesh[] seatPlates = new TextMesh[4];
        private readonly PlayClock clock = new();

        /// <summary>Every accepted move of the session, in order; a save file is this log as text.</summary>
        private SessionLog moveLog = new();

        private ReplayCursor replay = null!;
        private bool replayOpen;
        private GameSettings settings = null!;
        private GameMenu gameMenu = null!;

        /// <summary>The welcome screen: the way into a game, a save and the settings (У-2).</summary>
        private TitleScreen titleScreen = null!;
        private TitlePanel titlePanel = null!;
        private bool titleOpen;

        private BotLevel botsLevel;
        private string sessionSavePath;

        /// <summary>The seed of the game being played; what the welcome screen repeats and a save keeps.</summary>
        private int gameSeed;

        /// <summary>
        /// The deals of one session. Every hand after the first takes its seed from here, so a fixed
        /// seed replays the whole game (8.7) and not just its first hand.
        /// </summary>
        private System.Random dealRandom = new(12345);

        /// <summary>True until the first hand of a game is dealt: that hand is dealt from the seed itself.</summary>
        private bool firstDealPending = true;

        /// <summary>A one-line notice under the HUD: "game saved", "game resumed".</summary>
        private string statusNote = string.Empty;
        private float statusNoteUntil;

        /// <summary>Names the save file of this session; fixed at the start so every save overwrites it.</summary>
        private string sessionStamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        /// <summary>The opening beat the table has already sounded; empty after the deal (4.10).</summary>
        private OpeningStep? openingStep;

        private float nextBotAction;
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
            // Language, bot strength and bot speed have one place where they are parsed (GameSettings);
            // everything below builds its captions from Loc, so the language is set before the scene.
            settings = GameSettings.FromEnvironment(InspectorLevel());
            Loc.Current = settings.Language;
            // The palette and the size of the interface (8.10) are one place: every panel and the
            // table itself ask UiStyle for a colour instead of writing one, so they are set before the
            // scene is built and a capture run can paint the whole game light with MAHJONG_THEME.
            UiStyle.Apply(settings);
            gameMenu = new GameMenu(settings);
            bots = CreateBots(settings.Level, randomSeed);

            BuildScene();
            sound = SoundPlayer.Create(transform);
            sound.ApplyVolume(settings.SoundVolume);
            BeginSession(new System.Random(randomSeed).Next());

            // The game opens on the welcome screen (У-2). MAHJONG_TITLE=0 deals the first hand straight
            // away, which is what a capture run and a scripted check want.
            if (welcomeScreen && TitleFromEnvironment())
            {
                OpenTitle();
                return;
            }

            if (!ResumeFromEnvironment()) StartHand(TakeDealSeed());
        }

        /// <summary>
        /// Seeds a fresh session (8.7): the deals, the three bots, the session clock and the points. The
        /// bots are built from the same seed, so "repeat this game" repeats the whole table and not only
        /// the hand the player saw first.
        /// </summary>
        private void BeginSession(int seed)
        {
            gameSeed = seed;
            settings.LastSeed = seed;
            dealRandom = new System.Random(seed);
            firstDealPending = true;
            bots = CreateBots(settings.Level, seed);
            session = new GameSession(settings.SessionLimit, settings.Variant);
            BotTable.UpdateMatchContext(session, bots);
        }

        /// <summary>
        /// The seed of the next deal: the first hand of a game is dealt from the game's own seed, so the
        /// number on the welcome screen is the number that game is rebuilt from; every hand after it
        /// takes the next deal of the session.
        /// </summary>
        private int TakeDealSeed()
        {
            if (!firstDealPending) return dealRandom.Next();
            firstDealPending = false;
            return gameSeed;
        }

        /// <summary>The three bots at the requested strength, dealt from the seed of the game.</summary>
        private IMahjongBot[] CreateBots(BotLevel level, int seed)
        {
            botsLevel = level;
            var random = new System.Random(seed);
            return new IMahjongBot[]
            {
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level),
                new HeuristicBot(random.Next(), level)
            };
        }

        /// <summary>Bot strength from the inspector slider; MAHJONG_BOT_LEVEL overrides it (see GameSettings).</summary>
        private BotLevel InspectorLevel() => botLevel switch
        {
            1 => BotLevel.Fast,
            2 => BotLevel.Cautious,
            _ => BotLevel.Strategic
        };

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
            var eye = new Vector3(0f, 2.70f, -2.60f);
            var lookAt = new Vector3(0f, 0f, 0.18f);
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
            // The felt has to hold the whole square of walls plus a hand row on every side, so its
            // size is not a matter of taste: it comes from the wall layout in TableLayout.
            var feltSide = TableLayout.FeltHalf * 2f;
            var tableSide = feltSide + 0.16f;
            var tableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableObject.name = "Table";
            tableObject.transform.position = new Vector3(0f, -0.045f, 0f);
            tableObject.transform.localScale = new Vector3(tableSide, 0.05f, tableSide);
            tableRenderer = tableObject.GetComponent<Renderer>();
            table = tableObject.transform;

            var feltObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            feltObject.name = "Felt";
            feltObject.transform.SetParent(table, false);
            feltObject.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            feltObject.transform.localScale = new Vector3(feltSide / tableSide, 0.25f, feltSide / tableSide);
            feltRenderer = feltObject.GetComponent<Renderer>();

            // A slightly lighter square in the middle marks the well where the discards go.
            var wellSide = 0.66f;
            var wellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wellObject.name = "Well";
            wellObject.transform.SetParent(table, false);
            wellObject.transform.localPosition = new Vector3(0f, 0.83f, 0f);
            wellObject.transform.localScale =
                new Vector3(wellSide / tableSide, 0.03f, wellSide / tableSide);
            wellRenderer = wellObject.GetComponent<Renderer>();
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
            status.characterSize = HudTextSize * UiStyle.Scale;
            status.color = Color.white;
            UiFont.Assign(status);
            BuildHudPlate(camera.transform);

            bar = ActionBar.Create(camera.transform);
            controls = ControlsPanel.Create(camera.transform);
            rules = RulesPanel.Create(camera.transform);
            menu = MenuPanel.Create(camera.transform);
            titlePanel = TitlePanel.Create(camera.transform);
            resultPanel = Mahjong.Unity.ResultPanel.Create(camera.transform);
            free = FreePlayTable.Create(board, randomSeed);
            dice = DiceTray.Create(board);
            breakMarker = BuildBreakMarker();

            BuildSeatPlates(camera.transform);
            PaintTable();
        }

        /// <summary>
        /// Four small plates with the wind of each seat, one per side, just outside that side's row of
        /// tiles. They are billboards turned towards the camera every frame: a plate lying flat on the
        /// felt was three pixels tall and unreadable, and one turned to its own player reads upside
        /// down from the only screen the game has.
        /// </summary>
        private void BuildSeatPlates(Transform cameraTransform)
        {
            var sides = new Vector3[4];
            for (var seat = 0; seat < 4; seat++)
            {
                var plate = TableLayout.PlatePosition(seat);
                sides[seat] = new Vector3(plate.x, SeatPlateY, plate.z);
            }

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
                mesh.color = UiStyle.Of(UiRole.TextMain);
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
            hudPlateRenderer = renderer;
            hudPlate = quad.transform;
            LayoutHud(cameraTransform.GetComponent<Camera>());
        }

        private void StartHand(int seed)
        {
            session.StartNextHand(seed);
            // The hand is logged from the deal that produced it, so a save replays from seeds alone.
            var dealt = session.State!;
            moveLog.BeginHand(dealt.Dealer, seed, dealt.RoundWind);
            BotTable.UpdateMatchContext(session, bots);
            BeginOpening();
            BeginHandView();
        }

        /// <summary>
        /// Resets what belongs to one hand rather than to the session: the flags, the clock, the prompt
        /// and the tile views. Shared by a fresh deal and by a resumed hand.
        /// </summary>
        private void BeginHandView()
        {
            waitingForNextHand = false;
            humanDiscardedThisHand = false;
            resultPresented = false;
            CloseResultScreen();
            stateVersion++;
            claimCheckVersion = -1;
            clock.Stop();
            clockKey = string.Empty;
            ClosePrompt();
            announcedMelds.Clear();
            ClearViews();
            Rebuild();
        }

        private void Update()
        {
            // The welcome screen owns the table until the player starts or loads a game.
            if (titleOpen)
            {
                HandleTitleInput();
                SyncInterfaceVisibility();
                return;
            }

            if (session?.State == null) return;

            FaceSeatPlates(Camera.main != null ? Camera.main.transform : transform);
            SyncInterfaceVisibility();

            // У-6: the tray answers the cursor the way the action bar does. The ray is only cast when
            // the mouse moved or the row was rebuilt, so an idle frame costs nothing.
            var mouse = Input.mousePosition;
            if (controls != null && controls.IsOpen && controls.gameObject.activeSelf
                && mouse != lastTrayMouse)
            {
                controls.Hover(MouseRay());
                lastTrayMouse = mouse;
            }

            // The dice, the break and the deal come before the first decision of a hand: the engine
            // has already dealt, so the table waits for the show to finish before it asks for a tile.
            if (!IsMenuOpen && !rules.IsOpen) TickOpening();
            if (opening.IsRunning)
            {
                RefreshStatus();
                return;
            }

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
                    ClosePrompt();
                    StopClock();
                    session.CompleteHand();
                    PresentResult();
                    SaveReplay();
                    return;
                }

                // The result screen holds the table (У-7). The wait is ticked here and nowhere else, so
                // the menu, the rulebook, the review and the free table pause it together with the hand.
                if (resultScreen.Tick(Time.deltaTime)) NextHand();
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
            // The menu owns the screen while it is open; everything else is paused behind it.
            if (menu != null && menu.IsOpen)
            {
                HandleMenuKeys();
                return true;
            }

            // The next hand is a decision of its own: Enter, space or the button under the numbers.
            // The keys below still work, so a player can read a rule or open the menu first.
            if (resultScreen.IsOpen && resultScreen.CanContinue)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                    || Input.GetKeyDown(KeyCode.Space))
                {
                    NextHand();
                    return true;
                }

                if (Input.GetMouseButtonDown(0) && resultPanel.TryPick(MouseRay()))
                {
                    NextHand();
                    return true;
                }
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                ToggleLanguage();
                return false;
            }

            // У-6: the on-screen tray turns a click into the same actions the hot keys do.
            if (Input.GetMouseButtonDown(0) && controls.TryPick(MouseRay(), out var tile))
            {
                switch (tile.Type)
                {
                    case ControlType.Rulebook:
                        OpenRules();
                        return true;
                    case ControlType.Menu:
                        OpenMenu();
                        return true;
                    case ControlType.Replay:
                        OpenReplay();
                        return true;
                    case ControlType.Save:
                        if (session?.State != null)
                        {
                            SaveReplay();
                            RenderStatus(Loc.Get("replay.saved"));
                        }
                        return false;
                    case ControlType.Language:
                        ToggleLanguage();
                        return false;
                    case ControlType.FreePlay:
                        OpenFreePlay();
                        return true;
                }
            }

            if (rules.IsOpen)
            {
                // У-5: the page carries its own arrows and a table of contents, so a click is read
                // here as well; a turned page is not a closed book.
                if (Input.GetMouseButtonDown(0) && ClickRules(MouseRay())) return true;
                if (rules.HandleKeys() == RulesKey.Close) CloseRules();
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
                OpenRules();
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OpenMenu();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                var saved = SaveReplay();
                if (saved.Length > 0) RenderStatus(Loc.Format("replay.saved", Path.GetFileName(saved)));
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                ResumeSavedSession();
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

        /// <summary>
        /// Switches the interface language; every caption is rebuilt from the catalogue. The key goes
        /// through the same setting the menu row shows, otherwise the menu would print a language the
        /// table is not in and its next step would change nothing.
        /// </summary>
        public Language ToggleLanguage()
        {
            if (settings != null)
            {
                settings.Language = settings.Language == Language.Ru ? Language.En : Language.Ru;
                Loc.Current = settings.Language;
            }
            else
            {
                Loc.Toggle();
            }

            bar.Hide();
            // The tray is drawn from the catalogue, so it keeps the old language until it is rebuilt.
            controls.Relabel();
            if (replayOpen) status.text = DescribeReplay();
            else if (free.IsOpen) status.text = free.StatusText();
            else RefreshStatus();
            RefreshResultTexts();
            return Loc.Current;
        }

        /// <summary>
        /// Opens the rulebook (У-5). The book hangs over the paused hand; when it was opened from the
        /// menu, closing it puts the menu back where the player left it instead of dropping him at the table.
        /// </summary>
        public void OpenRules(bool fromMenu = false)
        {
            ClosePrompt();
            StopClock();
            rulesFromMenu = fromMenu;
            rules.SetOpen(true);
            SyncInterfaceVisibility();
        }

        /// <summary>Closes the book and brings back the screen it was opened from.</summary>
        public void CloseRules()
        {
            var backToMenu = rulesFromMenu;
            rulesFromMenu = false;
            rules.Close();

            if (backToMenu && menu != null)
            {
                menu.SetOpen(true);
                menu.Refresh(gameMenu);
            }

            SyncInterfaceVisibility();
            RefreshStatus();
        }

        /// <summary>
        /// Reads a click inside the open book: the two plates of the bottom line and the rows of the
        /// table of contents. Public because raw mouse input cannot be pressed from a test — the test
        /// casts the same ray a player's cursor would.
        /// </summary>
        public bool ClickRules(Ray ray)
        {
            if (rules == null || !rules.IsOpen) return false;

            var click = rules.TryPick(ray);
            switch (click.Kind)
            {
                case RulesClickKind.Previous:
                    rules.Turn(-1);
                    return true;
                case RulesClickKind.Next:
                    rules.Turn(1);
                    return true;
                case RulesClickKind.Page:
                    rules.GoToPage(click.Page);
                    return true;
                default:
                    return false;
            }
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

        // ---------------------------------------------------------------- title

        /// <summary>
        /// The welcome screen is the way the game starts. MAHJONG_TITLE=0 turns it off so a capture run
        /// or a scripted check deals the first hand straight away, the way every earlier build did.
        /// </summary>
        private static bool TitleFromEnvironment()
        {
            var raw = System.Environment.GetEnvironmentVariable("MAHJONG_TITLE");
            if (string.IsNullOrWhiteSpace(raw)) return true;
            return raw.Trim().ToLowerInvariant() is not ("0" or "false" or "no" or "off");
        }

        /// <summary>
        /// Opens the game without the welcome screen: a capture run, a tool or a play-mode test that
        /// needs a live table calls this, and it does exactly what picking "new game" does. Called
        /// before the first frame it only sets the flag, because the table is built in Start.
        /// </summary>
        public void SkipWelcome()
        {
            welcomeScreen = false;
            // The inspector seed: a capture run and a scripted check want the same table every time,
            // which is why this door does not take the deal the player chose on the screen (8.7).
            if (titleOpen) StartNewGame(new System.Random(randomSeed).Next());
        }

        /// <summary>Opens the welcome screen with the saved games of the player's folder (У-2).</summary>
        public void OpenTitle()
        {
            titleOpen = true;
            titleScreen = new TitleScreen(settings, SaveSlots.Scan(SaveDirectory()));
            titlePanel.SetOpen(true);
            titlePanel.Refresh(titleScreen);
            ClosePrompt();
            StopClock();
            bar.Hide();
            SyncInterfaceVisibility();
        }

        /// <summary>Puts the welcome screen away so the table takes the frame.</summary>
        public void CloseTitle()
        {
            titleOpen = false;
            if (titlePanel != null) titlePanel.SetOpen(false);
        }

        /// <summary>Moves the cursor through the rows of the welcome screen; the selection wraps.</summary>
        public void TitleMove(int rows)
        {
            if (!titleOpen) return;
            titleScreen.Move(rows);
            titlePanel.Refresh(titleScreen);
        }

        /// <summary>Steps the setting under the cursor and puts it into effect at once.</summary>
        public bool TitleAdjust(int delta)
        {
            if (!titleOpen) return false;
            var changed = titleScreen.Adjust(delta);
            if (changed)
            {
                ApplySettings();
                titlePanel.Refresh(titleScreen);
            }
            return changed;
        }

        /// <summary>Picks the row under the cursor: a setting steps, a command opens its screen.</summary>
        public void TitleActivate()
        {
            if (!titleOpen) return;
            var command = titleScreen.Activate();
            ApplySettings();
            titlePanel.Refresh(titleScreen);

            switch (command)
            {
                case TitleCommand.NewGame:
                    StartNewGame(DealSeedSettings.Resolve(settings.Seed));
                    break;
                case TitleCommand.Continue:
                case TitleCommand.LoadSave:
                    if (!ResumeSavedSession(titleScreen.PendingSave))
                    {
                        // The save would not load: the screen stays, with the reason under the rows.
                        titleScreen.SetStatus(Loc.Get("title.save.broken"));
                        titlePanel.Refresh(titleScreen);
                    }
                    break;
                case TitleCommand.Rules:
                    OpenRules();
                    break;
                case TitleCommand.Quit:
                    QuitGame();
                    break;
            }
        }

        /// <summary>
        /// Deals the first hand of a fresh session with the settings chosen on the welcome screen.
        /// Points, penalties, the move log and the name of the save file all start over.
        /// </summary>
        public void StartNewGame(int seed)
        {
            CloseTitle();
            BeginSession(seed);
            moveLog = new SessionLog();
            sessionSavePath = null;
            sessionStamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            StartHand(TakeDealSeed());
            SyncInterfaceVisibility();
        }

        /// <summary>Leaves the game; the session is written out first, as it is on any other exit.</summary>
        public void QuitGame()
        {
            SaveReplay();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Keys and clicks of the welcome screen; the row actions are public, so a test drives them.</summary>
        private void HandleTitleInput()
        {
            // The rulebook opened from the welcome screen closes back to it, not to a hand nobody dealt.
            if (rules.IsOpen)
            {
                if (Input.GetMouseButtonDown(0) && ClickRules(MouseRay())) return;
                if (rules.HandleKeys() == RulesKey.Close) CloseRules();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) TitleMove(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) TitleMove(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) TitleAdjust(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) TitleAdjust(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                TitleActivate();
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Back from the list of saves, and out of the game from the main page.
                if (!titleScreen.Back()) QuitGame();
                else titlePanel.Refresh(titleScreen);
            }
            else if (Input.GetMouseButtonDown(0) && titlePanel.TryPick(MouseRay(), out var row))
            {
                TitleMove(row - titleScreen.Index);
                TitleActivate();
            }
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
            var menuOpen = menu != null && menu.IsOpen;
            var welcomeOpen = titleOpen;

            // The rulebook can be read before a game is started: it is drawn over the welcome screen and
            // the rows step aside, the same way they do for the menu.
            if (titlePanel != null && titlePanel.IsOpen != (welcomeOpen && !rulesOpen))
                titlePanel.SetOpen(welcomeOpen && !rulesOpen);

            // A finished hand stays on the result screen until the next deal, but the pages that pause
            // the table are drawn over it: two dark panels on the same plane read as one broken panel.
            var resultOpen = resultScreen.IsOpen && !rulesOpen && !menuOpen && !freeOpen && !replayOpen;
            if (resultPanel != null && resultPanel.IsOpen != resultOpen) resultPanel.SetOpen(resultOpen);

            // The page covers the middle of the screen; a HUD drawn over it reads as two texts in
            // the same place. The result screen fills that middle as well and carries the numbers a
            // player would look for in the HUD.
            // The HUD is a text mesh on a dark plate of its own: hiding the text alone leaves the plate
            // in the corner of the frame, over whatever page the player is reading.
            var hudWanted = !rulesOpen && !menuOpen && !resultOpen && !welcomeOpen;
            if (status != null && status.gameObject.activeSelf != hudWanted)
                status.gameObject.SetActive(hudWanted);
            if (hudPlate != null && hudPlate.gameObject.activeSelf != hudWanted)
                hudPlate.gameObject.SetActive(hudWanted);

            // The tray rides the bottom edge of the frame and is never part of a full-screen page:
            // it is shown whenever the table is playable and hidden only while the welcome screen is up.
            if (controls != null && controls.gameObject.activeSelf != hudWanted)
                controls.gameObject.SetActive(hudWanted);

            // Wind plates belong to the scored table. The result screen is a page over the table as
            // well, and a plate is a billboard of text drawn without a depth test: left standing, it
            // prints straight through the panel.
            var platesWanted = !rulesOpen && !freeOpen && !menuOpen && !resultOpen && !welcomeOpen;
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
            // Free play covers the middle of the table with its own grid, so the dice step aside
            // with the rest of the scored layout instead of poking through it. The same goes for the
            // rulebook, the menu and the result screen: the numbers on the dice are text without a
            // depth test, so a page over the table would show them floating in the middle of it.
            var pageOpen = rulesOpen || menuOpen || resultOpen || welcomeOpen;
            if (dice != null) dice.SetShown(tilesWanted && diceVisible && !pageOpen);

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
            nextBotAction = Time.time + (float)settings.ScaledDelay(botDelay);

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
            nextBotAction = Time.time + (float)settings.ScaledDelay(botDelay);

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

        /// <summary>
        /// Takes the first meld the human has on offer, if any. A capture run and a test have nobody
        /// at the keyboard: every other window is run out and passed, so the player never claims and
        /// his own meld never appears on the table — which is exactly what a frame of the melds has to
        /// show. Returns false when there is nothing to claim.
        /// </summary>
        public bool ClaimIfPossible()
        {
            if (!Prompt.IsOpen) return false;
            foreach (var option in Prompt.Options)
            {
                if (option.Action.Kind is ActionKind.Pass or ActionKind.Win) continue;
                Play(option.Action);
                return true;
            }

            return false;
        }

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
            if (CueFor(action.Kind, seat) is { } cue) PlayCue(cue);
        }

        /// <summary>
        /// What a move sounds like (4.10). The table answers every action with its own noise, and the
        /// win is the one cue that depends on who won: the player hears his own hand in a rising chime
        /// and somebody else's in a falling one, before he has read a word of the result screen.
        /// </summary>
        private SoundCue? CueFor(ActionKind kind, int seat) => kind switch
        {
            ActionKind.Draw => SoundCue.Draw,
            ActionKind.Discard => SoundCue.Discard,
            ActionKind.Chow or ActionKind.Pung => SoundCue.Claim,
            ActionKind.OpenKong or ActionKind.AddedKong or ActionKind.ClosedKong => SoundCue.Kong,
            ActionKind.Win => seat == humanSeat ? SoundCue.Win : SoundCue.Defeat,
            _ => null
        };

        /// <summary>Plays a cue if the table has ears; a table built by a test may not.</summary>
        private void PlayCue(SoundCue cue) => sound?.Play(cue);

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
                clock.Restart(ClockWindow.Claim, settings.Windows);
            else
                // Rule 3.5.7.6 gives twenty seconds to arrange the tiles; the human gets them for his
                // first discard of the hand, so a fresh hand is not rushed. The windows themselves are
                // a table setting (У-1): the rules' own 20/10/3 or a house rule that says so on the HUD.
                clock.Restart(humanDiscardedThisHand ? ClockWindow.Discard : ClockWindow.Arrange,
                    settings.Windows);
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

        /// <summary>
        /// Shows the settled winning hand, gives its tiles a clear result-state highlight and opens the
        /// result screen. The screen is what replaced the three seconds of HUD text (У-7): the hand no
        /// longer disappears before a player has read what it was worth.
        /// </summary>
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

            resultScreen.Show(Text.HandResult(session, humanSeat), settings.Result);
            resultPanel.Refresh(resultScreen);
            SyncInterfaceVisibility();
        }

        /// <summary>
        /// Deals the next hand from the result screen, or leaves the last table standing when the
        /// session is over. Public so a button, a key and a test all go through the same door.
        /// </summary>
        public void NextHand()
        {
            if (session?.State == null) return;
            if (!resultScreen.IsOpen || !resultScreen.CanContinue) return;
            if (session.State.Phase != GamePhase.HandFinished) return;

            CloseResultScreen();
            if (session.IsFinished)
            {
                status.text = Loc.Get(session.EndedByTimeLimit ? "session.timeUp" : "session.complete")
                              + "\n" + Text.Standings(session.Standings(), humanSeat);
                SyncInterfaceVisibility();
                return;
            }

            StartHand(TakeDealSeed());
        }

        /// <summary>Puts the result screen away; the hand it describes is already in the score sheet.</summary>
        private void CloseResultScreen()
        {
            resultScreen.Hide();
            if (resultPanel != null) resultPanel.SetOpen(false);
        }

        // ------------------------------------------------------- save and review

        /// <summary>
        /// Writes the move log of the session to a file and returns its path. Called after every
        /// finished hand, on F5 and on quit, so an interrupted session is still reviewable.
        /// </summary>
        public string SaveReplay()
        {
            if (moveLog.IsEmpty) return string.Empty;
            // The clock is stamped at the save, so a resumed game lands on the same minute of the session.
            moveLog.Info = new SessionLog.SessionInfo(session.Elapsed, session.TimeLimit);
            moveLog.Variant = session.Variant;
            try
            {
                // The path is worked out on every save: MAHJONG_SAVE may be set after the game started,
                // and the file name itself is stable for the whole session (see sessionStamp).
                sessionSavePath = ReplaySavePath();
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

        /// <summary>Where a save goes: MAHJONG_SAVE, else a file named after the moment the game began.</summary>
        private string ReplaySavePath()
        {
            var forced = System.Environment.GetEnvironmentVariable("MAHJONG_SAVE");
            if (!string.IsNullOrWhiteSpace(forced)) return forced;
            return Path.Combine(SaveDirectory(), $"replay-{sessionStamp}.mahjong");
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
        private static string NewestSave() => SaveSlots.Newest(SaveDirectory());

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

        /// <summary>
        /// Continues the saved session: the same hands, the points those hands pay, the same session
        /// clock, and the hand that was in progress left where it stopped.
        /// </summary>
        public bool ResumeSavedSession() => ResumeSavedSession(ReplaySourcePath());

        /// <summary>
        /// Continues the save at <paramref name="path"/>; an empty path means the newest one. The
        /// welcome screen picks the file, so one method serves "continue", the list of saves and F9.
        /// </summary>
        public bool ResumeSavedSession(string path)
        {
            // Only a file can be resumed: the game in progress has no session clock to come back to.
            if (string.IsNullOrWhiteSpace(path)) path = ReplaySourcePath();
            if (path.Length == 0)
            {
                status.text = Loc.Get("replay.nothing");
                return false;
            }

            SessionLog log;
            try
            {
                log = SessionLog.Load(path);
            }
            catch (System.Exception failure)
                when (failure is IOException or System.FormatException or System.UnauthorizedAccessException)
            {
                status.text = Loc.Format("replay.unreadable", failure.Message);
                return false;
            }

            if (log.IsEmpty)
            {
                status.text = Loc.Get("replay.nothing");
                return false;
            }

            GameSession resumed;
            SessionRestore.Outcome outcome;
            try
            {
                resumed = SessionRestore.Restore(log, out outcome, settings.SessionLimit);
            }
            catch (System.Exception failure)
                when (failure is System.InvalidOperationException or System.ArgumentException)
            {
                Debug.LogWarning($"[replay] the saved session cannot be resumed: {failure.Message}");
                status.text = Loc.Format("replay.unreadable", failure.Message);
                return false;
            }

            session = resumed;
            // A game is under way now: the welcome screen has done its job.
            CloseTitle();
            // The loaded log becomes the session's log, so the next save holds the whole game.
            moveLog = log;
            BotTable.UpdateMatchContext(session, bots);
            replayOpen = false;
            // The wall of a resumed hand was broken before the save, so the dice are not replayed.
            opening.Skip();
            dice.SetOpen(false);
            diceVisible = false;
            BeginHandView();

            var summary = Loc.Format("session.resumed", outcome.HandsCompleted, outcome.MovesReplayed);
            if (outcome.HandInProgress)
            {
                // The first-discard window of the resumed hand is long over.
                humanDiscardedThisHand = session.State!.Discards.Count > 0;
                RenderStatus(summary);
            }
            else if (session.IsFinished)
            {
                status.text = Loc.Get(session.EndedByTimeLimit ? "session.timeUp" : "session.complete")
                              + "\n" + Text.Standings(session.Standings(), humanSeat) + "\n" + summary;
            }
            else
            {
                // The saved hand was already settled: the game goes on with the next deal.
                StartHand(TakeDealSeed());
                RenderStatus(summary);
            }

            Debug.Log($"[replay] resumed {outcome}");
            return true;
        }

        /// <summary>The table HUD with one line about the resume under it, kept for a few seconds.</summary>
        private void RenderStatus(string note)
        {
            statusNote = note;
            statusNoteUntil = Time.time + 6f;
            status.text = DescribeState();
        }

        /// <summary>MAHJONG_RESUME=1 continues the newest save instead of dealing a new game.</summary>
        private bool ResumeFromEnvironment()
        {
            var raw = System.Environment.GetEnvironmentVariable("MAHJONG_RESUME");
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var asked = raw.Trim().ToLowerInvariant() is not ("0" or "false" or "no" or "off");
            return asked && ResumeSavedSession();
        }

        /// <summary>Opens the menu: the hand is paused while the player changes the table.</summary>
        public void OpenMenu()
        {
            ClosePrompt();
            StopClock();
            gameMenu = new GameMenu(settings);
            menu.SetOpen(true);
            menu.Refresh(gameMenu);
            SyncInterfaceVisibility();
            RefreshStatus();
        }

        /// <summary>Closes the menu and puts the table HUD back the way it was.</summary>
        public void CloseMenu()
        {
            if (menu == null || !menu.IsOpen) return;
            menu.SetOpen(false);
            SyncInterfaceVisibility();
            RefreshStatus();
        }

        /// <summary>Moves the cursor through the menu rows; the selection wraps.</summary>
        public void MenuMove(int rows)
        {
            if (!menu.IsOpen) return;
            gameMenu.Move(rows);
            menu.Refresh(gameMenu);
        }

        /// <summary>Steps the setting under the cursor and puts it into effect at once.</summary>
        public bool MenuAdjust(int delta)
        {
            if (!menu.IsOpen) return false;
            var changed = gameMenu.Adjust(delta);
            if (changed) ApplySettings();
            menu.Refresh(gameMenu);
            return changed;
        }

        /// <summary>Picks the row under the cursor: a setting steps, a command opens its screen.</summary>
        public void MenuActivate()
        {
            if (!menu.IsOpen) return;
            var command = gameMenu.Activate();
            ApplySettings();
            menu.Refresh(gameMenu);

            switch (command)
            {
                case MenuCommand.Save:
                    var saved = SaveReplay();
                    CloseMenu();
                    if (saved.Length > 0) RenderStatus(Loc.Format("replay.saved", Path.GetFileName(saved)));
                    break;
                case MenuCommand.Resume:
                    CloseMenu();
                    ResumeSavedSession();
                    break;
                case MenuCommand.Review:
                    CloseMenu();
                    OpenReplay();
                    break;
                case MenuCommand.Rules:
                    CloseMenu();
                    OpenRules(fromMenu: true);
                    break;
                case MenuCommand.Close:
                    CloseMenu();
                    break;
            }
        }

        /// <summary>Puts the settings into effect: language, bot strength and the words on screen.</summary>
        private void ApplySettings()
        {
            Loc.Current = settings.Language;
            if (bots != null && botsLevel != settings.Level)
            {
                bots = CreateBots(settings.Level, gameSeed != 0 ? gameSeed : randomSeed);
                if (session != null) BotTable.UpdateMatchContext(session, bots);
            }
            if (status != null && !menu.IsOpen && !rules.IsOpen) RefreshStatus();
            // 4.10: the volume of the table follows the setting the moment the row is stepped.
            if (sound != null) sound.ApplyVolume(settings.SoundVolume);
            // 8.10: the palette and the size are the same kind of setting: stepped, they show at once.
            if (UiStyle.Theme.Id != settings.Theme.Id ||
                System.Math.Abs(UiStyle.Scale - (float)settings.UiScale) > 0.0001f)
                PaintLook();
            RefreshResultTexts();
        }

        /// <summary>
        /// Paints the table and the HUD in the palette that is in effect: the background behind the
        /// table, the wooden rim, the cloth, the well, the plate behind the HUD text, the wind names and
        /// the marker of the break. Called once the scene stands and again whenever the look changes.
        /// </summary>
        private void PaintTable()
        {
            var camera = Camera.main;
            if (camera != null) camera.backgroundColor = UiStyle.Of(UiRole.Background);

            if (tableRenderer != null) tableRenderer.material.color = UiStyle.Of(UiRole.Wood);
            if (feltRenderer != null) feltRenderer.material.color = UiStyle.Of(UiRole.Felt);
            // The well is the same cloth a shade deeper, so the middle of the table reads as a pit.
            if (wellRenderer != null) wellRenderer.material.color = UiStyle.Sink(UiStyle.Of(UiRole.Felt), 0.3f);
            if (hudPlateRenderer != null) hudPlateRenderer.material.color = UiStyle.Of(UiRole.Surface, 0.88f);
            if (status != null)
            {
                status.color = UiStyle.Of(UiRole.TextMain);
                // The size of the interface reaches the HUD as well: the plate is measured from the
                // text, so a bigger line gets a bigger plate by itself.
                status.characterSize = HudTextSize * UiStyle.Scale;
                if (camera != null) LayoutHud(camera);
            }

            var marker = breakMarker != null ? breakMarker.GetComponent<Renderer>() : null;
            if (marker != null) marker.material.color = UiStyle.Of(UiRole.Accent);

            if (session?.State != null) RefreshSeatPlates(session.State);
        }

        /// <summary>
        /// Puts the look in effect on everything that is already drawn. The panels watch the revision of
        /// <see cref="UiStyle"/> on their own and repaint themselves on their next frame; what they
        /// cannot watch is the table, so the table repaints the tiles it holds.
        /// </summary>
        public void PaintLook()
        {
            UiStyle.Apply(settings);
            PaintTable();
            foreach (var tile in tilesById.Values)
                if (tile != null) tile.Repaint();
            foreach (var tile in free.Views)
                if (tile != null) tile.Repaint();
        }

        /// <summary>
        /// Rewrites an open result screen in the language that is now in effect. Its countdown keeps
        /// running from where it was: switching the language must not give a player a fresh wait.
        /// </summary>
        private void RefreshResultTexts()
        {
            if (session?.State == null || resultPanel == null || !resultScreen.IsOpen) return;
            resultScreen.Relocalize(Text.HandResult(session, humanSeat));
            resultPanel.Refresh(resultScreen);
        }

        /// <summary>Keys of the menu; the row actions are public, so a test drives them without a keyboard.</summary>
        private void HandleMenuKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) MenuMove(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) MenuMove(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) MenuAdjust(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) MenuAdjust(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) MenuActivate();
            else if (Input.GetMouseButtonDown(0) && menu.TryPick(MouseRay(), out var row))
            {
                MenuMove(row - gameMenu.Index);
                MenuActivate();
            }
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

            var path = ReplaySourcePath();
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

        /// <summary>Which file the review or a resume reads: MAHJONG_REPLAY, else the newest save.</summary>
        private static string ReplaySourcePath()
        {
            var forced = System.Environment.GetEnvironmentVariable("MAHJONG_REPLAY");
            return string.IsNullOrWhiteSpace(forced) ? NewestSave() : forced;
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
        /// Highlights the hand when it is the human turn. Every tile is reset first: a tile that left the
        /// hand has to lose the highlight instead of carrying it into the discard pile. A tile that is
        /// still waiting in the wall remembers the wish and picks it up when it shows its face.
        /// </summary>
        private void HighlightHand(bool on)
        {
            foreach (var tile in tilesById.Values)
            {
                if (tile == null) continue;
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
            dealDelays.Clear();

            // The wall is laid out first, because a tile that is dealt has to be seen leaving its own
            // place in the wall: on a real table nothing arrives out of thin air (3.5.7.4).
            PlaceWall(state, used);

            var sorted = hand.Concealed.OrderBy(t => t.Type.Suit).ThenBy(t => t.Type.Rank).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var view = Place(sorted[i], used, "hand", TableLayout.HandPosition(humanSeat, i),
                    TableLayout.HandRotationY(humanSeat));
                handTiles[sorted[i].InstanceId] = view;
            }

            // Every declared meld is exposed, not just the human's: a meld is public information and
            // it is what a player reads the table from. A claimed tile is turned sideways, which is
            // both the conventional mahjong marking and an explicit visual phase for an open claim.
            // A closed kong shows its outer tiles face down and the middle pair face up, which is how
            // a concealed kong is marked on a real table.
            var allMelds = new List<Meld>();
            for (var seat = 0; seat < 4; seat++)
            {
                var melds = state.Players[seat].Melds;
                for (var i = 0; i < melds.Count; i++)
                {
                    var meld = melds[i];
                    allMelds.Add(meld);
                    // The newest meld of a seat stands a step higher: a meld is declared by a bot as
                    // well, and a player has to see the declaration, not find it on the next calm turn.
                    var stand = TableLayout.MeldStand(i, melds.Count);
                    for (var k = 0; k < meld.Tiles.Count; k++)
                    {
                        var tile = meld.Tiles[k];
                        var faceUp = meld.Kind != MeldKind.ClosedKong || (k == 1 || k == 2);
                        var sideways = IsMeldMarker(meld, k) ? 90f : 0f;
                        var place = TableLayout.MeldPosition(seat, i, k);
                        Place(tile, used, "meld", place + new Vector3(0f, stand, 0f),
                            TableLayout.HandRotationY(seat) + sideways, faceUp);
                    }
                }
            }

            AnnounceKongs(allMelds);

            for (var player = 0; player < 4; player++)
            {
                var discards = state.Discards.Where(d => d.Player == player).Select(d => d.Tile).ToList();
                for (var i = 0; i < discards.Count; i++)
                    Place(discards[i], used, "discard", TableLayout.DiscardPosition(player, i),
                        TableLayout.DiscardRotationY(player));

                // A flower is exposed face up beside the player who drew it (3.5.7.6): the tile is
                // out of the hand, but it is still one of the 144 and it stays on the table.
                var flowers = state.Players[player].Flowers;
                for (var i = 0; i < flowers.Count; i++)
                {
                    Place(flowers[i], used, "flower", TableLayout.FlowerPosition(player, i),
                        TableLayout.HandRotationY(player));
                }

                if (player == humanSeat) continue;
                // A hand that is not the human's stands outside its own wall, like the human's own.
                var concealed = state.Players[player].Concealed;
                for (var i = 0; i < concealed.Count; i++)
                {
                    Place(concealed[i], used, "opponent", TableLayout.HandPosition(player, i),
                        TableLayout.HandRotationY(player), faceUp: false);
                }
            }

            PlaceBreakMarker(state);

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

        /// <summary>
        /// A short bar on the felt at the break. On a real table the wall is pushed apart there and
        /// the gap stays visible for the whole hand, which is how everybody sees where the deal
        /// started and where the replacement tiles come from.
        /// </summary>
        private Transform BuildBreakMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "BreakMarker";
            marker.transform.SetParent(board, false);
            Destroy(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = UiStyle.Of(UiRole.Accent);
            marker.SetActive(false);
            return marker.transform;
        }

        /// <summary>Puts the break marker at the gap the dice opened and hides it once the deal is over.</summary>
        private void PlaceBreakMarker(GameState state)
        {
            if (breakMarker == null) return;

            var slot = WallGeometry.SlotAt(state.Wall.BreakIndex);
            var position = TableLayout.WallPosition(slot);
            var inward = slot.Side switch
            {
                0 => Vector3.forward,
                2 => Vector3.back,
                3 => Vector3.right,
                _ => Vector3.left
            };

            breakMarker.position = position + inward * (TileObject.Width * 0.75f) + Vector3.up * 0.001f;
            breakMarker.localRotation = Quaternion.identity;
            breakMarker.localScale = slot.Side is 1 or 3
                ? new Vector3(0.009f, 0.002f, TileObject.Width * 0.9f)
                : new Vector3(TileObject.Width * 0.9f, 0.002f, 0.009f);
            breakMarker.gameObject.SetActive(!opening.IsDone);
        }

        /// <summary>
        /// Starts the opening of a hand: two dice throws, the break and then the deal, at the table's
        /// own pace. The engine has already dealt, so this only decides when the tiles may move.
        /// </summary>
        private void BeginOpening()
        {
            var state = session.State!;
            opening = new HandOpening(state.DealerRoll, state.BreakRoll, state.DiceThrower, state.BreakStacks)
            {
                Pace = OpeningPace()
            };
            // A capture run has nobody to watch the dice, and MAHJONG_OPENING_SPEED skips the show.
            // The dice stay on the table afterwards either way: their numbers are what explains the
            // gap in the wall for the rest of the hand (3.5.7.5).
            if (opening.Pace <= 0d) opening.Skip();
            dice.SetOpen(true);
            var shown = opening.IsDone ? opening.Second : opening.First;
            dice.Settle(shown.First, shown.Second);
            diceVisible = true;
            // 4.10: the first throw rattles as the tile wall goes up; the later beats of the opening
            // sound as they arrive (see TickOpening), so a skipped show is silent and a slow one is
            // heard beat by beat.
            openingStep = opening.Step;
            if (!opening.IsDone) PlayCue(SoundCue.Dice);
        }

        /// <summary>
        /// Opening pace from <c>MAHJONG_OPENING_SPEED</c>: it is a speed, so 2 shows the dice twice as
        /// fast and 0 skips them (see <see cref="HandOpening.PaceFromSpeed"/>).
        /// </summary>
        private static double OpeningPace()
        {
            var raw = System.Environment.GetEnvironmentVariable("MAHJONG_OPENING_SPEED");
            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed)
                ? HandOpening.PaceFromSpeed(speed)
                : 1d;
        }

        /// <summary>
        /// The visible opening: the dice tumble, the second throw lands, the break marker lights up and
        /// then the tiles leave the wall. While it runs the table takes no clicks — the hand tiles are
        /// still standing in the wall, so a click there would not mean what the player sees.
        /// </summary>
        private void TickOpening()
        {
            if (opening.IsDone) return;

            if (opening.Advance(System.TimeSpan.FromSeconds(Time.deltaTime).TotalSeconds))
            {
                // A beat changed: the deal may start now, so lay the table out again and let the hand
                // tiles set off from their places in the wall.
                if (opening.Step == OpeningStep.Deal) Rebuild();
            }

            // Every beat of the opening has its own noise: the second throw, the break of the wall and
            // the tiles leaving it. The sound follows the step, so it cannot sound ahead of the show.
            if (opening.Step != openingStep)
            {
                openingStep = opening.Step;
                switch (openingStep)
                {
                    case OpeningStep.SecondRoll:
                        PlayCue(SoundCue.Dice);
                        break;
                    case OpeningStep.Break:
                        PlayCue(SoundCue.Break);
                        break;
                    case OpeningStep.Deal:
                        PlayCue(SoundCue.Deal);
                        break;
                }
            }

            dice.Show(opening);
            // The dice stay where they landed for the rest of the hand: on a real table nobody picks
            // them up until the hand is over, and the numbers explain the gap in the wall.
            if (opening.Step == OpeningStep.Break) PlaceBreakMarker(session.State!);
        }

        /// <summary>Names the four seats by their wind; the dealer changes every hand.</summary>
        private void RefreshSeatPlates(GameState state)
        {
            for (var seat = 0; seat < 4; seat++)
            {
                if (seatPlates[seat] == null) continue;
                var wind = Text.SeatName(state.SeatWindOf(seat));
                seatPlates[seat].text = seat == humanSeat ? wind + " ▲" : wind;
                // The player's own seat is the gold line of the palette, the others are muted text.
                seatPlates[seat].color = seat == humanSeat
                    ? UiStyle.Of(UiRole.Accent)
                    : UiStyle.Of(UiRole.TextMuted);
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

        /// <summary>
        /// Lays out the whole wall from the engine: every tile of the set stands in its own slot,
        /// face down, two tiles high. Tiles that have left the wall are placed by the caller.
        ///
        /// On a fresh hand the views do not exist yet, so they are created here, in their slots. That
        /// is what makes the deal visible: the tile a player takes is already standing in the wall and
        /// simply slides from there to its place in the hand.
        /// </summary>
        private void PlaceWall(GameState state, HashSet<int> used)
        {
            var wall = state.Wall;
            foreach (var tile in wall.TilesInRingOrder)
            {
                var ring = wall.RingOf(tile) ?? 0;
                var slot = WallGeometry.SlotAt(ring);
                var position = TableLayout.WallPosition(slot);
                var rotation = TableLayout.WallRotationY(slot);

                if (!tilesById.TryGetValue(tile.InstanceId, out var view) || view == null)
                {
                    view = TileObject.Create(board, "wall");
                    view.SpawnAt(position);
                    view.SetBack();
                    tilesById[tile.InstanceId] = view;
                    // Nobody checks the piles before the deal, but a fresh deal must not look like one
                    // cube arriving everywhere at once: each tile leaves the wall in its own beat.
                    dealDelays[tile.InstanceId] = DealDelayOf(ring, wall);
                }

                if (!wall.IsStanding(tile)) continue;   // it is in a hand or in a pile, placed below
                view.SetBack();
                view.gameObject.name = "wall";
                view.SetTarget(position, rotation);
                used.Add(tile.InstanceId);
            }
        }

        /// <summary>
        /// How long a tile waits before it leaves the wall. The tiles of a hand are taken in ring
        /// order from the break, so the distance to the break is the place in the deal; a flower
        /// replacement comes from the other end of the wall, after everyone has their tiles.
        /// </summary>
        private float DealDelayOf(int ring, Wall wall)
        {
            const int dealtTiles = 53;
            var forward = (ring - wall.BreakIndex + WallGeometry.TileCount) % WallGeometry.TileCount;
            var backward = (wall.BreakIndex - 1 - ring + WallGeometry.TileCount) % WallGeometry.TileCount;

            if (forward < dealtTiles) return forward * DealStagger;
            if (backward < 24) return ReplacementDelay + backward * DealStagger;
            return 0f;
        }

        private TileObject Place(Tile tile, HashSet<int> used, string name, Vector3 position, float rotationY,
            bool faceUp = true)
        {
            if (!tilesById.TryGetValue(tile.InstanceId, out var view) || view == null)
            {
                // A tile the wall does not know about: the free table and a resumed hand build their
                // own. It appears in the middle and slides out, there is no place in the wall for it.
                view = TileObject.Create(board, name);
                view.SpawnAt(new Vector3(0f, TileY, 0f));
                tilesById[tile.InstanceId] = view;
            }

            view.gameObject.name = name;

            // A tile that has just left the wall waits its turn in the deal; anything later moves at
            // once, which is what makes a drawn tile fly straight out of the wall into the hand.
            var delay = dealDelays.TryGetValue(tile.InstanceId, out var wait)
                ? wait + (float)opening.TimeUntilDeal
                : 0f;

            // The place comes first: whether the tile may show its face right now depends on how long
            // it still has to stand in the wall, and only the target knows that (see TileObject.SetFace).
            view.SetTarget(position, rotationY, delay);
            if (faceUp) view.SetFace(tile.Type);
            else view.SetBack();

            used.Add(tile.InstanceId);
            return view;
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
                Loc.Format("hud.dice", state.DealerRoll.ToString(),
                    Text.SeatName(state.SeatWindOf(state.DiceThrower)), state.BreakStacks),
                Loc.Format("hud.points", string.Join("  ",
                    session.Scores.Select((score, player) => $"P{player + 1}={score}"))),
                Loc.Format("hud.clock", Clock(session.Elapsed),
                    session.HasTimeLimit ? Clock(session.TimeLimit) : Loc.Get("hud.noClock"))
                + (session.IsTimeUp ? "  " + Loc.Get("hud.timeUp")
                    : session.IsFinalQuarterHour ? "  " + Loc.Get("hud.lastQuarter") : string.Empty)
                + (SessionLengthSettings.FollowsRules(session.TimeLimit)
                    ? string.Empty : "  " + Loc.Get("hud.notByRules")),
                Loc.Format("hud.rules", Text.VariantName(session.Variant))
                + (ClockPaceSettings.FollowsRules(settings.ClockPace)
                    ? string.Empty : "  " + Loc.Get("hud.notByRules"))
            };

            // The key list lives in the menu hint and in the rulebook: on the HUD it was the widest
            // line by far, and the plate it forced covered the far wall.

            if (clock.IsRunning)
                lines.Add(clock.IsLimited
                    ? Loc.Format("hud.seconds", Text.ClockLabel(clock.Window), clock.SecondsLeft)
                    : Loc.Format("hud.secondsNone", Text.ClockLabel(clock.Window)));

            if (state.IsWinForbidden(humanSeat))
                lines.Add(Loc.Format("hud.huForbidden", state.WinForbiddenReason(humanSeat)));

            if (statusNote.Length > 0 && Time.time < statusNoteUntil) lines.Add(statusNote);

            return string.Join("\n", lines);
        }

        private static string Clock(System.TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }
}
