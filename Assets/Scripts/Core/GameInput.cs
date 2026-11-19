using UnityEngine;
using UnityEngine.InputSystem;

namespace LLTacticalWar
{
    // One place for every binding. The 2015 build used Z = confirm, X = cancel and the arrow keys;
    // those stay, with Enter / Backspace and WASD as aliases.
    public static class GameInput
    {
        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;

        static bool Pressed(params Key[] keys)
        {
            var kb = Kb;
            if (kb == null) return false;
            foreach (var k in keys) if (kb[k].wasPressedThisFrame) return true;
            return false;
        }

        static bool Held(params Key[] keys)
        {
            var kb = Kb;
            if (kb == null) return false;
            foreach (var k in keys) if (kb[k].isPressed) return true;
            return false;
        }

        public static bool Confirm => Pressed(Key.Z, Key.Enter, Key.NumpadEnter) || Debug("confirm");
        public static bool Cancel  => Pressed(Key.X, Key.Backspace) || Debug("cancel");
        public static bool Pause   => Pressed(Key.Escape) || Debug("pause");
        public static bool AnyStart => Pressed(Key.Enter, Key.NumpadEnter, Key.Z, Key.Space) || Debug("confirm");

        public static bool UpPressed    => Pressed(Key.UpArrow, Key.W) || Debug("up");
        public static bool DownPressed  => Pressed(Key.DownArrow, Key.S) || Debug("down");
        public static bool LeftPressed  => Pressed(Key.LeftArrow, Key.A) || Debug("left");
        public static bool RightPressed => Pressed(Key.RightArrow, Key.D) || Debug("right");

        public static bool UpHeld    => Held(Key.UpArrow, Key.W) || debugMove.y > 0.5f && Time.time < debugMoveUntil;
        public static bool DownHeld  => Held(Key.DownArrow, Key.S) || debugMove.y < -0.5f && Time.time < debugMoveUntil;
        public static bool LeftHeld  => Held(Key.LeftArrow, Key.A) || debugMove.x < -0.5f && Time.time < debugMoveUntil;
        public static bool RightHeld => Held(Key.RightArrow, Key.D) || debugMove.x > 0.5f && Time.time < debugMoveUntil;

        // ---- verification hooks: lets an automated play-test press buttons without real keys ----
        static readonly System.Collections.Generic.HashSet<string> debugPending = new System.Collections.Generic.HashSet<string>();
        static int debugFrame = -1;
        static Vector2 debugMove;
        static float debugMoveUntil;

        // A press is live for exactly the next full frame after it is queued, whatever point in
        // the player loop queued it.
        public static void DebugPress(string action) { debugPending.Add(action); debugFrame = Time.frameCount + 1; }
        public static void DebugHold(Vector2 move, float seconds) { debugMove = move; debugMoveUntil = Time.time + seconds; }

        static bool Debug(string action)
        {
            if (debugPending.Count == 0) return false;
            int f = Time.frameCount;
            if (f < debugFrame) return false;
            if (f > debugFrame) { debugPending.Clear(); return false; }
            return debugPending.Contains(action);
        }

        // Battle controls
        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                if (LeftHeld) v.x -= 1f;
                if (RightHeld) v.x += 1f;
                if (UpHeld) v.y += 1f;
                if (DownHeld) v.y -= 1f;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool Attack  => Pressed(Key.J) || (Ms != null && Ms.leftButton.wasPressedThisFrame) || Debug("attack");
        public static bool AttackHeld => Held(Key.J) || (Ms != null && Ms.leftButton.isPressed);
        public static bool Skill   => Pressed(Key.K) || (Ms != null && Ms.rightButton.wasPressedThisFrame) || Debug("skill");
        public static bool Command => Pressed(Key.Space) || Debug("command");

        // Debug hook so a verification drive can move the cursor without simulated key events.
        public static Vector2Int debugCursorNudge;
    }
}
