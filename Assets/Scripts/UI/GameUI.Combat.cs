using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LLTacticalWar
{
    // The combat forecast (map, Target state) and the battle HUD: parchment cards with each side's
    // wax seal, names in the display face, the numbers that decide the exchange large and tabular.
    public partial class GameUI
    {
        class ForecastColumn { public TextMeshProUGUI name, weapon, hp, dmg, hit, crit; public StampMark brk; public Image seal; public string probe; }

        Card forecastCard;
        ForecastColumn fcLeft, fcRight;

        // What the forecast panel shows right now, read by the verify probe.
        public string ForecastText { get; private set; } = "";

        static readonly string[] ForecastLabels = { "HP", "Dmg", "Hit", "Crit" };
        const float FcWidth = 580f, FcCol = 196f, FcPad = 30f, FcRow = 44f;

        void BuildForecast()
        {
            forecastCard = MakeCard(tacticGroup.transform, "Forecast", Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(FcWidth, 352f));
            var c = forecastCard.rt;
            var title = Text(c, "Forecast", 22, Palette.InkFaded, TextAlignmentOptions.Center, Face.Display);
            title.characterSpacing = 10f;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 30f));
            Rule(c, new Vector2(0.5f, 1f), new Vector2(-150f, -32f), 110f, 0.4f);
            Rule(c, new Vector2(0.5f, 1f), new Vector2(150f, -32f), 110f, 0.4f);
            float mid = FcPad + FcCol, midW = FcWidth - 2f * (FcPad + FcCol);
            for (int i = 0; i < ForecastLabels.Length; i++)
            {
                var l = Text(c, ForecastLabels[i], 20, Palette.InkFaded, TextAlignmentOptions.Center, Face.Display);
                l.characterSpacing = 6f;
                TopLeft(l.rectTransform, new Vector2(mid, -142f - i * FcRow), new Vector2(midW, 30f));
                if (i > 0) Rule(c, new Vector2(0.5f, 1f), new Vector2(0f, -134f - i * FcRow), FcWidth - 70f, 0.16f);
            }
            fcLeft = Column(c, FcPad, true);
            fcRight = Column(c, FcWidth - FcPad - FcCol, false);
            forecastCard.rt.gameObject.SetActive(false);
        }

        ForecastColumn Column(RectTransform parent, float x, bool left)
        {
            var col = new ForecastColumn();
            var align = left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            col.seal = SealAt(parent, left ? Team.Player : Team.Enemy, new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 20f : -20f, -18f), 84f, left ? -8f : 8f);
            col.name = Text(parent, "", 26, Palette.AzureDark, align, Face.Display);
            TopLeft(col.name.rectTransform, new Vector2(x, -60f), new Vector2(FcCol, 32f));
            col.name.enableAutoSizing = true;
            col.name.fontSizeMin = 17f;
            col.name.fontSizeMax = 26f;
            col.weapon = Text(parent, "", 17, Palette.InkFaded, align);
            TopLeft(col.weapon.rectTransform, new Vector2(x, -94f), new Vector2(FcCol, 24f));
            var rows = new TextMeshProUGUI[4];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Text(parent, "", 34, Palette.Ink, align);
                TopLeft(rows[i].rectTransform, new Vector2(x, -134f - i * FcRow), new Vector2(FcCol, 42f));
            }
            col.hp = rows[0]; col.dmg = rows[1]; col.hit = rows[2]; col.crit = rows[3];
            col.brk = MakeStamp(parent, new Vector2(0f, 1f), new Vector2(x + FcCol * 0.5f, -322f), new Vector2(128f, 40f), left ? -5f : 5f);
            SetStamp(col.brk, "Break", Palette.CrimsonDark);
            return col;
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
            if (!forecastCard.rt.gameObject.activeSelf) SlideIn(forecastCard, -70f, 0.18f);
            ForecastText = fcLeft.probe + "  ||  " + fcRight.probe;
        }

        public void HideForecast() { Hide(forecastCard); ForecastText = ""; }

        void FillColumn(ForecastColumn c, SideForecast s, int hpAfter, SideForecast foe)
        {
            c.name.text = s.unit.DisplayName;
            c.name.color = Palette.TeamDark(s.unit.team);
            c.seal.sprite = UiKit.Seal(s.unit.team);
            int tri = Weapons.Triangle(s.weapon.Kind, foe.weapon.Kind);
            string mark = tri > 0 ? Tint("▲", Palette.Brass) : tri < 0 ? Tint("▼", Palette.Crimson) : "";
            bool left = c == fcLeft;
            string weapon = s.weapon.Name.ToUpper();
            c.weapon.text = mark.Length == 0 ? weapon : left ? weapon + "  " + mark : mark + "  " + weapon;
            string after = hpAfter < s.unit.currentHealth ? Tint(hpAfter.ToString(), Palette.Crimson) : hpAfter.ToString();
            c.hp.text = s.unit.currentHealth + " → " + after;
            bool strikes = s.canCounter;
            c.dmg.text = strikes ? s.damage + (s.strikes > 1 ? " " + Tint("x2", Palette.Brass) : "") : "--";
            c.hit.text = strikes ? s.hit.ToString() : "--";
            c.crit.text = strikes ? s.crit.ToString() : "--";
            bool brk = strikes && s.triangleAdvantage && !foe.unit.broken;
            c.brk.rt.gameObject.SetActive(brk);
            c.probe = s.unit.DisplayName.ToUpper() + " | " + c.weapon.text + " | HP " + c.hp.text + " | DMG " + c.dmg.text + " | HIT " + c.hit.text + " | CRIT " + c.crit.text + (brk ? " | BREAK" : "");
        }

        // ---------------- battle HUD ----------------

        // One side's card. The bar drains fast; a pale ghost segment holds, then catches up.
        class BattleSide
        {
            public Card card;
            public TextMeshProUGUI name, weapon, hp, stats;
            public Bar bar;
            public StampMark broken;
            public Image seal;
            public string probeName = "";
            public float target, shown, ghostShown, ghostHold;
            public int max = 1;
        }

        CanvasGroup battleGroup;
        BattleSide bsLeft, bsRight;
        Card battleHintSlip;
        TextMeshProUGUI battleHint;
        const float BsWidth = 640f, BsHeight = 182f;

        void BuildBattleHud()
        {
            battleGroup = Group("BattleHUD");
            bsLeft = BattleSidePanel(true);
            bsRight = BattleSidePanel(false);
            battleHintSlip = MakeCard(battleGroup.transform, "BattleHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(400f, 42f), false, 1.4f);
            battleHint = Text(battleHintSlip.rt, "", 18, Palette.Ink, TextAlignmentOptions.Center);
            Fill(battleHint.rectTransform, 10f, 10f, 0f, 0f);
            Show(battleGroup, false);
        }

        BattleSide BattleSidePanel(bool left)
        {
            var b = new BattleSide();
            var anchor = new Vector2(left ? 0f : 1f, 0f);
            b.card = MakeCard(battleGroup.transform, left ? "BattleLeft" : "BattleRight", anchor, anchor, new Vector2(left ? 24f : -24f, 24f), new Vector2(BsWidth, BsHeight));
            var p = b.card.rt;
            // positions are written for the left card and mirrored for the right
            Vector2 At(float x, float y, float w) => new Vector2(left ? x : BsWidth - x - w, y);
            var near = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            var far = left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            var team = left ? Team.Player : Team.Enemy;
            b.seal = SealAt(p, team, new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 18f : -18f, -14f), 92f, left ? -8f : 8f);
            b.name = Text(p, "", 32, Palette.TeamDark(team), near, Face.Display);
            b.name.characterSpacing = 5f;
            TopLeft(b.name.rectTransform, At(78f, -16f, 340f), new Vector2(340f, 40f));
            b.weapon = Text(p, "", 17, Palette.InkFaded, near);
            TopLeft(b.weapon.rectTransform, At(80f, -58f, 300f), new Vector2(300f, 24f));
            b.hp = Text(p, "", 68, Palette.Ink, far);
            TopLeft(b.hp.rectTransform, At(440f, -6f, 170f), new Vector2(170f, 76f));
            b.bar = MakeBar(p, At(30f, -98f, 580f), new Vector2(580f, 26f), Palette.Team(team), true);
            if (!left)
            {
                // the enemy's bar drains toward its own seal
                b.bar.fill.fillOrigin = 1;
                b.bar.ghost.fillOrigin = 1;
            }
            b.stats = Text(p, "", 21, Palette.Ink, near);
            TopLeft(b.stats.rectTransform, At(32f, -138f, 580f), new Vector2(580f, 30f));
            // stamped in the card's lower far corner, clear of the name, the number and the stat line
            b.broken = MakeStamp(p, new Vector2(0f, 1f), new Vector2(left ? 536f : BsWidth - 536f, -150f), new Vector2(124f, 40f), left ? -6f : 6f);
            SetStamp(b.broken, "Broken", Palette.CrimsonDark);
            return b;
        }

        // Player on the left, enemy on the right.
        public void ShowBattleHud(Forecast f)
        {
            bool playerAttacks = f.attacker.unit.team == Team.Player;
            SetSide(bsLeft, playerAttacks ? f.attacker : f.defender);
            SetSide(bsRight, playerAttacks ? f.defender : f.attacker);
            SetBattleHint(false);
            Show(battleGroup, true);
            SlideIn(bsLeft.card, -90f, 0.22f);
            SlideIn(bsRight.card, 90f, 0.22f);
        }

        public void HideBattleHud() { Show(battleGroup, false); }

        void SetSide(BattleSide b, SideForecast s)
        {
            var u = s.unit;
            b.probeName = u.DisplayName.ToUpper();
            b.name.text = u.DisplayName;
            b.weapon.text = s.weapon.Name.ToUpper();
            b.max = Mathf.Max(1, u.MaxHP);
            b.target = b.shown = b.ghostShown = u.currentHealth;
            b.ghostHold = 0f;
            string L(string label) => Tint(label, Palette.InkFaded);
            b.stats.text = s.canCounter
                ? L("HIT") + " " + s.hit + "      " + L("DMG") + " " + s.damage + (s.strikes > 1 ? " " + Tint("x2", Palette.Brass) : "") + "      " + L("CRIT") + " " + s.crit
                : L("HIT") + " --      " + L("DMG") + " --      " + L("CRIT") + " --";
            b.broken.rt.gameObject.SetActive(u.broken);
            ApplySide(b);
        }

        public void SetBattleHp(bool left, int hp)
        {
            var b = left ? bsLeft : bsRight;
            if (hp < b.target) b.ghostHold = 0.35f;
            b.target = hp;
        }

        public void SetBattleBroken(bool left)
        {
            var b = left ? bsLeft : bsRight;
            if (!b.broken.rt.gameObject.activeSelf) Slam(b.broken.rt);
        }

        public void SetBattleHint(bool fast) => battleHint.text = HintMarkup(fast ? "Z held  x2     X  skip" : "HOLD Z  x2     X  skip");

        // Shown HP on each panel, for the verify probe.
        public string BattleHudText => bsLeft.probeName + " " + bsLeft.hp.text + "  ||  " + bsRight.probeName + " " + bsRight.hp.text;

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
            b.bar.fill.fillAmount = b.shown / b.max;
            b.bar.ghost.fillAmount = Mathf.Max(b.ghostShown, b.shown) / b.max;
            b.hp.text = Mathf.CeilToInt(b.shown).ToString();
            b.hp.color = b.shown <= b.max * 0.25f ? Palette.Crimson : Palette.Ink;
        }
    }
}
