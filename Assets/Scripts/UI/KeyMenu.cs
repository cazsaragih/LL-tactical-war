using System;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Keyboard-first menu model (Up/Down, Z/Enter to choose, X to back out; Left/Right adjusts
    // sliders), the way the 2015 build drove its Move / Attack / Wait buttons. MenuView draws it.
    public class KeyMenu
    {
        public class Item
        {
            public Func<string> label;
            public Func<bool> enabled;
            public Action onSelect;
            public Action<int> onAdjust;
            public string detail;
        }

        public readonly List<Item> items = new List<Item>();
        public int selected;
        public Action onCancel;
        public string title;
        int openedFrame = -1;

        public KeyMenu(string title = null) { this.title = title; }

        public KeyMenu Add(string label, Action onSelect, Func<bool> enabled = null, string detail = null)
        {
            items.Add(new Item { label = () => label, onSelect = onSelect, enabled = enabled, detail = detail });
            return this;
        }

        public KeyMenu AddSlider(Func<string> label, Action<int> onAdjust)
        {
            items.Add(new Item { label = label, onAdjust = onAdjust, onSelect = () => onAdjust(1) });
            return this;
        }

        public bool IsEnabled(int i) => i >= 0 && i < items.Count && (items[i].enabled == null || items[i].enabled());

        public void Open()
        {
            openedFrame = Time.frameCount;
            if (!IsEnabled(selected)) selected = NextEnabled(-1, 1);
        }

        int NextEnabled(int from, int dir)
        {
            for (int n = 1; n <= items.Count; n++)
            {
                int i = ((from + dir * n) % items.Count + items.Count) % items.Count;
                if (IsEnabled(i)) return i;
            }
            return Mathf.Clamp(from, 0, items.Count - 1);
        }

        public void Tick()
        {
            if (Time.frameCount == openedFrame || items.Count == 0) return;
            if (GameInput.UpPressed) { selected = NextEnabled(selected, -1); AudioBank.I?.Play(Sfx.UiMove, 0.4f); }
            else if (GameInput.DownPressed) { selected = NextEnabled(selected, 1); AudioBank.I?.Play(Sfx.UiMove, 0.4f); }
            else if (GameInput.LeftPressed && items[selected].onAdjust != null) { items[selected].onAdjust(-1); AudioBank.I?.Play(Sfx.UiMove, 0.5f); }
            else if (GameInput.RightPressed && items[selected].onAdjust != null) { items[selected].onAdjust(1); AudioBank.I?.Play(Sfx.UiMove, 0.5f); }
            else if (GameInput.Confirm) Activate(selected);
            else if (GameInput.Cancel && onCancel != null) { AudioBank.I?.Play(Sfx.UiCancel); onCancel(); }
        }

        public void Activate(int i)
        {
            if (!IsEnabled(i)) { AudioBank.I?.Play(Sfx.UiCancel, 0.6f); return; }
            selected = i;
            AudioBank.I?.Play(Sfx.UiConfirm);
            items[i].onSelect?.Invoke();
        }
    }
}
