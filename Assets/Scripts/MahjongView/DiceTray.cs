using Mahjong.Controls;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The two dice on the table (rule 3.5.7.5). They are thrown into the middle of the four walls,
    /// twice: the first throw picks who throws again, the second one is added to it to find where the
    /// wall breaks. The tray owns only the look of that — which beat is running comes from
    /// <see cref="HandOpening"/>, so no rule lives here.
    /// </summary>
    public sealed class DiceTray : MonoBehaviour
    {
        /// <summary>Side of one die; two of them fit the middle of the table.</summary>
        public const float Size = 0.034f;

        /// <summary>How far the two dice lie from the middle, left and right of the well.</summary>
        private const float Spread = 0.042f;

        private static readonly Color DieColour = new(0.96f, 0.94f, 0.88f);
        private static readonly Color OneColour = new(0.78f, 0.16f, 0.13f);
        private static readonly Color PipColour = new(0.10f, 0.11f, 0.13f);

        private Transform[] dice = null!;
        private TextMesh[] pips = null!;
        private Renderer[] bodies = null!;

        /// <summary>Values the dice show right now; the tests read them instead of the screen.</summary>
        public int ShownFirst { get; private set; }
        public int ShownSecond { get; private set; }

        public bool IsVisible => gameObject.activeSelf;

        public static DiceTray Create(Transform parent)
        {
            var root = new GameObject("DiceTray");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, TableLayout.TileY, 0f);

            var tray = root.AddComponent<DiceTray>();
            tray.dice = new Transform[2];
            tray.pips = new TextMesh[2];
            tray.bodies = new Renderer[2];

            for (var index = 0; index < 2; index++)
            {
                var die = GameObject.CreatePrimitive(PrimitiveType.Cube);
                die.name = $"die-{index + 1}";
                die.transform.SetParent(root.transform, false);
                die.transform.localScale = new Vector3(Size, Size, Size);
                // The dice are decoration: a collider here would swallow a click meant for the table.
                Object.Destroy(die.GetComponent<Collider>());
                die.GetComponent<Renderer>().material.color = DieColour;

                var pipObject = new GameObject("pip");
                pipObject.transform.SetParent(die.transform, false);
                // A die is scaled non-uniformly, so the digit lives on a child that brings its own size.
                pipObject.transform.localPosition = new Vector3(0f, 0.51f, 0f);
                pipObject.transform.localScale = new Vector3(1f / Size, 1f / Size, 1f / Size);
                pipObject.transform.localRotation = Quaternion.identity;

                var pip = pipObject.AddComponent<TextMesh>();
                pip.fontSize = 64;
                // The pip object is scaled by 1/Size, so its own text size is in plain world units.
                pip.characterSize = 0.0052f;
                pip.anchor = TextAnchor.MiddleCenter;
                pip.alignment = TextAlignment.Center;
                pip.richText = false;
                UiFont.Assign(pip);

                tray.dice[index] = die.transform;
                tray.pips[index] = pip;
                tray.bodies[index] = die.GetComponent<Renderer>();
            }

            tray.SetOpen(false);
            return tray;
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open)
            {
                ShownFirst = 0;
                ShownSecond = 0;
            }
        }

        /// <summary>
        /// Hides the dice without forgetting what they show: free play takes the middle of the table
        /// for its own grid, and the numbers have to be back when the hand comes back.
        /// </summary>
        public void SetShown(bool shown) => gameObject.SetActive(shown);

        /// <summary>
        /// Draws the opening as it runs: while a throw is in the air the dice tumble and roll, and by
        /// the end of the beat they lie still showing their numbers.
        /// </summary>
        public void Show(HandOpening opening)
        {
            var throwing = opening.Step is OpeningStep.FirstRoll or OpeningStep.SecondRoll;
            var values = opening.Step switch
            {
                OpeningStep.FirstRoll => (opening.First.First, opening.First.Second),
                _ => (opening.Second.First, opening.Second.Second)
            };

            for (var index = 0; index < 2; index++)
            {
                var settled = !throwing || opening.Progress > 0.62;
                var value = settled ? (index == 0 ? values.Item1 : values.Item2) : 0;

                // A tumbling die hops, slides a little and turns; a settled one lies flat.
                var tumble = throwing && !settled ? (1f - (float)opening.Progress) : 0f;
                var hop = Mathf.Abs(Mathf.Sin((float)opening.ElapsedInStep * 22f + index)) * 0.05f * tumble;
                var wobble = Mathf.Sin((float)opening.ElapsedInStep * 34f + index * 2f) * 34f * tumble;
                var slide = Mathf.Sin((float)opening.ElapsedInStep * 17f + index) * 0.012f * tumble;

                var side = index == 0 ? -Spread : Spread;
                dice[index].localPosition = new Vector3(side + slide, Size * 0.5f + hop, slide * 0.5f);
                dice[index].localRotation = Quaternion.Euler(throwing ? 0f : 0f,
                    throwing ? opening.Step == OpeningStep.FirstRoll ? wobble : -wobble : 0f, 0f);

                if (value > 0)
                {
                    ShowPip(index, value);
                }
                else
                {
                    pips[index].text = string.Empty;
                }
            }

            if (opening.Step is OpeningStep.FirstRoll or OpeningStep.SecondRoll && opening.Progress > 0.62)
            {
                ShownFirst = values.Item1;
                ShownSecond = values.Item2;
            }
        }

        /// <summary>Lays the dice still, showing a pair: used at rest and by the tests.</summary>
        public void Settle(int first, int second)
        {
            ShownFirst = first;
            ShownSecond = second;
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -Spread : Spread;
                dice[index].localPosition = new Vector3(side, Size * 0.5f, 0f);
                dice[index].localRotation = Quaternion.identity;
                ShowPip(index, index == 0 ? first : second);
            }
        }

        private void ShowPip(int index, int value)
        {
            // The Chinese dice colour their one and their four red, the rest blue or black (3.1.2).
            var red = value is 1 or 4;
            pips[index].text = value.ToString();
            pips[index].color = red ? OneColour : PipColour;
            bodies[index].material.color = DieColour;
        }
    }
}
