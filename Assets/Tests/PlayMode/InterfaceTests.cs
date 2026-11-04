using System.Collections;
using System.Linq;
using Mahjong.Localization;
using Mahjong.Net;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The screens that are not the table itself: the Russian rulebook, the free-play table and the
    /// language switch. They are checked in play mode because they are made of scene objects — text
    /// meshes, quads and tile views — that no editor test can see.
    /// </summary>
    public sealed class InterfaceTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongInterfaceTestHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the table starts its own game.
            game.SkipWelcome();
            return game;
        }

        [UnityTest]
        public IEnumerator RulebookOpensWithRussianPages()
        {
            var game = Game();
            yield return null;
            yield return null;

            var rules = game.Rules;
            Assert.IsNotNull(rules, "The rulebook was not created with the table.");
            Assert.IsFalse(rules.IsOpen, "The rulebook must not cover the table at the start.");
            Assert.GreaterOrEqual(rules.PageCount, 15, "The rulebook is thinner than the rules it explains.");

            rules.SetOpen(true);
            yield return null;

            Assert.IsTrue(rules.IsOpen, "Opening the rulebook did nothing.");
            Assert.IsTrue(rules.gameObject.activeInHierarchy, "The rulebook object is still disabled.");
            Assert.IsFalse(game.Status.gameObject.activeInHierarchy,
                "The HUD is still drawn on top of the open rulebook.");
            Assert.IsNotEmpty(rules.Title.text, "The first page has no title.");
            Assert.IsNotEmpty(rules.Body.text, "The first page has no text.");
            StringAssert.IsMatch("[А-Яа-яЁё]", rules.Body.text, "The rulebook is not in Russian.");
            StringAssert.Contains("1/" + rules.PageCount, rules.Footer.text,
                "The footer does not say which page is on screen: " + rules.Footer.text);

            // The panel hangs half a metre in front of the camera, where the frame is only about
            // 0.61 x 0.31: a page that is bigger than that is cropped, and the player loses the first
            // and last line of every page. All four corners of the backdrop have to be on screen.
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The prototype has no camera to measure the rulebook against.");
            var backdrop = rules.Backdrop;
            Assert.IsNotNull(backdrop, "The rulebook has no backdrop.");
            foreach (var corner in new[]
                     {
                         new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                         new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                     })
            {
                var point = camera!.WorldToViewportPoint(backdrop!.transform.TransformPoint(corner));
                Assert.Greater(point.z, 0f, "The rulebook is behind the camera.");
                Assert.IsTrue(point.x is >= 0f and <= 1f && point.y is >= 0f and <= 1f,
                    $"The rulebook page hangs off the screen at {point.x:0.00} x {point.y:0.00}.");
            }

            var firstTitle = rules.Title.text;
            var firstBody = rules.Body.text;
            rules.Turn(1);
            yield return null;
            Assert.AreNotEqual(firstTitle, rules.Title.text, "Turning a page did not change the page.");

            rules.Turn(-1);
            yield return null;
            Assert.AreEqual(firstTitle, rules.Title.text, "Turning back does not return to the first page.");
            Assert.AreEqual(firstBody, rules.Body.text, "The text of the first page changed.");

            // The fan reference is the last page of the book.
            rules.Turn(-1);
            yield return null;
            StringAssert.Contains("фан", rules.Title.text, "The fan reference page is missing.");

            rules.Close();
            yield return null;
            Assert.IsFalse(rules.IsOpen, "Closing the rulebook did nothing.");
            Assert.IsFalse(rules.gameObject.activeInHierarchy, "The rulebook is still on screen.");
            Assert.IsTrue(game.Status.gameObject.activeInHierarchy, "The HUD did not come back with the table.");
        }

        [UnityTest]
        public IEnumerator FreePlayLaysOutTheWholeSetAndMovesTiles()
        {
            var game = Game();
            yield return null;
            yield return null;

            var free = game.FreePlay;
            Assert.IsNotNull(free, "The free-play table was not created.");

            game.OpenFreePlay();
            yield return null;

            Assert.IsTrue(free.IsOpen, "Free play did not open.");
            Assert.AreEqual(144, free.Table.TileCount, "The free table does not hold the whole set.");
            Assert.AreEqual(144, free.TileViewCount, "Not every tile of the free table has a view.");
            Assert.AreEqual(0, free.ActiveSeat, "Free play starts at the first seat.");
            Assert.AreEqual(0, game.VisibleTableTileCount,
                "The scored hand is still lying on the table under the free set.");
            StringAssert.Contains(Loc.Get("free.title"), game.Status.text,
                "The HUD does not explain free play:\\n" + game.Status.text);

            // A click on the tile at cell (4, 2) has to pick that very tile up. The ray is cast straight
            // down onto the cell, the same way the player's cursor would arrive there.
            var cellWorld = free.transform.TransformPoint(FreePlayTable.CellToLocal(4, 2));
            var ray = new Ray(cellWorld + Vector3.up * 0.5f, Vector3.down);
            var tileThere = free.Table.TileAt(4, 2);
            Assert.IsNotNull(tileThere, "Cell 4,2 is empty in the opening layout.");

            var taken = free.TryClick(ray, flip: false);
            Assert.IsTrue(taken, "Clicking a tile did not pick it up: " + free.LastMessage);
            Assert.AreEqual(0, free.Table.Find(tileThere!.Id)!.HeldBy, "The picked tile does not belong to seat 0.");
            Assert.AreEqual((int?)tileThere.Id, free.HoldingId, "The table does not report the carried tile.");

            // Another seat cannot take a tile seat 0 is carrying.
            free.NextSeat();
            Assert.AreEqual(1, free.ActiveSeat, "Tab did not pass the table to the next seat.");
            Assert.IsFalse(free.Apply($"take|{tileThere.Id}"), "Another seat took a tile that was already held.");
            Assert.IsNotEmpty(free.LastMessage, "The refusal was not explained.");

            // Seat 1 turns a free tile over and puts it back.
            var freeTile = free.Table.TileAt(7, 3)!;
            Assert.IsTrue(free.Apply($"flip|{freeTile.Id}"), "A free tile could not be turned over.");
            Assert.IsFalse(free.Table.Find(freeTile.Id)!.FaceUp, "The tile still shows its face.");

            // The seat that carries a tile can put it down on a free cell.
            free.NextSeat();
            free.NextSeat();
            free.NextSeat();
            Assert.AreEqual(0, free.ActiveSeat, "Four taps of Tab must come back to the first seat.");
            var empty = free.Table.TileAt(11, 5)!;
            free.Apply($"take|{empty.Id}");
            Assert.IsTrue(free.Apply($"place|{empty.Id}|11|5"), "Placing back on the freed cell failed.");
            Assert.AreEqual(11, free.Table.Find(empty.Id)!.X, "The tile did not stay where it was put.");

            game.CloseFreePlay();
            yield return null;
            Assert.IsFalse(free.IsOpen, "Free play did not close.");
            Assert.AreEqual(0, free.TileViewCount, "The free tiles were left on the scored table.");
            Assert.AreEqual(game.TableTileCount, game.VisibleTableTileCount,
                "The scored hand did not come back after free play.");
            StringAssert.Contains(Loc.Get("hud.header").Split('{')[0].Trim(), game.Status.text,
                "Leaving free play does not bring the scored table back:\\n" + game.Status.text);
        }

        [UnityTest]
        public IEnumerator InterfaceSwitchesLanguage()
        {
            var game = Game();
            yield return null;
            yield return null;

            var russian = game.UiLanguage;
            var russianText = game.Status.text;
            StringAssert.IsMatch("[А-Яа-яЁё]", russianText, "The interface does not start in Russian:\\n" + russianText);

            var switched = game.ToggleLanguage();
            yield return null;

            Assert.AreNotEqual(russian, switched, "The language did not change.");
            Assert.AreNotEqual(russianText, game.Status.text, "The HUD did not follow the language switch.");
            StringAssert.Contains("Wall", game.Status.text, "The english HUD does not say Wall:\\n" + game.Status.text);
            // Separators such as the middle dot are typography, not language: what must be gone is
            // every Cyrillic letter, so the check is on the alphabet, not on the code page.
            StringAssert.DoesNotMatch("[А-Яа-яЁё]", game.Status.text,
                "The english HUD still contains russian text:\\n" + game.Status.text);

            game.ToggleLanguage();
            yield return null;
            StringAssert.IsMatch("[А-Яа-яЁё]", game.Status.text, "Switching back does not restore Russian.");
        }

        [UnityTest]
        public IEnumerator InterfaceFontCanDrawCyrillic()
        {
            yield return null;

            var font = UiFont.Get();
            Assert.IsNotNull(font, "No interface font at all: Russian captions would be invisible.");
            Assert.IsTrue(UiFont.HasCyrillic(font),
                $"The interface font '{font!.name}' has no Cyrillic glyphs, so Russian captions cannot be read.");

            var game = Game();
            yield return null;
            yield return null;
            Assert.IsTrue(UiFont.HasCyrillic(game.Status.font),
                "The HUD text mesh does not use a font with Cyrillic glyphs.");

            // The free table is the other screen with Russian text.
            game.OpenFreePlay();
            yield return null;
            Assert.IsTrue(UiFont.HasCyrillic(game.Status.font), "Free play uses a font without Cyrillic.");
            game.CloseFreePlay();
        }
    }
}
