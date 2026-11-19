using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LLTacticalWar
{
    // One place for every binding. The 2015 build used Z = confirm, X = cancel and the arrow keys;
    // those stay, with Enter / Backspace and WASD as aliases.
    public static class GameInput
    {
        static Keyboard Kb => Keyboard.current;

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
        public static bool Danger  => Pressed(Key.LeftShift, Key.RightShift, Key.C) || Debug("danger");

        // Held Z plays the battle scene at double speed.
        public static bool ConfirmHeld => Held(Key.Z, Key.Enter, Key.NumpadEnter) || DebugHeld("confirm");

        public static bool UpPressed    => Pressed(Key.UpArrow, Key.W) || Debug("up");
        public static bool DownPressed  => Pressed(Key.DownArrow, Key.S) || Debug("down");
        public static bool LeftPressed  => Pressed(Key.LeftArrow, Key.A) || Debug("left");
        public static bool RightPressed => Pressed(Key.RightArrow, Key.D) || Debug("right");

        public static bool UpHeld    => Held(Key.UpArrow, Key.W) || DebugHeld("up");
        public static bool DownHeld  => Held(Key.DownArrow, Key.S) || DebugHeld("down");
        public static bool LeftHeld  => Held(Key.LeftArrow, Key.A) || DebugHeld("left");
        public static bool RightHeld => Held(Key.RightArrow, Key.D) || DebugHeld("right");

        // ---- verification hooks: lets an automated play-test press buttons without real keys ----
        static readonly HashSet<string> debugPending = new HashSet<string>();
        static readonly Dictionary<string, float> debugHeldUntil = new Dictionary<string, float>();
        static int debugFrame = -1;

        // A press is live for exactly the next full frame after it is queued, whatever point in
        // the player loop queued it.
        public static void DebugPress(string action) { debugPending.Add(action); debugFrame = Time.frameCount + 1; }

        // A hold lasts `seconds` of real time, whatever the time scale.
        public static void DebugHold(string action, float seconds) { debugHeldUntil[action] = Time.unscaledTime + seconds; }

        static bool Debug(string action)
        {
            if (debugPending.Count == 0) return false;
            int f = Time.frameCount;
            if (f < debugFrame) return false;
            if (f > debugFrame) { debugPending.Clear(); return false; }
            return debugPending.Contains(action);
        }

        static bool DebugHeld(string action) => debugHeldUntil.TryGetValue(action, out var until) && Time.unscaledTime < until;

        // Debug hook so a verification drive can move the cursor without simulated key events.
        public static Vector2Int debugCursorNudge;
    }
}
