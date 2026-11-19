using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LLTacticalWar
{
    // The combat forecast panel (map, Target state).
    public partial class GameUI
    {
        class ForecastColumn { public TextMeshProUGUI name, weapon, hp, dmg, hit, crit; public Image brk; }

        RectTransform forecastPanel;
        ForecastColumn fcLeft, fcRight;

        // What the forecast panel shows right now, read by the verify probe.
        public string ForecastText { get; private set; } = "";

        static readonly string[] ForecastLabels = { "HP", "DMG", "HIT", "CRIT" };
        const float FcWidth = 500f, FcCol = 200f, FcPad = 16f;

        void BuildForecast()
        {
            var panel = Panel(tacticGroup.transform, "Forecast", Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(FcWidth, 308), Palette.WithAlpha(Palette.Panel, 0.95f));
            forecastPanel = panel.rectTransform;
            var title = Text(panel.transform, "FORECAST", 15, Palette.Gold, TextAlignmentOptions.Center);
            TopLeft(title.rectTransform, new Vector2(0, -10), new Vector2(FcWidth, 20));
            for (int i = 0; i < ForecastLabels.Length; i++)
            {
                var l = Text(panel.transform, ForecastLabels[i], 17, Palette.Muted, TextAlignmentOptions.Center);
                TopLeft(l.rectTransform, new Vector2(FcPad + FcCol, -126 - i * 36), new Vector2(FcWidth - 2 * (FcPad + FcCol), 30));
            }
            fcLeft = Column(panel.transform, FcPad, TextAlignmentOptions.Right, Palette.Azure);
            fcRight = Column(panel.transform, FcWidth - FcPad - FcCol, TextAlignmentOptions.Left, Palette.Crimson);
            forecastPanel.gameObject.SetActive(false);
        }

        ForecastColumn Column(Transform parent, float x, TextAlignmentOptions align, Color team)
        {
            var c = new ForecastColumn();
            Panel(parent, "Strip", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -36), new Vector2(FcCol, 6), team);
            c.name = Text(parent, "", 24, Palette.Parchment, align);
            TopLeft(c.name.rectTransform, new Vector2(x, -48), new Vector2(FcCol, 30));
            c.name.enableAutoSizing = true;
            c.name.fontSizeMin = 16f;
            c.name.fontSizeMax = 24f;
            c.weapon = Text(parent, "", 16, Palette.Parchment, align);
            TopLeft(c.weapon.rectTransform, new Vector2(x, -84), new Vector2(FcCol, 24));
            c.weapon.richText = true;
            var rows = new TextMeshProUGUI[4];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Text(parent, "", 26, Palette.Parchment, align);
                TopLeft(rows[i].rectTransform, new Vector2(x, -122 - i * 36), new Vector2(FcCol, 34));
                rows[i].richText = true;
            }
            c.hp = rows[0]; c.dmg = rows[1]; c.hit = rows[2]; c.crit = rows[3];
            bool left = align == TextAlignmentOptions.Right;
            c.brk = Panel(parent, "Break", new Vector2(0, 1), new Vector2(left ? 1 : 0, 1), new Vector2(left ? x + FcCol : x, -270), new Vector2(104, 26), Palette.Gold);
            var t = Text(c.brk.transform, "BREAK", 15, Palette.Ink, TextAlignmentOptions.Center, chipMat);
            Fill(t.rectTransform);
            return c;
        }

        // Player on the left, enemy on the right, whoever attacks.
        public void ShowForecast(Forecast f)
        {
            bool playerAttacks = f.attacker.unit.team == Team.Player;
            var hp = Combat.HpIfAllHit(f);
            var left = playerAttacks ? f.attacker : f.defender;
            var right = playerAttacks ? f.defender : f.attacker;
            FillColumn(fcLeft, left, playerAttacks ? hp.attacker : hp.defender, right);
            FillColumn(fcRight, right, playerAttacks ? hp.defender : hp.attacker, left);
            forecastPanel.gameObject.SetActive(true);
            ForecastText = ColumnText(fcLeft) + "  ||  " + ColumnText(fcRight);
        }

        public void HideForecast() { forecastPanel.gameObject.SetActive(false); ForecastText = ""; }

        void FillColumn(ForecastColumn c, SideForecast s, int hpAfter, SideForecast foe)
        {
            c.name.text = s.unit.DisplayName.ToUpper();
            c.name.color = Palette.TeamLight(s.unit.team);
            int tri = Weapons.Triangle(s.weapon.Kind, foe.weapon.Kind);
            string mark = tri > 0 ? "<color=#" + Hex(Palette.Gold) + ">▲</color>" : tri < 0 ? "<color=#" + Hex(Palette.CrimsonLight) + ">▼</color>" : "";
            bool left = c == fcLeft;
            c.weapon.text = mark.Length == 0 ? s.weapon.Name.ToUpper() : left ? s.weapon.Name.ToUpper() + "  " + mark : mark + "  " + s.weapon.Name.ToUpper();
            string after = hpAfter < s.unit.currentHealth ? "<color=#" + Hex(Palette.CrimsonLight) + ">" + hpAfter + "</color>" : hpAfter.ToString();
            c.hp.text = s.unit.currentHealth + " → " + after;
            bool strikes = s.canCounter;
            c.dmg.text = strikes ? s.damage + (s.strikes > 1 ? " <color=#" + Hex(Palette.Gold) + ">x2</color>" : "") : "--";
            c.hit.text = strikes ? s.hit.ToString() : "--";
            c.crit.text = strikes ? s.crit.ToString() : "--";
            c.brk.gameObject.SetActive(strikes && s.triangleAdvantage && !foe.unit.broken);
        }

        static string ColumnText(ForecastColumn c) =>
            c.name.text + " | " + c.weapon.text + " | HP " + c.hp.text + " | DMG " + c.dmg.text + " | HIT " + c.hit.text + " | CRIT " + c.crit.text + (c.brk.gameObject.activeSelf ? " | BREAK" : "");

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ---------------- battle HUD ----------------

        // One side's panel. The bar drains fast; a pale ghost segment holds, then catches up.
        class BattleSide
        {
            public TextMeshProUGUI name, weapon, hp, stats;
            public Image fill, ghost, broken;
            public float target, shown, ghostShown, ghostHold;
            public int max = 1;
        }

        CanvasGroup battleGroup;
        BattleSide bsLeft, bsRight;
        TextMeshProUGUI battleHint;

        void BuildBattleHud()
        {
            battleGroup = Group("BattleHUD");
            bsLeft = BattleSidePanel(true);
            bsRight = BattleSidePanel(false);
            battleHint = Text(battleGroup.transform, "", 18, Palette.WithAlpha(Palette.Parchment, 0.8f), TextAlignmentOptions.Center);
            Place(battleHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(520, 30));
            battleHint.richText = true;
            Show(battleGroup, false);
        }

        BattleSide BattleSidePanel(bool left)
        {
            var b = new BattleSide();
            var anchor = new Vector2(left ? 0f : 1f, 0f);
            var p = Panel(battleGroup.transform, left ? "BattleLeft" : "BattleRight", anchor, anchor, new Vector2(left ? 24 : -24, 24), new Vector2(620, 172), Palette.WithAlpha(Palette.Panel, 0.94f));
            Panel(p.transform, "Stripe", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(620, 8), left ? Palette.Azure : Palette.Crimson);
            b.name = Text(p.transform, "", 30, left ? Palette.AzureLight : Palette.CrimsonLight, TextAlignmentOptions.Left);
            TopLeft(b.name.rectTransform, new Vector2(24, -18), new Vector2(400, 36));
            b.weapon = Text(p.transform, "", 17, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(b.weapon.rectTransform, new Vector2(26, -56), new Vector2(400, 24));
            b.hp = Text(p.transform, "", 56, Palette.Parchment, TextAlignmentOptions.Right, titleMat);
            TopLeft(b.hp.rectTransform, new Vector2(420, -10), new Vector2(176, 66));

            var bg = Panel(p.transform, "HpBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -88), new Vector2(572, 26), Palette.WithAlpha(Palette.Ink, 0.95f));
            b.ghost = FillImage(bg.transform, Palette.Parchment);
            b.fill = FillImage(bg.transform, left ? Palette.Azure : Palette.Crimson);

            b.stats = Text(p.transform, "", 20, Palette.Parchment, TextAlignmentOptions.Left);
            TopLeft(b.stats.rectTransform, new Vector2(26, -128), new Vector2(560, 28));
            b.stats.richText = true;

            b.broken = Panel(p.transform, "Broken", new Vector2(0, 1), new Vector2(0, 1), new Vector2(250, -54), new Vector2(110, 26), Palette.Gold);
            var bt = Text(b.broken.transform, "BROKEN", 15, Palette.Ink, TextAlignmentOptions.Center, chipMat);
            Fill(bt.rectTransform);
            return b;
        }

        Image FillImage(Transform parent, Color c)
        {
            var rt = Rect(parent, "Fill", Vector2.zero, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = whiteSprite;          // Filled needs a sprite, or it silently does nothing
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // Player on the left, enemy on the right.
        public void ShowBattleHud(Forecast f)
        {
            bool playerAttacks = f.attacker.unit.team == Team.Player;
            SetSide(bsLeft, playerAttacks ? f.attacker : f.defender);
            SetSide(bsRight, playerAttacks ? f.defender : f.attacker);
            SetBattleHint(false);
            Show(battleGroup, true);
        }

        public void HideBattleHud() { Show(battleGroup, false); }

        void SetSide(BattleSide b, SideForecast s)
        {
            var u = s.unit;
            b.name.text = u.DisplayName.ToUpper();
            b.weapon.text = s.weapon.Name.ToUpper();
            b.max = Mathf.Max(1, u.MaxHP);
            b.target = b.shown = b.ghostShown = u.currentHealth;
            b.ghostHold = 0f;
            string muted = "<color=#" + Hex(Palette.Muted) + ">";
            b.stats.text = s.canCounter
                ? muted + "HIT</color> " + s.hit + "     " + muted + "DMG</color> " + s.damage + (s.strikes > 1 ? " x2" : "") + "     " + muted + "CRIT</color> " + s.crit
                : muted + "HIT</color> --     " + muted + "DMG</color> --     " + muted + "CRIT</color> --";
            b.broken.gameObject.SetActive(u.broken);
            ApplySide(b);
        }

        public void SetBattleHp(bool left, int hp)
        {
            var b = left ? bsLeft : bsRight;
            if (hp < b.target) b.ghostHold = 0.35f;
            b.target = hp;
        }

        public void SetBattleBroken(bool left) { (left ? bsLeft : bsRight).broken.gameObject.SetActive(true); }

        public void SetBattleHint(bool fast)
        {
            string gold = "<color=#" + Hex(Palette.Gold) + ">";
            battleHint.text = fast ? gold + "x2</color>     X  skip" : "HOLD Z  x2     X  skip";
        }

        // Shown HP on each panel, for the verify probe.
        public string BattleHudText => bsLeft.name.text + " " + bsLeft.hp.text + "  ||  " + bsRight.name.text + " " + bsRight.hp.text;

        void UpdateBattleHud(float dt)
        {
            if (!battleGroup.gameObject.activeSelf) return;
            UpdateSide(bsLeft, dt);
            UpdateSide(bsRight, dt);
        }

        static void UpdateSide(BattleSide b, float dt)
        {
            b.shown = Mathf.MoveTowards(b.shown, b.target, dt * Mathf.Max(30f, b.max * 3f));
            if (b.ghostHold > 0f) b.ghostHold -= dt;
            else b.ghostShown = Mathf.MoveTowards(b.ghostShown, b.shown, dt * b.max * 1.4f);
            ApplySide(b);
        }

        static void ApplySide(BattleSide b)
        {
            b.fill.fillAmount = b.shown / b.max;
            b.ghost.fillAmount = Mathf.Max(b.ghostShown, b.shown) / b.max;
            b.hp.text = Mathf.CeilToInt(b.shown).ToString();
            b.hp.color = b.shown <= b.max * 0.25f ? Palette.CrimsonLight : Palette.Parchment;
        }
    }
}
