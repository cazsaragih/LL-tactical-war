using UnityEngine;

namespace LLTacticalWar
{
    // The board cursor, ported from the 2015 PointerScript, drawn as a brass map pin: it hovers over its
    // tile with a soft bob and a shadow under it, and brass registration marks frame the tile. It is
    // locked to tile indices, glides between them on a critically damped spring (a long jump eases in
    // and out instead of snapping), repeats when a key is held, and answers every press (Feel.Ack).
    public class PointerScript : MonoBehaviour
    {
        public static PointerScript Active { get; private set; }

        public int tileXIndex, tileZIndex;
        public bool inputEnabled;

        const float Hover = 2.35f;      // the needle's point above the tile: clear of a rider's helmet
        const float Glide = 0.075f;     // spring time: one tile in about a quarter second

        TileMap map;
        Transform marks, pin, shadow, ripple;
        Material shadowMat, rippleMat;
        float repeatTimer, rippleT = 1f, answerT = 1f;
        Ack answer;
        Vector2Int heldDir;
        Vector3 target, velocity;

        public TileMapInfo Tile => map.TileAt(tileXIndex, tileZIndex);

        void Awake()
        {
            Active = this;
            map = TileMap.instance;

            // registration marks: a brass L at each corner of the tile
            marks = Prim.Pivot("Marks", transform, Vector3.zero);
            float s = map.tileSize * 0.5f - 0.1f, arm = 0.44f, th = 0.09f;
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                Prim.Part("MarkX", PrimitiveType.Cube, marks, new Vector3(sx * (s - arm / 2f), 0.09f, sz * s), new Vector3(arm, 0.05f, th), Palette.Brass, default, false);
                Prim.Part("MarkZ", PrimitiveType.Cube, marks, new Vector3(sx * s, 0.09f, sz * (s - arm / 2f)), new Vector3(th, 0.05f, arm), Palette.Brass, default, false);
            }

