using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
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

        private readonly Dictionary<int, TileObject> handTiles = new();
        private readonly List<TileObject> discardTiles = new();
        private readonly List<TileObject> meldTiles = new();
        private readonly List<TileObject> opponentTiles = new();

        private GameSession session = null!;
        private IMahjongBot[] bots = null!;
        private TextMesh status = null!;
        private Transform table = null!;
        private float nextBotAction;
        private float nextHandAt;
        private bool waitingForNextHand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<MahjongGame>() != null) return;
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
            session = new GameSession();
            StartHand(random.Next());
        }

        private void BuildScene()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0.75f, -0.95f);
                camera.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            }

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

            var statusObject = new GameObject("Status");
            statusObject.transform.SetParent(transform, false);
            statusObject.transform.position = new Vector3(0f, 0.55f, 0.62f);
            statusObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            status = statusObject.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperCenter;
            status.alignment = TextAlignment.Center;
            status.fontSize = 64;
            status.characterSize = 0.012f;
            status.color = Color.white;
        }

        private void StartHand(int seed)
        {
            waitingForNextHand = false;
            session.StartNextHand(seed);
            ClearViews();
            Rebuild();
        }

        private void Update()
        {
            if (session.State == null) return;

            if (session.State.Phase == GamePhase.HandFinished)
            {
                if (!waitingForNextHand)
                {
                    waitingForNextHand = true;
                    nextHandAt = Time.time + 3f;
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
            HighlightHand(true);

            var declarable = options.FirstOrDefault(option => option.Kind == ActionKind.Win);
            if (declarable != null && Input.GetKeyDown(KeyCode.H))
            {
                state.Apply(declarable);
                AfterAction();
                return;
            }

            var draw = options.FirstOrDefault(option => option.Kind == ActionKind.Draw);
            if (draw != null)
            {
                state.Apply(draw);
                AfterAction();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            var picked = PickTileUnderMouse();
            if (!picked.HasValue) return;

            var discard = options.FirstOrDefault(option =>
                option.Kind == ActionKind.Discard && option.Tile?.InstanceId == picked.Value);
            if (discard == null) return;

            state.Apply(discard);
            AfterAction();
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

        private void HandleClaims()
        {
            if (Time.time < nextBotAction) return;
            nextBotAction = Time.time + botDelay;

            var state = session.State!;
            var claiming = state.Phase;
            for (var player = 0; player < 4; player++)
            {
                var options = state.GetLegalActions(player);
                if (options.Count == 0) continue;

                if (claiming == GamePhase.AwaitingKongRobbery && options.All(o => o.Kind != ActionKind.Win))
                    continue;

                LegalAction action;
                if (player == humanSeat)
                {
                    action = options.FirstOrDefault(o => o.Kind == ActionKind.Win)
                             ?? options.First(o => o.Kind == ActionKind.Pass);
                }
                else
                {
                    action = bots[player].Choose(state, player, options);
                }

                state.Apply(action);
                if (state.Phase != claiming) break;
            }

            AfterAction();
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
                var tile = TileObject.Create(table, "hand");
                tile.Bind(sorted[i].Type);
                tile.SetPosition(new Vector3(-0.36f + i * 0.057f, 0.03f, -0.52f), 0f);
                handTiles[sorted[i].InstanceId] = tile;
            }

            for (var i = 0; i < hand.Melds.Count; i++)
            {
                var meld = hand.Melds[i];
                for (var k = 0; k < meld.Tiles.Count; k++)
                {
                    var tile = TileObject.Create(table, "meld");
                    tile.Bind(meld.Tiles[k].Type);
                    tile.SetPosition(new Vector3(-0.36f + k * 0.05f, 0.03f, -0.38f - i * 0.05f), 0f);
                    meldTiles.Add(tile);
                }
            }

            for (var player = 0; player < 4; player++)
            {
                var discards = state.Discards.Where(d => d.Player == player).Select(d => d.Tile).ToList();
                for (var i = 0; i < discards.Count; i++)
                {
                    var tile = TileObject.Create(table, "discard");
                    tile.Bind(discards[i].Type);
                    var (origin, rowDirection) = DiscardOrigin(player);
                    tile.SetPosition(origin + rowDirection * (i % 6) + new Vector3(0f, 0f, 0.05f * (i / 6)), player * 90f);
                    discardTiles.Add(tile);
                }

                if (player == humanSeat) continue;
                var count = state.Players[player].Concealed.Count;
                for (var i = 0; i < count; i++)
                {
                    var tile = TileObject.Create(table, "opponent");
                    tile.BindBack();
                    var (origin, direction) = OpponentRow(player);
                    tile.SetPosition(origin + direction * i + new Vector3(0f, 0.02f, 0f), player * 90f);
                    opponentTiles.Add(tile);
                }
            }

            HighlightHand(state.CurrentPlayer == humanSeat && state.Phase == GamePhase.AwaitingDiscard);
            status.text = DescribeState();
        }

        private (Vector3 Origin, Vector3 Direction) DiscardOrigin(int player)
        {
            return player switch
            {
                0 => (new Vector3(-0.14f, 0.03f, -0.12f), new Vector3(1f, 0f, 0f)),
                1 => (new Vector3(0.20f, 0.03f, -0.10f), new Vector3(0f, 0f, 1f)),
                2 => (new Vector3(-0.10f, 0.03f, 0.22f), new Vector3(-1f, 0f, 0f)),
                _ => (new Vector3(-0.24f, 0.03f, -0.10f), new Vector3(0f, 0f, 1f))
            };
        }

        private (Vector3 Origin, Vector3 Direction) OpponentRow(int player)
        {
            return player switch
            {
                1 => (new Vector3(0.74f, 0.03f, -0.30f), new Vector3(0f, 0f, 0.055f)),
                2 => (new Vector3(-0.30f, 0.03f, 0.74f), new Vector3(0.055f, 0f, 0f)),
                _ => (new Vector3(-0.74f, 0.03f, -0.30f), new Vector3(0f, 0f, 0.055f))
            };
        }

        private int? PickTileUnderMouse()
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5f)) return null;

            var tile = hit.collider.GetComponent<TileObject>();
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
                    ? "Your turn: click a tile to discard, H to declare Hu"
                    : $"Waiting for player {state.CurrentPlayer}",
                $"Wall: {state.Wall.Remaining}",
                $"Points: {string.Join("  ", session.Scores.Select((score, player) => $"P{player}={score}"))}"
            };
            return string.Join("\n", lines);
        }

        private string DescribeResult()
        {
            if (session.LastSettlement == null) return "Hand finished";
            var settlement = session.LastSettlement;
            if (settlement.WinnerPlayer < 0) return "Draw: the wall is empty";
            return $"Player {settlement.WinnerPlayer} wins\n{settlement.Explanation}\nPaid: {settlement.TotalPaid}";
        }
    }
}
