using System.Linq;
using Mahjong.Controls;
using Mahjong.Rules;
using NUnit.Framework;

namespace Mahjong.EditorTests
{
    /// <summary>
    /// The action bar is driven by a pure C# prompt, so its contents can be checked without
    /// entering Play mode. These cases pin the behaviour the 3D bar relies on.
    /// </summary>
    public sealed class ActionPromptTests
    {
        private static Tile Tile(TileSuit suit, int rank, int id) => new(new TileType(suit, rank), id);

        private static LegalAction Action(ActionKind kind, Tile? tile = null, params Tile[] required) =>
            new(kind, 1, tile, required, $"{kind} of {tile?.Type}");

        [Test]
        public void ClaimWindowIsOrderedByPriorityAndNumberedFromOne()
        {
            var discard = Tile(TileSuit.Characters, 5, 40);
            var prompt = ActionPrompt.FromLegalActions(new[]
            {
                Action(ActionKind.Pass),
                Action(ActionKind.Chow, discard, Tile(TileSuit.Characters, 3, 1), Tile(TileSuit.Characters, 4, 2)),
                Action(ActionKind.Pung, discard, Tile(TileSuit.Characters, 5, 3), Tile(TileSuit.Characters, 5, 4)),
                Action(ActionKind.Win, discard)
            });

            Assert.IsTrue(prompt.IsOpen);
            CollectionAssert.AreEqual(
                new[] { ActionKind.Win, ActionKind.Pung, ActionKind.Chow, ActionKind.Pass },
                prompt.Options.Select(option => option.Action.Kind).ToArray(),
                "Hu comes first, the pass is last of all.");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, prompt.Options.Select(option => option.Number).ToArray());
            CollectionAssert.AreEqual(new[] { "Hu", "Pung 5c", "Chow 5c", "Pass" },
                prompt.Options.Select(option => option.Label).ToArray());
        }

        [Test]
        public void ChowsAreOrderedByTheirLowestRank()
        {
            var discard = Tile(TileSuit.Bamboo, 5, 50);
            var prompt = ActionPrompt.FromLegalActions(new[]
            {
                Action(ActionKind.Chow, discard, Tile(TileSuit.Bamboo, 4, 1), Tile(TileSuit.Bamboo, 6, 2)),
                Action(ActionKind.Chow, discard, Tile(TileSuit.Bamboo, 3, 3), Tile(TileSuit.Bamboo, 4, 4)),
                Action(ActionKind.Chow, discard, Tile(TileSuit.Bamboo, 5, 5), Tile(TileSuit.Bamboo, 6, 6)),
                Action(ActionKind.Pass)
            });

            var firstRanks = prompt.Options
                .Where(option => option.Action.Kind == ActionKind.Chow)
                .Select(option => option.Action.RequiredTiles.Select(tile => tile.Type.Rank).Min())
                .ToArray();
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, firstRanks, "The same hand must always show the same order.");
        }

        [Test]
        public void APassOnItsOwnOpensNothing()
        {
            var prompt = ActionPrompt.FromLegalActions(new[] { Action(ActionKind.Pass) });

            Assert.IsFalse(prompt.IsOpen, "A window where the only choice is to pass is not a decision.");
            Assert.IsFalse(prompt.RequiresChoice);
            Assert.AreEqual(0, prompt.Count);
        }

        [Test]
        public void DiscardsAndDrawsAreNotButtons()
        {
            var hand = new[]
            {
                Action(ActionKind.Discard, Tile(TileSuit.Dots, 1, 7)),
                Action(ActionKind.Draw),
                Action(ActionKind.ClosedKong, Tile(TileSuit.Dots, 9, 8),
                    Tile(TileSuit.Dots, 9, 8), Tile(TileSuit.Dots, 9, 9), Tile(TileSuit.Dots, 9, 10), Tile(TileSuit.Dots, 9, 11)),
                Action(ActionKind.Win)
            };

            var prompt = ActionPrompt.FromLegalActions(hand);

            CollectionAssert.AreEqual(new[] { ActionKind.Win, ActionKind.ClosedKong },
                prompt.Options.Select(option => option.Action.Kind).ToArray(),
                "Discarding is a tile click and drawing is automatic, so neither becomes a button.");
        }

        [Test]
        public void DiscardPhaseAloneOpensNothing()
        {
            var prompt = ActionPrompt.FromLegalActions(new[]
            {
                Action(ActionKind.Discard, Tile(TileSuit.Dots, 1, 7)),
                Action(ActionKind.Discard, Tile(TileSuit.Dots, 2, 8))
            });

            Assert.IsFalse(prompt.IsOpen, "With no claim and no win there is nothing to put on the bar.");
        }

        [Test]
        public void SelectionWorksByNumberAndByKind()
        {
            var discard = Tile(TileSuit.Dots, 5, 30);
            var prompt = ActionPrompt.FromLegalActions(new[]
            {
                Action(ActionKind.Pass),
                Action(ActionKind.Pung, discard, Tile(TileSuit.Dots, 5, 31), Tile(TileSuit.Dots, 5, 32)),
                Action(ActionKind.Win, discard)
            });

            Assert.IsTrue(prompt.TrySelect(1, out var byNumber));
            Assert.AreEqual(ActionKind.Win, byNumber.Kind);

            Assert.IsTrue(prompt.TrySelectKind(ActionKind.Pung, out var byKind));
            Assert.AreEqual(ActionKind.Pung, byKind.Kind);

            Assert.IsFalse(prompt.TrySelect(9, out _), "There is no ninth button here.");
        }

        [Test]
        public void FingerprintIdentifiesTheOptionSet()
        {
            var discard = Tile(TileSuit.Characters, 2, 12);
            LegalAction[] Window() => new[]
            {
                Action(ActionKind.Pass),
                Action(ActionKind.Pung, discard, Tile(TileSuit.Characters, 2, 13), Tile(TileSuit.Characters, 2, 14))
            };

            var first = ActionPrompt.FromLegalActions(Window());
            var second = ActionPrompt.FromLegalActions(Window());
            Assert.AreEqual(first.Fingerprint, second.Fingerprint,
                "The same options must not rebuild the 3D bar every frame.");

            var other = ActionPrompt.FromLegalActions(new[] { Action(ActionKind.Pass), Action(ActionKind.Win, discard) });
            Assert.AreNotEqual(first.Fingerprint, other.Fingerprint,
                "Different options must rebuild the bar.");
        }

        [Test]
        public void HintComesFromTheEngineDescription()
        {
            var discard = Tile(TileSuit.Wind, (int)Wind.East, 20);
            var action = Action(ActionKind.OpenKong, discard,
                Tile(TileSuit.Wind, (int)Wind.East, 21), Tile(TileSuit.Wind, (int)Wind.East, 22), Tile(TileSuit.Wind, (int)Wind.East, 23));

            var prompt = ActionPrompt.FromLegalActions(new[] { action, Action(ActionKind.Pass) });

            Assert.AreEqual(action, prompt.Options[0].Action);
            Assert.AreEqual(action.Description, prompt.Options[0].Hint);
            Assert.AreEqual("Kong East", prompt.Options[0].Label);
        }

        [Test]
        public void ClosedPromptIsNotOpen()
        {
            Assert.IsFalse(ActionPrompt.Closed.IsOpen);
            Assert.IsFalse(ActionPrompt.Closed.TrySelect(1, out _));
            Assert.IsFalse(ActionPrompt.Closed.TrySelectKind(ActionKind.Win, out _));
            Assert.IsNull(ActionPrompt.Closed.Find(ActionKind.Win));
        }
    }
}
