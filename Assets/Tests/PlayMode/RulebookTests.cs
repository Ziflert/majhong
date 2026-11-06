using System.Collections;
using Mahjong.Controls;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The rulebook as a page a player can work with (У-5): it opens on its table of contents, a row of
    /// the contents opens the page it names, and the two plates at the bottom of the page turn it.
    ///
    /// The clicks go through <see cref="MahjongGame.ClickRules"/> with the same ray a cursor would cast,
    /// because raw mouse input cannot be pressed from a test. That is the seam the whole click path
    /// shares with the player, except for the single line that reads the button.
    /// </summary>
    public sealed class RulebookTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongRulebookTestHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the table starts its own game.
            game.SkipWelcome();
            return game;
        }

        /// <summary>The ray a player's cursor would cast at a point of the open book.</summary>
        private static Ray RayAt(Vector3 worldPoint)
        {
            var camera = Camera.main!;
            return new Ray(camera.transform.position, (worldPoint - camera.transform.position).normalized);
        }

        [UnityTest]
        public IEnumerator TheContentsPageOpensEveryPageOfTheBook()
        {
            var game = Game();
            yield return null;
            yield return null;

            var rules = game.Rules;
            game.OpenRules();
            // The reader keeps the page he left, so the book may well open somewhere else.
            rules.GoToPage(RulesPanel.ContentsPage);
            yield return null;

            Assert.AreEqual(RulesPanel.ContentsPage, rules.Page, "The contents page cannot be opened by its index.");
            Assert.IsTrue(rules.IsContents, "The contents page is not recognised as one.");
            Assert.AreEqual(rules.PageCount - 1, rules.RowCount,
                "The contents does not have one row per page of the book.");

            // Every row names the page it opens, and the number is the number the counter shows.
            for (var index = 0; index < rules.RowCount; index++)
            {
                var expected = index + 2;   // page 1 is the contents itself
                StringAssert.StartsWith($"{expected}. ", rules.RowText(index),
                    $"Row {index} does not lead with its page number: {rules.RowText(index)}");
                Assert.IsTrue(rules.RowsShown(index), $"Row {index} of the contents is not drawn.");
            }

            // A click on the fourth row opens the page that row names.
            var rowIndex = 3;
            Assert.IsTrue(game.ClickRules(RayAt(rules.RowPosition(rowIndex))), "The click on a contents row was lost.");
            yield return null;
            Assert.AreEqual(rowIndex + 1, rules.Page, "The row did not open the page it names.");
            Assert.IsFalse(rules.IsContents, "The book is still on the contents after opening a page.");
            Assert.IsFalse(rules.RowsShown(rowIndex), "The rows of the contents are still drawn over the page.");
            Assert.IsNotEmpty(rules.Footer.text,
                "Every page has to cite the rule it explains.");

            // Home always brings the contents back.
            rules.GoToPage(rules.PageCount - 1);
            Assert.IsFalse(rules.IsContents, "The last page of the book is not a page.");
            Assert.IsFalse(rules.RowsShown(0), "The rows are drawn over the fan table.");
            rules.GoToPage(RulesPanel.ContentsPage);
            Assert.IsTrue(rules.IsContents, "The contents cannot be opened by its index.");

            game.CloseRules();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePageButtonsTurnTheBook()
        {
            var game = Game();
            yield return null;
            yield return null;

            var rules = game.Rules;
            game.OpenRules();
            rules.GoToPage(2);
            yield return null;

            var camera = Camera.main!;
            var next = rules.NextButton.transform.position;
            var previous = rules.PreviousButton.transform.position;
            foreach (var point in new[] { next, previous })
            {
                var view = camera.WorldToViewportPoint(point);
                Assert.IsTrue(view.z > 0f && view.x is >= 0f and <= 1f && view.y is >= 0f and <= 1f,
                    $"A page button hangs off the screen at {view.x:0.00} x {view.y:0.00}.");
            }

            Assert.IsTrue(game.ClickRules(RayAt(next)), "The click on the forward button was lost.");
            yield return null;
            Assert.AreEqual(3, rules.Page, "The forward plate did not turn the page.");

            Assert.IsTrue(game.ClickRules(RayAt(previous)), "The click on the back button was lost.");
            yield return null;
            Assert.AreEqual(2, rules.Page, "The back plate did not turn the page back.");

            // The counter is the page the reader is on; it is the only place the numbers are printed.
            StringAssert.Contains("3 / " + rules.PageCount, rules.Counter.text,
                "The counter does not say where the reader is: " + rules.Counter.text);

            // A click that touches nothing leaves the book where it was; it must not close it either.
            var empty = camera.ViewportToWorldPoint(new Vector3(0.02f, 0.98f, 0.5f));
            Assert.IsFalse(game.ClickRules(RayAt(empty)), "A click on the empty frame was taken for a page turn.");
            Assert.IsTrue(rules.IsOpen, "A click on the empty frame closed the book.");
            Assert.AreEqual(2, rules.Page, "A click on the empty frame turned the page.");

            game.CloseRules();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryPageFitsInsideThePage()
        {
            var game = Game();
            yield return null;
            yield return null;

            var rules = game.Rules;
            game.OpenRules();
            yield return null;

            var camera = Camera.main!;
            var aspect = camera.aspect;
            var tables = 0;

            // The book is read in the same window the player resizes, and a page of prose, a page of a
            // table and a narrow 4:3 window all have to hold their text: the panel breaks a line again
            // for the measure it has (4.12.1), so every shape of the page is checked.
            try
            {
                foreach (var shape in new[] { aspect, 4f / 3f })
                {
                    camera.aspect = shape;
                    yield return null;

                    for (var page = 0; page < rules.PageCount; page++)
                    {
                        rules.GoToPage(page);
                        yield return null;
                        tables += CheckPageFits(rules, page);
                    }
                }
            }
            finally
            {
                camera.aspect = aspect;
            }

            // The catalogue of fans is longer than a page of the book holds, so it has to arrive as
            // tables of two columns; a book with no such page would pass every check above for nothing.
            Assert.GreaterOrEqual(tables, 2, "No page of the book is a table of two columns.");

            game.CloseRules();
            yield return null;
        }

        /// <summary>Checks that one page stands inside its sheet; 1 when the page is a table.</summary>
        private static int CheckPageFits(RulesPanel rules, int page)
        {
            var sheet = rules.Backdrop.bounds;
            var title = rules.Title.GetComponent<Renderer>().bounds;
            var body = rules.Body.GetComponent<Renderer>().bounds;
            var tail = rules.Footer.GetComponent<Renderer>().bounds;
            var where = $"page {page + 1} ({rules.CurrentTitle}) at {Camera.main!.aspect:0.00}";

            // A page whose text was never drawn would pass every fit below for nothing. The contents
            // page holds a single line of hint, so anything above one line of type will do.
            Assert.Greater(body.size.y, 0.008f, $"The text of {where} was not drawn.");
            Assert.Greater(body.size.x, 0.05f, $"The text of {where} was not drawn.");

            // The text of a page is broken to the width of the page by the panel itself (4.12.1),
            // so a page that runs over the edge means the measure of a line is wrong.
            Assert.LessOrEqual(body.max.x, sheet.max.x - 0.004f,
                $"The text of {where} runs over the right edge of the page: {body.max.x:0.000} > {sheet.max.x:0.000}.");
            Assert.GreaterOrEqual(body.min.x, sheet.min.x,
                $"The text of {where} starts left of the page.");
            Assert.LessOrEqual(body.max.y, title.min.y + 0.002f,
                $"The text of {where} runs into its heading.");
            Assert.GreaterOrEqual(body.min.y, tail.max.y + 0.002f,
                $"The text of {where} runs into the line at the foot of the page.");

            var right = rules.BodyRight;
            if (right.text.Length == 0) return 0;

            var column = right.GetComponent<Renderer>().bounds;
            Assert.Greater(column.size.y, 0.008f, $"The second column of {where} was not drawn.");

            // A table runs down two columns: the second one must stand clear of the first.
            Assert.GreaterOrEqual(column.min.x, body.max.x + 0.004f,
                $"The two columns of {where} overlap: the first ends at {body.max.x:0.000}, the second starts at {column.min.x:0.000}.");
            Assert.LessOrEqual(column.max.x, sheet.max.x - 0.004f,
                $"The right column of {where} runs over the edge of the page.");
            Assert.GreaterOrEqual(column.min.y, tail.max.y + 0.002f,
                $"The right column of {where} runs into the line at the foot of the page.");
            return 1;
        }

        [UnityTest]
        public IEnumerator TheBookComesBackToTheMenuItCameFrom()
        {
            var game = Game();
            yield return null;
            yield return null;

            // Opened from the menu, the book goes back to the menu — not to a table the player did not ask for.
            game.OpenMenu();
            var guard = game.Menu.RowCount;
            while (game.Menu.Selected != MenuItem.Rules && guard-- > 0) game.MenuMove(1);
            Assert.AreEqual(MenuItem.Rules, game.Menu.Selected, "The menu has no rules row to open.");
            var rulesRow = game.Menu.Index;
            game.MenuActivate();
            yield return null;
            Assert.IsTrue(game.Rules.IsOpen, "The rules row did not open the book.");
            Assert.IsFalse(game.IsMenuOpen, "The menu stayed open over the book.");

            game.CloseRules();
            yield return null;
            Assert.IsTrue(game.IsMenuOpen, "The book dropped the player at the table instead of the menu.");
            Assert.AreEqual(rulesRow, game.Menu.Index, "The menu came back on a different row.");
            game.CloseMenu();
            yield return null;

            // Opened with the hot key or the tray, the book goes back to the table.
            game.OpenRules();
            yield return null;
            game.CloseRules();
            yield return null;
            Assert.IsFalse(game.IsMenuOpen, "A book opened from the table came back to the menu.");
            Assert.IsFalse(game.Rules.IsOpen, "The book did not close.");
        }
    }
}
