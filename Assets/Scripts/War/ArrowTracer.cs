using UnityEngine;

namespace LLTacticalWar
{
    // A short-lived streak for arrows. Pooled: a fixed ring of LineRenderers reused in turn.
    public class ArrowTracer : MonoBehaviour
    {
        static ArrowTracer inst;
        LineRenderer[] pool;
        float[] until;
        int next;

        public static void Fire(Vector3 from, Vector3 to, Color c)
        {
            if (inst == null)
            {
                var root = new GameObject("[ArrowTracers]");
                inst = root.AddComponent<ArrowTracer>();
                inst.Build();
            }
            inst.Show(from, to, c);
        }

        void Build()
        {
            pool = new LineRenderer[24];
            until = new float[24];
            var mat = Palette.Particle();
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.widthMultiplier = 0.07f;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                pool[i] = lr;
            }
        }

        void Show(Vector3 from, Vector3 to, Color c)
        {
            var l = pool[next];
            until[next] = Time.time + 0.12f;
            next = (next + 1) % pool.Length;
            l.SetPosition(0, from);
            l.SetPosition(1, to);
            l.startColor = Palette.WithAlpha(c, 0f);
            l.endColor = Palette.Parchment;
            l.enabled = true;
        }

        void Update()
        {
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].enabled && Time.time > until[i]) pool[i].enabled = false;
        }
    }
}
