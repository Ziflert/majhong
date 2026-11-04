using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// A single 3D tile built from primitives: a cube body plus a label on the top face.
    /// No prefabs and no art assets are needed for the prototype.
    ///
    /// The tile is an empty root with two children on purpose: the body cube carries a non-uniform
    /// scale, so parenting the label or the tile itself to a scaled object would squash the text
    /// and the geometry. The root keeps scale 1 and owns the collider used for mouse picking.
    /// </summary>
    public sealed class TileObject : MonoBehaviour
    {
        // Sized for a 1.35-wide table: a tile takes about 1/19 of it, close to a real set.
        public const float Length = 0.072f;
        public const float Width = 0.052f;
        public const float Height = 0.028f;

        /// <summary>How fast a tile slides to its new place, in table units per second.</summary>
        public const float SlideSpeed = 1.8f;

        /// <summary>How fast a tile turns to its new orientation, in degrees per second.</summary>
        public const float TurnSpeed = 720f;

        /// <summary>Colour of the back of a tile: the green lacquer of a real set.</summary>
        public static readonly Color BackColour = new(0.25f, 0.45f, 0.3f);

        /// <summary>How far a tile under the cursor rises; enough to read as "this one".</summary>
        public const float HoverLift = 0.012f;

        private TextMesh label = null!;
        private Renderer body = null!;
        private Renderer atlasFace = null!;
        private static readonly Texture2D TileAtlas = Resources.Load<Texture2D>("Tiles/tile-atlas");

        /// <summary>A fresh tile shows neither face nor back until one of them is bound.</summary>
        private bool faceSet;

        public TileType Type { get; private set; }
        public bool IsFaceUp { get; private set; }

        /// <summary>Where the tile is heading; the engine lays out the table and the tiles follow.</summary>
        public Vector3 TargetPosition { get; private set; }

        public float TargetRotationY { get; private set; }

        private Vector3 animationStartPosition;
        private Quaternion animationStartRotation;
        private float animationElapsed;
        private float animationDuration;
        private float animationDelay;
        private float animationArc;
        private bool hasTarget;
        private bool animating;

        /// <summary>Where the slide ends; the hop of <see cref="Pulse"/> is added on top of it.</summary>
        private Vector3 basePosition;

        private bool hovered;
        private float hoverOffset;

        private float pulseElapsed;
        private float pulseDelay;
        private float pulseDuration;
        private float pulseHeight;
        private float pulseOffset;
        private bool pulsing;

        /// <summary>How long the tile still needs before it lands; a hop waits for this.</summary>
        public float LandingDelay => animating ? Mathf.Max(0f, animationDelay + animationDuration - animationElapsed) : 0f;

        /// <summary>True once the tile has arrived: the position is reached, the turn is done and no hop is left.</summary>
        public bool IsSettled => hasTarget && !animating && !pulsing;

        public static TileObject Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "body";
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localScale = new Vector3(Width, Height, Length);
            // picking is done through the root collider, which has the same box
            Object.Destroy(bodyObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Width, Height, Length);

            var tile = root.AddComponent<TileObject>();
            tile.body = bodyObject.GetComponent<Renderer>();
            tile.atlasFace = CreateAtlasFace(root.transform);
            tile.label = CreateLabel(root.transform);
            return tile;
        }

        private static Renderer CreateAtlasFace(Transform parent)
        {
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "atlas-face";
            face.transform.SetParent(parent, false);
            face.transform.localPosition = new Vector3(0f, Height * 0.5f + 0.0018f, 0f);
            // A Quad's normal points along -Z, so -90 about X would turn it face down and the
            // single-sided Unlit/Transparent shader would cull it from the top view. +90 lifts the
            // normal to +Y, the same orientation the text label uses.
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(Width * 0.92f, Length * 0.92f, 1f);
            Object.Destroy(face.GetComponent<Collider>());
            var renderer = face.GetComponent<Renderer>();
            if (TileAtlas != null)
            {
                var material = new Material(Shader.Find("Unlit/Transparent"));
                material.mainTexture = TileAtlas;
                renderer.material = material;
            }
            face.SetActive(TileAtlas != null);
            return renderer;
        }

        private static TextMesh CreateLabel(Transform parent)
        {
            var labelObject = new GameObject("label");
            labelObject.transform.SetParent(parent, false);
            // sit just above the top face; the root is unscaled, so these are world units
            labelObject.transform.localPosition = new Vector3(0f, Height * 0.5f + 0.0015f, 0f);
            // Lay the label flat and face up, with the text top pointing away from the player seat.
            // A +90 degree turn about X maps the text normal to world up (+Y) and the text up
            // direction to +Z, so the glyphs read upright from the camera in front of the table.
            // LookRotation(up, forward) looks right on paper but shows the back of the text: the
            // screenshot of the prototype had doubled, upside-down captions.
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            // TextMesh world height follows characterSize; 0.005 is about a third of the tile width
            mesh.characterSize = 0.005f;
            mesh.text = string.Empty;
            return mesh;
        }

        public void Bind(TileType type)
        {
            Type = type;
            IsFaceUp = true;
            faceSet = true;
            label.text = TileLabel.Short(type);
            label.color = TileLabel.Text(type);
            body.material.color = TileLabel.Face(type);
            label.gameObject.SetActive(TileAtlas == null);
            if (TileAtlas != null)
            {
                atlasFace.material.mainTextureScale = AtlasScale(type);
                atlasFace.material.mainTextureOffset = AtlasOffset(type);
                atlasFace.gameObject.SetActive(true);
            }
        }

        public void BindBack()
        {
            IsFaceUp = false;
            faceSet = true;
            label.text = string.Empty;
            body.material.color = BackColour;
            label.gameObject.SetActive(false);
            atlasFace.gameObject.SetActive(false);
        }

        /// <summary>Shows a face, doing nothing when the tile already shows it.</summary>
        public void SetFace(TileType type)
        {
            if (faceSet && IsFaceUp && Type == type) return;
            Bind(type);
        }

        /// <summary>Turns the tile face down, doing nothing when it already is.</summary>
        public void SetBack()
        {
            if (faceSet && !IsFaceUp) return;
            BindBack();
        }

        private static Vector2 AtlasScale(TileType type) => new(1f / 9f, 1f / 5f);

        private static Vector2 AtlasOffset(TileType type)
        {
            var column = 0;
            var row = 0;
            if (type.IsNumbered)
            {
                column = type.Rank - 1;
                row = type.Suit switch { TileSuit.Characters => 4, TileSuit.Dots => 3, _ => 2 };
            }
            else if (type.IsWind) { column = type.Rank; row = 1; }
            else if (type.IsDragon) { column = 4 + type.Rank; row = 1; }
            else { column = type.Rank; row = 0; }
            return new Vector2(column / 9f, (4 - row) / 5f);
        }

        public void SetHighlight(bool highlighted)
        {
            // A face-down tile must stay the colour of its back: tinting it with the face colour
            // would show which tile is being highlighted even when the player cannot see the face.
            if (!IsFaceUp)
            {
                body.material.color = highlighted ? Color.Lerp(BackColour, Color.yellow, 0.35f) : BackColour;
                return;
            }

            var colour = TileLabel.Face(Type);
            body.material.color = highlighted ? Color.Lerp(colour, Color.yellow, 0.45f) : colour;
        }

        /// <summary>Raises the tile slightly while the cursor is over it.</summary>
        public void SetHovered(bool on) => hovered = on;

        /// <summary>Current lift of the hover, in table units; zero when the cursor is elsewhere.</summary>
        public float HoverOffset => hoverOffset;

        /// <summary>Persistent victory colour used while the hand-result screen is shown.</summary>
        public void SetVictoryHighlight()
        {
            body.material.color = Color.Lerp(TileLabel.Face(Type), Color.yellow, 0.7f);
        }

        /// <summary>Moves the tile to a place at once; used at the deal and by tests.</summary>
        public void SetPosition(Vector3 position, float rotationY)
        {
            SetTarget(position, rotationY);
            Snap();
        }

        /// <summary>
        /// Sets where the tile has to go; the next frames animate it with a short hop. An extra delay
        /// lets the caller deal the table in sequence instead of moving every tile on the same frame.
        /// </summary>
        public void SetTarget(Vector3 position, float rotationY, float extraDelay = 0f)
        {
            var destinationRotation = Quaternion.Euler(0f, rotationY, 0f);
            TargetPosition = position;
            TargetRotationY = rotationY;
            hasTarget = true;

            var distance = Vector3.Distance(basePosition, position);
            var angle = Mathf.Abs(Mathf.DeltaAngle(transform.rotation.eulerAngles.y, rotationY));
            animationStartPosition = basePosition;
            animationStartRotation = transform.rotation;
            animationElapsed = 0f;
            animationDelay = Mathf.Max(0f, extraDelay) + (distance > 0.2f ? 0.025f : 0f);
            animationDuration = Mathf.Clamp(Mathf.Max(distance / SlideSpeed, angle / TurnSpeed), 0.08f, 0.42f);
            animationArc = distance > 0.12f ? Mathf.Clamp(distance * 0.22f, 0.012f, 0.045f) : 0f;
            animating = distance > 0.0001f || angle > 0.5f;
            if (!animating)
            {
                basePosition = position;
                transform.rotation = destinationRotation;
            }

            transform.position = basePosition + Vector3.up * pulseOffset;
        }

        /// <summary>
        /// A short upward hop, used to mark a declared kong. The hop is an offset on top of the slide
        /// target, so it can be scheduled while the tile is still travelling and never fights with it.
        /// </summary>
        public void Pulse(float height = 0.03f, float duration = 0.24f, float delay = 0f)
        {
            pulseElapsed = 0f;
            pulseDelay = Mathf.Max(0f, delay);
            pulseDuration = Mathf.Max(0.05f, duration);
            pulseHeight = height;
            pulsing = true;
        }

        /// <summary>Puts the tile at its target without animating.</summary>
        public void Snap()
        {
            hasTarget = true;
            basePosition = TargetPosition;
            transform.rotation = Quaternion.Euler(0f, TargetRotationY, 0f);
            animating = false;
            pulsing = false;
            pulseOffset = 0f;
            transform.position = basePosition;
        }

        public void SpawnAt(Vector3 position)
        {
            basePosition = position;
            transform.position = position;
            hasTarget = false;
            animating = false;
            pulsing = false;
            pulseOffset = 0f;
        }

        /// <summary>
        /// The whole animation of the table: every tile glides towards the place the engine gave it
        /// and can hop on top of that. Tiles keep their identity between rebuilds, so a discard travels
        /// from the hand to the pile instead of jumping there as a new object.
        /// </summary>
        private void Update()
        {
            UpdateSlide();
            UpdatePulse();
            UpdateHover();
            transform.position = basePosition + Vector3.up * (pulseOffset + hoverOffset);
        }

        private void UpdateHover()
        {
            var target = hovered ? HoverLift : 0f;
            if (Mathf.Approximately(hoverOffset, target)) return;
            hoverOffset = Mathf.MoveTowards(hoverOffset, target, Time.deltaTime * 0.12f);
        }

        private void UpdateSlide()
        {
            if (!animating) return;

            animationElapsed += Time.deltaTime;
            var t = Mathf.Clamp01((animationElapsed - animationDelay) / animationDuration);
            // Smooth-step gives the move a deliberate ease-in/ease-out instead of robotic constant speed.
            var eased = t * t * (3f - 2f * t);
            basePosition = Vector3.LerpUnclamped(animationStartPosition, TargetPosition, eased);
            var arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * animationArc);
            basePosition += arc;
            transform.rotation = Quaternion.Slerp(animationStartRotation,
                Quaternion.Euler(0f, TargetRotationY, 0f), eased);

            if (t >= 1f)
            {
                basePosition = TargetPosition;
                transform.rotation = Quaternion.Euler(0f, TargetRotationY, 0f);
                animating = false;
            }
        }

        private void UpdatePulse()
        {
            if (!pulsing) return;

            pulseElapsed += Time.deltaTime;
            if (pulseElapsed < pulseDelay)
            {
                pulseOffset = 0f;
                return;
            }

            var t = Mathf.Clamp01((pulseElapsed - pulseDelay) / pulseDuration);
            pulseOffset = Mathf.Sin(t * Mathf.PI) * pulseHeight;
            if (t >= 1f)
            {
                pulsing = false;
                pulseOffset = 0f;
            }
        }

        public void SetFaceDownRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }
}
