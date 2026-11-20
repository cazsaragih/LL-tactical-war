using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LLTacticalWar
{
    // Draws a KeyMenu as orders on a parchment card (or plain rows on a card that holds it). The card
    // unfolds, the rows slide in 8 px one after another, and the selection gets a brass pointer and an
    // ink underline that draws itself left to right. All of it is a function of time since the menu
    // opened and since the selection last moved, so it never drifts. Only the top menu takes input.
    public class MenuView : MonoBehaviour
    {
        [NonSerialized] public KeyMenu menu;
        public RectTransform panel;

        sealed class Row
        {
            public RectTransform rt, labelRt;
            public CanvasGroup group;
            public Image wash, pointer, underline;
            public TextMeshProUGUI label;
            public string text;
            public float textWidth;
            public Vector2 home;
        }

        const float RowH = 46f, Stagger = 0.035f, Slide = 8f;

        CanvasGroup group;
        TextMeshProUGUI title;
        Image titleRule;
        readonly List<Row> rows = new List<Row>();
        float width, pad, shownAt, selectedAt;
        int lastSelected = -1;
        bool framed;

        public bool IsTop => GameUI.I != null && GameUI.I.TopMenu == this;

        public static MenuView Create(Transform parent, string name, float width, bool framed)
        {
            var rt = GameUI.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 100f));
            var view = rt.gameObject.AddComponent<MenuView>();
            view.panel = rt;
            view.width = width;
            view.framed = framed;
            view.pad = framed ? 20f : 0f;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            if (framed)
            {
                var shadow = GameUI.Img(rt, "Shadow", UiKit.Shadow, Image.Type.Sliced, Palette.WithAlpha(Palette.Ink, 0.42f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                Stretch(shadow.rectTransform, -18f, -30f, -30f, -18f);
                var face = GameUI.Img(rt, "Face", UiKit.Card, Image.Type.Tiled, Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                Stretch(face.rectTransform, 0f, 0f, 0f, 0f);
            }
            view.title = GameUI.I.Text(rt, "", 24, Palette.Ink, TextAlignmentOptions.Left, GameUI.Face.Display);
            view.title.characterSpacing = 8f;
            var trt = view.title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0, 1); trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(view.pad + 14f, -20f); trt.sizeDelta = new Vector2(width - 2f * view.pad - 28f, 30f);
            view.titleRule = GameUI.Img(rt, "TitleRule", UiKit.Stroke, Image.Type.Simple, Palette.WithAlpha(Palette.Ink, 0.45f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(view.pad + 8f, -56f), new Vector2(width - 2f * view.pad - 16f, 12f));
            rt.gameObject.SetActive(false);
            return view;
        }

        static void Stretch(RectTransform rt, float left, float right, float bottom, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        public void Show(KeyMenu m)
        {
            menu = m;
            m.Open();
            foreach (var r in rows) Destroy(r.rt.gameObject);
            rows.Clear();
            bool hasTitle = !string.IsNullOrEmpty(m.title);
            title.text = hasTitle ? m.title : "";
            titleRule.gameObject.SetActive(hasTitle);
            float y = hasTitle ? -66f : -pad;
            for (int i = 0; i < m.items.Count; i++)
            {
                var r = new Row { home = new Vector2(pad, y) };
                r.rt = GameUI.Rect(panel, "Row" + i, new Vector2(0, 1), new Vector2(0, 1), r.home, new Vector2(width - 2f * pad, RowH));
                r.group = r.rt.gameObject.AddComponent<CanvasGroup>();
                r.wash = GameUI.Img(r.rt, "Wash", null, Image.Type.Simple, Palette.WithAlpha(Palette.PaperDark, 0f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                Stretch(r.wash.rectTransform, 4f, 4f, 2f, 2f);
                r.wash.raycastTarget = true;   // the mouse hovers rows through it (HoverSelect)
                r.pointer = GameUI.Img(r.rt, "Pointer", UiKit.Pointer, Image.Type.Simple, Color.white, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 1f), new Vector2(20f, 20f));
                r.label = GameUI.I.Text(r.rt, "", 26, Palette.Ink, TextAlignmentOptions.Left, GameUI.Face.Display);
                r.label.characterSpacing = 5f;
                r.labelRt = r.label.rectTransform;
                Stretch(r.labelRt, 42f, 10f, 4f, 0f);
                r.underline = GameUI.Img(r.rt, "Underline", UiKit.Stroke, Image.Type.Filled, Palette.Ink, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(38f, 8f), new Vector2(100f, 14f));
                var hs = r.rt.gameObject.AddComponent<HoverSelect>();
                hs.view = this;
                hs.index = i;
                rows.Add(r);
                y -= RowH + 2f;
            }
            panel.sizeDelta = new Vector2(width, -y + pad);
            gameObject.SetActive(true);
            GameUI.I.PushMenu(this);
            shownAt = Time.unscaledTime;
            lastSelected = -1;
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
            float now = Time.unscaledTime, since = now - shownAt;
            float unfold = Ease.OutCubic(Mathf.Clamp01(since / 0.12f));
            group.alpha = unfold;
            panel.localScale = framed ? new Vector3(1f, Mathf.Lerp(0.9f, 1f, unfold), 1f) : Vector3.one;
            if (menu.selected != lastSelected) { lastSelected = menu.selected; selectedAt = now; }
            for (int i = 0; i < rows.Count && i < menu.items.Count; i++)
            {
                var r = rows[i];
                bool en = menu.IsEnabled(i), sel = i == menu.selected;
                string text = menu.items[i].label();
                if (text != r.text) { r.text = text; r.label.text = text; r.textWidth = r.label.GetPreferredValues(text).x; }
                float k = Ease.OutCubic(Mathf.Clamp01((since - 0.03f - i * Stagger) / 0.16f));
                r.group.alpha = k;
                r.rt.anchoredPosition = r.home - new Vector2(Slide * (1f - k), 0f);
                r.label.color = !en ? Palette.WithAlpha(Palette.InkFaded, 0.6f) : sel ? Palette.Ink : Palette.WithAlpha(Palette.Ink, 0.78f);
                r.labelRt.offsetMin = new Vector2(sel ? 46f : 42f, 4f);
                r.pointer.enabled = sel;
                r.wash.color = Palette.WithAlpha(Palette.PaperDark, sel ? 0.42f : 0f);
                r.underline.enabled = sel;
                if (!sel) continue;
                r.underline.fillAmount = Ease.OutCubic(Mathf.Clamp01((now - selectedAt) / 0.16f));
                r.underline.rectTransform.sizeDelta = new Vector2(r.textWidth + 18f, 14f);
                r.underline.color = en ? Palette.Ink : Palette.InkFaded;
            }
        }
    }
}
