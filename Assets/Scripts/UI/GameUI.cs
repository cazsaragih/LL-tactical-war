using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LLTacticalWar
{
    // Every screen, built in code on one uGUI canvas with TextMeshPro, in the general's-paperwork
    // language UiKit draws: parchment cards with an ink border, an inner rule and brass corners, wax
    // seals for the two armies, rubber stamps, scrolls on wooden rollers. Headers are set in the
    // display face in small caps; numbers in the body face, large where they matter. Cards unfold,
    // forecasts slide in from their side, banners unroll. The combat forecast and the battle HUD live
    // in GameUI.Combat.cs, menus in MenuView.
    public partial class GameUI : MonoBehaviour
    {
        public static GameUI I;

        public enum Face { Display, Body }

        Canvas canvas;
        RectTransform root;
        CanvasGroup titleGroup, tacticGroup, bannerGroup, pauseGroup, endGroup;
        Image fadeImage, flashImage;

        // a parchment card: its root (with the CanvasGroup its motion fades) and where it rests
        sealed class Card { public RectTransform rt; public CanvasGroup group; public Vector2 home; }
        // a scroll: paper revealed between two rollers as it unrolls
        sealed class Scroll { public RectTransform root, mask, paper, rodL, rodR; public Vector2 size; }
        // a rubber stamp: a ruled frame and a word, inked in one colour
        sealed class StampMark { public RectTransform rt; public Image frame; public TextMeshProUGUI label; }
        // an inked HP bar: a trough, a pale ghost that lingers, the fill
        sealed class Bar { public Image fill, ghost; }

        // title
        Scroll titleScroll;
        RectTransform titleStamp;
        CanvasGroup promptGroup;
        float titleShownAt;
        bool titleLeaving;
        // tactics HUD
        Card turnPlate, armyPlate, hintSlip, unitCard;
        Image turnSeal;
        TextMeshProUGUI turnText, armyBlue, armyRed, hintText;
        Image cardSeal;
        TextMeshProUGUI cardName, cardClass, cardHp, cardHpMax, cardWeapon, cardStats;
        Bar cardBar;
        StampMark cardStamp;
        Player cardUnit;
        float cardHpTarget;
        int shownBlue = -1, shownRed = -1;
        // banner
        Scroll bannerScroll;
        TextMeshProUGUI bannerText, bannerSub;
        Image bannerSeal;
        Coroutine bannerRoutine;
        // menus
        MenuView contextMenu, pauseMenu, endMenu;
        readonly List<MenuView> menuStack = new List<MenuView>();
        Vector3 contextWorld;
        Scroll pauseScroll;
        // end
        Card endCard;
        TextMeshProUGUI endTitle, endSub;
        Image endSeal;
        readonly List<(TextMeshProUGUI label, TextMeshProUGUI value)> ledger = new List<(TextMeshProUGUI, TextMeshProUGUI)>();

        // unit tags + floating text
        class Tag { public RectTransform rt; public CanvasGroup group; public Image fill; public TextMeshProUGUI label; public float shown; }
        readonly Dictionary<Player, Tag> tags = new Dictionary<Player, Tag>();
        class Floater { public RectTransform rt; public TextMeshProUGUI text; public Vector3 world; public float t, life, rise, popFrom; public bool active; }
        readonly List<Floater> floaters = new List<Floater>();
        RectTransform tagLayer, floatLayer;

        readonly Dictionary<UnityEngine.Object, Coroutine> motions = new Dictionary<UnityEngine.Object, Coroutine>();

        public MenuView TopMenu => menuStack.Count > 0 ? menuStack[menuStack.Count - 1] : null;

        // ---------------- construction ----------------

        void Awake()
        {
            I = this;
            if (TMP_Settings.defaultFontAsset == null) Debug.LogError("[GameUI] TMP default font missing - run 'LL Tactical War/Run Setup' to import TMP Essentials.");
            UiKit.Build();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            tagLayer = Stretch(root, "Tags");
            floatLayer = Stretch(root, "Floaters");
            BuildTactic();
            BuildForecast();
            BuildBattleHud();
            BuildBanner();
            BuildTitle();
            contextMenu = MenuView.Create(root, "ContextMenu", 300f, true);
            BuildPause();
            BuildEnd();
            BuildOverlays();

            SetTacticHud(false);
        }

        // ---------------- helpers ----------------

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset;
            return rt;
        }

        // Ink on paper by default; `lifted` adds an ink outline and shadow for text over the world.
        public TextMeshProUGUI Text(Transform parent, string s, float size, Color c, TextAlignmentOptions align, Face face = Face.Body, bool lifted = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            SetFace(t, face, lifted);
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void SetFace(TextMeshProUGUI t, Face face, bool lifted)
        {
            bool display = face == Face.Display;
            t.font = display ? UiKit.Display : UiKit.Body;
            t.fontSharedMaterial = display ? (lifted ? UiKit.DisplayLifted : UiKit.DisplayInk) : (lifted ? UiKit.BodyLifted : UiKit.BodyInk);
            t.fontStyle = display ? FontStyles.SmallCaps : FontStyles.Bold;
            t.characterSpacing = display ? 4f : 1f;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Image.Type type, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var img = Rect(parent, name, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
            if (type == Image.Type.Filled) { img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; }
            return img;
        }

        static void Fill(RectTransform rt, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // A parchment card with a soft shadow. `brass` gives it the corner fittings and inner rule
        // (otherwise it is a plain slip); `grain` > 1 shrinks the card's slices for a short plate.
        Card MakeCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool brass = true, float grain = 1f)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            var shadow = Img(rt, "Shadow", UiKit.Shadow, Image.Type.Sliced, Palette.WithAlpha(Palette.Ink, 0.42f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(shadow.rectTransform, -18f, -30f, -30f, -18f);
            var face = Img(rt, "Face", brass ? UiKit.Card : UiKit.Slip, Image.Type.Tiled, Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            face.pixelsPerUnitMultiplier = grain;
            Fill(face.rectTransform);
            return new Card { rt = rt, group = g, home = pos };
        }

        Image SealAt(Transform parent, Team team, Vector2 anchor, Vector2 pos, float size, float tilt = -8f)
        {
            var img = Img(parent, "Seal", UiKit.Seal(team), Image.Type.Simple, Color.white, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            return img;
        }

        StampMark MakeStamp(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float tilt)
        {
            var rt = Rect(parent, "Stamp", anchor, new Vector2(0.5f, 0.5f), pos, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var frame = Img(rt, "Frame", UiKit.Stamp, Image.Type.Sliced, Palette.CrimsonDark, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            frame.pixelsPerUnitMultiplier = 1.6f;
            Fill(frame.rectTransform);
            var label = Text(rt, "", size.y * 0.5f, Palette.CrimsonDark, TextAlignmentOptions.Center, Face.Display);
            label.characterSpacing = 6f;
            Fill(label.rectTransform, 4f, 4f, 0f, 2f);
            return new StampMark { rt = rt, frame = frame, label = label };
        }

        static void SetStamp(StampMark s, string word, Color ink)
        {
            s.label.text = word;
            s.label.color = ink;
            s.frame.color = Palette.WithAlpha(ink, 0.9f);
        }

        Bar MakeBar(Transform parent, Vector2 pos, Vector2 size, Color fill, bool ghost)
        {
            var trough = Img(parent, "Bar", UiKit.Trough, Image.Type.Sliced, Color.white, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            trough.pixelsPerUnitMultiplier = size.y < 20f ? 2f : 1f;
            var b = new Bar();
            if (ghost)
            {
                b.ghost = Img(trough.transform, "Ghost", UiKit.Wash, Image.Type.Filled, Palette.Gold, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                Fill(b.ghost.rectTransform, 3f, 3f, 3f, 3f);
            }
            b.fill = Img(trough.transform, "Fill", UiKit.Wash, Image.Type.Filled, fill, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(b.fill.rectTransform, 3f, 3f, 3f, 3f);
            return b;
        }

        // A thin ink rule (the brush stroke at a steady width).
        Image Rule(Transform parent, Vector2 anchor, Vector2 pos, float width, float alpha = 0.55f)
        {
            return Img(parent, "Rule", UiKit.Stroke, Image.Type.Simple, Palette.WithAlpha(Palette.Ink, alpha), anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, 14f));
        }

        // Paper between two rollers; SetUnrolled reveals it from the middle out.
        Scroll MakeScroll(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var s = new Scroll { size = size };
            s.root = Rect(parent, name, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            s.mask = Rect(s.root, "Unrolled", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            s.mask.gameObject.AddComponent<RectMask2D>();
            var paper = Img(s.mask, "Paper", UiKit.Card, Image.Type.Tiled, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            paper.pixelsPerUnitMultiplier = size.y < 140f ? 1.4f : 1f;
            s.paper = paper.rectTransform;
            var rodSize = new Vector2(30f, size.y + 38f);
            s.rodL = Img(s.root, "RodL", UiKit.Rod, Image.Type.Sliced, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rodSize).rectTransform;
            s.rodR = Img(s.root, "RodR", UiKit.Rod, Image.Type.Sliced, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rodSize).rectTransform;
            SetUnrolled(s, 1f);
            return s;
        }

        static void SetUnrolled(Scroll s, float k)
        {
            float half = s.size.x * 0.5f * Mathf.Max(0f, k);
            s.mask.sizeDelta = new Vector2(half * 2f, s.size.y);
            s.rodL.anchoredPosition = new Vector2(-half - 4f, 0f);
            s.rodR.anchoredPosition = new Vector2(half + 4f, 0f);
        }

        CanvasGroup Group(string name)
        {
            var rt = Stretch(root, name);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
        }

        Image Dim(Transform parent, float alpha)
        {
            var dim = Img(parent, "Dim", null, Image.Type.Simple, Palette.WithAlpha(Palette.Ink, alpha), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(dim.rectTransform);
            return dim;
        }

        static void Show(CanvasGroup g, bool on) { g.alpha = on ? 1f : 0f; g.gameObject.SetActive(on); }

        public Vector2 WorldToCanvas(Vector3 world, out bool visible)
        {
            var cam = Camera.main;
            visible = false;
            if (cam == null) return Vector2.zero;
            var sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
            return local;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static string Tint(string s, Color c) => "<color=#" + Hex(c) + ">" + s + "</color>";

        // ---------------- motion ----------------

        void Animate(UnityEngine.Object key, IEnumerator routine)
        {
            if (motions.TryGetValue(key, out var running) && running != null) StopCoroutine(running);
            motions[key] = StartCoroutine(routine);
        }

        void Halt(UnityEngine.Object key)
        {
            if (motions.TryGetValue(key, out var running) && running != null) StopCoroutine(running);
            motions.Remove(key);
        }

        // A card unfolds: scale-Y up from 0.9 as it fades in, 120 ms, ease-out.
        void Unfold(Card c, float seconds = 0.12f)
        {
            c.rt.gameObject.SetActive(true);
            c.rt.anchoredPosition = c.home;
            Animate(c.rt, Ease.Tween(seconds, k => { c.group.alpha = k; c.rt.localScale = new Vector3(1f, Mathf.Lerp(0.9f, 1f, k), 1f); }, Ease.OutCubic));
        }

        // A card slides in from `from` px off its resting place along x, fading in.
        void SlideIn(Card c, float from, float seconds = 0.16f)
        {
            c.rt.gameObject.SetActive(true);
            c.rt.localScale = Vector3.one;
            Animate(c.rt, Ease.Tween(seconds, k => { c.group.alpha = k; c.rt.anchoredPosition = c.home + new Vector2(from * (1f - k), 0f); }, Ease.OutCubic));
        }

        void Hide(Card c)
        {
            Halt(c.rt);
            c.rt.gameObject.SetActive(false);
        }

        // A stamp lands: down from 1.5x in 140 ms, with a thud.
        void Slam(RectTransform rt, CanvasGroup g = null, float from = 1.5f)
        {
            rt.gameObject.SetActive(true);
            AudioBank.I?.Play(Sfx.Stamp, 0.8f);
            Animate(rt, Ease.Tween(0.14f, k => { rt.localScale = Vector3.one * Mathf.Lerp(from, 1f, k); if (g != null) g.alpha = k; }, Ease.InCubic));
        }

        IEnumerator Unroll(Scroll s, float from, float to, float seconds, Func<float, float> ease)
        {
            yield return Ease.Tween(seconds, k => SetUnrolled(s, Mathf.Lerp(from, to, k)), ease);
        }

        // ---------------- title ----------------

        void BuildTitle()
        {
            titleGroup = Group("Title");
            // the name, lettered on a parchment scroll hung in the sky above the board
            titleScroll = MakeScroll(titleGroup.transform, "TitleScroll", new Vector2(0.5f, 0.83f), new Vector2(1120f, 206f));
            var name = Text(titleScroll.paper, "LL Tactical " + Tint("War", Palette.CrimsonDark), 88, Palette.Ink, TextAlignmentOptions.Center, Face.Display);
            name.characterSpacing = 6f;
            Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(820f, 110f));
            Rule(titleScroll.paper, new Vector2(0.5f, 0.5f), new Vector2(-200f, -38f), 300f);
            Rule(titleScroll.paper, new Vector2(0.5f, 0.5f), new Vector2(200f, -38f), 300f);
            Img(titleScroll.paper, "Pointer", UiKit.Pointer, Image.Type.Simple, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(22f, 22f));
            var sub = Text(titleScroll.paper, "A Tactics Revival  ·  2015 / 2026", 26, Palette.InkFaded, TextAlignmentOptions.Center, Face.Display);
            sub.characterSpacing = 10f;
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(900f, 36f));
            SealAt(titleScroll.paper, Team.Player, new Vector2(0f, 0.5f), new Vector2(78f, 4f), 100f, 10f);
            SealAt(titleScroll.paper, Team.Enemy, new Vector2(1f, 0.5f), new Vector2(-78f, 4f), 100f, -10f);

            // "Press Enter", stamped in red ink on a slip, a red seal pressed over its end
            var slip = MakeCard(titleGroup.transform, "Prompt", new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 96f), false);
            promptGroup = slip.group;
            var stamp = MakeStamp(slip.rt, new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(400f, 66f), -3f);
            SetStamp(stamp, "Press Enter", Palette.CrimsonDark);
            stamp.label.fontSize = 34f;
            stamp.label.characterSpacing = 12f;
            titleStamp = stamp.rt;
            SealAt(slip.rt, Team.Enemy, new Vector2(0f, 0.5f), new Vector2(4f, 2f), 100f, -14f);

            var keys = Text(titleGroup.transform, "ARROWS / WASD  move     Z / ENTER  confirm     X  back     ESC  pause", 20, Palette.Parchment, TextAlignmentOptions.Center, Face.Body, true);
            keys.characterSpacing = 3f;
            Place(keys.rectTransform, new Vector2(0.5f, 0.045f), Vector2.zero, new Vector2(1600f, 30f));
            Show(titleGroup, false);
        }

        public void ShowTitle(bool on)
        {
            if (on)
            {
                Show(titleGroup, true);
                titleShownAt = Time.unscaledTime;
                titleLeaving = false;
                SetUnrolled(titleScroll, 1f);
                titleStamp.gameObject.SetActive(false);
                return;
            }
            if (!titleGroup.gameObject.activeSelf || titleLeaving) return;
            titleLeaving = true;
            Animate(titleGroup, LeaveTitle());
        }

        // Start: the stamp punches, the scroll rolls up and the title fades as the camera swings down.
        IEnumerator LeaveTitle()
        {
            titleStamp.gameObject.SetActive(true);
            yield return Ease.Tween(0.1f, k => titleStamp.localScale = Vector3.one * (1f + 0.14f * (1f - k)), Ease.OutCubic);
            StartCoroutine(Unroll(titleScroll, 1f, 0f, 0.3f, Ease.InCubic));
            yield return Ease.Tween(0.35f, k => titleGroup.alpha = 1f - k, Ease.InOutSine);
            Show(titleGroup, false);
        }

        void UpdateTitle()
        {
            if (!titleGroup.gameObject.activeSelf || titleLeaving) return;
            float since = Time.unscaledTime - titleShownAt;
            // the stamp comes down once the fade-in has cleared, then the slip breathes
            if (since >= 0.85f && !titleStamp.gameObject.activeSelf) Slam(titleStamp, null, 1.7f);
            promptGroup.alpha = since < 0.85f ? 0.85f : Mathf.Lerp(0.72f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.6f));
        }

        // ---------------- tactics HUD ----------------

        void BuildTactic()
        {
            tacticGroup = Group("TacticHUD");

            turnPlate = MakeCard(tacticGroup.transform, "TurnPlate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(600f, 70f), true, 1.6f);
            turnSeal = SealAt(turnPlate.rt, Team.Player, new Vector2(0f, 0.5f), new Vector2(6f, -2f), 78f);
            turnText = Text(turnPlate.rt, "", 32, Palette.AzureDark, TextAlignmentOptions.Center, Face.Display);
            turnText.characterSpacing = 6f;
            Fill(turnText.rectTransform, 50f, 10f, 2f, 0f);

            armyPlate = MakeCard(tacticGroup.transform, "ArmyPlate", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(372f, 70f), true, 1.6f);
            armyBlue = ArmyCount(Team.Player, "Your Army", 40f);
            armyRed = ArmyCount(Team.Enemy, "Enemy", 214f);

            hintSlip = MakeCard(tacticGroup.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 16f), new Vector2(980f, 46f), false, 1.4f);
            hintText = Text(hintSlip.rt, "", 19, Palette.Ink, TextAlignmentOptions.Center);
            Fill(hintText.rectTransform, 14f, 14f, 0f, 0f);

            BuildUnitCard();
        }

        TextMeshProUGUI ArmyCount(Team team, string label, float x)
        {
            SealAt(armyPlate.rt, team, new Vector2(0f, 0.5f), new Vector2(x, 0f), 52f, team == Team.Player ? -8f : 8f);
            var l = Text(armyPlate.rt, label, 17, Palette.InkFaded, TextAlignmentOptions.Left, Face.Display);
            TopLeft(l.rectTransform, new Vector2(x + 32f, -12f), new Vector2(120f, 20f));
            var n = Text(armyPlate.rt, "", 30, Palette.Ink, TextAlignmentOptions.Left);
            TopLeft(n.rectTransform, new Vector2(x + 32f, -30f), new Vector2(80f, 32f));
            return n;
        }

        void BuildUnitCard()
        {
            unitCard = MakeCard(tacticGroup.transform, "UnitCard", Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(560f, 244f));
            var c = unitCard.rt;
            cardSeal = SealAt(c, Team.Player, new Vector2(0f, 1f), new Vector2(18f, -14f), 88f);
            cardName = Text(c, "", 34, Palette.AzureDark, TextAlignmentOptions.Left, Face.Display);
            TopLeft(cardName.rectTransform, new Vector2(70f, -18f), new Vector2(330f, 42f));
            cardName.characterSpacing = 5f;
            cardName.enableAutoSizing = true;
            cardName.fontSizeMin = 22f;
            cardName.fontSizeMax = 34f;
            cardClass = Text(c, "", 15, Palette.InkFaded, TextAlignmentOptions.TopLeft);
            TopLeft(cardClass.rectTransform, new Vector2(72f, -60f), new Vector2(460f, 40f));
            cardClass.textWrappingMode = TextWrappingModes.Normal;
            cardClass.fontStyle = FontStyles.Italic;
            cardStamp = MakeStamp(c, new Vector2(1f, 1f), new Vector2(-84f, -40f), new Vector2(128f, 42f), 6f);

            var hpLabel = Text(c, "HP", 20, Palette.InkFaded, TextAlignmentOptions.Left, Face.Display);
            TopLeft(hpLabel.rectTransform, new Vector2(38f, -108f), new Vector2(40f, 24f));
            cardHp = Text(c, "", 40, Palette.Ink, TextAlignmentOptions.Right);
            TopLeft(cardHp.rectTransform, new Vector2(70f, -96f), new Vector2(66f, 44f));
            cardHpMax = Text(c, "", 20, Palette.InkFaded, TextAlignmentOptions.Left);
            TopLeft(cardHpMax.rectTransform, new Vector2(140f, -110f), new Vector2(70f, 24f));
            cardBar = MakeBar(c, new Vector2(214f, -110f), new Vector2(310f, 22f), Palette.Azure, false);

            cardWeapon = Text(c, "", 19, Palette.Ink, TextAlignmentOptions.Left);
            TopLeft(cardWeapon.rectTransform, new Vector2(38f, -152f), new Vector2(490f, 26f));
            Rule(c, new Vector2(0f, 1f), new Vector2(280f, -186f), 480f, 0.3f);
            cardStats = Text(c, "", 18, Palette.Ink, TextAlignmentOptions.Left);
            TopLeft(cardStats.rectTransform, new Vector2(38f, -198f), new Vector2(500f, 26f));
            unitCard.rt.gameObject.SetActive(false);
        }

        public void SetTacticHud(bool on) { Show(tacticGroup, on); tagLayer.gameObject.SetActive(on); }

        public void SetTurnInfo(Team team, int round)
        {
            turnText.text = (team == Team.Player ? "Your Turn" : "Enemy Turn") + "  ·  Round " + round;
            turnText.color = Palette.TeamDark(team);
            turnSeal.sprite = UiKit.Seal(team);
            Unfold(turnPlate, 0.18f);
        }

        // Keys in ink, what they do in faded ink: "ARROWS  move cursor     Z  select unit".
        public void SetHint(string s)
        {
            bool on = !string.IsNullOrEmpty(s);
            hintSlip.rt.gameObject.SetActive(on);
            if (on) hintText.text = HintMarkup(s);
        }

        static string HintMarkup(string s)
        {
            var parts = s.Split(new[] { "     " }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                int cut = parts[i].IndexOf("  ", StringComparison.Ordinal);
                parts[i] = cut < 0 ? Tint(parts[i], Palette.InkFaded) : parts[i].Substring(0, cut) + "  " + Tint(parts[i].Substring(cut + 2).Trim(), Palette.InkFaded);
            }
            return string.Join("      ", parts);
        }

        public void ShowUnitCard(Player p)
        {
            if (p == null) { HideUnitCard(); return; }
            bool fresh = p != cardUnit || !unitCard.rt.gameObject.activeSelf;
            cardUnit = p;
            var c = p.playerClass;
            cardSeal.sprite = UiKit.Seal(p.team);
            cardName.text = p.DisplayName;
            cardName.color = Palette.TeamDark(p.team);
            cardClass.text = c.ClassType + "  ·  " + c.ClassDescription;
            cardStamp.rt.gameObject.SetActive(p.broken || p.isTurnOver);
            SetStamp(cardStamp, p.broken ? "Broken" : "Done", p.broken ? Palette.CrimsonDark : Palette.InkFaded);
            var s = p.stats;
            var w = p.Weapon;
            cardHp.text = p.currentHealth.ToString();
            cardHp.color = p.currentHealth <= p.MaxHP / 4 ? Palette.Crimson : Palette.Ink;
            cardHpMax.text = "/ " + p.MaxHP;
            cardBar.fill.color = Palette.Team(p.team);
            string L(string label) => Tint(label, Palette.InkFaded);
            cardWeapon.text = w.Name.ToUpper() + "      " + L("MT") + " " + w.Might + "    " + L("HIT") + " " + w.Hit + "    " + L("RNG") + " " + w.RangeText;
            cardStats.text = (w.Magic ? L("MAG") + " " + s.Mag : L("STR") + " " + s.Str) + "  " + L("SKL") + " " + s.Skl + "  " + L("SPD") + " " + s.Spd + "  " + L("LCK") + " " + s.Lck
                             + "  " + L("DEF") + " " + s.Def + "  " + L("RES") + " " + s.Res + "  " + L("MOV") + " " + c.TileMove;
            cardHpTarget = p.MaxHP > 0 ? (float)p.currentHealth / p.MaxHP : 0f;
            if (fresh) { cardBar.fill.fillAmount = cardHpTarget; Unfold(unitCard); }
        }

        public void HideUnitCard() { Hide(unitCard); cardUnit = null; }

        public void RegisterUnit(Player p)
        {
            if (tags.ContainsKey(p)) return;
            var rt = Rect(tagLayer, "Tag_" + p.name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(128f, 36f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var face = Img(rt, "Face", UiKit.Slip, Image.Type.Tiled, Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            face.pixelsPerUnitMultiplier = 1.6f;
            Fill(face.rectTransform);
            SealAt(rt, p.team, new Vector2(0f, 0.5f), new Vector2(4f, 1f), 34f);
            var label = Text(rt, "", 15, Palette.TeamDark(p.team), TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.42f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(22f, 0f); lrt.offsetMax = new Vector2(-4f, -1f);
            var bar = MakeBar(rt, new Vector2(26f, -24f), new Vector2(94f, 10f), Palette.Team(p.team), false);
            bar.fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            bar.fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            tags[p] = new Tag { rt = rt, group = group, fill = bar.fill, label = label, shown = 1f };
        }

        // ---------------- menus ----------------

        public void PushMenu(MenuView v) { menuStack.Remove(v); menuStack.Add(v); }
        public void PopMenu(MenuView v) { menuStack.Remove(v); }

        public void ShowContextMenu(KeyMenu m, Vector3 world)
        {
            contextWorld = world;
            contextMenu.Show(m);
            PositionContext();
        }

        public void HideContextMenu() { contextMenu.Hide(); }

        void PositionContext()
        {
            var p = WorldToCanvas(contextWorld + Vector3.up * 1.5f, out _);
            contextMenu.panel.anchoredPosition = p + new Vector2(70f, 60f);
        }

        void BuildPause()
        {
            pauseGroup = Group("Pause");
            Dim(pauseGroup.transform, 0.58f);
            pauseScroll = MakeScroll(pauseGroup.transform, "PauseScroll", new Vector2(0.5f, 0.79f), new Vector2(640f, 128f));
            var t = Text(pauseScroll.paper, "Paused", 66, Palette.Ink, TextAlignmentOptions.Center, Face.Display);
            t.characterSpacing = 14f;
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(560f, 90f));
            SealAt(pauseScroll.paper, Team.Player, new Vector2(0f, 0.5f), new Vector2(70f, 0f), 80f, 8f);
            SealAt(pauseScroll.paper, Team.Enemy, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), 80f, -8f);
            pauseMenu = MenuView.Create(pauseGroup.transform, "PauseMenu", 460f, true);
            pauseMenu.panel.anchorMin = pauseMenu.panel.anchorMax = new Vector2(0.5f, 0.68f);
            pauseMenu.panel.pivot = new Vector2(0.5f, 1f);
            Show(pauseGroup, false);
        }

        public void ShowPause(KeyMenu m)
        {
            Show(pauseGroup, true);
            pauseGroup.transform.SetAsLastSibling();
            fadeImage.transform.parent.SetAsLastSibling();
            Animate(pauseScroll.root, Unroll(pauseScroll, 0.15f, 1f, 0.26f, Ease.OutCubic));
            pauseMenu.Show(m);
        }

        public void HidePause() { pauseMenu.Hide(); Show(pauseGroup, false); }

        // ---------------- end ----------------

        void BuildEnd()
        {
            endGroup = Group("Ended");
            Dim(endGroup.transform, 0.55f);
            endCard = MakeCard(endGroup.transform, "Dispatch", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(960f, 660f));
            var c = endCard.rt;
            endTitle = Text(c, "Victory", 124, Palette.AzureDark, TextAlignmentOptions.Center, Face.Display);
            endTitle.characterSpacing = 16f;
            Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(860f, 150f));
            endSub = Text(c, "", 26, Palette.InkFaded, TextAlignmentOptions.Center, Face.Display);
            endSub.characterSpacing = 10f;
            Place(endSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(760f, 36f));
            Rule(c, new Vector2(0.5f, 1f), new Vector2(0f, -226f), 640f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                float y = -268f - i * 62f;
                var l = Text(c, "", 26, Palette.InkFaded, TextAlignmentOptions.Left, Face.Display);
                l.characterSpacing = 6f;
                Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-150f, y), new Vector2(420f, 40f));
                var v = Text(c, "", 44, Palette.Ink, TextAlignmentOptions.Right);
                Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(230f, y), new Vector2(160f, 50f));
                Rule(c, new Vector2(0.5f, 1f), new Vector2(0f, y - 30f), 620f, 0.18f);
                ledger.Add((l, v));
            }
            endSeal = SealAt(c, Team.Player, new Vector2(1f, 1f), new Vector2(-64f, -58f), 176f, -12f);
            endMenu = MenuView.Create(c, "EndMenu", 400f, false);
            endMenu.panel.anchorMin = endMenu.panel.anchorMax = new Vector2(0.5f, 0f);
            endMenu.panel.pivot = new Vector2(0.5f, 0f);
            endMenu.panel.anchoredPosition = new Vector2(0f, 26f);
            Show(endGroup, false);
        }

        // The end of the match as a dispatch: the verdict, the winner's seal pressed on, the ledger.
        public void ShowEnded(bool victory, IReadOnlyList<(string label, int value)> rows, KeyMenu menu)
        {
            SetTacticHud(false);
            HideForecast();
            Show(endGroup, true);
            endGroup.transform.SetAsLastSibling();
            fadeImage.transform.parent.SetAsLastSibling();
            var winner = victory ? Team.Player : Team.Enemy;
            endTitle.text = victory ? "Victory" : "Defeat";
            endTitle.color = Palette.TeamDark(winner);
            endSub.text = victory ? "The field is held" : "The field is lost";
            endSeal.sprite = UiKit.Seal(winner);
            for (int i = 0; i < ledger.Count; i++)
            {
                bool used = i < rows.Count;
                ledger[i].label.text = used ? rows[i].label : "";
                ledger[i].value.text = used ? "0" : "";
            }
            StartCoroutine(EndMoment(victory, rows, menu));
        }

        IEnumerator EndMoment(bool victory, IReadOnlyList<(string label, int value)> rows, KeyMenu menu)
        {
            Flash(victory ? Palette.Gold : Palette.CrimsonLight);
            endSeal.gameObject.SetActive(false);
            Unfold(endCard, 0.22f);
            yield return Ease.WaitUnscaled(0.3f);
            Slam(endSeal.rectTransform, null, 2.2f);
            yield return Ease.WaitUnscaled(0.18f);
            yield return Ease.Tween(0.45f, k =>
            {
                for (int i = 0; i < rows.Count && i < ledger.Count; i++) ledger[i].value.text = Mathf.RoundToInt(rows[i].value * k).ToString();
            }, Ease.OutCubic);
            endMenu.Show(menu);
        }

        // ---------------- banners, flash, fade ----------------

        void BuildBanner()
        {
            bannerGroup = Group("Banner");
            bannerScroll = MakeScroll(bannerGroup.transform, "TurnScroll", new Vector2(0.5f, 0.6f), new Vector2(980f, 150f));
            bannerSeal = SealAt(bannerScroll.paper, Team.Player, new Vector2(0f, 0.5f), new Vector2(92f, 2f), 104f);
            bannerText = Text(bannerScroll.paper, "", 76, Palette.AzureDark, TextAlignmentOptions.Center, Face.Display);
            bannerText.characterSpacing = 14f;
            Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, 16f), new Vector2(760f, 96f));
            bannerSub = Text(bannerScroll.paper, "", 26, Palette.InkFaded, TextAlignmentOptions.Center, Face.Display);
            bannerSub.characterSpacing = 12f;
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, -44f), new Vector2(600f, 36f));
            Show(bannerGroup, false);
        }

        // The turn banner: a scroll unrolls from the middle as a horn sounds, holds, and rolls up.
        public void Banner(string title, string sub, Team team)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(title, sub, team));
        }

        IEnumerator BannerRoutine(string title, string sub, Team team)
        {
            Show(bannerGroup, true);
            bannerText.text = title;
            bannerText.color = Palette.TeamDark(team);
            bannerSub.text = sub ?? "";
            bannerSeal.sprite = UiKit.Seal(team);
            SetUnrolled(bannerScroll, 0f);
            AudioBank.I?.Play(Sfx.Horn, 0.75f, 0f, 0f);
            yield return Unroll(bannerScroll, 0f, 1f, 0.48f, Ease.OutCubic);
            yield return Ease.WaitUnscaled(0.95f);
            yield return Ease.Tween(0.28f, k => { SetUnrolled(bannerScroll, 1f - k); bannerGroup.alpha = 1f - k * k; }, Ease.InCubic);
            Show(bannerGroup, false);
            bannerRoutine = null;
        }

        void BuildOverlays()
        {
            var fx = Stretch(root, "Overlays");
            flashImage = Img(fx, "Flash", null, Image.Type.Simple, Palette.WithAlpha(Palette.Gold, 0f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(flashImage.rectTransform);
            fadeImage = Img(fx, "Fade", null, Image.Type.Simple, Palette.WithAlpha(Palette.Ink, 1f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Fill(fadeImage.rectTransform);
        }

        public void Flash(Color c)
        {
            StartCoroutine(Ease.Tween(0.12f, k => flashImage.color = Palette.WithAlpha(c, 0.6f * (1f - k)), Ease.OutCubic));
        }

        public IEnumerator Fade(float to, float duration)
        {
            float from = fadeImage.color.a;
            yield return Ease.Tween(duration, k => fadeImage.color = Palette.WithAlpha(Palette.Ink, Mathf.Lerp(from, to, k)), Ease.InOutSine);
        }

        public float FadeAlpha => fadeImage.color.a;

        // ---------------- floating text ----------------

        // rise: world units it drifts up over its life. popFrom: the scale it pops in from
        // (above 1 for a stamp that slams down, below 1 for a number that springs up). Numbers are set
        // in the body face, words (MISS, CRITICAL, BREAK) in the display face.
        public void FloatText(Vector3 world, string text, Color color, float scale = 1f, float rise = 1.2f, float popFrom = 0.5f)
        {
            Floater f = null;
            foreach (var x in floaters) if (!x.active) { f = x; break; }
            if (f == null)
            {
                if (floaters.Count >= 40) f = floaters[0];
                else
                {
                    var t = Text(floatLayer, "", 30, Color.white, TextAlignmentOptions.Center, Face.Body, true);
                    t.rectTransform.sizeDelta = new Vector2(700, 60);
                    f = new Floater { rt = t.rectTransform, text = t };
                    floaters.Add(f);
                }
            }
            bool number = text.Length > 0 && char.IsDigit(text[0]);
            SetFace(f.text, number ? Face.Body : Face.Display, true);
            if (!number) f.text.characterSpacing = 6f;
            f.world = world;
            f.text.text = text;
            f.text.color = color;
            f.text.fontSize = 30f * scale;
            f.t = 0f;
            f.life = 1.1f;
            f.rise = rise;
            f.popFrom = popFrom;
            f.active = true;
            PlaceFloater(f);   // its first frame already stands where it belongs
        }

        void PlaceFloater(Floater f)
        {
            var pos = WorldToCanvas(f.world + Vector3.up * (Ease.OutCubic(f.t) * f.rise), out bool vis);
            f.rt.anchoredPosition = pos;
            f.text.alpha = f.t < 0.7f ? 1f : 1f - (f.t - 0.7f) / 0.3f;
            float pop = f.t < 0.15f ? Mathf.LerpUnclamped(f.popFrom, 1f, Ease.OutBack(f.t / 0.15f)) : 1f;
            f.rt.localScale = Vector3.one * pop;
            f.rt.gameObject.SetActive(vis);
        }

        // ---------------- per-frame ----------------

        void Update()
        {
            UpdateTitle();
            if (contextMenu.gameObject.activeSelf) PositionContext();

            // card bars tween instead of snapping
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
            cardBar.fill.fillAmount = Mathf.Lerp(cardBar.fill.fillAmount, cardHpTarget, k);

            UpdateTags(k);
            UpdateFloaters();
            UpdateBattleHud(Time.unscaledDeltaTime);

            var tMan = TurnManager.instance;
            if (tMan != null && tacticGroup.gameObject.activeSelf)
            {
                if (tMan.players.Count != shownBlue) armyBlue.text = (shownBlue = tMan.players.Count).ToString();
                if (tMan.enemies.Count != shownRed) armyRed.text = (shownRed = tMan.enemies.Count).ToString();
            }
        }

        static readonly List<Player> deadTags = new List<Player>();
        void UpdateTags(float k)
        {
            deadTags.Clear();
            foreach (var kv in tags)
            {
                var p = kv.Key;
                var tag = kv.Value;
                if (p == null) { deadTags.Add(p); Destroy(tag.rt.gameObject); continue; }
                // a rider sits higher: its tag rides up with it, clear of the helmet
                var pos = WorldToCanvas(p.transform.position + Vector3.up * (2.55f + p.model.Lift * 0.6f), out bool vis);
                tag.rt.gameObject.SetActive(vis && p.isAlive && p.gameObject.activeInHierarchy);
                tag.rt.anchoredPosition = pos;
                tag.label.text = (p.broken ? Tint("BROKEN", Palette.CrimsonDark) : p.playerClass.ClassName.ToUpper()) + "  " + Tint(p.currentHealth.ToString(), Palette.Ink);
                tag.shown = Mathf.Lerp(tag.shown, p.MaxHP > 0 ? (float)p.currentHealth / p.MaxHP : 0f, k);
                tag.fill.fillAmount = tag.shown;
                tag.group.alpha = p.isTurnOver ? 0.6f : 1f;
            }
            foreach (var d in deadTags) tags.Remove(d);
        }

        void UpdateFloaters()
        {
            foreach (var f in floaters)
            {
                if (!f.active) continue;
                f.t += Time.unscaledDeltaTime / f.life;
                if (f.t >= 1f) { f.active = false; f.rt.gameObject.SetActive(false); continue; }
                PlaceFloater(f);
            }
        }
    }
}
