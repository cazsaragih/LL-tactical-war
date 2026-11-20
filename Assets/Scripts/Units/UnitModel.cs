using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LLTacticalWar
{
    // The figure on top of a unit: a KayKit model, and for a rider the mount it sits on, each
    // driven by its own Playables mixer with no controller asset (a Rig). Every one-shot (attack,
    // hit, dodge, death, cheer) plays on both rigs; the mount carries the figure over the ground
    // and the rider's hips and legs hold a straddling seat over whatever its own clips do.
    public class UnitModel : MonoBehaviour
    {
        public bool footDust;
        public float maxSpeed = 6f;
        public Transform flag;           // banner cloth: sways
        public Transform RightHand { get; private set; }   // shots and spells leave from here
        public float Lift { get; private set; }            // a rider's seat height, 0 on foot

        enum Slot { Idle, Move, Gallop, Attack, Hit, HeavyHit, Dodge, Death, Cheer, Count }
        const float Fade = 0.07f;        // cross-fade time constant: ~95% blended in 0.2 s
        const float FadeLead = 0.15f;    // a one-shot starts handing back this long before it ends
        const float Straddle = 65f;      // a rider's thighs open this far around the vertical
        const float FlashGain = 0.8f;
        const float DimMul = 0.55f;      // a spent unit greys out: darker, and lifted toward flat grey
        static readonly Color DimLift = Palette.Muted * 0.28f;

        Rig body, mount;                 // mount is null on foot
        Rig Legs => mount ?? body;       // the rig that carries the figure over the ground
        float impact, mountImpact;

        Transform[] seatBones;
        Quaternion[] seatPose;
        Transform hips;
        Vector3 hipsPose;

        // one entry per material slot: a mount's single mesh carries several flat colours
        readonly List<(Renderer r, int slot, Color color)> tinted = new List<(Renderer, int, Color)>();
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Vector3 lastPos;
        float groundSpeed, speedN, steps, idleT, yawLag, lastYaw;
        bool yawPrimed;
        float punchT = -1f, flashT = -1f;
        bool dimmed, dead;
        Vector3 baseScale = Vector3.one;

        public void Init(UnitLook look, Animator rider, Animator mountAnimator)
        {
            Lift = transform.InverseTransformPoint(rider.transform.position).y;
            impact = look.Impact;
            RightHand = UnitFactory.Bone(rider.transform, "handslot.r");
            var clips = Resources.LoadAll<AnimationClip>(UnitLooks.CharacterDir + look.Model);
            if (look.Seat != null) CaptureSeat(rider.transform, Find(clips, look.Seat), Find(clips, look.Idle));
            body = new Rig(rider, clips, new[] { look.Idle, look.Move, null, look.Attack, look.Hit, look.HeavyHit, look.Dodge, look.Death, look.Cheer },
                           look.Move != null ? new[] { (Slot.Move, UnitLooks.RunGroundSpeed) } : new (Slot, float)[0]);
            var m = look.Mount;
            if (m != null)
            {
                mountImpact = m.Impact;
                mount = new Rig(mountAnimator, Resources.LoadAll<AnimationClip>(UnitLooks.MountDir + m.Model),
                                new[] { m.Idle, m.Walk, m.Gallop, m.Attack, m.Hit, m.Hit, null, m.Death, null },
                                new[] { (Slot.Move, m.WalkSpeed), (Slot.Gallop, m.GallopSpeed) });
            }

            idleT = Random.value * 10f;
            baseScale = transform.localScale;
            CacheRenderers();
            ResetMotionAnchor();
        }

        static AnimationClip Find(AnimationClip[] clips, string name)
        {
            foreach (var c in clips) if (c.name == name) return c;
            return null;
        }

        // The seat clip's legs, thighs opened around the vertical so the knees clear the barrel,
        // and the hips pinned where the idle stands them, so no clip lifts the rider off the saddle.
        void CaptureSeat(Transform rig, AnimationClip seat, AnimationClip idle)
        {
            hips = UnitFactory.Bone(rig, "hips");
            idle.SampleAnimation(rig.gameObject, 0f);
            hipsPose = hips.localPosition;
            seat.SampleAnimation(rig.gameObject, 0f);
            seatBones = System.Array.ConvertAll(new[] { "upperleg.l", "lowerleg.l", "foot.l", "upperleg.r", "lowerleg.r", "foot.r" }, n => UnitFactory.Bone(rig, n));
            seatBones[0].rotation = Quaternion.AngleAxis(-Straddle, rig.up) * seatBones[0].rotation;
            seatBones[3].rotation = Quaternion.AngleAxis(Straddle, rig.up) * seatBones[3].rotation;
            seatPose = System.Array.ConvertAll(seatBones, b => b.localRotation);
        }

        void OnDestroy()
        {
            body?.Destroy();
            mount?.Destroy();
        }

        public void CacheRenderers()
        {
            tinted.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    tinted.Add((r, i, mats[i] != null && mats[i].HasProperty(BaseColorId) ? mats[i].GetColor(BaseColorId) : Color.white));
            }
            mpb = new MaterialPropertyBlock();
        }

        public void SetDimmed(bool on)
        {
            if (dimmed == on) return;
            dimmed = on;
            ApplyTint(0f);
        }

        // Dimmed, fallen or flashing: the toon shader multiplies _BaseColor and adds _EmissionColor
        // on every part, flat or atlas-textured.
        void ApplyTint(float flash)
        {
            if (mpb == null) return;
            float mul = dead ? 0.6f : dimmed ? DimMul : 1f;
            var lift = dimmed && !dead ? DimLift : Color.black;
            foreach (var (r, slot, color) in tinted)
            {
                if (r == null) continue;
                var c = color * mul;
                c.a = 1f;
                r.GetPropertyBlock(mpb, slot);
                mpb.SetColor(BaseColorId, Color.Lerp(c, Palette.Parchment, flash));
                mpb.SetColor(EmissionId, Color.Lerp(lift, Palette.Parchment * FlashGain, flash));
                r.SetPropertyBlock(mpb, slot);
            }
        }

        // ---------------- clips ----------------

        void Play(Slot s, float speed, bool hold = false)
        {
            if (dead && s != Slot.Death) return;
            body.Play(s, speed, hold);
            mount?.Play(s, speed, hold);
        }

        // Plays the attack so its blow (or release) lands `impactIn` seconds from now, within a
        // speed range that still reads as the same move, and the mount's attack drives home on
        // the same frame. Returns when it really lands.
        public float Swing(float impactIn)
        {
            float speed = Mathf.Clamp(impact / Mathf.Max(0.05f, impactIn), 0.8f, 1.6f);
            float lands = impact / speed;
            if (dead) return lands;
            body.Play(Slot.Attack, speed);
            mount?.Play(Slot.Attack, Mathf.Clamp(mountImpact / lands, 0.6f, 1.6f));
            return lands;
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
            if (dt <= 0f || body == null) return;

            // only travel along the facing drives the gait: a figure sliding back (a knockback,
            // the return from a lunge) keeps its pose instead of running in reverse
            var pos = transform.position;
            float forward = Mathf.Max(0f, Vector3.Dot(pos - lastPos, transform.forward) / dt);
            lastPos = pos;
            groundSpeed = Mathf.Lerp(groundSpeed, forward, 1f - Mathf.Exp(-dt * 10f));   // settle, never freeze
            speedN = Mathf.Clamp01(groundSpeed / Mathf.Max(0.1f, maxSpeed));
            idleT += dt;

            float at = Legs.Blend(dt, groundSpeed);
            if (mount != null) body.Blend(dt, 0f);
            if (at >= 0f) FootFalls(at);
            if (!dead) TurnLag(dt);
            if (seatBones != null)
            {
                hips.localPosition = hipsPose;
                for (int i = 0; i < seatBones.Length; i++) seatBones[i].localRotation = seatPose[i];
            }
            // the hung cloth stirs at rest and streams back on the run
            if (flag != null) flag.localRotation = Quaternion.Euler(Mathf.Sin(idleT * 2.2f) * 6f + 24f * speedN, 0f, Mathf.Sin(idleT * 1.3f) * 4f);
            Punches(dt);
        }

        // Dust each time the step count passes a whole step (two per gait cycle).
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

        // One skeleton's clips on one mixer. Idle and the gaits blend on ground speed, in a line
        // from standing through each gait's own speed, and each gait plays at the speed the
        // figure really travels so feet and hooves don't skate. One-shots cross-fade over the
        // top and hand back as they end.
        sealed class Rig
        {
            readonly PlayableGraph graph;
            readonly AnimationMixerPlayable mixer;
            readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[(int)Slot.Count];
            readonly float[] weights = new float[(int)Slot.Count];
            readonly float[] targets = new float[(int)Slot.Count];
            readonly (Slot slot, float speed)[] gaits;   // slowest first; speed is ground speed at 1x
            int oneShot = -1;
            bool holdLast;

            public Rig(Animator animator, AnimationClip[] clips, string[] names, (Slot, float)[] gaits)
            {
                this.gaits = gaits;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Figure_" + animator.name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);   // hit-stop and 2x reach the clips too
                mixer = AnimationMixerPlayable.Create(graph, (int)Slot.Count);
                AnimationPlayableOutput.Create(graph, "Figure", animator).SetSourcePlayable(mixer);
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
            }

            public void Destroy()
            {
                if (graph.IsValid()) graph.Destroy();
            }

            public void Play(Slot s, float speed, bool hold = false)
            {
                var p = slots[(int)s];
                if (!p.IsValid()) return;
                p.SetTime(0);
                p.SetSpeed(speed);
                p.Play();
                oneShot = (int)s;
                holdLast = hold;
            }

            // Steps the weights toward this frame's targets. Returns the leading gait's step
            // count (two per cycle) while it carries the figure, or -1.
            public float Blend(float dt, float groundSpeed)
            {
                if (oneShot >= 0)
                {
                    var p = slots[oneShot];
                    float len = p.GetAnimationClip().length;
                    if (p.GetTime() >= len) { p.SetTime(len); p.Pause(); }   // hold the last frame
                    if (!holdLast && p.GetTime() >= len - FadeLead * p.GetSpeed()) oneShot = -1;
                }

                System.Array.Clear(targets, 0, targets.Length);
                int from = (int)Slot.Idle, to = from;
                float k = 0f, below = 0f;
                foreach (var (slot, speed) in gaits)
                {
                    from = to;
                    to = (int)slot;
                    k = Mathf.InverseLerp(below, speed, groundSpeed);
                    below = speed;
                    slots[to].SetSpeed(Mathf.Clamp(groundSpeed / speed, 0.6f, 3f));
                    if (k < 1f) break;
                }
                if (oneShot >= 0) targets[oneShot] = 1f;
                else { targets[from] += 1f - k; targets[to] += k; }

                float fade = 1f - Mathf.Exp(-dt / Fade);
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] += (targets[i] - weights[i]) * fade;
                    if (slots[i].IsValid()) mixer.SetInputWeight(i, weights[i]);
                }

                int lead = k >= 0.5f ? to : from;
                if (oneShot >= 0 || lead == (int)Slot.Idle || weights[lead] < 0.5f) return -1f;
                var g = slots[lead];
                return (float)(g.GetTime() / g.GetAnimationClip().length) * 2f;
            }
        }
    }
}