            // the pin: a steel needle, a brass collar and a polished brass head, leaning toward the camera
            pin = Prim.Pivot("Pin", transform, new Vector3(0f, Hover, 0f));
            Prim.Part("Needle", PrimitiveType.Cylinder, pin, new Vector3(0f, 0.45f, 0f), new Vector3(0.075f, 0.45f, 0.075f), Palette.Steel, default, false);
            Prim.Part("Point", PrimitiveType.Cube, pin, new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.06f, 0.06f), Palette.Steel, new Vector3(45f, 0f, 45f), false);
            Prim.Part("Collar", PrimitiveType.Cylinder, pin, new Vector3(0f, 0.9f, 0f), new Vector3(0.24f, 0.03f, 0.24f), Palette.BrassDark, default, false);
            Prim.Part("Head", PrimitiveType.Sphere, pin, new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.54f, 0.62f), Palette.Brass, default, false);
            Prim.Part("Glint", PrimitiveType.Sphere, pin, new Vector3(-0.12f, 1.33f, -0.16f), new Vector3(0.14f, 0.1f, 0.14f), Palette.BrassLight, default, false)
                .GetComponent<MeshRenderer>().sharedMaterial = Palette.Glow(Palette.BrassLight, 0.6f);

            shadow = Flat("Shadow", Soft(32, false), 3002, out shadowMat);
            shadowMat.color = Palette.WithAlpha(Palette.Ink, 0.5f);
            ripple = Flat("Ripple", Soft(64, true), 3003, out rippleMat);
            ripple.gameObject.SetActive(false);
        }

        Transform Flat(string name, Texture2D tex, int queue, out Material mat)
        {
            var go = Prim.Part(name, PrimitiveType.Quad, transform, new Vector3(0f, 0.08f, 0f), Vector3.one, Palette.Ink, new Vector3(90f, 0f, 0f), false);
            var r = go.GetComponent<MeshRenderer>();
            mat = Palette.Overlay();
            mat.mainTexture = tex;
            mat.renderQueue = queue;
            r.sharedMaterial = mat;
            r.receiveShadows = false;
            return go.transform;
        }

        // A soft round shadow, or a ring of ink for the ripple.
        static Texture2D Soft(int n, bool ring)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = ring ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.07f) : Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        public void SetTile(int x, int z, bool snap = false)
        {
            tileXIndex = Mathf.Clamp(x, 0, map.tileX - 1);
            tileZIndex = Mathf.Clamp(z, 0, map.tileZ - 1);
            target = map.TileAt(tileXIndex, tileZIndex).position;
            if (snap) { transform.position = target; velocity = Vector3.zero; }
        }

        public void AdjustPosition(Vector3 world, bool snap = false)
        {
            int idx = map.WorldToTilePoint(world);
            SetTile(idx % map.tileX, idx / map.tileX, snap);
        }

        // The pin's answer to a press, starting this frame.
        public void Answer(Ack a)
        {
            answer = a;
            answerT = 0f;
            if (a == Ack.Confirm) { rippleT = 0f; ripple.gameObject.SetActive(true); }
        }

        static float Decay(float t, float duration) => t >= duration ? 0f : (1f - t / duration) * (1f - t / duration);

        void Update()
        {
            if (inputEnabled) HandleInput();
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, Glide, Mathf.Infinity, dt);

            answerT += dt;
            float dip = 0f, pop = 0f, shake = 0f;
            switch (answer)
            {
                case Ack.Move: dip = -0.16f * Decay(answerT, 0.18f); pop = 0.1f * Decay(answerT, 0.16f); break;
                case Ack.Confirm: dip = -0.42f * Decay(answerT, 0.26f); pop = 0.18f * Decay(answerT, 0.22f); break;
                case Ack.Cancel: dip = 0.22f * Decay(answerT, 0.22f); break;
                case Ack.Refuse: shake = 16f * Mathf.Sin(answerT * 42f) * Decay(answerT, 0.34f); break;
            }
            float bob = Mathf.Sin(now * 2.1f) * 0.08f;
            pin.localPosition = new Vector3(0f, Hover + bob + dip, 0f);
            pin.localRotation = Quaternion.Euler(-15f, 0f, shake);
            float m = 1f + pop + Mathf.Sin(now * 3f) * 0.012f;
            marks.localScale = new Vector3(m, 1f, m);
            float sh = 1.25f - 0.3f * (bob + dip);
            shadow.localScale = new Vector3(sh, sh, 1f);

            if (rippleT < 1f)
            {
                rippleT += dt / 0.38f;
                float k = Ease.OutCubic(rippleT);
                ripple.localScale = new Vector3(0.7f + 1.5f * k, 0.7f + 1.5f * k, 1f);
                rippleMat.color = Palette.WithAlpha(Palette.Ink, 0.75f * (1f - k));
                if (rippleT >= 1f) ripple.gameObject.SetActive(false);
            }
        }

        void HandleInput()
        {
            var dir = Vector2Int.zero;
            if (GameInput.UpHeld) dir.y += 1;
            if (GameInput.DownHeld) dir.y -= 1;
            if (GameInput.RightHeld) dir.x += 1;
            if (GameInput.LeftHeld) dir.x -= 1;
            if (dir.x != 0 && dir.y != 0) dir.y = 0; // no diagonals, like the 2015 cursor

            if (GameInput.debugCursorNudge != Vector2Int.zero)
            {
                Step(GameInput.debugCursorNudge);
                GameInput.debugCursorNudge = Vector2Int.zero;
                return;
            }

            if (dir == Vector2Int.zero) { heldDir = Vector2Int.zero; return; }
            if (dir != heldDir)
            {
                heldDir = dir;
                repeatTimer = 0.28f;
                Step(dir);
                return;
            }
            repeatTimer -= Time.unscaledDeltaTime;
            if (repeatTimer <= 0f)
            {
                repeatTimer = 0.085f;
                Step(dir);
            }
        }

        void Step(Vector2Int d)
        {
            int nx = Mathf.Clamp(tileXIndex + d.x, 0, map.tileX - 1);
            int nz = Mathf.Clamp(tileZIndex + d.y, 0, map.tileZ - 1);
            if (nx == tileXIndex && nz == tileZIndex) return;
            SetTile(nx, nz);
            Feel.Ack(Ack.Move);
        }
    }
}
