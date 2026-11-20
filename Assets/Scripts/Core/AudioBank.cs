using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    public enum Sfx { UiMove, UiConfirm, UiCancel, Step, Hit, Death, Arrow, Skill, Horn, Victory, Defeat, Flank, Stamp }

    // Sound effects are synthesized at startup; the two music tracks are CC0 recordings under
    // Resources/Music (see LICENSE.txt there). Volumes run through a static gain layer (Music / Sfx)
    // because an AudioMixer can't be authored headlessly; the settings sliders bind to these floats.
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank I;

        public static float MusicVolume = 0.6f;
        public static float SfxVolume = 0.85f;

        public const string MusicDir = "Music";

        const int SR = 22050;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        readonly List<AudioSource> sfxSources = new List<AudioSource>();
        int nextSource;

        // One source per track: the one you leave fades out and pauses, so each piece picks up
        // where it was. Battles are short, so the battle theme runs on across them.
        sealed class Track { public AudioSource src; public float gain, startAt, weight; public bool started; }
        Track mapTrack, battleTrack, current;
        Track[] tracks;
        const float CrossfadeSeconds = 0.6f;
        const float BattleStart = 4f;   // past the theme's quiet opening swell

        AudioSource ambientSource;
        AudioClip ambientBed;
        float ambientBase = 0.18f;
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
            mapTrack = MakeTrack("map_forest_exploration", 0.45f);
            battleTrack = MakeTrack("battle_theme", 0.55f);
            battleTrack.startAt = BattleStart;
            tracks = new[] { mapTrack, battleTrack };
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true; ambientSource.playOnAwake = false;

            Build();
            ambientSource.clip = ambientBed;
            ambientSource.Play();
            PlayTacticMusic();
        }

        Track MakeTrack(string clip, float gain)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.volume = 0f;
            s.clip = Resources.Load<AudioClip>(MusicDir + "/" + clip);
            return new Track { src = s, gain = gain };
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / CrossfadeSeconds;
            foreach (var t in tracks)
            {
                t.weight = Mathf.MoveTowards(t.weight, t == current ? 1f : 0f, step);
                t.src.volume = t.gain * t.weight * musicFade * MusicVolume;
                if (t.weight == 0f && t.src.isPlaying) t.src.Pause();
            }
            ambientSource.volume = ambientBase * MusicVolume;
        }

        public void PlayTacticMusic() => SwitchTo(mapTrack);
        public void PlayWarMusic() => SwitchTo(battleTrack);

        void SwitchTo(Track t)
        {
            current = t;
            if (t.src.clip == null) return;
            if (t.started) { t.src.UnPause(); return; }
            t.src.Play();
            t.src.time = t.startAt;   // after Play: a streamed clip can drop a seek made before it
            t.started = true;
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
            clips[Sfx.Horn]      = MakeFiltered("horn", 1.1f, 0.35f, (t, i) => HornNote(t, 233.08f, 0f, 0.46f) + HornNote(t, 349.23f, 0.4f, 0.68f));
            clips[Sfx.Victory]   = Make("victory", 0.6f, (t, i) => Triad(t, 523.25f, 659.25f, 783.99f, 0.12f) * 0.45f);
            clips[Sfx.Defeat]    = Make("defeat", 0.75f, (t, i) => Triad(t, 392f, 311.13f, 261.63f, 0.18f) * 0.45f);
            clips[Sfx.Stamp]     = MakeFiltered("stamp", 0.12f, 0.5f, (t, i) => (Sine(Mathf.Lerp(120f, 60f, t / 0.12f), t) * 0.8f + Noise() * 0.5f * Mathf.Exp(-t * 70f)) * Env(t, 0.12f, 0.002f, 0.09f));
            clips[Sfx.Flank]     = Make("flank", 0.3f, (t, i) => (Square(740f, t) * 0.2f + Square(988f, t - 0.08f) * 0.2f * (t > 0.08f ? 1f : 0f)) * Env(t, 0.3f, 0.005f, 0.2f));

            ambientBed  = MakeLoop("ambient_wind", 6f, BrownNoise(6f, 0.025f));
        }

        static float Sine(float f, float t) => t < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float f, float t) => t < 0f ? 0f : Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.7f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f;
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

        // One call of a field horn: brassy harmonics swelling in, a slow vibrato.
        static float HornNote(float t, float f, float start, float len)
        {
            float local = t - start;
            if (local < 0f || local > len) return 0f;
            float vib = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * local);
            float v = 0f;
            for (int h = 1; h <= 7; h++) v += Mathf.Sin(2f * Mathf.PI * f * h * vib * local) / Mathf.Pow(h, 1.25f);
            return v * 0.16f * Env(local, len, 0.06f, 0.24f);
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
    }
}
