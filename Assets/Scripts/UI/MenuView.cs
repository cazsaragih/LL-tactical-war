using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LLTacticalWar
{
    // Draws a KeyMenu as a palette-styled panel. Only the top-most visible menu takes input.
    public class MenuView : MonoBehaviour
    {
        [NonSerialized] public KeyMenu menu;
        public Func<int> marked;
        public RectTransform panel;

        TextMeshProUGUI title;
        readonly List<(Image bg, TextMeshProUGUI label, Image pip)> rows = new List<(Image, TextMeshProUGUI, Image)>();
        float width;
        float appear;

        public bool IsTop => GameUI.I != null && GameUI.I.TopMenu == this;

        public static MenuView Create(Transform parent, string name, float width)
        {
            var rt = GameUI.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 100f));
            var view = rt.gameObject.AddComponent<MenuView>();
            view.panel = rt;
            view.width = width;
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = GameUI.I.RoundSprite;
            bg.type = Image.Type.Sliced;
            bg.color = Palette.WithAlpha(Palette.Panel, 0.94f);
            view.title = GameUI.I.Text(rt, "", 22, Palette.Gold, TextAlignmentOptions.Left);
            var trt = view.title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0, 1); trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(22, -14); trt.sizeDelta = new Vector2(width - 44, 30);
            rt.gameObject.SetActive(false);
            return view;
        }

        public void Show(KeyMenu m, Func<int> markedIndex = null)
        {
            menu = m;
            marked = markedIndex;
            m.Open();
            foreach (var r in rows) Destroy(r.bg.gameObject);
            rows.Clear();
            bool hasTitle = !string.IsNullOrEmpty(m.title);
            title.text = hasTitle ? m.title : "";
            float y = hasTitle ? -52f : -14f;
            for (int i = 0; i < m.items.Count; i++)
            {
                var row = GameUI.Rect(panel, "Row" + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, y), new Vector2(width - 24, 44));
                var img = row.gameObject.AddComponent<Image>();
                img.sprite = GameUI.I.RoundSprite;
                img.type = Image.Type.Sliced;
                var label = GameUI.I.Text(row, "", 24, Palette.Parchment, TextAlignmentOptions.Left);
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(34, 0); lrt.offsetMax = new Vector2(-10, 0);
                var pip = GameUI.Rect(row, "Pip", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(17, 0), new Vector2(10, 10)).gameObject.AddComponent<Image>();
                pip.sprite = GameUI.I.RoundSprite;
                pip.color = Palette.Gold;
                var hs = row.gameObject.AddComponent<HoverSelect>();
                hs.view = this;
                hs.index = i;
                rows.Add((img, label, pip));
                y -= 50f;
            }
            panel.sizeDelta = new Vector2(width, -y + 8f);
            gameObject.SetActive(true);
            GameUI.I.PushMenu(this);
            appear = 0f;
            Refresh();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            GameUI.I.PopMenu(this);
            menu = null;
        }

        void Update()
        {
            if (menu == null) return;
            var current = menu;
            if (IsTop) current.Tick();
            if (menu != current || menu == null) return;   // the action swapped or closed the menu
            Refresh();
        }

        void Refresh()
        {
            appear = Mathf.Min(1f, appear + Time.unscaledDeltaTime / 0.18f);
            float s = Mathf.LerpUnclamped(0.85f, 1f, Ease.OutBack(appear));
            panel.localScale = new Vector3(s, s, 1f);
            int mk = marked != null ? marked() : -1;
            for (int i = 0; i < rows.Count && i < menu.items.Count; i++)
            {
                bool en = menu.IsEnabled(i), sel = i == menu.selected;
                var (bg, label, pip) = rows[i];
                label.text = menu.items[i].label();
                label.color = !en ? Palette.Muted : sel ? Palette.Ink : Palette.Parchment;
                bg.color = sel ? (en ? Palette.Gold : Palette.WithAlpha(Palette.Muted, 0.5f)) : Palette.WithAlpha(Palette.PanelLight, 0.9f);
                pip.gameObject.SetActive(mk == i || sel);
                pip.color = mk == i ? (sel ? Palette.Ink : Palette.Gold) : Palette.Ink;
            }
        }
    }
}
