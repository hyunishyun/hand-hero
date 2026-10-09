using System;
using System.Diagnostics;
using HandHero.Core;
using UnityEngine;

// Sound effects (P11, D14): every SfxId is synthesized once at scene load from
// SfxRecipes into an AudioClip (never mid-fight) and played through a small pool
// of AudioSources: 3D at the hero/bot for world events, 2D for UI. A real
// recording dropped into an event's slot replaces the synthesized sound.
public class SfxPlayer : MonoBehaviour
{
    [Serializable]
    public struct Slot
    {
        [Tooltip("The game moment this slot belongs to")]
        public SfxId id;
        [Tooltip("Optional recording; empty = the synthesized recipe (SfxRecipes)")]
        public AudioClip clip;
        [Tooltip("Volume of this event (x master volume)")]
        [Range(0f, 1f)] public float volume;
    }

    [Header("Volume")]
    [Tooltip("Overall effects volume")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;
    [Tooltip("One slot per event: per-event volume and an optional recording that overrides the synth")]
    [SerializeField] private Slot[] slots = DefaultSlots();

    [Header("Playback")]
    [Tooltip("AudioSources in the pool; when all are busy the oldest is cut")]
    [SerializeField] private int voices = 12;
    [Tooltip("3D share of world sounds (0 = flat stereo, 1 = fully positional)")]
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 0.6f;
    [Tooltip("World sounds are at full volume within this distance (meters)")]
    [SerializeField] private float minDistance = 8f;
    [Tooltip("World sounds fade out linearly up to this distance (meters)")]
    [SerializeField] private float maxDistance = 200f;
    [Tooltip("The same event repeated within this many seconds is dropped (no stacked copies)")]
    [SerializeField] private float repeatInterval = 0.04f;

    // The player of the loaded scene (null when there is none).
    public static SfxPlayer Active { get; private set; }
    // Startup cost of the synthesized bank, for the perf log header (-1 = not built yet).
    public static float GenerationMs { get; private set; } = -1f;
    public static long GeneratedBytes { get; private set; }

    private AudioClip[] _clips;
    private AudioClip[] _generated;
    private float[] _volumes;
    private AudioSource[] _sources;
    private int _next;
    private SfxRateLimiter _limiter;

    // World event at a position (shots, hits on bots, shockwave, telegraph).
    public static void Play(SfxId id, Vector3 position, float pitch = 1f)
    {
        if (Active != null) Active.PlayInternal(id, position, true, pitch);
    }

    // Flat UI / feedback sound (menus, countdown, hits taken, results).
    public static void PlayUi(SfxId id, float pitch = 1f)
    {
        if (Active != null) Active.PlayInternal(id, default, false, pitch);
    }

    private void Awake()
    {
        _limiter = new SfxRateLimiter(repeatInterval);
        BuildClips();
        BuildSources();
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void OnDestroy()
    {
        if (_generated == null) return;
        foreach (AudioClip clip in _generated)
            if (clip != null) Destroy(clip);
    }

    private void BuildClips()
    {
        int count = Enum.GetValues(typeof(SfxId)).Length;
        _clips = new AudioClip[count];
        _generated = new AudioClip[count];
        _volumes = new float[count];
        for (int i = 0; i < count; i++) _volumes[i] = 1f;

        if (slots != null)
            foreach (Slot slot in slots)
            {
                int i = (int)slot.id;
                if (i <= 0 || i >= count) continue;
                _volumes[i] = slot.volume;
                if (slot.clip != null) _clips[i] = slot.clip;
            }

        var watch = Stopwatch.StartNew();
        long bytes = 0;
        int rate = SfxSynth.DefaultSampleRate;
        for (int i = 1; i < count; i++)
        {
            if (_clips[i] != null) continue;
            SfxRecipe recipe = SfxRecipes.Get((SfxId)i);
            if (recipe.Duration <= 0f) continue;
            var samples = new float[SfxSynth.SampleCount(recipe, rate)];
            SfxSynth.Render(recipe, rate, samples);
            AudioClip clip = AudioClip.Create("sfx_" + (SfxId)i, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            _clips[i] = clip;
            _generated[i] = clip;
            bytes += samples.Length * sizeof(float);
        }
        GenerationMs = (float)watch.Elapsed.TotalMilliseconds;
        GeneratedBytes = bytes;
    }

    private void BuildSources()
    {
        int n = Mathf.Max(1, voices);
        _sources = new AudioSource[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            _sources[i] = source;
        }
    }

    private void PlayInternal(SfxId id, Vector3 position, bool world, float pitch)
    {
        int i = (int)id;
        if (_clips == null || i <= 0 || i >= _clips.Length || _clips[i] == null) return;
        if (!_limiter.Allow(id, Time.unscaledTime)) return;

        AudioSource source = NextSource();
        source.transform.position = world ? position : transform.position;
        source.spatialBlend = world ? spatialBlend : 0f;
        source.pitch = pitch;
        source.volume = masterVolume * _volumes[i];
        source.clip = _clips[i];
        source.Play();
    }

    // A free voice if there is one, else the oldest (round robin).
    private AudioSource NextSource()
    {
        for (int k = 0; k < _sources.Length; k++)
        {
            int j = (_next + k) % _sources.Length;
            if (_sources[j].isPlaying) continue;
            _next = (j + 1) % _sources.Length;
            return _sources[j];
        }
        AudioSource oldest = _sources[_next];
        _next = (_next + 1) % _sources.Length;
        return oldest;
    }

    // One slot per event at full volume, so the inspector lists every event.
    private static Slot[] DefaultSlots()
    {
        var ids = (SfxId[])Enum.GetValues(typeof(SfxId));
        var result = new Slot[ids.Length - 1];
        int n = 0;
        foreach (SfxId id in ids)
            if (id != SfxId.None) result[n++] = new Slot { id = id, volume = 1f };
        return result;
    }
}
