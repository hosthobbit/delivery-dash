using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Every sound in the game, synthesised at start-up (original, no audio files): effects, a
    /// freewheel that follows the bike's speed, morning ambience and a cheerful music loop.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public enum Kind { Throw, Land, Deliver, Perfect, Miss, Glass, Crash, Bump, Bell, Bark, Beep, Oi, Click, Count, Go, Whistle, Combo }

        public static Sfx Instance { get; private set; }
        const int Rate = 44100;

        [Range(0, 1)] public float SoundVolume = 0.8f;
        [Range(0, 1)] public float MusicVolume = 0.55f;
        public BikeController Bike;

        readonly Dictionary<Kind, AudioClip> clips = new Dictionary<Kind, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music, ambience, freewheel;
        System.Random rng = new System.Random(11);

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < 16; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                pool.Add(s);
            }
            clips[Kind.Throw] = Make(0.35f, Whoosh);
            clips[Kind.Land] = Make(0.25f, Thud);
            clips[Kind.Deliver] = Make(0.6f, d => Chime(d, new[] { 784f, 1046f }));
            clips[Kind.Perfect] = Make(0.9f, d => Chime(d, new[] { 784f, 988f, 1175f, 1568f }));
            clips[Kind.Combo] = Make(0.5f, d => Chime(d, new[] { 1046f, 1318f, 1568f }));
            clips[Kind.Miss] = Make(0.6f, Womp);
            clips[Kind.Glass] = Make(0.9f, Glass);
            clips[Kind.Crash] = Make(0.9f, Crash);
            clips[Kind.Bump] = Make(0.3f, d => Thump(d, 90f));
            clips[Kind.Bell] = Make(1.0f, Bell);
            clips[Kind.Bark] = Make(0.45f, Bark);
            clips[Kind.Beep] = Make(1.2f, Beep);
            clips[Kind.Oi] = Make(0.4f, Oi);
            clips[Kind.Click] = Make(0.06f, d => Tone(d, 1400f, 0.25f, 60f));
            clips[Kind.Count] = Make(0.2f, d => Tone(d, 660f, 0.35f, 12f));
            clips[Kind.Go] = Make(0.45f, d => { Tone(d, 990f, 0.35f, 5f); });
            clips[Kind.Whistle] = Make(1.2f, Whistle);

            music = Loop(Make(16f, Music), MusicVolume);
            ambience = Loop(Make(12f, Ambience), 0.35f);
            freewheel = Loop(Make(1.0f, Freewheel), 0f);
        }

        AudioSource Loop(AudioClip c, float vol)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.volume = vol; s.playOnAwake = false;
            s.Play();
            return s;
        }

        void Update()
        {
            if (music) music.volume = MusicVolume;
            if (ambience) ambience.volume = 0.32f * SoundVolume;
            if (freewheel)
            {
                float sp = Bike && Bike.Current != BikeController.State.Frozen ? Bike.Speed : 0f;
                freewheel.volume = Mathf.Clamp01(sp / 8f) * 0.22f * SoundVolume;
                freewheel.pitch = 0.6f + sp * 0.09f;
            }
        }

        public static void Play(Kind k, float volume = 1f, float pitch = 1f)
        {
            var me = Instance;
            if (!me || !me.clips.TryGetValue(k, out var clip)) return;
            foreach (var s in me.pool)
            {
                if (s.isPlaying) continue;
                s.clip = clip;
                s.volume = volume * me.SoundVolume;
                s.pitch = pitch;
                s.Play();
                return;
            }
        }

        // ------------------------------------------------------------------ synthesis

        AudioClip Make(float seconds, Action<float[]> synth)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            synth(data);
            var c = AudioClip.Create("sfx", data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) * decay);

        void Tone(float[] d, float f, float vol, float decay)
        {
            for (int i = 0; i < d.Length; i++) { float t = (float)i / Rate; d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * vol * Env(t, 0.005f, decay); }
        }

        void Whoosh(float[] d)
        {
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate, k = t / 0.35f;
                float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(k * Mathf.PI));
                lp += (Noise() - lp) * cutoff;
                d[i] = lp * Mathf.Sin(k * Mathf.PI) * 0.7f;
            }
        }

        void Thud(float[] d)
        {
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise() - lp) * 0.12f;
                d[i] = (lp * 0.9f + Mathf.Sin(2 * Mathf.PI * 140 * t) * 0.5f) * Env(t, 0.002f, 30f);
            }
        }

        void Thump(float[] d, float f)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = (Mathf.Sin(2 * Mathf.PI * f * (1 - t) * t * 6) * 0.8f + Noise() * 0.25f) * Env(t, 0.003f, 16f);
            }
        }

        void Chime(float[] d, float[] notes)
        {
            float step = 0.075f;
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float e = Env(t, 0.004f, 6f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * notes[n] * t) * 0.32f + Mathf.Sin(4 * Mathf.PI * notes[n] * t) * 0.08f) * e;
                }
            }
        }

        void Womp(float[] d)
        {
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = t < 0.25f ? 330f : 220f * (1 - (t - 0.25f) * 0.4f);
                ph += 2 * Mathf.PI * f / Rate;
                d[i] = (Mathf.Sign(Mathf.Sin(ph)) * 0.12f + Mathf.Sin(ph) * 0.2f) * (t < 0.25f ? Env(t, 0.01f, 8f) : Env(t - 0.25f, 0.01f, 5f));
            }
        }

        void Glass(float[] d)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float hp = Noise() * Env(t, 0.001f, 14f) * 0.6f;
                d[i] = hp;
            }
            // tinkles
            for (int k = 0; k < 9; k++)
            {
                float start = 0.05f + (float)rng.NextDouble() * 0.6f, f = 2500 + (float)rng.NextDouble() * 3500;
                int s0 = (int)(start * Rate);
                for (int i = s0; i < d.Length; i++) { float t = (float)(i - s0) / Rate; d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * 0.12f * Env(t, 0.001f, 30f); }
            }
        }

        void Crash(float[] d)
        {
            Thump(d, 70f);
            for (int k = 0; k < 6; k++)
            {
                float start = 0.08f + k * 0.07f, f = 600 + (float)rng.NextDouble() * 900;
                int s0 = (int)(start * Rate);
                for (int i = s0; i < d.Length; i++) { float t = (float)(i - s0) / Rate; d[i] += (Mathf.Sin(2 * Mathf.PI * f * t) * 0.15f + Noise() * 0.1f) * Env(t, 0.002f, 25f); }
            }
        }

        void Bell(float[] d)
        {
            // a classic bicycle "ring-ring"
            for (int r = 0; r < 2; r++)
            {
                int s0 = (int)(r * 0.16f * Rate);
                for (int i = s0; i < d.Length; i++)
                {
                    float t = (float)(i - s0) / Rate;
                    float e = Env(t, 0.002f, 5f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * 2350 * t) * 0.25f + Mathf.Sin(2 * Mathf.PI * 3125 * t) * 0.12f + Mathf.Sin(2 * Mathf.PI * 5890 * t) * 0.05f) * e;
                }
            }
        }

        void Bark(float[] d)
        {
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 420f + 260f * Mathf.Sin(Mathf.Min(1f, t / 0.12f) * Mathf.PI);
                ph += 2 * Mathf.PI * f / Rate;
                float tone = Mathf.Sin(ph) + 0.5f * Mathf.Sin(ph * 2.02f) + 0.3f * Noise();
                d[i] = tone * 0.35f * Env(t, 0.01f, 9f);
            }
        }

        void Beep(float[] d)
        {
            for (int b = 0; b < 3; b++)
            {
                int s0 = (int)(b * 0.4f * Rate);
                for (int i = s0; i < Mathf.Min(d.Length, s0 + (int)(0.2f * Rate)); i++)
                {
                    float t = (float)(i - s0) / Rate;
                    d[i] += Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 1250 * t)) * 0.08f * Mathf.Min(1f, t * 200f);
                }
            }
        }

        void Oi(float[] d)
        {
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 260f - t * 120f;
                ph += 2 * Mathf.PI * f / Rate;
                // a gruff "oi!" from a couple of formants
                float v = Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 3f) * 0.25f + Mathf.Sin(ph * 7.5f) * 0.12f;
                d[i] = v * 0.4f * Env(t, 0.02f, 7f);
            }
        }

        void Whistle(float[] d)
        {
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 1800f + 250f * Mathf.Sin(t * 40f);
                ph += 2 * Mathf.PI * f / Rate;
                d[i] = Mathf.Sin(ph) * 0.25f * Env(t, 0.02f, 2.5f);
            }
        }

        void Freewheel(float[] d)
        {
            // ratchet clicks
            for (int k = 0; k < 24; k++)
            {
                int s0 = (int)(k / 24f * Rate);
                for (int i = s0; i < Mathf.Min(d.Length, s0 + 500); i++) { float t = (float)(i - s0) / Rate; d[i] += Noise() * 0.5f * Mathf.Exp(-t * 900f); }
            }
        }

        void Ambience(float[] d)
        {
            // soft distant traffic + birdsong
            float lp = 0;
            for (int i = 0; i < d.Length; i++) { lp += (Noise() - lp) * 0.01f; d[i] = lp * 0.35f; }
            for (int k = 0; k < 18; k++)
            {
                float start = (float)rng.NextDouble() * 11f, f0 = 2800 + (float)rng.NextDouble() * 1800;
                int chirps = 2 + rng.Next(4);
                for (int c = 0; c < chirps; c++)
                {
                    int s0 = (int)((start + c * 0.11f) * Rate);
                    for (int i = s0; i < Mathf.Min(d.Length, s0 + (int)(0.08f * Rate)); i++)
                    {
                        float t = (float)(i - s0) / Rate;
                        float f = f0 + 900f * Mathf.Sin(t * 60f);
                        d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * 0.06f * Mathf.Sin(t / 0.08f * Mathf.PI);
                    }
                }
            }
        }

        void Music(float[] d)
        {
            // original, bouncy 8-bar loop in C major: plucked melody (Karplus-Strong), bass, brushy hats
            const float bpm = 120f, beat = 60f / bpm;
            int[] chords = { 0, 9, 5, 7, 0, 9, 2, 7 }; // C Am F G C Am Dm G (semitones from C)
            int[] melody = { 7, 9, 12, 9, 7, 4, 2, 4, 5, 9, 12, 14, 12, 11, 7, -1, 7, 9, 12, 14, 16, 14, 12, 9, 9, 7, 5, 4, 2, 4, 7, -1 };
            float C4 = 261.63f;
            for (int bar = 0; bar < 8; bar++)
            {
                int root = chords[bar];
                bool minor = root == 9 || root == 2;
                for (int b = 0; b < 4; b++)
                {
                    float t0 = (bar * 4 + b) * beat;
                    // bass on 1 and 3, fifth on 2 and 4
                    float bf = C4 / 4f * Mathf.Pow(2, (root + (b % 2 == 1 ? 7 : 0)) / 12f);
                    Pluck(d, t0, bf, 0.32f, 0.996f);
                    // chord stab on the off-beat
                    foreach (int iv in new[] { 0, minor ? 3 : 4, 7 }) Pluck(d, t0 + beat * 0.5f, C4 * Mathf.Pow(2, (root + iv) / 12f), 0.07f, 0.985f);
                    // hat
                    Hat(d, t0 + beat * 0.5f, 0.05f);
                }
                for (int n = 0; n < 4; n++)
                {
                    int note = melody[(bar * 4 + n) % melody.Length];
                    if (note < 0) continue;
                    Pluck(d, (bar * 4 + n) * beat, C4 * 2f * Mathf.Pow(2, note / 12f), 0.16f, 0.993f);
                }
            }
            float peak = 0.001f;
            foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] *= 0.6f / peak;
        }

        void Pluck(float[] d, float start, float f, float vol, float decay)
        {
            int n = Mathf.Max(2, (int)(Rate / f));
            var buf = new float[n];
            for (int i = 0; i < n; i++) buf[i] = Noise();
            int s0 = (int)(start * Rate);
            int len = Mathf.Min(d.Length - s0, (int)(1.2f * Rate));
            int idx = 0;
            for (int i = 0; i < len; i++)
            {
                float v = buf[idx];
                int next = (idx + 1) % n;
                buf[idx] = (v + buf[next]) * 0.5f * decay;
                idx = next;
                d[s0 + i] += v * vol;
            }
        }

        void Hat(float[] d, float start, float vol)
        {
            int s0 = (int)(start * Rate);
            float prev = 0;
            for (int i = 0; i < Mathf.Min(d.Length - s0, 3000); i++)
            {
                float t = (float)i / Rate, nz = Noise();
                d[s0 + i] += (nz - prev) * vol * Mathf.Exp(-t * 60f);
                prev = nz;
            }
        }
    }
}
