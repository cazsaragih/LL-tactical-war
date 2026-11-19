using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LLTacticalWar
{
    // The figure on top of a unit: a KayKit model driven by one Playables mixer, with no
    // controller asset. Idle and the run cycle blend on ground speed, and the run plays at the
    // speed the figure really travels so its feet don't skate. One-shots (attack, hit, dodge,
    // death, cheer) cross-fade over the top and hand back as they end. The Dragoon's code-built
    // horse keeps a procedural gait under a rider whose legs hold a straddling seat.
    public class UnitModel : MonoBehaviour
    {
        public bool footDust;
        public float maxSpeed = 6f;
        public Transform horse;          // the Dragoon's horse body: bobs with the gait
        public Transform[] horseLegs;
        public Transform flag;           // banner cloth: sways
        public Transform RightHand { get; private set; }   // shots and spells leave from here
        public float Lift { get; private set; }            // a rider's seat height, 0 on foot

        enum Slot { Idle, Move, Attack, Hit, HeavyHit, Dodge, Death, Cheer, Count }
        const float Fade = 0.07f;        // cross-fade time constant: ~95% blended in 0.2 s
        const float FadeLead = 0.15f;    // a one-shot starts handing back this long before it ends
        const float Straddle = 65f;      // a rider's thighs open this far around the vertical
        const float FlashGain = 0.8f;
        const float DimMul = 0.55f;      // a spent unit greys out: darker, and lifted toward flat grey
        static readonly Color DimLift = Palette.Muted * 0.28f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[(int)Slot.Count];
        readonly float[] weights = new float[(int)Slot.Count];
        int oneShot = -1;
        bool holdLast;
        float impact;

        Transform[] seatBones;
        Quaternion[] seatPose;

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Vector3 lastPos;
        float groundSpeed, speedN, steps, idleT, yawLag, lastYaw, horseBaseY;
        bool yawPrimed;
        float punchT = -1f, flashT = -1f;
        bool dimmed, dead;
        Vector3 baseScale = Vector3.one;

        public void Init(UnitLook look, Animator animator, float lift)
        {
            Lift = lift;
            impact = look.Impact;
            RightHand = UnitFactory.Bone(animator.transform, "handslot.r");
            var clips = Resources.LoadAll<AnimationClip>(UnitLooks.CharacterDir + look.Model);
            if (look.Seat != null) CaptureSeat(animator.transform, Find(clips, look.Seat));

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Figure_" + look.Model);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);   // hit-stop and 2x reach the clips too
            mixer = AnimationMixerPlayable.Create(graph, (int)Slot.Count);
            AnimationPlayableOutput.Create(graph, "Figure", animator).SetSourcePlayable(mixer);
            var names = new[] { look.Idle, look.Move, look.Attack, look.Hit, look.HeavyHit, look.Dodge, look.Death, look.Cheer };
            for (int i = 0; i < names.Length; i++)
            {
                var clip = Find(clips, names[i]);
                if (clip == null) continue;
                slots[i] = AnimationClipPlayable.Create(graph, clip);
                graph.Connect(slots[i], 0, mixer, i);
            }
            var idle = slots[(int)Slot.Idle];
            idle.SetTime(Random.value * idle.GetAnimationClip().length);   // armies don't breathe in step
            weights[(int)Slot.Idle] = 1f;
            mixer.SetInputWeight((int)Slot.Idle, 1f);
            graph.Play();

            idleT = Random.value * 10f;
            baseScale = transform.localScale;
            if (horse != null) horseBaseY = horse.localPosition.y;
            CacheRenderers();
            ResetMotionAnchor();
        }

        static AnimationClip Find(AnimationClip[] clips, string name)
        {
            foreach (var c in clips) if (c.name == name) return c;
            return null;
        }

        // The seat clip's legs, thighs opened around the vertical so the knees clear the barrel.
        void CaptureSeat(Transform rig, AnimationClip seat)
        {
            seat.SampleAnimation(rig.gameObject, 0f);
            seatBones = System.Array.ConvertAll(new[] { "upperleg.l", "lowerleg.l", "foot.l", "upperleg.r", "lowerleg.r", "foot.r" }, n => UnitFactory.Bone(rig, n));
            seatBones[0].rotation = Quaternion.AngleAxis(-Straddle, rig.up) * seatBones[0].rotation;
            seatBones[3].rotation = Quaternion.AngleAxis(Straddle, rig.up) * seatBones[3].rotation;
            seatPose = System.Array.ConvertAll(seatBones, b => b.localRotation);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        public void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
            mpb = new MaterialPropertyBlock();
        }

        public void SetDimmed(bool on)
        {
            if (dimmed == on) return;
            dimmed = on;
            ApplyTint(0f);
        }

        // Dimmed, fallen or flashing. Flat parts flash by their base colour, atlas-textured
        // parts by emission (only they carry the keyword).
        void ApplyTint(float flash)
        {
            if (renderers == null) return;
            float mul = dead ? 0.6f : dimmed ? DimMul : 1f;
            var lift = dimmed && !dead ? DimLift : Color.black;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = baseColors[i] * mul;
                c.a = 1f;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, Color.Lerp(c, Palette.Parchment, flash));
                mpb.SetColor(EmissionId, Color.Lerp(lift, Palette.Parchment * FlashGain, flash));
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        // ---------------- clips ----------------

        void Play(Slot s, float speed, bool hold = false)
        {
            var p = slots[(int)s];
            if (!p.IsValid() || (dead && s != Slot.Death)) return;
            p.SetTime(0);
            p.SetSpeed(speed);
            p.Play();
            oneShot = (int)s;
            holdLast = hold;
        }

        // Plays the attack so its blow (or release) lands `impactIn` seconds from now, within a
        // speed range that still reads as the same move. Returns when it really lands.
        public float Swing(float impactIn)
        {
            float speed = Mathf.Clamp(impact / Mathf.Max(0.05f, impactIn), 0.8f, 1.6f);
            Play(Slot.Attack, speed);
            return impact / speed;
        }

        public void Hit(bool heavy) { Play(heavy ? Slot.HeavyHit : Slot.Hit, 1.15f); flashT = 0f; }
        public void Dodge() => Play(Slot.Dodge, 1f);
        public void Cheer() => Play(Slot.Cheer, 1f);

        // A glint: flash and a scale pop, no clip.
        public void Punch() { punchT = 0f; flashT = 0f; }

        public void Die()
        {
            if (dead) return;
            Play(Slot.Death, 1f, true);
            dead = true;
            // clear any hit flash mid-blend and darken the fallen so they read as down, not lit
            flashT = punchT = -1f;
            transform.localScale = baseScale;
            ApplyTint(0f);
            if (horse != null) StartCoroutine(Topple());
        }

        // The horse has no clips: it goes over on its side with its rider.
        IEnumerator Topple()
        {
            var start = transform.localRotation;
            var end = start * Quaternion.Euler(0f, 0f, 80f);
            yield return Ease.Tween(0.45f, k => transform.localRotation = Quaternion.SlerpUnclamped(start, end, k), Ease.OutCubic, false);
        }

        public void ResetMotionAnchor()
        {
            lastPos = transform.position;
            yawPrimed = false;
        }

        // ---------------- per frame ----------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !graph.IsValid()) return;

            // only travel along the facing drives the gait: a figure sliding back (a knockback,
            // the return from a lunge) keeps its pose instead of running in reverse
            var pos = transform.position;
            float forward = Mathf.Max(0f, Vector3.Dot(pos - lastPos, transform.forward) / dt);
            lastPos = pos;
            groundSpeed = Mathf.Lerp(groundSpeed, forward, 1f - Mathf.Exp(-dt * 10f));   // settle, never freeze
            speedN = Mathf.Clamp01(groundSpeed / Mathf.Max(0.1f, maxSpeed));
            idleT += dt;

            Blend(dt);
            if (!dead) TurnLag(dt);
            if (seatBones != null)
                for (int i = 0; i < seatBones.Length; i++) seatBones[i].localRotation = seatPose[i];
            Gallop(dt);
            // the hung cloth stirs at rest and streams back on the run
            if (flag != null) flag.localRotation = Quaternion.Euler(Mathf.Sin(idleT * 2.2f) * 6f + 24f * speedN, 0f, Mathf.Sin(idleT * 1.3f) * 4f);
            Punches(dt);
        }

        void Blend(float dt)
        {
            if (oneShot >= 0)
            {
                var p = slots[oneShot];
                float len = p.GetAnimationClip().length;
                if (p.GetTime() >= len) { p.SetTime(len); p.Pause(); }   // hold the last frame
                if (!holdLast && p.GetTime() >= len - FadeLead * p.GetSpeed()) oneShot = -1;
            }

            var move = slots[(int)Slot.Move];
            float run = 0f;
            if (move.IsValid())
            {
                run = Mathf.Clamp01(speedN * 2.5f);
                move.SetSpeed(Mathf.Clamp(groundSpeed / UnitLooks.RunGroundSpeed, 0.6f, 3f));
                if (oneShot < 0 && run > 0.5f) FootFalls((float)(move.GetTime() / move.GetAnimationClip().length) * 2f);
            }

            float k = 1f - Mathf.Exp(-dt / Fade);
            for (int i = 0; i < weights.Length; i++)
            {
                float target = oneShot >= 0 ? (i == oneShot ? 1f : 0f)
                             : i == (int)Slot.Idle ? 1f - run
                             : i == (int)Slot.Move ? run : 0f;
                weights[i] += (target - weights[i]) * k;
                if (slots[i].IsValid()) mixer.SetInputWeight(i, weights[i]);
            }
        }

        // Dust each time the step count passes a whole step (two per run cycle, one per hoof pair).
        void FootFalls(float at)
        {
            if (footDust && speedN > 0.45f && Mathf.Floor(at) != Mathf.Floor(steps)) Fx.I?.FootDust(transform.position);
            steps = at;
        }

        // Turns ease in: the figure lags its unit's snapped facing and catches up in ~0.2 s.
        void TurnLag(float dt)
        {
            if (transform.parent == null) return;
            float yaw = transform.parent.eulerAngles.y;
            if (!yawPrimed) { lastYaw = yaw; yawLag = 0f; yawPrimed = true; }
            yawLag = (yawLag + Mathf.DeltaAngle(yaw, lastYaw)) * Mathf.Exp(-dt / 0.06f);
            lastYaw = yaw;
            transform.localRotation = Quaternion.Euler(0f, yawLag, 0f);
        }

        void Gallop(float dt)
        {
            if (horseLegs == null) return;
            float gait = steps + dt * 6.2f * speedN;   // half-strides: ~3 gallop cycles a second at full speed
            FootFalls(gait);
            float s = Mathf.Sin(gait * Mathf.PI);
            for (int i = 0; i < horseLegs.Length; i++)
                horseLegs[i].localRotation = Quaternion.Euler(((i % 3 == 0) ? s : -s) * 32f * speedN, 0f, 0f);
            var p = horse.localPosition;
            p.y = horseBaseY + Mathf.Abs(s) * 0.07f * speedN + Mathf.Sin(idleT * Mathf.PI) * 0.012f;
            horse.localPosition = p;
        }

        void Punches(float dt)
        {
            if (punchT >= 0f)
            {
                punchT += dt / 0.15f;
                transform.localScale = baseScale * (1f + Mathf.Sin(Mathf.Clamp01(punchT) * Mathf.PI) * 0.18f);
                if (punchT >= 1f) { punchT = -1f; transform.localScale = baseScale; }
            }
            if (flashT >= 0f)
            {
                flashT += dt / 0.12f;
                ApplyTint(1f - Mathf.Clamp01(flashT));
                if (flashT >= 1f) flashT = -1f;
            }
        }
    }
}
