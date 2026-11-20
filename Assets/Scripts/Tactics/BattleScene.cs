using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace LLTacticalWar
{
    // The 1v1 battle scene. It plays the strike list Combat.Resolve already decided, on a stage
    // far from the board: the attack clip, timed so its blow (or release) lands with the lunge
    // or the shot, then hit-stop, shake, the hit, dodge or death clip, numbers, the result.
    // It decides nothing, so skipping it (X) leaves the outcome exactly the same. Holding Z
    // plays it at double speed. Every time-scale change in a battle goes through ApplyTime.
    public class BattleScene : MonoBehaviour
    {
        public static BattleScene instance;

        public enum Phase { Idle, Intro, Strikes, Result, Outro }
        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public int StrikeIndex { get; private set; } = -1;
        public int StrikeCount { get; private set; }
        public bool Fast { get; private set; }
        public bool Skipped { get; private set; }

        class Actor
        {
            public Player unit;
            public UnitModel model;
            public Transform t;
            public Vector3 home;
            public Quaternion baseRot;
            public bool left, down;
            public int hp;
            public Vector3 Forward => left ? Vector3.right : Vector3.left;
            public float Reach => unit.playerClass.Mounted ? 0.975f : 0.525f;   // body ahead of its root: a horse's head reaches past its rider
            public Vector3 Chest => t.position + Vector3.up * (0.9f + model.Lift);
            public Vector3 Head => t.position + Vector3.up * (2.0f + model.Lift);
        }

        static readonly Vector3 CamHome = new Vector3(0f, 2.5f, -9f);
        static readonly Vector3 CamLook = new Vector3(0f, 0.8f, 0f);
        const float CamFov = 33f;

        Transform root, arrow, bolt;
        Action<Team> showKeep;
        CinemachineCamera cam;
        Actor left, right;
        float slowMo = 1f, hitStop = 1f, trauma;
        Vector3 camPos = CamHome, camLook = CamLook;
        float camFov = CamFov;

        void Awake() { instance = this; }

        public void Build(Transform battleRoot)
        {
            root = battleRoot;
            showKeep = Scenery.BuildStage(root);
            cam = new GameObject("CM_Battle").AddComponent<CinemachineCamera>();
            cam.transform.SetParent(root, false);
            cam.Lens.FarClipPlane = 400f;
            cam.Priority = 0;
            arrow = BuildArrow();
            bolt = BuildBolt();
            PoseCamera(0f);
        }

        Transform BuildArrow()
        {
            var a = Prim.Pivot("Arrow", root, Vector3.zero);
            Prim.Part("Shaft", PrimitiveType.Cylinder, a, Vector3.zero, new Vector3(0.035f, 0.42f, 0.035f), Palette.Wood, new Vector3(90f, 0, 0), false);
            Prim.Part("Tip", PrimitiveType.Cube, a, new Vector3(0, 0, 0.45f), new Vector3(0.07f, 0.07f, 0.14f), Palette.Steel, new Vector3(0, 0, 45f), false);
            Prim.Part("FletchA", PrimitiveType.Cube, a, new Vector3(0, 0, -0.36f), new Vector3(0.012f, 0.11f, 0.16f), Palette.Parchment, default, false);
            Prim.Part("FletchB", PrimitiveType.Cube, a, new Vector3(0, 0, -0.36f), new Vector3(0.11f, 0.012f, 0.16f), Palette.Parchment, default, false);
            a.gameObject.SetActive(false);
            return a;
        }

        Transform BuildBolt()
        {
            var b = Prim.Pivot("FireBolt", root, Vector3.zero);
            Prim.GlowPart("Core", PrimitiveType.Sphere, b, Vector3.zero, Vector3.one * 0.3f, Palette.Gold);
            Prim.GlowPart("Tail", PrimitiveType.Sphere, b, new Vector3(0, 0, -0.22f), Vector3.one * 0.18f, Palette.CrimsonLight);
            b.gameObject.SetActive(false);
            return b;
        }

        // ---------------- time and camera ----------------

        void Update()
        {
            if (CurrentPhase == Phase.Idle || !GameManager.IsPlaying) return;
            if (!Skipped && (CurrentPhase == Phase.Intro || CurrentPhase == Phase.Strikes) && GameInput.Cancel)
            {
                Skipped = true;
                AudioBank.I.Play(Sfx.UiCancel, 0.6f);
            }
            bool fast = GameInput.ConfirmHeld;
            if (fast != Fast) { Fast = fast; GameUI.I.SetBattleHint(fast); }
            ApplyTime();
        }

        void ApplyTime() =>
            GameManager.SetTimeScale(Skipped || CurrentPhase == Phase.Idle ? 1f : (Fast ? 2f : 1f) * slowMo * hitStop);

        void LateUpdate()
        {
            if (CurrentPhase == Phase.Idle) return;
            trauma = Mathf.MoveTowards(trauma, 0f, Time.unscaledDeltaTime * 2.4f);
            PoseCamera(trauma * trauma);
        }

        // Base pose (tweened for the crit push-in) plus a noise shake scaled by trauma squared.
        void PoseCamera(float shake)
        {
            float n = Time.unscaledTime * 30f;
            var rot = Quaternion.LookRotation(camLook - camPos);
            var offset = new Vector3(Mathf.PerlinNoise(n, 0.1f) - 0.5f, Mathf.PerlinNoise(0.4f, n) - 0.5f, 0f) * (0.7f * shake);
            cam.transform.localPosition = camPos + rot * offset;
            cam.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(n, n * 0.5f) - 0.5f) * 6f * shake);
            cam.Lens.FieldOfView = camFov;
        }

        void Shake(float amount) => trauma = Mathf.Min(1f, trauma + amount);

        IEnumerator CameraTo(Vector3 pos, Vector3 look, float fov, float seconds)
        {
            Vector3 p0 = camPos, l0 = camLook;
            float f0 = camFov;
            yield return Ease.Tween(seconds, k => { camPos = Vector3.Lerp(p0, pos, k); camLook = Vector3.Lerp(l0, look, k); camFov = Mathf.Lerp(f0, fov, k); }, Ease.OutCubic);
        }

        // Skip-aware waits on scaled time: once X is pressed they all finish at once.
        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !Skipped; t += Time.deltaTime) yield return null;
        }

        IEnumerator Tween(float seconds, Action<float> apply, Func<float, float> ease)
        {
            for (float t = 0f; t < 1f && !Skipped; t += Time.deltaTime / seconds)
            {
                apply(ease(t));
                yield return null;
            }
            apply(1f);
        }

        IEnumerator HitStop(float seconds)
        {
            hitStop = 0.04f;
            ApplyTime();
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !Skipped) yield return null;
            hitStop = 1f;
            ApplyTime();
        }

        // ---------------- the battle ----------------

        public IEnumerator Play(Forecast f, List<Strike> strikes)
        {
            GameManager.I.InBattle = true;
            Skipped = Fast = false;
            slowMo = hitStop = 1f;
            trauma = 0f;
            StrikeIndex = -1;
            StrikeCount = strikes.Count;
            CurrentPhase = Phase.Intro;
            yield return GameUI.I.Fade(1f, 0.22f);

            bool playerAttacks = f.attacker.unit.team == Team.Player;
            float half = f.attacker.unit.Tile.Distance(f.defender.unit.Tile) > 1 ? 2.4f : 1.45f;
            left = Spawn(playerAttacks ? f.attacker.unit : f.defender.unit, -half, true);
            right = Spawn(playerAttacks ? f.defender.unit : f.attacker.unit, half, false);
            var att = playerAttacks ? left : right;
            var def = playerAttacks ? right : left;

            camPos = CamHome; camLook = CamLook; camFov = CamFov;
            PoseCamera(0f);
            showKeep(f.defender.unit.team);
            cam.Priority = 100;
            GameManager.I.ShowBattle(true);
            GameUI.I.SetTacticHud(false);
            GameUI.I.ShowBattleHud(f);
            AudioBank.I.PlayWarMusic();
            yield return GameUI.I.Fade(0f, 0.22f);
            yield return Wait(0.3f);

            CurrentPhase = Phase.Strikes;
            for (int i = 0; i < strikes.Count && !Skipped; i++)
            {
                StrikeIndex = i;
                var s = strikes[i];
                yield return PlayStrike(s.byAttacker ? att : def, s.byAttacker ? def : att, s, s.byAttacker ? f.attacker : f.defender);
                yield return Wait(0.2f);
            }

            CurrentPhase = Phase.Result;
            StopAllCoroutines();   // reactions and camera moves still settling
            slowMo = hitStop = 1f;
            camPos = CamHome; camLook = CamLook; camFov = CamFov;
            arrow.gameObject.SetActive(false);
            bolt.gameObject.SetActive(false);
            Settle(att, def, strikes);
            ApplyTime();
            bool kill = att.down || def.down;
            yield return Ease.WaitUnscaled(kill ? 1.1f : 0.6f);

            CurrentPhase = Phase.Outro;
            yield return GameUI.I.Fade(1f, 0.22f);
            Destroy(left.t.gameObject);
            Destroy(right.t.gameObject);
            left = right = null;
            cam.Priority = 0;
            GameManager.I.ShowBattle(false);
            GameUI.I.HideBattleHud();
            GameUI.I.SetTacticHud(true);
            AudioBank.I.PlayTacticMusic();
            CurrentPhase = Phase.Idle;
            StrikeIndex = -1;
            GameManager.SetTimeScale(1f);
            yield return GameUI.I.Fade(0f, 0.22f);
            GameManager.I.InBattle = false;
        }

        Actor Spawn(Player p, float x, bool isLeft)
        {
            var t = new GameObject(isLeft ? "Actor_L" : "Actor_R").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(x, 0f, 0f);
            // three-quarter view: each turns a little toward the camera; the right one is
            // mirrored so its weapon hand faces the camera too
            t.localRotation = Quaternion.LookRotation(new Vector3(isLeft ? 1f : -1f, 0f, -0.38f));
            t.localScale = new Vector3(isLeft ? 1f : -1f, 1f, 1f);
            var m = UnitFactory.Create(p.playerClass, p.team, t);
            m.footDust = true;
            m.maxSpeed = 7f;
            return new Actor { unit = p, model = m, t = t, home = t.localPosition, baseRot = t.localRotation, left = isLeft, hp = p.currentHealth };
        }

        // The final pose whatever was skipped: HP bars, BROKEN chips, the fallen down, the rest home.
        void Settle(Actor att, Actor def, List<Strike> strikes)
        {
            int hpA = att.hp, hpD = def.hp;
            bool brokeA = false, brokeD = false;
            foreach (var s in strikes)
            {
                if (s.byAttacker) { hpD = s.targetHpAfter; brokeD |= s.breaks; }
                else { hpA = s.targetHpAfter; brokeA |= s.breaks; }
            }
            SettleActor(att, hpA, brokeA);
            SettleActor(def, hpD, brokeD);
            if (hpA > 0 && hpD <= 0) att.model.Cheer();
            if (hpD > 0 && hpA <= 0) def.model.Cheer();
        }

        void SettleActor(Actor a, int hp, bool broke)
        {
            GameUI.I.SetBattleHp(a.left, hp);
            if (broke) GameUI.I.SetBattleBroken(a.left);
            if (hp > 0) { a.t.localPosition = a.home; a.t.localRotation = a.baseRot; }
            else Fall(a);
        }

        IEnumerator PlayStrike(Actor by, Actor target, Strike s, SideForecast side)
        {
            bool crit = s.result == StrikeResult.Crit;
            if (crit) CritCue(by, target);
            if (side.weapon.Kind == WeaponKind.Bow) yield return Shoot(by, target, s);
            else if (side.weapon.Magic) yield return Cast(by, target, s);
            else yield return Melee(by, target, s);
            if (crit) StartCoroutine(CameraTo(CamHome, CamLook, CamFov, 0.4f));
        }

        // A crit telegraphs: the striker glints, time slows, the camera pushes in on the target.
        void CritCue(Actor by, Actor target)
        {
            slowMo = 0.35f;
            ApplyTime();
            by.model.Punch();
            AudioBank.I.Play(Sfx.Skill, 0.55f);
            var tx = target.home.x;
            StartCoroutine(CameraTo(new Vector3(tx * 0.4f, 1.9f, -7f), new Vector3(tx * 0.6f, 1.1f, 0f), 27f, 0.3f));
        }

        // The step back rides the clip's wind-up and the lunge arrives on its impact frame.
        IEnumerator Melee(Actor by, Actor target, Strike s)
        {
            var dir = by.Forward;
            var back = by.home - dir * 0.3f;
            var contact = target.home - dir * (by.Reach + target.Reach);
            float blow = by.model.Swing(0.42f);
            yield return Tween(blow * 0.45f, k => by.t.localPosition = Vector3.Lerp(by.home, back, k), Ease.OutCubic);
            AudioBank.I.Play(Sfx.Step, 0.7f);
            if (s.result == StrikeResult.Miss) StartCoroutine(Dodge(target, 0f));
            yield return Tween(blow * 0.55f, k => by.t.localPosition = Vector3.Lerp(back, contact, k), Ease.OutCubic);
            if (!Skipped) yield return Impact(by, target, s, target.Chest - dir * 0.25f);
            yield return Wait(0.1f);
            yield return Tween(0.26f, k => by.t.localPosition = Vector3.Lerp(contact, by.home, k), Ease.InOutSine);
        }

        IEnumerator Shoot(Actor by, Actor target, Strike s)
        {
            bool miss = s.result == StrikeResult.Miss;
            var dir = by.Forward;
            yield return Wait(by.model.Swing(0.3f));   // the crossbow kicks on the release frame
            AudioBank.I.Play(Sfx.Arrow, 0.9f);
            var from = by.model.RightHand.position + dir * 0.5f;
            var hitAt = target.Chest - dir * 0.15f;
            var to = miss ? hitAt + dir * 3.5f + Vector3.down * 1.1f : hitAt;
            if (miss) StartCoroutine(Dodge(target, 0.05f));
            float arc = 0.5f + Vector3.Distance(from, to) * 0.06f;
            var prev = from;
            arrow.position = from;
            arrow.gameObject.SetActive(true);
            yield return Tween(miss ? 0.42f : 0.3f, k =>
            {
                var p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                if ((p - prev).sqrMagnitude > 1e-6f) arrow.rotation = Quaternion.LookRotation(p - prev);
                arrow.position = p;
                prev = p;
            }, Ease.Linear);
            arrow.gameObject.SetActive(false);
            if (!Skipped) yield return Impact(by, target, s, hitAt);
            yield return Wait(0.15f);
        }

        IEnumerator Cast(Actor by, Actor target, Strike s)
        {
            bool miss = s.result == StrikeResult.Miss;
            var dir = by.Forward;
            AudioBank.I.Play(Sfx.Skill, 0.8f);
            yield return Wait(by.model.Swing(0.32f));   // the bolt leaves as the casting hand snaps forward
            var from = by.model.RightHand.position + dir * 0.35f;
            Fx.I?.Embers(from);
            var hitAt = target.Chest - dir * 0.2f;
            var to = miss ? hitAt + dir * 4f + Vector3.up * 0.6f : hitAt;
            if (miss) StartCoroutine(Dodge(target, 0.05f));
            bolt.position = from;
            bolt.rotation = Quaternion.LookRotation(to - from);
            bolt.gameObject.SetActive(true);
            yield return Tween(miss ? 0.45f : 0.32f, k =>
            {
                var p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI * 2f) * 0.08f;
                bolt.position = p;
                bolt.localScale = Vector3.one * (0.85f + Mathf.Sin(Time.time * 40f) * 0.15f);
                Fx.I?.Embers(p);
            }, Ease.Linear);
            bolt.gameObject.SetActive(false);
            if (!miss && !Skipped) Fx.I?.FireBurst(hitAt);
            if (!Skipped) yield return Impact(by, target, s, hitAt);
            yield return Wait(0.15f);
        }

        // The moment of contact: everything the player must read about this strike.
        IEnumerator Impact(Actor by, Actor target, Strike s, Vector3 at)
        {
            if (s.result == StrikeResult.Miss)
            {
                AudioBank.I.Play(Sfx.Arrow, 0.5f);
                GameUI.I.FloatText(target.Head, "MISS", Palette.Parchment, 1.8f, 0.5f, 0.6f);
                yield break;
            }
            bool crit = s.result == StrikeResult.Crit;
            var tc = Palette.Team(target.unit.team);
            target.model.Hit(crit || s.breaks);
            if (crit)
            {
                Fx.I?.CritSparks(at, tc);
                GameUI.I.Flash(Palette.Gold);
                GameUI.I.FloatText(target.Head + Vector3.up * 0.45f, "CRITICAL", Palette.Gold, 2.1f, 0.3f, 2.2f);
                AudioBank.I.Play(Sfx.Hit, 1f, 0f, 0f);
                AudioBank.I.Play(Sfx.Flank, 0.5f, 0f, 0f);
            }
            else
            {
                Fx.I?.HitSparks(at, tc);
                AudioBank.I.Play(Sfx.Hit, 0.9f);
            }
            GameUI.I.FloatText(at + Vector3.up * 0.2f, s.damage.ToString(), s.damage == 0 ? Palette.Muted : crit ? Palette.Gold : Palette.Parchment, crit ? 3.0f : 2.3f, 0.55f, 0.4f);
            GameUI.I.SetBattleHp(target.left, s.targetHpAfter);
            Shake(crit ? 0.9f : 0.45f);
            StartCoroutine(Knockback(target, by.Forward, crit ? 0.6f : 0.32f, s.breaks));
            if (s.breaks) StartCoroutine(BreakStamp(target));
            if (s.kills) Fall(target);
            yield return HitStop(crit ? 0.12f : 0.065f);
            slowMo = 1f;
            ApplyTime();
        }

        IEnumerator BreakStamp(Actor a)
        {
            yield return Wait(0.2f);
            GameUI.I.FloatText(a.Head + Vector3.up * 0.1f, "BREAK", Palette.CrimsonLight, 2.0f, 0.12f, 2.4f);
            GameUI.I.SetBattleBroken(a.left);
            AudioBank.I.Play(Sfx.Flank, 0.9f);
            Shake(0.25f);
        }

        void Fall(Actor a)
        {
            if (a.down) return;
            a.down = true;
            a.model.Die();
            AudioBank.I.Play(Sfx.Death);
            Fx.I?.DeathPuff(a.t.position);
            Fx.I?.TeamBurst(a.Chest, Palette.Team(a.unit.team));
            Shake(0.4f);
        }

        // Reactions run on this component so the attacker's return can overlap them. The hit
        // clip does the flinch; the root only slides, with a wobble when the blow breaks.

        IEnumerator Knockback(Actor a, Vector3 push, float distance, bool stagger)
        {
            var to = a.home + push * distance;
            float lean = stagger ? 10f : 0f;
            yield return Tween(0.08f, k => { a.t.localPosition = Vector3.Lerp(a.home, to, k); a.t.localRotation = a.baseRot * Quaternion.Euler(-lean * k, 0f, 0f); }, Ease.OutCubic);
            if (a.down) yield break;
            if (stagger)
                yield return Tween(0.45f, k => a.t.localRotation = a.baseRot * Quaternion.Euler(-lean + Mathf.Sin(k * Mathf.PI * 3f) * 6f * (1f - k), 0f, 0f), Ease.Linear);
            yield return Tween(stagger ? 0.35f : 0.24f, k => { a.t.localPosition = Vector3.Lerp(to, a.home, k); a.t.localRotation = a.baseRot * Quaternion.Euler(-lean * (1f - k), 0f, 0f); }, Ease.InOutSine);
        }

        // The dodge clip hops back from the blow and hands back to idle on its own.
        IEnumerator Dodge(Actor a, float delay)
        {
            yield return Wait(delay);
            a.model.Dodge();
        }
    }
}
