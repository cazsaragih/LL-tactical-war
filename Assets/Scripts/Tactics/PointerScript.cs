using UnityEngine;

namespace LLTacticalWar
{
    // The board cursor, ported from the 2015 PointerScript. It is now locked to tile indices
    // (it used to lerp freely and could drift off the grid), repeats when a key is held, and
    // the camera follows it through Cinemachine.
    public class PointerScript : MonoBehaviour
    {
        public int tileXIndex, tileZIndex;
        public bool inputEnabled;

        TileMap map;
        Transform visual, arrow;
        float repeatTimer;
        Vector2Int heldDir;
        Vector3 target;

        public TileMapInfo Tile => map.TileAt(tileXIndex, tileZIndex);

        void Awake()
        {
            map = TileMap.instance;
            visual = new GameObject("CursorFrame").transform;
            visual.SetParent(transform, false);
            float s = map.tileSize * 0.5f - 0.06f;
            float th = 0.12f;
            Prim.Part("N", PrimitiveType.Cube, visual, new Vector3(0, 0.1f, s), new Vector3(s * 2f, 0.1f, th), Palette.Gold, default, false);
            Prim.Part("S", PrimitiveType.Cube, visual, new Vector3(0, 0.1f, -s), new Vector3(s * 2f, 0.1f, th), Palette.Gold, default, false);
            Prim.Part("E", PrimitiveType.Cube, visual, new Vector3(s, 0.1f, 0), new Vector3(th, 0.1f, s * 2f), Palette.Gold, default, false);
            Prim.Part("W", PrimitiveType.Cube, visual, new Vector3(-s, 0.1f, 0), new Vector3(th, 0.1f, s * 2f), Palette.Gold, default, false);
            arrow = new GameObject("Arrow").transform;
            arrow.SetParent(transform, false);
            Prim.GlowPart("Gem", PrimitiveType.Cube, arrow, Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f), Palette.Gold, new Vector3(45f, 0, 45f));
        }

        public void SetTile(int x, int z, bool snap = false)
        {
            tileXIndex = Mathf.Clamp(x, 0, map.tileX - 1);
            tileZIndex = Mathf.Clamp(z, 0, map.tileZ - 1);
            target = map.TileAt(tileXIndex, tileZIndex).position;
            if (snap) transform.position = target;
        }

        public void AdjustPosition(Vector3 world, bool snap = false)
        {
            int idx = map.WorldToTilePoint(world);
            SetTile(idx % map.tileX, idx / map.tileX, snap);
        }

        void Update()
        {
            if (inputEnabled) HandleInput();
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            float t = Time.unscaledTime;
            arrow.localPosition = new Vector3(0, 2.6f + Mathf.Sin(t * 3f) * 0.15f, 0);
            arrow.localRotation = Quaternion.Euler(0, t * 90f, 0);
            float pulse = 1f + Mathf.Sin(t * 5f) * 0.03f;
            visual.localScale = new Vector3(pulse, 1f, pulse);
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
            AudioBank.I?.Play(Sfx.UiMove, 0.35f);
        }
    }
}
