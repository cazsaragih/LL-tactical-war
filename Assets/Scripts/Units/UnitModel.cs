using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // The figure on top of a unit: primitive parts plus procedural motion (gait, idle breathing,
    // weapon swing, hit punch, death fall). Nothing here is ever statuary.
    public class UnitModel : MonoBehaviour
    {
        public Transform body, head, legL, legR, armR, armL, flag, weaponHold;
        public Transform[] horseLegs;
        public bool footDust;
        public float maxSpeed = 6f;

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Vector3 lastPos;
        float speedN, phase, idleT;
        float swingT = -1f, swingDur = 0.4f;
        float punchT = -1f;
        float flashT = -1f;
        bool dimmed, dead;
        Vector3 baseScale;
        float bodyBaseY;

        void Awake()
        {
            CacheRenderers();
            lastPos = transform.position;
            idleT = Random.value * 10f;
            baseScale = transform.localScale;
            if (body != null) bodyBaseY = body.localPosition.y;
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
            ApplyTint(on ? 0.5f : 1f, Color.clear, 0f);
        }

        void ApplyTint(float mul, Color flash, float flashAmt)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = baseColors[i] * mul;
                c.a = 1f;
                if (flashAmt > 0f) c = Color.Lerp(c, flash, flashAmt);
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        public void Swing(float duration)
        {
            swingDur = Mathf.Max(0.1f, duration);
            swingT = 0f;
        }

        public void Punch() { punchT = 0f; flashT = 0f; }

        public void Die()
        {
            if (dead) return;
            dead = true;
            // clear any hit flash mid-blend and darken the fallen so they read as down, not lit
            flashT = punchT = -1f;
            transform.localScale = baseScale;
            ApplyTint(0.6f, Color.clear, 0f);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            var start = transform.localRotation;
            var end = start * Quaternion.Euler(-85f, 0f, Random.Range(-15f, 15f));
            yield return Ease.Tween(0.35f, k => transform.localRotation = Quaternion.SlerpUnclamped(start, end, k), Ease.OutCubic, false);
            yield return new WaitForSeconds(2f);
            var p0 = transform.localPosition;
            yield return Ease.Tween(1f, k => transform.localPosition = p0 + Vector3.down * 0.8f * k, Ease.InOutSine, false);
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var pos = transform.position;
            var vel = (pos - lastPos) / dt;
            lastPos = pos;
            vel.y = 0f;
            float target = Mathf.Clamp01(vel.magnitude / Mathf.Max(0.1f, maxSpeed));
            speedN = Mathf.Lerp(speedN, target, 1f - Mathf.Exp(-dt * 10f));   // settle, never freeze

            float prevPhase = phase;
            phase += dt * 2.6f * 2f * Mathf.PI * Mathf.Max(speedN, 0.0001f) * 1.2f;
            idleT += dt;

            float swing = Mathf.Sin(phase);
            float legAmp = 38f * speedN;

            if (legL != null) legL.localRotation = Quaternion.Euler(swing * legAmp, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-swing * legAmp, 0f, 0f);
            if (horseLegs != null)
                for (int i = 0; i < horseLegs.Length; i++)
                    horseLegs[i].localRotation = Quaternion.Euler(((i % 3 == 0) ? swing : -swing) * 32f * speedN, 0f, 0f);

            if (body != null)
            {
                float bob = Mathf.Abs(swing) * 0.07f * speedN + Mathf.Sin(idleT * Mathf.PI) * 0.012f;
                var bp = body.localPosition; bp.y = bodyBaseY + bob; body.localPosition = bp;
                float roll = Mathf.Sin(phase * 0.5f) * 2.5f * speedN;
                body.localRotation = Quaternion.Euler(7f * speedN, 0f, roll);
            }

            // Arms: off-hand swings opposite the legs; weapon arm follows the attack curve.
            if (armL != null) armL.localRotation = Quaternion.Euler(-10f - swing * 25f * speedN, 0f, -6f);
            if (armR != null)
            {
                float ax = -18f + swing * 20f * speedN;
                if (swingT >= 0f)
                {
                    swingT += dt / swingDur;
                    float k = swingT;
                    // wind up (0-35%) -> strike (35-55%) -> recover
                    if (k < 0.35f) ax = Mathf.Lerp(-18f, -165f, Ease.OutCubic(k / 0.35f));
                    else if (k < 0.55f) ax = Mathf.Lerp(-165f, -35f, Ease.OutCubic((k - 0.35f) / 0.2f));
                    else ax = Mathf.Lerp(-35f, -18f, Ease.InOutSine((k - 0.55f) / 0.45f));
                    if (swingT >= 1f) swingT = -1f;
                }
                armR.localRotation = Quaternion.Euler(ax, 0f, 6f);
            }

            if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(idleT * 0.7f) * 6f, 0f);
            if (flag != null) flag.localRotation = Quaternion.Euler(0f, Mathf.Sin(idleT * 2.2f) * 12f - 10f * speedN, 0f);

            // hit punch + flash
            if (punchT >= 0f)
            {
                punchT += dt / 0.15f;
                float s = 1f + Mathf.Sin(Mathf.Clamp01(punchT) * Mathf.PI) * 0.18f;
                transform.localScale = baseScale * s;
                if (punchT >= 1f) { punchT = -1f; transform.localScale = baseScale; }
            }
            if (flashT >= 0f)
            {
                flashT += dt / 0.12f;
                ApplyTint(dimmed ? 0.5f : 1f, Palette.Parchment, 1f - Mathf.Clamp01(flashT));
                if (flashT >= 1f) { flashT = -1f; ApplyTint(dimmed ? 0.5f : 1f, Color.clear, 0f); }
            }

            // footfall dust on each half-cycle
            if (footDust && speedN > 0.45f && Mathf.Floor(prevPhase / Mathf.PI) != Mathf.Floor(phase / Mathf.PI))
                Fx.I?.FootDust(pos);
        }

        public void ResetMotionAnchor() { lastPos = transform.position; }
    }
}
