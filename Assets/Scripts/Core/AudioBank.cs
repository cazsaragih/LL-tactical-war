using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    public enum Sfx { UiMove, UiConfirm, UiCancel, Step, Hit, Death, Arrow, Skill, Horn, Victory, Defeat, Turn, Flank }

    // Every clip is synthesized at startup: no audio assets. Volumes run through a static gain
    // layer (Music / Sfx) because an AudioMixer can't be authored headlessly; the settings
    // sliders bind to these two floats.
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank I;

        public static float MusicVolume = 0.6f;
        public static float SfxVolume = 0.85f;

        const int SR = 22050;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        readonly List<AudioSource> sfxSources = new List<AudioSource>();
        int nextSource;

        AudioSource musicSource, ambientSource;
        AudioClip tacticMusic, warMusic, ambientBed;
        float musicBase = 0.35f, ambientBase = 0.18f;
        float musicFade = 1f;

        void Awake()
        {
            I = this;
            for (int i = 0; i < 12; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                sfxSources.Add(s);
            }
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true; musicSource.playOnAwake = false;
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true; ambientSource.playOnAwake = false;

            Build();
            ambientSource.clip = ambientBed;
            ambientSource.Play();
            PlayTacticMusic();
        }

        void Update()
        {
            musicSource.volume = musicBase * musicFade * MusicVolume;
            ambientSource.volume = ambientBase * MusicVolume;
        }

        public void PlayTacticMusic() { SwapMusic(tacticMusic, 0.32f); }
        public void PlayWarMusic() { SwapMusic(warMusic, 0.42f); }

        void SwapMusic(AudioClip clip, float vol)
        {
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicBase = vol;
            musicSource.Play();
        }

        public void DuckMusic(bool duck) { musicFade = duck ? 0.5f : 1f; }

        // minGap throttles sounds that can fire hundreds of times a second in a battle.
        public void Play(Sfx sfx, float volume = 1f, float minGap = 0f, float pitchJitter = 0.08f)
        {
            if (!clips.TryGetValue(sfx, out var clip)) return;
            float now = Time.unscaledTime;
            if (minGap > 0f && lastPlayed.TryGetValue(sfx, out var last) && now - last < minGap) return;
            lastPlayed[sfx] = now;
            var s = sfxSources[nextSource];
            nextSource = (nextSource + 1) % sfxSources.Count;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume * SfxVolume);
        }

        // ---------------- synthesis ----------------

        void Build()
        {
            clips[Sfx.UiMove]    = Make("ui_move", 0.03f, (t, i) => Noise() * 0.5f * Env(t, 0.03f, 0.002f, 0.02f) + Sine(1700f, t) * 0.25f * Env(t, 0.03f, 0.002f, 0.02f));
            clips[Sfx.UiConfirm] = Make("ui_confirm", 0.11f, (t, i) => Sine(Mathf.Lerp(880f, 1320f, t / 0.09f), t) * 0.55f * Env(t, 0.11f, 0.004f, 0.08f));
            clips[Sfx.UiCancel]  = Make("ui_cancel", 0.11f, (t, i) => Sine(Mathf.Lerp(660f, 420f, t / 0.1f), t) * 0.5f * Env(t, 0.11f, 0.004f, 0.08f));
            clips[Sfx.Step]      = Make("step", 0.07f, (t, i) => (Sine(110f, t) * 0.6f + Noise() * 0.15f) * Env(t, 0.07f, 0.003f, 0.06f));
            clips[Sfx.Hit]       = MakeFiltered("hit", 0.09f, 0.55f, (t, i) => (Noise() * 0.7f + Sine(300f - t * 1200f, t) * 0.5f) * Env(t, 0.09f, 0.002f, 0.08f));
            clips[Sfx.Death]     = Make("death", 0.28f, (t, i) => Square(Mathf.Lerp(300f, 80f, t / 0.28f), t) * 0.25f * Env(t, 0.28f, 0.005f, 0.2f));
            clips[Sfx.Arrow]     = MakeFiltered("arrow", 0.16f, 0.25f, (t, i) => Noise() * 0.5f * Env(t, 0.16f, 0.03f, 0.12f));
            clips[Sfx.Skill]     = Make("skill", 0.4f, (t, i) => (Sine(Mathf.Lerp(200f, 1200f, t / 0.4f), t) * 0.4f + Noise() * 0.18f) * Env(t, 0.4f, 0.01f, 0.25f));
            clips[Sfx.Horn]      = Make("horn", 1.1f, (t, i) => Saw(t < 0.45f ? 220f : 330f, t) * 0.22f * Env(t, 1.1f, 0.05f, 0.4f));
            clips[Sfx.Victory]   = Make("victory", 0.6f, (t, i) => Triad(t, 523.25f, 659.25f, 783.99f, 0.12f) * 0.45f);
            clips[Sfx.Defeat]    = Make("defeat", 0.75f, (t, i) => Triad(t, 392f, 311.13f, 261.63f, 0.18f) * 0.45f);
            clips[Sfx.Turn]      = Make("turn", 0.35f, (t, i) => (Sine(587.33f, t) * Env(t, 0.18f, 0.005f, 0.15f) + Sine(880f, t - 0.1f) * Env(t - 0.1f, 0.25f, 0.005f, 0.2f)) * 0.4f);
            clips[Sfx.Flank]     = Make("flank", 0.3f, (t, i) => (Square(740f, t) * 0.2f + Square(988f, t - 0.08f) * 0.2f * (t > 0.08f ? 1f : 0f)) * Env(t, 0.3f, 0.005f, 0.2f));

            ambientBed  = MakeLoop("ambient_wind", 6f, BrownNoise(6f, 0.025f));
            tacticMusic = MakeLoop("music_tactic", 8f, TacticPad(8f));
            warMusic    = MakeLoop("music_war", 4f, WarDrums(4f));
        }

        static float Sine(float f, float t) => t < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float f, float t) => t < 0f ? 0f : Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.7f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f;
        static float Saw(float f, float t) { if (t < 0f) return 0f; float p = f * t; return 2f * (p - Mathf.Floor(p + 0.5f)); }
        static float Noise() => Random.value * 2f - 1f;

        // Attack / release envelope; every clip gets at least a 5 ms fade so edges never click.
        static float Env(float t, float len, float attack, float release)
        {
            if (t < 0f || t > len) return 0f;
            attack = Mathf.Max(attack, 0.005f);
            release = Mathf.Max(release, 0.005f);
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((len - t) / release);
            return Mathf.Min(a, r) * Mathf.Min(a, r);
        }

        static float Triad(float t, float f1, float f2, float f3, float step)
        {
            float v = 0f;
            v += Sine(f1, t) * Env(t, step * 2.2f, 0.005f, step);
            v += Sine(f2, t - step) * Env(t - step, step * 2.2f, 0.005f, step);
            v += (Sine(f3, t - step * 2f) * 0.8f + Sine(f3 * 2f, t - step * 2f) * 0.2f) * Env(t - step * 2f, step * 3f, 0.005f, step * 2f);
            return v * 0.6f;
        }

        AudioClip Make(string name, float len, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(len * SR);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)SR, i), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        // One-pole low-pass, alpha in (0,1]: smaller = darker.
        AudioClip MakeFiltered(string name, float len, float alpha, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(len * SR);
            var data = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                y += alpha * (f(i / (float)SR, i) - y);
                data[i] = Mathf.Clamp(y * 1.4f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip MakeLoop(string name, float len, float[] data)
        {
            // crossfade the tail into the head so the loop point is seamless
            int fade = SR / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[data.Length - fade + i] * (1f - k);
            }
            System.Array.Resize(ref data, data.Length - fade);
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] BrownNoise(float len, float gain)
        {
            int n = Mathf.CeilToInt(len * SR);
            var d = new float[n];
            float b = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                b = Mathf.Clamp(b + Noise() * 0.02f, -1f, 1f);
                lp += 0.02f * (b - lp);
                float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * (i / (float)SR) / len);
                d[i] = lp * swell * gain * 30f;
            }
            return d;
        }

        // Slow two-chord pad (A minor -> F major), soft sines with a gentle swell.
        static float[] TacticPad(float len)
        {
            int n = Mathf.CeilToInt(len * SR);
            var d = new float[n];
            float[] chordA = { 220f, 261.63f, 329.63f, 440f };
            float[] chordB = { 174.61f, 220f, 261.63f, 349.23f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float half = len / 2f;
                float local = t % half;
                var chord = t < half ? chordA : chordB;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(local / half));
                float v = 0f;
                for (int c = 0; c < chord.Length; c++)
                    v += Mathf.Sin(2f * Mathf.PI * chord[c] * t + c) * (c == 0 ? 0.35f : 0.2f);
                // plucked melody note every beat for some motion
                float beat = 0.5f;
                float bt = t % beat;
                int bi = Mathf.FloorToInt(t / beat) % 8;
                float[] mel = { 659.25f, 587.33f, 523.25f, 440f, 523.25f, 587.33f, 523.25f, 493.88f };
                v += Mathf.Sin(2f * Mathf.PI * mel[bi] * bt) * Mathf.Exp(-bt * 9f) * 0.18f;
                d[i] = v * 0.25f * (0.55f + 0.45f * env);
            }
            return d;
        }

        // War drums: kick on the beat, toms on the off-beat, a driving bass pulse. 120 bpm.
        static float[] WarDrums(float len)
        {
            int n = Mathf.CeilToInt(len * SR);
            var d = new float[n];
            float beat = 0.5f;
            float[] bass = { 110f, 110f, 130.81f, 98f, 110f, 110f, 146.83f, 130.81f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float bt = t % beat;
                int bi = Mathf.FloorToInt(t / beat) % 8;
                float kick = Mathf.Sin(2f * Mathf.PI * (55f + 90f * Mathf.Exp(-bt * 30f)) * bt) * Mathf.Exp(-bt * 7f);
                float ot = (t + beat / 2f) % beat;
                float tom = Mathf.Sin(2f * Mathf.PI * 150f * ot) * Mathf.Exp(-ot * 14f) * (bi % 2 == 1 ? 0.6f : 0.35f);
                float hat = Noise() * Mathf.Exp(-((t % (beat / 2f)) * 60f)) * 0.12f;
                float bassV = Saw(bass[bi], t) * 0.18f * Mathf.Exp(-bt * 2.5f);
                d[i] = (kick * 0.7f + tom * 0.5f + hat + bassV) * 0.5f;
            }
            return d;
        }
    }
}
