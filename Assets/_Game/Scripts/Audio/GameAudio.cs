using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The game's whole audio layer (Phase 6 procedural audio spec; MAIN_GDD §13 Ses, K-8).
    /// 2D pooled AudioSources per event (voice limit, oldest stolen, min gap, pitch/volume jitter), five buses
    /// (SFX, Voice, UI, MusicBase, MusicMelody) and the music: the bank's single <see cref="SoundBank.MusicTrack"/> (kept
    /// playing across scene loads) when
    /// set (user decision 2026-10-25), else the two stems started on the same dspTime, all looping.
    /// Contract 2: <see cref="AudioCosts.VoiceMuted"/> mutes the Voice bus instantly; <see cref="AudioCosts.MusicLayerMuted"/>
    /// fades the melody stem out over 1.5 s of unscaled time, or with the single track sweeps a low-pass filter down
    /// over the same time so the music sounds muffled and far away (it never stops). Unscaled events ignore listener pause and use unscaled
    /// cooldowns; audio is never paused on timeScale 0. Listens to existing gameplay events; no per-frame allocations.
    /// Mix trims on top of the baked bank (first listening fixes, no re-bake needed): the rifle is quieter and cycles
    /// through small pitch steps so 6 shots a second do not repeat one identical "pew"; the explosion is a little
    /// lower; both music stems sit a few dB under the SFX.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Accessibility: multiplies every sound and the music (0..1).</summary>
        public static float MasterVolume = 1f;
        /// <summary>Settings menu: music stems (0..1).</summary>
        public static float MusicVolume = 1f;
        /// <summary>Settings menu: every sound event that is not music (0..1).</summary>
        public static float SfxVolume = 1f;

        public static GameAudio Instance { get; private set; }

        private enum Cue
        {
            RifleShot, RocketLaunch, Explosion, MacheteSwing, MacheteHit, BulletHitEnemy, BulletHitWall, EchoBounce,
            EnemyShot, ChaserTell, ShooterTell, SpawnWarning, EnemyDie, PlayerHurt, PlayerDodge, DodgeReady,
            DefneVoiceHurt, OfferAppear, ModuleAccept, ModuleReject, ModuleEject, DevilAppear, ContractYes, ContractNo,
            ReviveShockwave, EndingNoFirst, EndingNoLate, EndingAllYes, UiClick, SkeletonHit, MummyHit, SkeletonDie, MummyDie, SkeletonTell, MummyTell, SkeletonMove, MummyMove, CritHit, Count,
        }

        private static readonly string[] CueNames =
        {
            "rifle_shot", "rocket_launch", "explosion", "machete_swing", "machete_hit", "bullet_hit_enemy",
            "bullet_hit_wall", "echo_bounce", "enemy_shot", "enemy_tell_chaser", "enemy_tell_shooter", "spawn_warning",
            "enemy_die", "player_hurt", "player_dodge", "dodge_ready", "defne_voice_hurt", "module_offer_appear",
            "module_accept", "module_reject_throw", "module_eject", "devil_appear", "contract_yes", "contract_no",
            "revive_shockwave", "ending_sting_no_first", "ending_sting_no_late", "ending_sting_all_yes", "ui_click",
            "skeleton_hit", "mummy_hit", "skeleton_die", "mummy_die", "skeleton_tell", "mummy_tell", "skeleton_move", "mummy_move",
            "crit_hit",
        };

        private const float TripleShotPitch = 0.94f;
        private const float RunnerTellPitch = 1.3f;
        private const float ModuleExplosionPitch = 1.25f;
        private const float ModuleExplosionVolume = 0.501f; // -6 dB

        [Tooltip("Sound bank (Dance/Audio/Bake All creates Assets/_Game/Audio/SoundBank.asset).")]
        [SerializeField] private SoundBank _bank;

        [Header("Event sources (optional; missing ones just stay silent)")]
        [Tooltip("Player: dodge, hurt/voice and whisper module events.")]
        [SerializeField] private PlayerController _player;
        [Tooltip("Wave director: spawn warnings and enemy deaths.")]
        [SerializeField] private WaveDirector _waves;
        [Tooltip("Devil flow: appearance, EVET/HAYIR, revive shockwave, ending sting.")]
        [SerializeField] private DevilDirector _devil;
        [Tooltip("Camera rig used to pan enemy sounds by screen x. Empty = all centred.")]
        [SerializeField] private PixelCameraRig _cameraRig;

        [Header("Music")]
        [Tooltip("Melody stem fade-out after contract 2, unscaled seconds (spec: 1.5).")]
        [SerializeField, Min(0.01f)] private float _melodyFadeSeconds = 1.5f;
        [Tooltip("Delay before both stems start on the same dspTime, seconds.")]
        [SerializeField, Min(0.01f)] private float _musicStartDelay = 0.1f;
        [Tooltip("Added to both music stems, dB, so the music sits under the SFX.")]
        [SerializeField, Range(-24f, 6f)] private float _musicTrimDb = -5f;
        [Tooltip("Single music track after contract 2: low-pass cutoff in Hz (muffled, far away).")]
        [SerializeField, Range(200f, 5000f)] private float _muffledCutoff = 700f;

        [Header("Mix trims (dB added to the bank's event volume)")]
        [Tooltip("Every gameplay sound (Sfx and Voice buses; not the UI or the music), so the music sits in front.")]
        [SerializeField, Range(-24f, 0f)] private float _effectsTrimDb = -5f;
        [Tooltip("rifle_shot (bank -14 dB): the most frequent sound, kept below the rest.")]
        [SerializeField, Range(-24f, 6f)] private float _rifleTrimDb = -3f;
        [Tooltip("explosion (bank -4 dB): rocket blasts stacking with kills must not clip.")]
        [SerializeField, Range(-24f, 6f)] private float _explosionTrimDb = -3f;
        [Tooltip("Rifle shots cycle through these pitch factors on top of the bank's jitter (stand-in round-robin).")]
        [SerializeField] private float[] _riflePitchSteps = { 1f, 0.955f, 1.04f, 0.975f, 1.02f };
        [Tooltip("crit_hit (bank -3 dB, built by Dance/Audio/Build Crit Sound): Defne's crits, well above the rifle.")]
        [SerializeField, Range(-24f, 6f)] private float _critTrimDb = 0f;
        [Tooltip("Fewest unscaled seconds between two crit sounds (a pierce or machete sweep crits several at once).")]
        [SerializeField, Min(0f)] private float _critMinGap = 0.05f;

        private sealed class Voice
        {
            public AudioSource Source;
            public float Start;
            public float End;
            public int Serial;
        }

        private sealed class Slot
        {
            public SoundEvent Event;
            public Voice[] Voices;
            public float LastStart = float.NegativeInfinity;
            public int RoundRobin;
            public float BaseVolume;
        }

        private readonly Dictionary<string, int> _ids = new Dictionary<string, int>(64);
        private readonly int[] _cueIds = new int[(int)Cue.Count];
        private readonly List<AudioSource> _voiceBusSources = new List<AudioSource>(4);
        private readonly List<AudioSource> _ownedSources = new List<AudioSource>(80);
        private readonly Dictionary<Enemy, int> _tellHandles = new Dictionary<Enemy, int>(32);
        private struct MovementSound { public Vector3 Position; public float Distance; public float Next; }
        private readonly Dictionary<Enemy, MovementSound> _movement = new Dictionary<Enemy, MovementSound>(32);
        private Slot[] _slots = new Slot[0];
        private AudioSource _musicBase;
        private AudioSource _musicMelody;
        private AudioSource _musicTrack;
        private AudioLowPassFilter _musicMuffle;
        // The single music track outlives scene loads (menu → arena, quit → menu): the next scene's GameAudio adopts
        // it when it plays the same clip, so the music carries on from where it was instead of starting over. After an
        // ending (the music faded out) the next scene starts it fresh.
        private static AudioSource s_persistentTrack;
        private static bool s_restartTrack;
        private float _melodyFade = 1f;
        private const float OpenCutoff = 22000f;
        private bool _voiceMuted;
        private int _serial;
        private bool _subscribed;
        private Health _playerHealth;
        private PlayerModules _modules;
        private ModuleThrower _thrower;
        private DevilState _lastDevilState = DevilState.Playing;
        private float _musicTrim = 1f;
        private int _rifleStep;
        private float _lastCrit = float.NegativeInfinity;
        private bool _runEnded;
        private float _endFade = 1f;

        /// <summary>0..1 melody stem fade (0 after contract 2 has faded it out).</summary>
        public float MelodyFade => _melodyFade;
        public AudioSource MusicBaseSource => _musicBase;
        public AudioSource MusicMelodySource => _musicMelody;
        /// <summary>The single music track's source (null when the bank has none and the stems play).</summary>
        public AudioSource MusicTrackSource => _musicTrack;
        /// <summary>Low-pass cutoff on the single track now, Hz (22 kHz = open, lower after contract 2).</summary>
        public float MusicTrackCutoff => _musicMuffle != null ? _musicMuffle.cutoffFrequency : OpenCutoff;
        /// <summary>True while the Voice bus is muted (contract 2).</summary>
        public bool VoiceBusMuted => _voiceMuted;
        public SoundBank Bank => _bank;
        /// <summary>True once the ending screen is up: only Ui-bus sounds still play, music fades out.</summary>
        public bool RunEnded => _runEnded;

        /// <summary>
        /// The run is over: stops every gameplay voice now, fades the music out over <paramref name="fadeSeconds"/>
        /// (unscaled) and lets only Ui-bus sounds (ending sting, clicks) play from here. A scene reload resets it.
        /// </summary>
        public void EndRun(float fadeSeconds = 1.2f)
        {
            if (_runEnded) return;
            _runEnded = true;
            s_restartTrack = true; // the music fades out now; the next scene starts it from the top
            _endFadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            for (int s = 0; s < _slots.Length; s++)
            {
                Slot slot = _slots[s];
                if (slot.Event != null && slot.Event.Bus == AudioBus.Ui) continue;
                for (int v = 0; v < slot.Voices.Length; v++)
                {
                    slot.Voices[v].Source.Stop();
                    slot.Voices[v].End = float.NegativeInfinity;
                }
            }
            _tellHandles.Clear();
            _movement.Clear();
        }

        private float _endFadeSeconds = 1.2f;

        private void Awake()
        {
            Instance = this;
            if (_bank != null) Initialize(_bank);
        }

        private void Start() => Subscribe(true);

        private void OnDestroy()
        {
            Subscribe(false);
            if (Instance == this) Instance = null;
        }

        /// <summary>(Re)builds the voice pools from <paramref name="bank"/> and starts the music stems.</summary>
        public void Initialize(SoundBank bank)
        {
            _bank = bank;
            for (int i = 0; i < _ownedSources.Count; i++)
                if (_ownedSources[i] != null) Destroy(_ownedSources[i]);
            _ownedSources.Clear();
            _voiceBusSources.Clear();
            _ids.Clear();
            _tellHandles.Clear();
            _musicBase = null;
            _musicMelody = null;
            if (_musicTrack != null && _musicTrack != s_persistentTrack) Destroy(_musicTrack.gameObject);
            _musicTrack = null;
            _musicMuffle = null;
            _voiceMuted = false;
            _melodyFade = AudioCosts.MusicLayerMuted ? 0f : 1f;

            SoundEvent[] events = bank != null && bank.Events != null ? bank.Events : new SoundEvent[0];
            _slots = new Slot[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                SoundEvent e = events[i];
                var slot = new Slot { Event = e, Voices = new Voice[e != null ? e.MaxVoices : 1] };
                slot.BaseVolume = e != null ? SoundBank.DbToLinear(e.VolumeDb) : 0f;
                for (int v = 0; v < slot.Voices.Length; v++)
                {
                    AudioSource source = CreateSource(e != null && e.Unscaled);
                    if (e != null && e.Bus == AudioBus.Voice) _voiceBusSources.Add(source);
                    slot.Voices[v] = new Voice { Source = source, End = float.NegativeInfinity };
                }
                _slots[i] = slot;
                if (e != null && !string.IsNullOrEmpty(e.Name)) _ids[e.Name] = i;
            }

            for (int c = 0; c < _cueIds.Length; c++)
                _cueIds[c] = _ids.TryGetValue(CueNames[c], out int id) ? id : -1;
            Trim(Cue.RifleShot, _rifleTrimDb);
            Trim(Cue.Explosion, _explosionTrimDb);
            Trim(Cue.CritHit, _critTrimDb);
            _musicTrim = SoundBank.DbToLinear(_musicTrimDb);

            if (bank != null && bank.MusicTrack != null) StartMusicTrack(bank.MusicTrack);
            else if (bank != null && bank.MusicBase != null && bank.MusicMelody != null) StartMusic(bank);
            ApplyBuses(true);
        }

        private void Trim(Cue cue, float db)
        {
            int id = _cueIds[(int)cue];
            if (id >= 0 && id < _slots.Length && _slots[id] != null) _slots[id].BaseVolume *= SoundBank.DbToLinear(db);
        }

        private AudioSource CreateSource(bool unscaled)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            source.ignoreListenerPause = unscaled;
            _ownedSources.Add(source);
            return source;
        }

        private void StartMusic(SoundBank bank)
        {
            _musicBase = CreateSource(true);
            _musicMelody = CreateSource(true);
            _musicBase.clip = bank.MusicBase;
            _musicMelody.clip = bank.MusicMelody;
            _musicBase.loop = true;
            _musicMelody.loop = true;
            _musicBase.priority = 0;
            _musicMelody.priority = 0;
            double start = AudioSettings.dspTime + _musicStartDelay;
            _musicBase.PlayScheduled(start);
            _musicMelody.PlayScheduled(start);
        }

        /// <summary>
        /// The single track on its own child object: an AudioLowPassFilter filters every source on its GameObject,
        /// and the sound pools live on this one.
        /// </summary>
        private void StartMusicTrack(AudioClip clip)
        {
            if (s_persistentTrack != null && s_persistentTrack.clip == clip && s_persistentTrack.isPlaying && !s_restartTrack)
            {
                _musicTrack = s_persistentTrack;
                if (FiltersSupported)
                {
                    _musicMuffle = _musicTrack.GetComponent<AudioLowPassFilter>();
                    if (_musicMuffle == null) _musicMuffle = _musicTrack.gameObject.AddComponent<AudioLowPassFilter>();
                }
                return;
            }
            if (s_persistentTrack != null) Destroy(s_persistentTrack.gameObject);
            s_restartTrack = false;

            var go = new GameObject("MusicTrack");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            _musicTrack = go.AddComponent<AudioSource>();
            s_persistentTrack = _musicTrack;
            _musicTrack.playOnAwake = false;
            _musicTrack.spatialBlend = 0f;
            _musicTrack.ignoreListenerPause = true;
            _musicTrack.clip = clip;
            _musicTrack.loop = true;
            _musicTrack.priority = 0;
            if (FiltersSupported)
            {
                _musicMuffle = go.AddComponent<AudioLowPassFilter>();
                _musicMuffle.cutoffFrequency = MuffleCutoff(_melodyFade);
                _musicTrack.PlayScheduled(AudioSettings.dspTime + _musicStartDelay);
            }
            else
            {
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                _musicTrack.Play();
            }
        }

        // WebGL has no audio filters and its audio clock is not reliable for PlayScheduled while the browser still
        // holds audio back (until the first click or key): there the track starts with Play(), "muffled" becomes a
        // quieter track, and a watchdog restarts it if the browser dropped the start.
