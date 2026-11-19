using UnityEngine;

namespace LLTacticalWar
{
    // Pooled particle effects: one ParticleSystem per effect, fired with Emit() - never
    // Instantiate per event. Colours always come from the palette.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        ParticleSystem sparks, dust, burst, confetti, motes;
        Texture2D dot;

        void Awake()
        {
            I = this;
            dot = MakeDot();
            sparks   = MakeSystem("Sparks", 0.35f, 0.6f, 256);
            dust     = MakeSystem("Dust", 0.6f, 0.25f, 512);
            burst    = MakeSystem("Burst", 0.7f, 0.5f, 256);
            confetti = MakeSystem("Confetti", 1.4f, 1.1f, 256);
        }

        static Texture2D MakeDot()
        {
            const int s = 32;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d));
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        ParticleSystem MakeSystem(string name, float lifetime, float gravity, int max, Transform parent = null)
        {
            var go = new GameObject("Fx_" + name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;          // keeps the system simulating; emission is off, Emit() feeds it
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 0.2f;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.startColor = Palette.Parchment;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = Palette.Particle();
            mat.mainTexture = dot;
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 pos, Color c, int count, float speed, float size, float lifetime, Vector3 bias = default, float spread = 1f)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere * spread + bias;
                p.position = pos + Random.insideUnitSphere * 0.08f;
                p.velocity = dir.normalized * speed * Random.Range(0.6f, 1.2f);
                p.startColor = Color.Lerp(c, Palette.WithAlpha(c, 0.7f), Random.value);
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startLifetime = lifetime * Random.Range(0.8f, 1.2f);
                ps.Emit(p, 1);
            }
        }

        public void HitSparks(Vector3 pos, Color teamColor)
        {
            Emit(sparks, pos, Palette.Gold, 4, 3.2f, 0.12f, 0.3f, Vector3.up * 0.6f);
            Emit(sparks, pos, teamColor, 3, 2.4f, 0.14f, 0.3f, Vector3.up * 0.4f);
        }

        // Heavier sparks for a critical hit.
        public void CritSparks(Vector3 pos, Color teamColor)
        {
            Emit(sparks, pos, Palette.Gold, 12, 5.5f, 0.16f, 0.4f, Vector3.up * 0.4f);
            Emit(sparks, pos, Palette.Parchment, 6, 4f, 0.12f, 0.35f);
            Emit(burst, pos, teamColor, 8, 3.2f, 0.24f, 0.45f, Vector3.up * 0.3f);
        }

        // The fire bolt's trail, emitted along its path each frame.
        public void Embers(Vector3 pos)
        {
            Emit(sparks, pos, Palette.Gold, 1, 0.5f, 0.2f, 0.3f, Vector3.up * 1.5f);
            Emit(sparks, pos, Palette.CrimsonLight, 1, 0.4f, 0.26f, 0.35f, Vector3.up * 1.5f);
        }

        public void FireBurst(Vector3 pos)
        {
            Emit(burst, pos, Palette.CrimsonLight, 14, 3.4f, 0.3f, 0.5f, Vector3.up * 0.8f);
            Emit(burst, pos, Palette.Gold, 10, 2.6f, 0.24f, 0.45f, Vector3.up * 0.8f);
            Emit(dust, pos, Palette.WithAlpha(Palette.Ink, 0.6f), 5, 1f, 0.4f, 0.6f, Vector3.up);
        }

        public void DeathPuff(Vector3 pos) { Emit(dust, pos + Vector3.up * 0.3f, Palette.Canvas, 8, 1.4f, 0.45f, 0.6f, Vector3.up * 0.5f); }
        public void FootDust(Vector3 pos) { Emit(dust, pos + Vector3.up * 0.05f, Palette.WithAlpha(Palette.Canvas, 0.8f), 3, 0.7f, 0.3f, 0.35f, Vector3.up * 0.8f); }

        public void TeamBurst(Vector3 pos, Color teamColor)
        {
            Emit(burst, pos, teamColor, 14, 3f, 0.3f, 0.6f, Vector3.up * 0.5f);
            Emit(burst, pos, Palette.Gold, 8, 2.5f, 0.22f, 0.6f, Vector3.up * 0.5f);
        }

        public void Confetti(Vector3 pos)
        {
            Color[] cols = { Palette.Gold, Palette.Azure, Palette.Parchment };
            for (int i = 0; i < 3; i++) Emit(confetti, pos, cols[i], 14, 7f, 0.28f, 1.3f, Vector3.up * 2.5f, 0.6f);
        }

        // Ambient motes: slow gold-white dust drifting over the board. The job: it makes the
        // war table feel like a sunlit room instead of a void.
        public ParticleSystem CreateMotes(Transform parent, Vector3 center, Vector3 size)
        {
            var ps = MakeSystem("Motes", 6f, -0.01f, 64, parent);
            ps.transform.position = center;
            var main = ps.main;
            main.loop = true;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = Palette.WithAlpha(Palette.SunKey, 0.55f);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 6f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var size2 = ps.sizeOverLifetime;
            size2.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0)));
            ps.Play();
            motes = ps;
            return ps;
        }
    }
}
