using System;
using UnityEngine;

namespace HandHero.Core
{
    // Every game moment that makes a sound (D14). None = no sound.
    public enum SfxId
    {
        None,
        BeamFire,
        EnemyFire,
        ChargeStart,
        ChargeReady,
        ChargeRelease,
        BotTelegraph,
        HitDealt,
        HitTaken,
        Shockwave,
        BotDown,
        CountdownTick,
        Fight,
        IslandCleared,
        ChestOpen,
        ItemPick,
        ShopBuy,
        Reroll,
        Denied,
        PortalPick,
        Victory,
        Defeat,
        MenuPoint,
        MenuPress,
        Pause,
        Resume,
    }

    public enum SfxWave
    {
        Sine,
        Triangle,
        Square,
        Saw,
        Noise,
    }

    // One synthesized sound: an oscillator with an exponential pitch sweep, optional
    // noise and vibrato, an ADSR envelope (per note when Notes is set) and a one-pole
    // low-pass that keeps square and saw waves soft.
    public struct SfxRecipe
    {
        public SfxWave Wave;
        public float StartHz;
        public float EndHz;
        // Seconds. With Notes, each note gets Duration / Notes.Length.
        public float Duration;
        public float Attack;
        public float Decay;
        // Level held after the decay (0..1); 0 = attack/decay only.
        public float Sustain;
        // Fade-out seconds at the end of each note.
        public float Release;
        // Share of white noise mixed into the oscillator (0..1).
        public float NoiseMix;
        // Low-pass cutoff in Hz; 0 = SfxSynth.DefaultLowPassHz.
        public float LowPassHz;
        public float VibratoHz;
        // Vibrato depth as a share of the frequency.
        public float VibratoDepth;
        // Semitone offsets played one after another; null = one note.
        public float[] Notes;
        // Peak level after rendering (capped at SfxSynth.MaxPeak).
        public float Volume;
    }

    // Renders SfxRecipes into PCM once at startup (never mid-fight). Pure math,
    // deterministic (seeded noise), so the shape of every sound is testable.
    public static class SfxSynth
    {
        public const int DefaultSampleRate = 22050;
        public const float MaxPeak = 0.9f;
        public const float DefaultLowPassHz = 6000f;
        // The whole generated bank stays under this (float samples).
        public const long BudgetBytes = 2L * 1024 * 1024;

        private const float MinAttack = 0.002f;
        private const float MinRelease = 0.006f;

        public static int SampleCount(in SfxRecipe r, int sampleRate)
        {
            return Mathf.Max(1, Mathf.RoundToInt(r.Duration * sampleRate));
        }

        public static void Render(in SfxRecipe r, int sampleRate, float[] buffer, uint seed = 0x9E3779B9u)
        {
            int count = SampleCount(r, sampleRate);
            if (buffer == null || buffer.Length < count)
                throw new ArgumentException("buffer shorter than SampleCount", nameof(buffer));

            int notes = r.Notes != null && r.Notes.Length > 0 ? r.Notes.Length : 1;
            float noteLength = r.Duration / notes;
            float start = Mathf.Max(1f, r.StartHz);
            float end = Mathf.Max(1f, r.EndHz);
            float cutoff = r.LowPassHz > 0f ? r.LowPassHz : DefaultLowPassHz;
            float lowPass = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            uint noise = seed == 0 ? 1u : seed;

            double phase = 0.0;
            float filtered = 0f;
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                int note = Mathf.Min(notes - 1, (int)(t / noteLength));
                float noteTime = t - note * noteLength;

                // Exponential sweep over the whole sound, transposed per note.
                float hz = start * Mathf.Pow(end / start, r.Duration > 0f ? t / r.Duration : 0f);
                if (r.Notes != null && r.Notes.Length > 0) hz *= Mathf.Pow(2f, r.Notes[note] / 12f);
                if (r.VibratoHz > 0f) hz *= 1f + r.VibratoDepth * Mathf.Sin(2f * Mathf.PI * r.VibratoHz * t);
                phase += hz / sampleRate;
                phase -= Math.Floor(phase);

                noise ^= noise << 13;
                noise ^= noise >> 17;
                noise ^= noise << 5;
                float white = noise / (float)uint.MaxValue * 2f - 1f;

                float osc = r.Wave == SfxWave.Noise ? white : Oscillator(r.Wave, (float)phase);
                float x = osc * (1f - r.NoiseMix) + white * r.NoiseMix;
                filtered += lowPass * (x - filtered);

                float s = filtered * Envelope(r, noteTime, noteLength);
                buffer[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            float target = Mathf.Min(Mathf.Max(0f, r.Volume), MaxPeak);
            float gain = peak > 0f ? target / peak : 0f;
            for (int i = 0; i < count; i++) buffer[i] *= gain;
        }

        private static float Oscillator(SfxWave wave, float p)
        {
            switch (wave)
            {
                case SfxWave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case SfxWave.Square: return p < 0.5f ? 1f : -1f;
                case SfxWave.Saw: return 2f * p - 1f;
                default: return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        // Attack -> decay to Sustain -> hold, multiplied by a fade to zero over the
        // last Release seconds, so every note starts and ends at silence (no clicks).
        private static float Envelope(in SfxRecipe r, float t, float length)
        {
            float attack = Mathf.Max(MinAttack, r.Attack);
            float level;
            if (t < attack) level = t / attack;
            else if (r.Decay > 0f && t < attack + r.Decay) level = Mathf.Lerp(1f, r.Sustain, (t - attack) / r.Decay);
            else level = r.Decay > 0f ? r.Sustain : 1f;

            float release = Mathf.Max(MinRelease, r.Release);
            float fade = Mathf.Clamp01((length - t) / release);
            return level * fade;
        }
    }
}
