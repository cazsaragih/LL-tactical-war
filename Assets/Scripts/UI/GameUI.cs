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
    // Every screen, built in code on one uGUI canvas with TextMeshPro: title, tactics HUD,
    // unit tags, menus, battle HUD, pause, ending, banners, floating text and the fade.
    // This replaces the 2015 OnGUI labels and buttons.
    public class GameUI : MonoBehaviour
    {
        public static GameUI I;

        public Sprite RoundSprite { get; private set; }
        Sprite whiteSprite;
        TMP_FontAsset font;
        Material outlineMat, titleMat;

        Canvas canvas;
        RectTransform root;
        CanvasGroup titleGroup, tacticGroup, warGroup, bannerGroup, pauseGroup, endGroup;
        Image fadeImage, flashImage;

        // title
        TextMeshProUGUI titlePrompt;
        // tactics HUD
        TextMeshProUGUI turnText, armyText, hintText;
        RectTransform unitCard;
        TextMeshProUGUI cardName, cardClass, cardStats, cardHp, cardTroops;
        Image cardStripe, cardHpFill, cardTroopFill;
        float cardHpTarget, cardTroopTarget;
        // banner
        TextMeshProUGUI bannerText, bannerSub;
        Coroutine bannerRoutine;
        // war HUD
        TextMeshProUGUI warTimer, warPName, warEName, warPTroops, warETroops, warSkill;
        Image warPHp, warEHp, warSkillFill;
        Soldier boundP, boundE;
        // menus
        MenuView contextMenu, sideMenu, pauseMenu, endMenu;
        readonly List<MenuView> menuStack = new List<MenuView>();
        Vector3 contextWorld;
        TextMeshProUGUI endTitle, endStats;

        // unit tags + floating text
        class Tag { public RectTransform rt; public Image fill; public TextMeshProUGUI label; public float shown; }
        readonly Dictionary<Player, Tag> tags = new Dictionary<Player, Tag>();
        class Floater { public RectTransform rt; public TextMeshProUGUI text; public Vector3 world; public float t, life; public bool active; }
        readonly List<Floater> floaters = new List<Floater>();
        RectTransform tagLayer, floatLayer;

        public MenuView TopMenu => menuStack.Count > 0 ? menuStack[menuStack.Count - 1] : null;

        // ---------------- construction ----------------

        void Awake()
        {
            I = this;
            try { font = TMP_Settings.defaultFontAsset; } catch (Exception) { font = null; }
            if (font == null) Debug.LogError("[GameUI] TMP default font missing - run 'LL Tactical War/Run Setup' to import TMP Essentials.");
            MakeSprites();
            MakeMaterials();

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
            BuildWar();
            BuildBanner();
            BuildTitle();
            contextMenu = MenuView.Create(root, "ContextMenu", 300f);
            sideMenu = MenuView.Create(root, "SideMenu", 440f);
            BuildPause();
            BuildEnd();
            BuildOverlays();

            SetTacticHud(false);
            SetWarHud(false);
        }

        void MakeSprites()
        {
            const int s = 64, r = 18;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            tex.Apply();
            RoundSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            var w = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            w.SetPixels(px); w.Apply();
            whiteSprite = Sprite.Create(w, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        void MakeMaterials()
        {
            if (font == null) return;
            outlineMat = new Material(font.material) { name = "TMP_Outline" };
            outlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, Palette.Ink);
            outlineMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);

            titleMat = new Material(outlineMat) { name = "TMP_Title" };
            titleMat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            titleMat.SetColor(ShaderUtilities.ID_UnderlayColor, Palette.WithAlpha(Palette.Ink, 0.85f));
            titleMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.7f);
            titleMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
            titleMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
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

        static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public TextMeshProUGUI Text(Transform parent, string s, float size, Color c, TextAlignmentOptions align, Material mat = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if ((mat ?? outlineMat) != null) t.fontSharedMaterial = mat ?? outlineMat;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 3f;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Image Bar(Transform parent, Vector2 pos, Vector2 size, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, "Bar", new Vector2(0, 1), new Vector2(0, 1), pos, size, Palette.WithAlpha(Palette.Ink, 0.9f));
            var frt = Rect(bg.transform, "Fill", Vector2.zero, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            fillImage = frt.gameObject.AddComponent<Image>();
            fillImage.sprite = whiteSprite;          // Filled needs a sprite, or it silently does nothing
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.color = fill;
            fillImage.raycastTarget = false;
            return bg;
        }

        CanvasGroup Group(string name)
        {
            var rt = Stretch(root, name);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
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

        // ---------------- title ----------------

        void BuildTitle()
        {
            titleGroup = Group("Title");
            var scrim = Panel(titleGroup.transform, "Scrim", new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 260), Palette.WithAlpha(Palette.Ink, 0.62f));
            var name = Text(titleGroup.transform, "LL TACTICAL WAR", 132, Palette.Gold, TextAlignmentOptions.Center, titleMat);
            Place(name.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0, 22), new Vector2(1600, 160));
            name.characterSpacing = 8f;
            var sub = Text(titleGroup.transform, "A TACTICS REVIVAL  -  2015 / 2026", 30, Palette.Parchment, TextAlignmentOptions.Center);
            Place(sub.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0, -78), new Vector2(1200, 40));
            sub.characterSpacing = 12f;
            titlePrompt = Text(titleGroup.transform, "PRESS ENTER TO BEGIN", 30, Palette.Parchment, TextAlignmentOptions.Center);
            Place(titlePrompt.rectTransform, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(900, 40));
            titlePrompt.characterSpacing = 14f;
            var credit = Text(titleGroup.transform, "ARROWS / WASD  move     Z / ENTER  confirm     X  back     ESC  pause", 20, Palette.WithAlpha(Palette.Parchment, 0.7f), TextAlignmentOptions.Center);
            Place(credit.rectTransform, new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(1600, 30));
            Show(titleGroup, false);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public void ShowTitle(bool on) { Show(titleGroup, on); }

        // ---------------- tactics HUD ----------------

        void BuildTactic()
        {
            tacticGroup = Group("TacticHUD");
            var top = Panel(tacticGroup.transform, "TurnPill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(560, 58), Palette.WithAlpha(Palette.Panel, 0.92f));
            turnText = Text(top.transform, "", 28, Palette.Parchment, TextAlignmentOptions.Center);
            Fill(turnText.rectTransform);

            var army = Panel(tacticGroup.transform, "Army", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -22), new Vector2(380, 58), Palette.WithAlpha(Palette.Panel, 0.92f));
            armyText = Text(army.transform, "", 22, Palette.Parchment, TextAlignmentOptions.Center);
            armyText.richText = true;
            Fill(armyText.rectTransform);

            var hint = Panel(tacticGroup.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(1100, 48), Palette.WithAlpha(Palette.Panel, 0.85f));
            hintText = Text(hint.transform, "", 20, Palette.Parchment, TextAlignmentOptions.Center);
            Fill(hintText.rectTransform);

            var card = Panel(tacticGroup.transform, "UnitCard", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(440, 236), Palette.WithAlpha(Palette.Panel, 0.94f));
            unitCard = card.rectTransform;
            cardStripe = Panel(card.transform, "Stripe", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(14, 212), Palette.Azure);
            cardName = Text(card.transform, "", 32, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(cardName.rectTransform, new Vector2(38, -12), new Vector2(390, 40));
            cardClass = Text(card.transform, "", 16, Palette.Muted, TextAlignmentOptions.TopLeft);
            TopLeft(cardClass.rectTransform, new Vector2(40, -52), new Vector2(380, 44));
            cardClass.textWrappingMode = TextWrappingModes.Normal;
            cardClass.characterSpacing = 1f;
            cardHp = Text(card.transform, "", 18, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(cardHp.rectTransform, new Vector2(40, -104), new Vector2(390, 22));
            Bar(card.transform, new Vector2(40, -128), new Vector2(380, 20), Palette.Gold, out cardHpFill);
            cardTroops = Text(card.transform, "", 18, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(cardTroops.rectTransform, new Vector2(40, -154), new Vector2(390, 22));
            Bar(card.transform, new Vector2(40, -178), new Vector2(380, 14), Palette.Azure, out cardTroopFill);
            cardStats = Text(card.transform, "", 18, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(cardStats.rectTransform, new Vector2(40, -204), new Vector2(390, 22));
            unitCard.gameObject.SetActive(false);
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 0); rt.offsetMax = new Vector2(-12, 0);
        }

        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public void SetTacticHud(bool on) { Show(tacticGroup, on); tagLayer.gameObject.SetActive(on); }
        public void SetTurnInfo(string s, Color c) { turnText.text = s; turnText.color = c; }
        public void SetHint(string s) { hintText.text = s; hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s)); }

        public void ShowUnitCard(Player p)
        {
            if (p == null) { HideUnitCard(); return; }
            unitCard.gameObject.SetActive(true);
            var c = p.playerClass;
            cardStripe.color = Palette.Team(p.team);
            cardName.text = p.DisplayName.ToUpper();
            cardName.color = Palette.TeamLight(p.team);
            cardClass.text = c.ClassType.ToUpper() + "  -  " + c.ClassDescription;
            cardHp.text = "GENERAL HP  " + p.currentHealth + " / " + p.maxHealth;
            cardTroops.text = "TROOPS  " + p.currentUnits + " / " + p.startUnits;
            cardStats.text = "ATK " + p.modifiedAttack + "    DEF " + p.modifiedDefense + "    MOVE " + c.TileMove + "    RANGE " + c.TileAttack + (p.isTurnOver ? "    DONE" : "");
            cardHpTarget = p.maxHealth > 0 ? (float)p.currentHealth / p.maxHealth : 0f;
            cardTroopTarget = p.startUnits > 0 ? (float)p.currentUnits / p.startUnits : 0f;
            cardTroopFill.color = Palette.Team(p.team);
        }

        public void HideUnitCard() { unitCard.gameObject.SetActive(false); }

        public void RegisterUnit(Player p)
        {
            if (tags.ContainsKey(p)) return;
            var bg = Panel(tagLayer, "Tag_" + p.name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(96, 34), Palette.WithAlpha(Palette.Panel, 0.85f));
            var label = Text(bg.transform, "", 15, Palette.TeamLight(p.team), TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.45f); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var barBg = Panel(bg.transform, "HpBg", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 5), new Vector2(84, 10), Palette.Ink);
            var frt = Rect(barBg.transform, "Fill", Vector2.zero, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            var fill = frt.gameObject.AddComponent<Image>();
            fill.sprite = whiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = Palette.Team(p.team);
            fill.raycastTarget = false;
            tags[p] = new Tag { rt = bg.rectTransform, fill = fill, label = label, shown = 1f };
        }

        // ---------------- war HUD ----------------

        void BuildWar()
        {
            warGroup = Group("WarHUD");
            var left = Panel(warGroup.transform, "PlayerGeneral", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -22), new Vector2(520, 112), Palette.WithAlpha(Palette.Panel, 0.92f));
            warPName = Text(left.transform, "", 26, Palette.AzureLight, TextAlignmentOptions.Left);
            TopLeft(warPName.rectTransform, new Vector2(20, -10), new Vector2(480, 32));
            Bar(left.transform, new Vector2(20, -46), new Vector2(480, 24), Palette.Azure, out warPHp);
            warPTroops = Text(left.transform, "", 18, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(warPTroops.rectTransform, new Vector2(20, -78), new Vector2(480, 24));

            var right = Panel(warGroup.transform, "EnemyGeneral", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -22), new Vector2(520, 112), Palette.WithAlpha(Palette.Panel, 0.92f));
            warEName = Text(right.transform, "", 26, Palette.CrimsonLight, TextAlignmentOptions.Right);
            TopLeft(warEName.rectTransform, new Vector2(20, -10), new Vector2(480, 32));
            Bar(right.transform, new Vector2(20, -46), new Vector2(480, 24), Palette.Crimson, out warEHp);
            warEHp.fillOrigin = (int)Image.OriginHorizontal.Right;
            warETroops = Text(right.transform, "", 18, Palette.Parchment, TextAlignmentOptions.Right);
            TopLeft(warETroops.rectTransform, new Vector2(20, -78), new Vector2(480, 24));

            var timer = Panel(warGroup.transform, "Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(150, 80), Palette.WithAlpha(Palette.Panel, 0.92f));
            warTimer = Text(timer.transform, "60", 46, Palette.Gold, TextAlignmentOptions.Center);
            Fill(warTimer.rectTransform);

            var skill = Panel(warGroup.transform, "Skill", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 90), new Vector2(330, 74), Palette.WithAlpha(Palette.Panel, 0.92f));
            warSkill = Text(skill.transform, "AURA THRUST", 20, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(warSkill.rectTransform, new Vector2(18, -8), new Vector2(300, 26));
            Bar(skill.transform, new Vector2(18, -40), new Vector2(294, 20), Palette.Gold, out warSkillFill);
        }

        public void SetWarHud(bool on) { Show(warGroup, on); }

        public void BindWar(Soldier p, Soldier e, string pName, string eName)
        {
            boundP = p; boundE = e;
            warPName.text = pName.ToUpper() + " GENERAL";
            warEName.text = eName.ToUpper() + " GENERAL";
            warPHp.fillAmount = (float)p.currentHealth / Mathf.Max(1, p.maxHealth);
            warEHp.fillAmount = (float)e.currentHealth / Mathf.Max(1, e.maxHealth);
            UpdateWar(60f, 0, 0, 0f);
        }

        public void UpdateWar(float timeLeft, int pTroops, int eTroops, float skill01)
        {
            warTimer.text = Mathf.CeilToInt(timeLeft).ToString();
            warTimer.color = timeLeft <= 10f ? Palette.CrimsonLight : Palette.Gold;
            if (boundP != null) warPTroops.text = "HP " + Mathf.Max(0, boundP.currentHealth) + "    TROOPS " + pTroops + "    ATK " + boundP.modifiedAttack + "  DEF " + boundP.modifiedDefense;
            if (boundE != null) warETroops.text = "TROOPS " + eTroops + "    HP " + Mathf.Max(0, boundE.currentHealth);
            warSkillFill.fillAmount = skill01;
            warSkill.text = skill01 >= 1f ? "AURA THRUST  READY" : "AURA THRUST";
            warSkill.color = skill01 >= 1f ? Palette.Gold : Palette.Parchment;
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

        public void ShowSideMenu(KeyMenu m, Func<int> marked)
        {
            sideMenu.panel.anchorMin = sideMenu.panel.anchorMax = new Vector2(0f, 0.5f);
            sideMenu.panel.pivot = new Vector2(0f, 0.5f);
            sideMenu.panel.anchoredPosition = new Vector2(40f, 0f);
            sideMenu.Show(m, marked);
        }

        public void HideSideMenu() { sideMenu.Hide(); }

        void BuildPause()
        {
            pauseGroup = Group("Pause");
            var dim = Panel(pauseGroup.transform, "Dim", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Palette.WithAlpha(Palette.Ink, 0.72f));
            dim.type = Image.Type.Simple; dim.sprite = whiteSprite;
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            var t = Text(pauseGroup.transform, "PAUSED", 84, Palette.Gold, TextAlignmentOptions.Center, titleMat);
            Place(t.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900, 110));
            t.characterSpacing = 10f;
            pauseMenu = MenuView.Create(pauseGroup.transform, "PauseMenu", 440f);
            pauseMenu.panel.anchorMin = pauseMenu.panel.anchorMax = new Vector2(0.5f, 0.64f);
            pauseMenu.panel.pivot = new Vector2(0.5f, 1f);
            Show(pauseGroup, false);
        }

        public void ShowPause(KeyMenu m)
        {
            Show(pauseGroup, true);
            pauseGroup.transform.SetAsLastSibling();
            fadeImage.transform.parent.SetAsLastSibling();
            pauseMenu.Show(m);
        }

        public void HidePause() { pauseMenu.Hide(); Show(pauseGroup, false); }

        void BuildEnd()
        {
            endGroup = Group("Ended");
            var dim = Panel(endGroup.transform, "Dim", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Palette.WithAlpha(Palette.Ink, 0.6f));
            dim.type = Image.Type.Simple; dim.sprite = whiteSprite;
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            endTitle = Text(endGroup.transform, "VICTORY", 150, Palette.Gold, TextAlignmentOptions.Center, titleMat);
            Place(endTitle.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1400, 180));
            endTitle.characterSpacing = 14f;
            endStats = Text(endGroup.transform, "", 26, Palette.Parchment, TextAlignmentOptions.Center);
            Place(endStats.rectTransform, new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(1400, 120));
            endStats.textWrappingMode = TextWrappingModes.Normal;
            endMenu = MenuView.Create(endGroup.transform, "EndMenu", 400f);
            endMenu.panel.anchorMin = endMenu.panel.anchorMax = new Vector2(0.5f, 0.45f);
            endMenu.panel.pivot = new Vector2(0.5f, 1f);
            Show(endGroup, false);
        }

        public void ShowEnded(bool victory, string stats, KeyMenu menu)
        {
            Show(endGroup, true);
            endGroup.transform.SetAsLastSibling();
            fadeImage.transform.parent.SetAsLastSibling();
            endTitle.text = victory ? "VICTORY" : "DEFEAT";
            endTitle.color = victory ? Palette.Gold : Palette.CrimsonLight;
            endStats.text = stats;
            StartCoroutine(EndMoment(victory, menu));
        }

        IEnumerator EndMoment(bool victory, KeyMenu menu)
        {
            Flash(victory ? Palette.Gold : Palette.Crimson);
            endTitle.rectTransform.localScale = Vector3.zero;
            endStats.alpha = 0f;
            yield return Ease.WaitUnscaled(0.05f);
            yield return Ease.Tween(0.35f, k => endTitle.rectTransform.localScale = Vector3.one * k, Ease.OutBack);
            yield return Ease.Tween(0.3f, k => endStats.alpha = k, Ease.OutCubic);
            endMenu.Show(menu);
        }

        // ---------------- banners, flash, fade ----------------

        void BuildBanner()
        {
            bannerGroup = Group("Banner");
            var band = Panel(bannerGroup.transform, "Band", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 170), Palette.WithAlpha(Palette.Ink, 0.55f));
            band.type = Image.Type.Simple; band.sprite = whiteSprite;
            bannerText = Text(bannerGroup.transform, "", 86, Palette.Gold, TextAlignmentOptions.Center, titleMat);
            Place(bannerText.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0, 14), new Vector2(1800, 110));
            bannerText.characterSpacing = 10f;
            bannerSub = Text(bannerGroup.transform, "", 28, Palette.Parchment, TextAlignmentOptions.Center);
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0, -56), new Vector2(1600, 40));
            bannerSub.characterSpacing = 10f;
            Show(bannerGroup, false);
        }

        public void Banner(string title, string sub, Color color, bool flash = false)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(title, sub, color, flash));
        }

        IEnumerator BannerRoutine(string title, string sub, Color color, bool flash)
        {
            Show(bannerGroup, true);
            bannerText.text = title;
            bannerText.color = color;
            bannerSub.text = sub ?? "";
            if (flash) Flash(color);
            var rt = bannerText.rectTransform;
            rt.localScale = Vector3.zero;
            bannerGroup.alpha = 1f;
            yield return Ease.WaitUnscaled(0.05f);
            yield return Ease.Tween(0.35f, k => rt.localScale = Vector3.one * k, Ease.OutBack);
            yield return Ease.WaitUnscaled(1.0f);
            yield return Ease.Tween(0.25f, k => { bannerGroup.alpha = 1f - k; rt.localScale = Vector3.one * (1f + k * 0.08f); }, Ease.OutCubic);
            Show(bannerGroup, false);
            bannerRoutine = null;
        }

        void BuildOverlays()
        {
            var fx = Stretch(root, "Overlays");
            var f = Rect(fx, "Flash", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
            flashImage = f.gameObject.AddComponent<Image>();
            flashImage.sprite = whiteSprite;
            flashImage.color = Palette.WithAlpha(Palette.Gold, 0f);
            flashImage.raycastTarget = false;
            var d = Rect(fx, "Fade", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            d.anchorMin = Vector2.zero; d.anchorMax = Vector2.one; d.offsetMin = d.offsetMax = Vector2.zero;
            fadeImage = d.gameObject.AddComponent<Image>();
            fadeImage.sprite = whiteSprite;
            fadeImage.color = Palette.WithAlpha(Palette.Ink, 1f);
            fadeImage.raycastTarget = false;
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

        public void FloatText(Vector3 world, string text, Color color, float scale = 1f)
        {
            Floater f = null;
            foreach (var x in floaters) if (!x.active) { f = x; break; }
            if (f == null)
            {
                if (floaters.Count >= 40) f = floaters[0];
                else
                {
                    var t = Text(floatLayer, "", 30, Color.white, TextAlignmentOptions.Center);
                    t.rectTransform.sizeDelta = new Vector2(600, 50);
                    f = new Floater { rt = t.rectTransform, text = t };
                    floaters.Add(f);
                }
            }
            f.world = world;
            f.text.text = text;
            f.text.color = color;
            f.text.fontSize = 30f * scale;
            f.t = 0f;
            f.life = 1.1f;
            f.active = true;
            f.rt.gameObject.SetActive(true);
        }

        // ---------------- per-frame ----------------

        void Update()
        {
            if (titleGroup.gameObject.activeSelf)
                titlePrompt.alpha = Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.6f));

            if (contextMenu.gameObject.activeSelf) PositionContext();

            // card bars tween instead of snapping
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
            cardHpFill.fillAmount = Mathf.Lerp(cardHpFill.fillAmount, cardHpTarget, k);
            cardTroopFill.fillAmount = Mathf.Lerp(cardTroopFill.fillAmount, cardTroopTarget, k);
            if (boundP != null) warPHp.fillAmount = Mathf.Lerp(warPHp.fillAmount, Mathf.Max(0, boundP.currentHealth) / (float)Mathf.Max(1, boundP.maxHealth), k);
            if (boundE != null) warEHp.fillAmount = Mathf.Lerp(warEHp.fillAmount, Mathf.Max(0, boundE.currentHealth) / (float)Mathf.Max(1, boundE.maxHealth), k);

            UpdateTags(k);
            UpdateFloaters();

            var tMan = TurnManager.instance;
            if (tMan != null && tacticGroup.gameObject.activeSelf)
                armyText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.AzureLight) + ">YOUR ARMY " + tMan.players.Count + "</color>     <color=#" + ColorUtility.ToHtmlStringRGB(Palette.CrimsonLight) + ">ENEMY " + tMan.enemies.Count + "</color>";
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
                var pos = WorldToCanvas(p.transform.position + Vector3.up * 2.55f, out bool vis);
                tag.rt.gameObject.SetActive(vis && p.gameObject.activeInHierarchy);
                tag.rt.anchoredPosition = pos;
                tag.label.text = p.playerClass.ClassName.ToUpper() + "  x" + p.currentUnits;
                tag.shown = Mathf.Lerp(tag.shown, p.maxHealth > 0 ? (float)p.currentHealth / p.maxHealth : 0f, k);
                tag.fill.fillAmount = tag.shown;
                tag.label.alpha = p.isTurnOver ? 0.55f : 1f;
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
                var pos = WorldToCanvas(f.world + Vector3.up * (f.t * 1.2f), out bool vis);
                f.rt.anchoredPosition = pos;
                f.text.alpha = f.t < 0.7f ? 1f : 1f - (f.t - 0.7f) / 0.3f;
                float pop = f.t < 0.15f ? Mathf.LerpUnclamped(0.5f, 1f, Ease.OutBack(f.t / 0.15f)) : 1f;
                f.rt.localScale = Vector3.one * pop;
                f.rt.gameObject.SetActive(vis);
            }
        }
    }
}
