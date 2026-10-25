using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>Mixer bus of a sound (Phase 6 audio spec §0.2). One static volume per bus, no AudioMixer.</summary>
    public enum AudioBus
    {
        /// <summary>Weapons, impacts, enemy tells, gameplay cues. Stays after contract 2.</summary>
        Sfx,
        /// <summary>Defne's sounds. Muted by contract 2 (K-8).</summary>
        Voice,
        /// <summary>Devil screen, cards, clicks, endings, module cues.</summary>
        Ui,
        /// <summary>Music stem A (always plays).</summary>
        MusicBase,
        /// <summary>Music stem B (faded out by contract 2).</summary>
        MusicMelody,
    }

    /// <summary>One named sound event: clips (round-robin) plus its runtime mix rules (spec §1 table).</summary>
    [Serializable]
    public sealed class SoundEvent
    {
        [Tooltip("Event name used by GameAudio.Play (e.g. rifle_shot).")]
        [SerializeField] private string _name = "";
        [Tooltip("Clips; more than one = round-robin.")]
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [Tooltip("Runtime volume in dB (0 = clip as rendered, which is normalised to -1 dBFS).")]
        [SerializeField] private float _volumeDb = -12f;
        [Tooltip("Most simultaneous voices; when full the oldest is stolen.")]
        [SerializeField, Min(1)] private int _maxVoices = 1;
        [Tooltip("Minimum seconds between two starts; requests inside the gap are dropped.")]
        [SerializeField, Min(0f)] private float _minGap;
        [Tooltip("Random pitch per play, as a fraction (0.05 = ±5%).")]
        [SerializeField, Range(0f, 0.5f)] private float _pitchJitter;
        [Tooltip("Random volume per play, ± dB.")]
        [SerializeField, Min(0f)] private float _volumeJitterDb;
        [Tooltip("Bus (Voice is muted by contract 2).")]
        [SerializeField] private AudioBus _bus = AudioBus.Sfx;
        [Tooltip("Triggers and cooldowns on unscaled time; the source ignores listener pause (Devil screen).")]
        [SerializeField] private bool _unscaled;
        [Tooltip("Pan by screen x (enemy events, spec §0.3).")]
        [SerializeField] private bool _pan;

        public SoundEvent() { }

        public SoundEvent(string name, AudioClip[] clips, float volumeDb, int maxVoices, float minGap, float pitchJitter,
            float volumeJitterDb, AudioBus bus, bool unscaled, bool pan)
        {
            _name = name;
            _clips = clips ?? Array.Empty<AudioClip>();
            _volumeDb = volumeDb;
            _maxVoices = Mathf.Max(1, maxVoices);
            _minGap = Mathf.Max(0f, minGap);
            _pitchJitter = Mathf.Clamp(pitchJitter, 0f, 0.5f);
            _volumeJitterDb = Mathf.Max(0f, volumeJitterDb);
            _bus = bus;
            _unscaled = unscaled;
            _pan = pan;
        }

        public string Name => _name;
        public AudioClip[] Clips => _clips;
        public float VolumeDb => _volumeDb;
        public int MaxVoices => Mathf.Max(1, _maxVoices);
        public float MinGap => _minGap;
        public float PitchJitter => _pitchJitter;
        public float VolumeJitterDb => _volumeJitterDb;
        public AudioBus Bus => _bus;
        public bool Unscaled => _unscaled;
        public bool Pan => _pan;
    }

    /// <summary>
    /// Every sound event and the two music stems (Phase 6 procedural audio spec). Baked by Dance/Audio/Bake All into
    /// Assets/_Game/Audio/SoundBank.asset; tests build one in memory with <see cref="Set"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dance/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [SerializeField] private SoundEvent[] _events = Array.Empty<SoundEvent>();
        [Tooltip("Music stem A (bass, drone, heartbeat). Loops.")]
        [SerializeField] private AudioClip _musicBase;
        [Tooltip("Music stem B (melody). Same length as stem A, started in sync. Muted by contract 2.")]
        [SerializeField] private AudioClip _musicMelody;
        [Tooltip("Stem A runtime volume, dB (spec: -14).")]
        [SerializeField] private float _musicBaseDb = -14f;
        [Tooltip("Stem B runtime volume, dB (spec: -17).")]
        [SerializeField] private float _musicMelodyDb = -17f;

        [Tooltip("The game's music as one track (user decision 2026-10-25, K-8). When set it plays instead of the two " +
                 "stems; contract 2 muffles it. Not touched by Set, so re-baking keeps it.")]
        [SerializeField] private AudioClip _musicTrack;
        [Tooltip("Music track runtime volume, dB.")]
        [SerializeField] private float _musicTrackDb = -18f;

        public SoundEvent[] Events => _events;
        public AudioClip MusicTrack => _musicTrack;
        public float MusicTrackDb => _musicTrackDb;
        public AudioClip MusicBase => _musicBase;
        public AudioClip MusicMelody => _musicMelody;
        public float MusicBaseDb => _musicBaseDb;
        public float MusicMelodyDb => _musicMelodyDb;

        /// <summary>Replaces the whole content (editor baker, tests).</summary>
        public void Set(SoundEvent[] events, AudioClip musicBase, AudioClip musicMelody, float musicBaseDb = -14f,
            float musicMelodyDb = -17f)
        {
            _events = events ?? Array.Empty<SoundEvent>();
            _musicBase = musicBase;
            _musicMelody = musicMelody;
            _musicBaseDb = musicBaseDb;
            _musicMelodyDb = musicMelodyDb;
        }

        /// <summary>Sets the single music track (editor setup, tests); null goes back to the two stems.</summary>
        public void SetMusicTrack(AudioClip track, float trackDb)
        {
            _musicTrack = track;
            _musicTrackDb = trackDb;
        }

        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);
    }
}
