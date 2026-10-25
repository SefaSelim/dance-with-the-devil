using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Not a gate: records a ~3.5 minute gameplay video as numbered JPG frames plus a WAV of the game's own mix
    /// (AudioRenderer, frame-locked at 30 fps through Time.captureFramerate) to Logs/VideoCapture. Main menu for a few
    /// seconds, then the Arena played by <see cref="RunBot"/>: the Devil's lines and cards are left on screen long enough
    /// to read (always EVET), the Q ring opens and a module is thrown now and then, the machete swings at close enemies,
    /// and at the end Defne is killed and says HAYIR so the ending screen shows. Gameplay sounds are lowered for the
    /// recording so the music leads. Explicit; run it with -testFilter GameplayVideoCaptureTests (takes several minutes).
    /// </summary>
    [Explicit("Records a gameplay video; run on demand.")]
    public sealed class GameplayVideoCaptureTests
    {
        private const string MenuPath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";
        private const int Fps = 30;
        private const float MenuSeconds = 6f;
        private const float GameplaySeconds = 192f;
        private const float EndingSeconds = 9f;
        private const float RecordingSfxVolume = 0.7f;
        private const int JpgQuality = 92;

        private string _frames;
        private int _frameIndex;
        private int _pendingWrites;
        private RenderTexture _grab;
        private FileStream _wav;
        private BinaryWriter _wavWriter;
        private int _channels;
        private int _sampleRate;
        private long _samplesWritten;
        private float _sfxBefore;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopRecording();
            yield return TestScenes.LeaveLoadedScenes();
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Gameplay_IsRecorded()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Editor only.");
            yield break;
#else
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "VideoCapture");
            _frames = Path.Combine(root, "frames");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(_frames);

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                MenuPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            StartRecording(Path.Combine(root, "audio.wav"));

            for (int i = 0; i < MenuSeconds * Fps; i++) yield return Frame();

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frame();

            var devil = Object.FindAnyObjectByType<DevilDirector>();
            var bot = Object.FindAnyObjectByType<RunBot>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<PlayerController>();
            var modules = Object.FindAnyObjectByType<PlayerModules>();
            var machete = player != null ? player.GetComponentInChildren<Machete>() : null;
            Assert.IsNotNull(devil, "DevilDirector");
            Assert.IsNotNull(bot, "RunBot");
            Assert.IsNotNull(player, "PlayerController");
            typeof(RunBot).GetField("_devil", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(bot, devil);
            Health health = player.GetComponent<Health>();

            float start = Time.time;
            float nextRing = start + 28f;
            float nextSwing = start + 6f;
            float nextOffer = start + 12f;
            float devilWait = 0f;
            DevilState lastDevil = DevilState.Playing;
            bool endingForced = false;

            while (true)
            {
                float t = Time.time - start;
                if (devil.State == DevilState.Ended) break;

                if (devil.State != DevilState.Playing)
                {
                    // The Devil's scenes at reading pace: lines advance after ~2.6 s, cards stay ~3.5 s.
                    if (bot.enabled) bot.enabled = false;
                    if (devil.State != lastDevil) devilWait = 0f;
                    devilWait += 1f / Fps;
                    switch (devil.State)
                    {
                        case DevilState.Speaking:
                        case DevilState.Freezing:
                            if (devilWait > 2.6f) { devil.Advance(); devilWait = 0f; }
                            break;
                        case DevilState.Forced:
                            if (devilWait > 3.5f) { devil.Advance(); devilWait = 0f; }
                            break;
                        case DevilState.Offer:
                            if (devilWait > 3.5f)
                            {
                                if (endingForced) devil.ChooseNo();
                                else devil.ChooseYes();
                                devilWait = 0f;
                            }
                            break;
                    }
                    lastDevil = devil.State;
                    yield return Frame();
                    continue;
                }
                lastDevil = DevilState.Playing;
                if (!bot.enabled && !endingForced) bot.enabled = true;

                // The end: kill Defne and say HAYIR on the card to show the ending screen.
                if (t >= GameplaySeconds && !endingForced && !health.IsDead && !health.IsInvulnerable)
                {
                    endingForced = true;
                    bot.enabled = false;
                    health.TakeDamage(new DamageInfo(99999f, player.transform.position, Vector3.forward, null, DamageKind.Contact));
                }

                if (modules != null && Time.time >= nextOffer && modules.EquippedCount == 0 && modules.CanPresentOffer)
                {
                    Vector3 spot = player.transform.position + player.AimDirection * 3f;
                    modules.PresentOffer((ModuleType)Random.Range(0, ModuleCatalog.Count), spot);
                    nextOffer = Time.time + 20f;
                }

                if (machete != null && Time.time >= nextSwing && !health.IsDead)
                {
                    Enemy close = NearestEnemy(player.transform.position, 2.6f);
                    if (close != null)
                    {
                        Vector3 dir = close.transform.position - player.transform.position;
                        dir.y = 0f;
                        if (machete.TrySwing(dir.normalized)) nextSwing = Time.time + 9f;
                    }
                }

                if (modules != null && Time.time >= nextRing && modules.EquippedCount > 0 && !health.IsDead)
                {
                    nextRing = Time.time + 55f;
                    yield return ShowRingAndThrow(bot, modules);
                    continue;
                }

                yield return Frame();
                if (Time.time - start > GameplaySeconds + 60f) break; // safety
            }

            for (int i = 0; i < EndingSeconds * Fps; i++) yield return Frame();
            StopRecording();
            while (Volatile.Read(ref _pendingWrites) > 0) yield return null;
            File.WriteAllText(Path.Combine(root, "info.txt"),
                $"frames {_frameIndex}\nfps {Fps}\nsampleRate {_sampleRate}\nchannels {_channels}\nending {devil.Ending}\n");
#endif
        }

        /// <summary>Holds Q for ~1.4 s (the selection ring), then throws the highlighted (or first) module.</summary>
        private IEnumerator ShowRingAndThrow(RunBot bot, PlayerModules modules)
        {
            bot.enabled = false; // gives the input back so the Q key reaches the modules
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Q));
            for (int i = 0; i < Fps * 1.4f; i++) yield return Frame();
            ModuleType? pick = modules.Highlighted;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Frame();
            if (pick == null)
                for (int s = 0; s < ModuleCatalog.SlotCount && pick == null; s++) pick = modules.SlotType(s);
            if (pick != null) modules.TryEject(pick.Value);
            for (int i = 0; i < Fps * 0.5f; i++) yield return Frame();
            bot.enabled = true;
        }

        private static Enemy NearestEnemy(Vector3 position, float range)
        {
            Enemy best = null;
            float bestSqr = range * range;
            foreach (Enemy e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e == null || !e.IsAlive) continue;
                Vector3 d = e.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = e;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ recording

        private void StartRecording(string wavPath)
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _sfxBefore = GameAudio.SfxVolume;
            GameAudio.SfxVolume = RecordingSfxVolume;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps; // real time ≈ game time, so unscaled UI timing stays right
            Time.captureFramerate = Fps;

            _channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            _sampleRate = AudioSettings.outputSampleRate;
            _wav = new FileStream(wavPath, FileMode.Create);
            _wavWriter = new BinaryWriter(_wav);
            _wavWriter.Write(new byte[44]); // header written on stop
            _samplesWritten = 0;
            AudioRenderer.Start();
        }

        private void StopRecording()
        {
            if (_wavWriter != null)
            {
                AudioRenderer.Stop();
                WriteWavHeader();
                _wavWriter.Close();
                _wavWriter = null;
                _wav = null;
            }
            if (Time.captureFramerate != 0)
            {
                Time.captureFramerate = 0;
                Application.targetFrameRate = -1;
                GameAudio.SfxVolume = _sfxBefore;
                InputSystem.settings.backgroundBehavior = _background;
                InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
                if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
            if (_grab != null)
            {
                _grab.Release();
                Object.Destroy(_grab);
                _grab = null;
            }
        }

        /// <summary>Ends the frame, grabs the screen (async readback, JPG on a worker thread) and this frame's audio.</summary>
        private IEnumerator Frame()
        {
            yield return new WaitForEndOfFrame();
            if (_grab == null || _grab.width != Screen.width || _grab.height != Screen.height)
            {
                if (_grab != null) _grab.Release();
                _grab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_grab);
            int index = _frameIndex++;
            int w = _grab.width, h = _grab.height;
            string path = Path.Combine(_frames, $"f_{index:00000}.jpg");
            Interlocked.Increment(ref _pendingWrites);
            AsyncGPUReadback.Request(_grab, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError)
                {
                    Interlocked.Decrement(ref _pendingWrites);
                    return;
                }
                byte[] pixels = request.GetData<byte>().ToArray();
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        byte[] jpg = ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h, 0, JpgQuality);
                        File.WriteAllBytes(path, jpg);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingWrites);
                    }
                });
            });

            if (_wavWriter != null)
            {
                int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                if (samples > 0)
                {
                    var buffer = new NativeArray<float>(samples * _channels, Allocator.Temp);
                    AudioRenderer.Render(buffer);
                    for (int i = 0; i < buffer.Length; i++)
                        _wavWriter.Write((short)(Mathf.Clamp(buffer[i], -1f, 1f) * short.MaxValue));
                    _samplesWritten += buffer.Length;
                    buffer.Dispose();
                }
            }
        }

        private void WriteWavHeader()
        {
            long dataBytes = _samplesWritten * 2;
            _wav.Seek(0, SeekOrigin.Begin);
            _wavWriter.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            _wavWriter.Write((int)(36 + dataBytes));
            _wavWriter.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            _wavWriter.Write(16);
            _wavWriter.Write((short)1);
            _wavWriter.Write((short)_channels);
            _wavWriter.Write(_sampleRate);
            _wavWriter.Write(_sampleRate * _channels * 2);
            _wavWriter.Write((short)(_channels * 2));
            _wavWriter.Write((short)16);
            _wavWriter.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            _wavWriter.Write((int)dataBytes);
        }
    }
}