#if UNITY_WEBGL && !UNITY_EDITOR
        private static readonly bool FiltersSupported = false;
#else
        private static readonly bool FiltersSupported = true;
#endif
        private const float WebMuffledVolume = 0.35f;
        private float _nextMusicCheck;

        private void KeepMusicPlaying()
        {
            if (FiltersSupported || _musicTrack == null || _runEnded || Time.unscaledTime < _nextMusicCheck) return;
            _nextMusicCheck = Time.unscaledTime + 1f;
            AudioClip clip = _musicTrack.clip;
            if (clip == null || _musicTrack.isPlaying) return;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Loaded) _musicTrack.Play();
        }

        // Even steps on a log scale, so the sweep is heard all the way down, not only at the end.
        private float MuffleCutoff(float open) =>
            _muffledCutoff * Mathf.Pow(OpenCutoff / _muffledCutoff, Mathf.Clamp01(open));

        private void Update()
        {
            ApplyBuses(false);
            KeepMusicPlaying();
            if (_waves == null || Time.timeScale <= 0f || _waves.Paused) return;
            var enemies = _waves.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !enemy.isActiveAndEnabled) continue;
                Vector3 position = enemy.transform.position;
                if (!_movement.TryGetValue(enemy, out MovementSound state))
                    state = new MovementSound { Position = position, Next = Time.time + 0.25f };
                float distance = Vector3.Distance(position, state.Position);
                state.Position = position;
                if (enemy.IsKnockedBack || distance > 2f) state.Distance = 0f;
                else state.Distance += distance;
                if (state.Distance >= 1.5f && Time.time >= state.Next)
                {
                    PlayCueAt(IsMummy(enemy) ? Cue.MummyMove : Cue.SkeletonMove, position);
                    state.Distance = 0f;
                    state.Next = Time.time + 0.8f;
                }
                _movement[enemy] = state;
            }
        }

        private void ApplyBuses(bool force)
        {
            bool voiceMuted = AudioCosts.VoiceMuted;
            if (force || voiceMuted != _voiceMuted)
            {
                _voiceMuted = voiceMuted;
                for (int i = 0; i < _voiceBusSources.Count; i++)
                    if (_voiceBusSources[i] != null) _voiceBusSources[i].mute = voiceMuted;
            }

            float target = AudioCosts.MusicLayerMuted ? 0f : 1f;
            _melodyFade = Mathf.MoveTowards(_melodyFade, target, Time.unscaledDeltaTime / _melodyFadeSeconds);

            if (_runEnded) _endFade = Mathf.MoveTowards(_endFade, 0f, Time.unscaledDeltaTime / _endFadeSeconds);

            if (_bank == null) return;
            float master = Mathf.Clamp01(MasterVolume) * Mathf.Clamp01(MusicVolume) * _endFade;
            if (_musicBase != null) _musicBase.volume = SoundBank.DbToLinear(_bank.MusicBaseDb) * master * _musicTrim;
            if (_musicMelody != null)
                _musicMelody.volume = SoundBank.DbToLinear(_bank.MusicMelodyDb) * master * _musicTrim * _melodyFade;
            if (_musicTrack != null)
            {
                float muffle = _musicMuffle != null ? 1f : Mathf.Lerp(WebMuffledVolume, 1f, _melodyFade);
                _musicTrack.volume = SoundBank.DbToLinear(_bank.MusicTrackDb) * master * muffle;
                if (_musicMuffle != null) _musicMuffle.cutoffFrequency = MuffleCutoff(_melodyFade);
            }
        }

        // ------------------------------------------------------------------ playback API

        /// <summary>Id of an event name, or -1. Resolve once and use <see cref="PlayId"/> in hot code.</summary>
        public int GetId(string eventName) =>
            eventName != null && _ids.TryGetValue(eventName, out int id) ? id : -1;

        /// <summary>Plays an event (centred). Returns a handle for <see cref="Stop"/>, or 0 if dropped.</summary>
        public int Play(string eventName, float pitch = 1f, float volumeScale = 1f) =>
            PlayId(GetId(eventName), pitch, volumeScale, 0f);

        /// <summary>Plays an event by id with an explicit stereo pan. Returns a handle, or 0 if dropped.</summary>
        public int PlayId(int id, float pitch = 1f, float volumeScale = 1f, float pan = 0f)
        {
            if (id < 0 || id >= _slots.Length) return 0;
            Slot slot = _slots[id];
            SoundEvent e = slot.Event;
            if (e == null || e.Clips == null || e.Clips.Length == 0) return 0;
            if (e.Bus == AudioBus.Voice && AudioCosts.VoiceMuted) return 0;
            if (_runEnded && e.Bus != AudioBus.Ui) return 0;

            float now = e.Unscaled ? Time.unscaledTime : Time.time;
            if (now < slot.LastStart + e.MinGap) return 0;

            AudioClip clip = e.Clips[slot.RoundRobin % e.Clips.Length];
            slot.RoundRobin = (slot.RoundRobin + 1) % e.Clips.Length;
            if (clip == null) return 0;

            Voice voice = slot.Voices[0];
            for (int i = 0; i < slot.Voices.Length; i++)
            {
                Voice candidate = slot.Voices[i];
                if (candidate.End <= now) { voice = candidate; break; }
                if (candidate.Start < voice.Start) voice = candidate; // steal the oldest
            }

            float finalPitch = Mathf.Max(0.05f, pitch * (1f + (e.PitchJitter > 0f ? Random.Range(-e.PitchJitter, e.PitchJitter) : 0f)));
            float jitterDb = e.VolumeJitterDb > 0f ? Random.Range(-e.VolumeJitterDb, e.VolumeJitterDb) : 0f;

            AudioSource source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = finalPitch;
            float effectsTrim = e.Bus == AudioBus.Sfx || e.Bus == AudioBus.Voice ? SoundBank.DbToLinear(_effectsTrimDb) : 1f;
            source.volume = Mathf.Clamp01(slot.BaseVolume * SoundBank.DbToLinear(jitterDb) * volumeScale * effectsTrim
                                          * Mathf.Clamp01(MasterVolume) * Mathf.Clamp01(SfxVolume));
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.mute = e.Bus == AudioBus.Voice && _voiceMuted;
            source.Play();

            slot.LastStart = now;
            voice.Start = now;
            voice.End = now + clip.length / finalPitch;
            voice.Serial = ++_serial;
            return voice.Serial;
        }

        /// <summary>Stops the voice started with <paramref name="handle"/> if it has not been reused since.</summary>
        public void Stop(int handle)
        {
            if (handle <= 0) return;
            for (int s = 0; s < _slots.Length; s++)
            {
                Voice[] voices = _slots[s].Voices;
                for (int v = 0; v < voices.Length; v++)
                {
                    if (voices[v].Serial != handle) continue;
                    voices[v].Source.Stop();
                    voices[v].End = float.NegativeInfinity;
                    return;
                }
            }
        }

        /// <summary>Voices of <paramref name="eventName"/> still sounding (by clip length).</summary>
        public int ActiveVoiceCount(string eventName)
        {
            int id = GetId(eventName);
            if (id < 0) return 0;
            Slot slot = _slots[id];
            float now = slot.Event.Unscaled ? Time.unscaledTime : Time.time;
            int count = 0;
            for (int i = 0; i < slot.Voices.Length; i++)
                if (slot.Voices[i].End > now) count++;
            return count;
        }

        private int PlayCue(Cue cue, float pitch = 1f, float volumeScale = 1f) =>
            PlayId(_cueIds[(int)cue], pitch, volumeScale, 0f);

        private int PlayCueAt(Cue cue, Vector3 world, float pitch = 1f, float volumeScale = 1f) =>
            PlayId(_cueIds[(int)cue], pitch, volumeScale, PanFor(world));

        /// <summary>Spec §0.3: pan = clamp(viewportX·1.2 − 0.6, −0.6, 0.6).</summary>
        private float PanFor(Vector3 world)
        {
            Camera cam = _cameraRig != null ? _cameraRig.GameCamera : null;
            if (cam == null) return 0f;
            float x = cam.WorldToViewportPoint(world).x;
            return Mathf.Clamp(x * 1.2f - 0.6f, -0.6f, 0.6f);
        }

        // ------------------------------------------------------------------ hooks

        private void Subscribe(bool on)
        {
            if (on == _subscribed) return;
            _subscribed = on;

            if (on)
            {
                if (_player != null)
                {
                    _playerHealth = _player.Health;
                    _modules = _player.GetComponentInChildren<PlayerModules>(true);
                    _thrower = _modules != null ? _modules.Thrower : null;
                }

                ImpactBus.OnImpact += OnImpact;
                Enemy.DamageTaken += OnEnemyDamaged;
                Rifle.Fired += OnRifleFired;
                RocketLauncher.Launched += OnRocketLaunched;
                RocketLauncher.Exploded += OnRocketExploded;
                Machete.SwingStarted += OnSwing;
                Machete.SwingHit += OnSwingHit;
                Projectile.Bounced += OnBounced;
                ChaserBrain.TellStarted += OnChaserTell;
                ChaserBrain.TellCancelled += OnTellCancelled;
                ShooterBrain.TellStarted += OnShooterTell;
                ShooterBrain.TellCancelled += OnTellCancelled;
                UiKit.Clicked += OnUiClick;
                if (_player != null)
                {
                    _player.DodgeStarted += OnDodge;
                    _player.DodgeReady += OnDodgeReady;
                }
                if (_playerHealth != null) _playerHealth.Damaged += OnPlayerDamaged;
                if (_modules != null)
                {
                    _modules.OfferAppeared += OnOfferAppeared;
                    _modules.Accepted += OnModuleAccepted;
                    _modules.Rejected += OnModuleRejected;
                    _modules.Ejected += OnModuleEjected;
                }
                if (_thrower != null) _thrower.Exploded += OnModuleExploded;
                if (_waves != null)
                {
                    _waves.SpawnWarningStarted += OnSpawnWarning;
                    _waves.EnemyKilled += OnEnemyKilled;
                    _waves.EnemySpawned += OnEnemySpawned;
                }
                if (_devil != null)
                {
                    _lastDevilState = _devil.State;
                    _devil.StateChanged += OnDevilState;
                    _devil.ContractChosen += OnContractChosen;
                }
                return;
            }

            ImpactBus.OnImpact -= OnImpact;
            Enemy.DamageTaken -= OnEnemyDamaged;
            _movement.Clear();
            Rifle.Fired -= OnRifleFired;
            RocketLauncher.Launched -= OnRocketLaunched;
            RocketLauncher.Exploded -= OnRocketExploded;
            Machete.SwingStarted -= OnSwing;
            Machete.SwingHit -= OnSwingHit;
            Projectile.Bounced -= OnBounced;
            ChaserBrain.TellStarted -= OnChaserTell;
            ChaserBrain.TellCancelled -= OnTellCancelled;
            ShooterBrain.TellStarted -= OnShooterTell;
            ShooterBrain.TellCancelled -= OnTellCancelled;
            UiKit.Clicked -= OnUiClick;
            if (_player != null)
            {
                _player.DodgeStarted -= OnDodge;
                _player.DodgeReady -= OnDodgeReady;
            }
            if (_playerHealth != null) _playerHealth.Damaged -= OnPlayerDamaged;
            if (_modules != null)
            {
                _modules.OfferAppeared -= OnOfferAppeared;
                _modules.Accepted -= OnModuleAccepted;
                _modules.Rejected -= OnModuleRejected;
                _modules.Ejected -= OnModuleEjected;
            }
            if (_thrower != null) _thrower.Exploded -= OnModuleExploded;
            if (_waves != null)
            {
                _waves.SpawnWarningStarted -= OnSpawnWarning;
                _waves.EnemyKilled -= OnEnemyKilled;
                _waves.EnemySpawned -= OnEnemySpawned;
            }
            if (_devil != null)
            {
                _devil.StateChanged -= OnDevilState;
                _devil.ContractChosen -= OnContractChosen;
            }
        }

        private void OnImpact(ImpactEvent e)
        {
            // Defne's crits (bullet, machete target, rocket blast that critted): a bright metallic ting + low thump on
            // top of the normal hit sound, the same frame as the hit. Unscaled gap so a machete hitstop cannot bunch them.
            if (e.isCrit && e.instigatorIsLocalPlayer && e.kind != ImpactKind.MuzzleFire
                && Time.unscaledTime - _lastCrit >= _critMinGap
                && PlayCueAt(Cue.CritHit, e.position, Random.Range(0.97f, 1.04f)) != 0)
                _lastCrit = Time.unscaledTime;

            switch (e.kind)
            {
                case ImpactKind.MuzzleFire:
                    // Player rifle/rocket have their own events; a non-player muzzle is an enemy shot.
                    if (!e.instigatorIsLocalPlayer) PlayCueAt(Cue.EnemyShot, e.position);
                    break;
                case ImpactKind.Bullet:
                    bool body = e.hitTransform != null && e.hitTransform.GetComponentInParent<Health>() != null;
                    if (!body) PlayCueAt(Cue.BulletHitWall, e.position);
                    else if (e.instigatorIsLocalPlayer && e.hitTransform.GetComponentInParent<Enemy>() == null)
                        PlayCueAt(Cue.BulletHitEnemy, e.position);
                    break; // enemy bullet on the player: player_hurt comes from Health.Damaged
            }
        }

        private void OnRifleFired(Rifle rifle, bool tripleShot)
        {
            float step = 1f;
            if (_riflePitchSteps != null && _riflePitchSteps.Length > 0)
            {
                _rifleStep = (_rifleStep + 1) % _riflePitchSteps.Length;
                step = Mathf.Max(0.5f, _riflePitchSteps[_rifleStep]);
            }
            PlayCue(Cue.RifleShot, 0.95f * (tripleShot ? TripleShotPitch : 1f) * step);
        }
        private void OnRocketLaunched(RocketLauncher launcher) => PlayCue(Cue.RocketLaunch);
        private void OnRocketExploded(Vector3 point) => PlayCueAt(Cue.Explosion, point);
        private void OnModuleExploded(Vector3 point) => PlayCueAt(Cue.Explosion, point, ModuleExplosionPitch, ModuleExplosionVolume);
        private void OnSwing(Machete machete) => PlayCue(Cue.MacheteSwing);
        private void OnSwingHit(Machete machete) => PlayCue(Cue.MacheteHit);
        private void OnBounced(Vector3 point) => PlayCueAt(Cue.EchoBounce, point);
        private void OnUiClick() => PlayCue(Cue.UiClick);
        private void OnDodge() => PlayCue(Cue.PlayerDodge);
        private void OnDodgeReady() => PlayCue(Cue.DodgeReady);
        private void OnOfferAppeared(ModuleType type) => PlayCue(Cue.OfferAppear);
        private void OnModuleAccepted(ModuleType type) => PlayCue(Cue.ModuleAccept);
        private void OnModuleRejected(ModuleType type) => PlayCue(Cue.ModuleReject);
        private void OnModuleEjected(ModuleType type) => PlayCue(Cue.ModuleEject);
        private void OnSpawnWarning(Vector3 position) => PlayCueAt(Cue.SpawnWarning, position);
        private void OnContractChosen(bool yes) => PlayCue(yes ? Cue.ContractYes : Cue.ContractNo);

        private void OnEnemyKilled(Enemy enemy)
        {
            if (enemy == null) return;
            OnTellCancelled(enemy);
            _tellHandles.Remove(enemy);
            _movement.Remove(enemy);
            PlayCueAt(IsMummy(enemy) ? Cue.MummyDie : Cue.SkeletonDie, enemy.transform.position);
        }

        // Approved art mapping: Chaser/Runner = skeleton; Shooter = mummy.
        private static bool IsMummy(Enemy enemy) => enemy.Type == EnemyType.Shooter;

        private void OnEnemySpawned(Enemy enemy)
        {
            _movement[enemy] = new MovementSound { Position = enemy.transform.position, Next = Time.time + 0.25f };
        }

        private void OnEnemyDamaged(Enemy enemy, float remaining)
        {
            if (enemy != null && remaining > 0f)
                PlayCueAt(IsMummy(enemy) ? Cue.MummyHit : Cue.SkeletonHit, enemy.transform.position);
        }

        private void OnPlayerDamaged(DamageInfo info, float remaining)
        {
            PlayCue(Cue.PlayerHurt);
            PlayCue(Cue.DefneVoiceHurt); // Voice bus: silent after contract 2
        }

        private void OnChaserTell(Enemy enemy, bool runner)
        {
            if (enemy == null) return;
            _tellHandles[enemy] = PlayCueAt(IsMummy(enemy) ? Cue.MummyTell : Cue.SkeletonTell, enemy.transform.position, runner ? RunnerTellPitch : 1f);
        }

        private void OnShooterTell(Enemy enemy)
        {
            if (enemy == null) return;
            _tellHandles[enemy] = PlayCueAt(IsMummy(enemy) ? Cue.MummyTell : Cue.SkeletonTell, enemy.transform.position);
        }

        private void OnTellCancelled(Enemy enemy)
        {
            if (enemy == null || !_tellHandles.TryGetValue(enemy, out int handle)) return;
            Stop(handle);
            _tellHandles[enemy] = 0;
        }

        private void OnDevilState(DevilState state)
        {
            DevilState previous = _lastDevilState;
            _lastDevilState = state;
            switch (state)
            {
                case DevilState.Freezing:
                    PlayCue(Cue.DevilAppear);
                    break;
                case DevilState.Playing:
                    // Offer/Forced → Playing is the revive (EVET), with the K-12 shockwave.
                    if (previous == DevilState.Offer || previous == DevilState.Forced) PlayCue(Cue.ReviveShockwave);
                    break;
                case DevilState.Ended:
                    EndRun();
                    switch (_devil != null ? _devil.Ending : EndingKind.None)
                    {
                        case EndingKind.KeptYourself: PlayCue(Cue.EndingNoFirst); break;
                        case EndingKind.DiminishedButStanding: PlayCue(Cue.EndingNoLate); break;
                        case EndingKind.AlwaysYes: PlayCue(Cue.EndingAllYes); break;
                    }
                    break;
            }
        }
    }
}
