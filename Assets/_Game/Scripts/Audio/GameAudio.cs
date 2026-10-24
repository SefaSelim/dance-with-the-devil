using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The game's whole audio layer (Phase 6 procedural audio spec; MAIN_GDD §13 Ses, K-8).
    /// 2D pooled AudioSources per event (voice limit, oldest stolen, min gap, pitch/volume jitter), five buses
    /// (SFX, Voice, UI, MusicBase, MusicMelody) and the two music stems started on the same dspTime and looping.
    /// Contract 2: <see cref="AudioCosts.VoiceMuted"/> mutes the Voice bus instantly; <see cref="AudioCosts.MusicLayerMuted"/>
    /// fades the melody stem out over 1.5 s of unscaled time. Unscaled events ignore listener pause and use unscaled
    /// cooldowns; audio is never paused on timeScale 0. Listens to existing gameplay events; no per-frame allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Accessibility: multiplies every sound and the music (0..1).</summary>
        public static float MasterVolume = 1f;

        public static GameAudio Instance { get; private set; }

        private enum Cue
        {
            RifleShot, RocketLaunch, Explosion, MacheteSwing, MacheteHit, BulletHitEnemy, BulletHitWall, EchoBounce,
            EnemyShot, ChaserTell, ShooterTell, SpawnWarning, EnemyDie, PlayerHurt, PlayerDodge, DodgeReady,
            DefneVoiceHurt, OfferAppear, ModuleAccept, ModuleReject, ModuleEject, DevilAppear, ContractYes, ContractNo,
            ReviveShockwave, EndingNoFirst, EndingNoLate, EndingAllYes, UiClick, Count,
        }

        private static readonly string[] CueNames =
        {
            "rifle_shot", "rocket_launch", "explosion", "machete_swing", "machete_hit", "bullet_hit_enemy",
            "bullet_hit_wall", "echo_bounce", "enemy_shot", "enemy_tell_chaser", "enemy_tell_shooter", "spawn_warning",
            "enemy_die", "player_hurt", "player_dodge", "dodge_ready", "defne_voice_hurt", "module_offer_appear",
            "module_accept", "module_reject_throw", "module_eject", "devil_appear", "contract_yes", "contract_no",
            "revive_shockwave", "ending_sting_no_first", "ending_sting_no_late", "ending_sting_all_yes", "ui_click",
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
        private Slot[] _slots = new Slot[0];
        private AudioSource _musicBase;
        private AudioSource _musicMelody;
        private float _melodyFade = 1f;
        private bool _voiceMuted;
        private int _serial;
        private bool _subscribed;
        private Health _playerHealth;
        private PlayerModules _modules;
        private ModuleThrower _thrower;
        private DevilState _lastDevilState = DevilState.Playing;

        /// <summary>0..1 melody stem fade (0 after contract 2 has faded it out).</summary>
        public float MelodyFade => _melodyFade;
        public AudioSource MusicBaseSource => _musicBase;
        public AudioSource MusicMelodySource => _musicMelody;
        /// <summary>True while the Voice bus is muted (contract 2).</summary>
        public bool VoiceBusMuted => _voiceMuted;
        public SoundBank Bank => _bank;

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

            if (bank != null && bank.MusicBase != null && bank.MusicMelody != null) StartMusic(bank);
            ApplyBuses(true);
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

        private void Update() => ApplyBuses(false);

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

            if (_bank == null) return;
            float master = Mathf.Clamp01(MasterVolume);
            if (_musicBase != null) _musicBase.volume = SoundBank.DbToLinear(_bank.MusicBaseDb) * master;
            if (_musicMelody != null) _musicMelody.volume = SoundBank.DbToLinear(_bank.MusicMelodyDb) * master * _melodyFade;
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
            source.volume = Mathf.Clamp01(slot.BaseVolume * SoundBank.DbToLinear(jitterDb) * volumeScale * Mathf.Clamp01(MasterVolume));
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
            }
            if (_devil != null)
            {
                _devil.StateChanged -= OnDevilState;
                _devil.ContractChosen -= OnContractChosen;
            }
        }

        private void OnImpact(ImpactEvent e)
        {
            switch (e.kind)
            {
                case ImpactKind.MuzzleFire:
                    // Player rifle/rocket have their own events; a non-player muzzle is an enemy shot.
                    if (!e.instigatorIsLocalPlayer) PlayCueAt(Cue.EnemyShot, e.position);
                    break;
                case ImpactKind.Bullet:
                    bool body = e.hitTransform != null && e.hitTransform.GetComponentInParent<Health>() != null;
                    if (!body) PlayCueAt(Cue.BulletHitWall, e.position);
                    else if (e.instigatorIsLocalPlayer) PlayCueAt(Cue.BulletHitEnemy, e.position);
                    break; // enemy bullet on the player: player_hurt comes from Health.Damaged
            }
        }

        private void OnRifleFired(Rifle rifle, bool tripleShot) => PlayCue(Cue.RifleShot, tripleShot ? TripleShotPitch : 1f);
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
            if (enemy != null) PlayCueAt(Cue.EnemyDie, enemy.transform.position);
        }

        private void OnPlayerDamaged(DamageInfo info, float remaining)
        {
            PlayCue(Cue.PlayerHurt);
            PlayCue(Cue.DefneVoiceHurt); // Voice bus: silent after contract 2
        }

        private void OnChaserTell(Enemy enemy, bool runner)
        {
            if (enemy == null) return;
            _tellHandles[enemy] = PlayCueAt(Cue.ChaserTell, enemy.transform.position, runner ? RunnerTellPitch : 1f);
        }

        private void OnShooterTell(Enemy enemy)
        {
            if (enemy == null) return;
            _tellHandles[enemy] = PlayCueAt(Cue.ShooterTell, enemy.transform.position);
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
