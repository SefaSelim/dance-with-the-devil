using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Phase 6 audio layer: voice limit and min gap per event, contract 2 (K-8) muting the Voice bus and fading the
    /// melody stem on unscaled time. Uses an in-memory SoundBank with tiny generated clips (no baked assets).
    /// </summary>
    public sealed class AudioTests
    {
        private GameObject _go;
        private SoundBank _bank;
        private GameAudio _audio;

        [SetUp]
        public void SetUp()
        {
            AudioCosts.Reset();
            Time.timeScale = 1f;
            AudioClip sfx = MakeClip("test_sfx", 44100);       // 1 s, stays "sounding" through the test
            AudioClip music = MakeClip("test_music", 44100 * 2);
            _bank = ScriptableObject.CreateInstance<SoundBank>();
            _bank.Set(new[]
            {
                new SoundEvent("rifle_shot", new[] { sfx }, -14f, 3, 0.05f, 0.05f, 1.5f, AudioBus.Sfx, false, false),
                new SoundEvent("defne_voice_hurt", new[] { sfx }, -10f, 1, 0f, 0f, 0f, AudioBus.Voice, false, false),
                new SoundEvent("ui_click", new[] { sfx }, -10f, 2, 0f, 0f, 0f, AudioBus.Ui, true, false),
            }, music, music);

            _go = new GameObject("GameAudio");
            _audio = _go.AddComponent<GameAudio>();
            _audio.Initialize(_bank);
        }

        [TearDown]
        public void TearDown()
        {
            AudioCosts.Reset();
            if (_go != null) Object.Destroy(_go);
            if (_bank != null) Object.Destroy(_bank);
        }

        private static AudioClip MakeClip(string name, int samples)
        {
            AudioClip clip = AudioClip.Create(name, samples, 1, 44100, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(i * 0.05f) * 0.2f;
            clip.SetData(data, 0);
            return clip;
        }

        [UnityTest]
        public IEnumerator RifleShot_RespectsMinGapAndMaxVoices()
        {
            Assert.AreNotEqual(0, _audio.Play("rifle_shot"), "first shot plays");
            Assert.AreEqual(0, _audio.Play("rifle_shot"), "second shot inside the 50 ms gap is dropped");
            Assert.AreEqual(1, _audio.ActiveVoiceCount("rifle_shot"));

            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.07f);
                Assert.AreNotEqual(0, _audio.Play("rifle_shot"), "shot after the gap plays (oldest voice stolen when full)");
            }
            Assert.AreEqual(3, _audio.ActiveVoiceCount("rifle_shot"), "never more than 3 voices");
        }

        [UnityTest]
        public IEnumerator ContractTwo_MutesVoiceBus_AndFadesMelody()
        {
            yield return null;
            Assert.IsFalse(_audio.VoiceBusMuted);
            Assert.AreNotEqual(0, _audio.Play("defne_voice_hurt"));
            Assert.Greater(_audio.MelodyFade, 0.99f);
            Assert.Greater(_audio.MusicMelodySource.volume, 0f);

            AudioCosts.VoiceMuted = true;
            AudioCosts.MusicLayerMuted = true;
            yield return null;
            Assert.IsTrue(_audio.VoiceBusMuted, "Voice bus muted at once");
            Assert.AreEqual(0, _audio.Play("defne_voice_hurt"), "voice sounds are dropped while muted");
            Assert.Greater(_audio.MelodyFade, 0f, "melody fades, it does not cut");

            yield return new WaitForSecondsRealtime(1.6f);
            yield return null;
            Assert.AreEqual(0f, _audio.MelodyFade, 1e-4f, "melody stem faded out within ~1.6 s unscaled");
            Assert.AreEqual(0f, _audio.MusicMelodySource.volume, 1e-4f);
            Assert.Greater(_audio.MusicBaseSource.volume, 0f, "base stem keeps playing");
        }
    
        [UnityTest]
        public IEnumerator EndRun_StopsGameplaySounds_FadesMusic_KeepsUi()
        {
            yield return null;
            Assert.AreNotEqual(0, _audio.Play("rifle_shot"));
            Assert.AreEqual(1, _audio.ActiveVoiceCount("rifle_shot"));
            Assert.Greater(_audio.MusicBaseSource.volume, 0f);

            _audio.EndRun(0.5f);
            Assert.IsTrue(_audio.RunEnded);
            Assert.AreEqual(0, _audio.ActiveVoiceCount("rifle_shot"), "gameplay voices stop at once");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0, _audio.Play("rifle_shot"), "no gameplay sound after the ending");
            Assert.AreNotEqual(0, _audio.Play("ui_click"), "ending sting and clicks still play");

            yield return new WaitForSecondsRealtime(0.6f);
            yield return null;
            Assert.AreEqual(0f, _audio.MusicBaseSource.volume, 1e-4f, "music faded out");
            Assert.AreEqual(0f, _audio.MusicMelodySource.volume, 1e-4f);
        }
    }
}
