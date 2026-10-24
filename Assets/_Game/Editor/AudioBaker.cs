using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Renders every Phase 6 sound (procedural audio spec §1, §2) to 16-bit / 44.1 kHz / mono WAV, sets the import
    /// settings and writes Assets/_Game/Audio/SoundBank.asset. Nothing is downloaded; renders are reproducible
    /// (fixed noise seeds). Menu: Dance/Audio/Bake All.
    /// </summary>
    public static class AudioBaker
    {
        private const int Rate = 44100;
        private const string SfxFolder = "Assets/_Game/Audio/Sfx";
        private const string MusicFolder = "Assets/_Game/Audio/Music";
        private const string BankPath = "Assets/_Game/Audio/SoundBank.asset";

        private enum W { Sin, Tri, Saw, Sqr, Noise, SNoise }

        /// <summary>One synth layer. Times in ms; 0 for End = until the end of the sound.</summary>
        private sealed class L
        {
            public W Wave;
            public float Duty = 0.5f, F0 = 440f, F1 = -1f, SweepMs, Weight = 1f;
            public bool Lin;
            public float A, D, S, R, RelAt = -1f;
            public float Lp0, Lp1 = -1f;
            public bool LpLin;
            public float TremR, TremD, VibR, VibP, VibDelay;
            public float Start, End;
            public float GlideAt = -1f, GlideRatio = 1f;
            public float GateHz, Amp0 = 1f, Amp1 = 1f, SnoiseRate = 6000f;
        }

        private sealed class Sfx
        {
            public string File;
            public float Dur;
            public int Bits = 8, Ds = 1, Seed;
            public L[] Layers;
        }

        // ------------------------------------------------------------------ entry point

        [MenuItem("Dance/Audio/Bake All")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(SfxFolder);
            Directory.CreateDirectory(MusicFolder);

            var clipPaths = new Dictionary<string, string>();
            List<Sfx> sounds = BuildSounds();
            foreach (Sfx sfx in sounds)
            {
                float[] data = RenderSfx(sfx);
                string path = SfxFolder + "/" + sfx.File + ".wav";
                WriteWav(path, data);
                clipPaths[sfx.File] = path;
            }

            string basePath = MusicFolder + "/music_base.wav";
            string melodyPath = MusicFolder + "/music_melody.wav";
            WriteWav(basePath, RenderMusicBase());
            WriteWav(melodyPath, RenderMusicMelody());

            AssetDatabase.Refresh();
            foreach (string path in clipPaths.Values) ConfigureImporter(path, false);
            ConfigureImporter(basePath, true);
            ConfigureImporter(melodyPath, true);

            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            bank.Set(BuildEvents(), Load(basePath), Load(melodyPath), -14f, -17f);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"AudioBaker: {sounds.Count} SFX + 2 music stems baked, bank at {BankPath}.");
        }

        private static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        private static void ConfigureImporter(string path, bool music)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = true;
            importer.loadInBackground = music;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // SFX: decompressed PCM (instant, exact). Music: ADPCM compressed in memory (no encoder padding, loops clean).
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ bank table (spec §1 Vol / Voices / Gap / Rand / U)

        private static SoundEvent[] BuildEvents()
        {
            var list = new List<SoundEvent>
            {
                E("rifle_shot", -14f, 3, 0.05f, 0.05f, 1.5f, AudioBus.Sfx, false, false),
                E("rocket_launch", -9f, 2, 0.08f, 0.04f, 0f, AudioBus.Sfx, false, false),
                E("explosion", -4f, 3, 0.04f, 0.06f, 0f, AudioBus.Sfx, false, false),
                E("machete_swing", -12f, 1, 0f, 0.06f, 0f, AudioBus.Sfx, false, false),
                E("machete_hit", -8f, 2, 0f, 0.05f, 0f, AudioBus.Sfx, false, false),
                E("bullet_hit_enemy", -16f, 3, 0.03f, 0.08f, 0f, AudioBus.Sfx, false, true),
                E("bullet_hit_wall", -20f, 2, 0.04f, 0.08f, 0f, AudioBus.Sfx, false, true),
                E("echo_bounce", -12f, 2, 0.06f, 0.03f, 0f, AudioBus.Sfx, false, false),
                E("enemy_shot", -13f, 3, 0.06f, 0.05f, 0f, AudioBus.Sfx, false, true),
                E("enemy_tell_chaser", -14f, 2, 0.12f, 0.03f, 0f, AudioBus.Sfx, false, true),
                E("enemy_tell_shooter", -12f, 2, 0.15f, 0f, 0f, AudioBus.Sfx, false, true),
                E("spawn_warning", -15f, 2, 0.25f, 0f, 0f, AudioBus.Sfx, false, true),
                E("enemy_die", -10f, 4, 0.035f, 0.08f, 0f, AudioBus.Sfx, false, true),
                E("player_hurt", -7f, 1, 0.6f, 0.03f, 0f, AudioBus.Sfx, false, false),
                E("player_dodge", -13f, 1, 0f, 0.04f, 0f, AudioBus.Sfx, false, false),
                E("dodge_ready", -18f, 1, 0f, 0f, 0f, AudioBus.Sfx, false, false),
                E("defne_voice_hurt", -10f, 1, 0.6f, 0.04f, 0f, AudioBus.Voice, false, false,
                    "defne_voice_hurt_1", "defne_voice_hurt_2", "defne_voice_hurt_3"),
                E("module_offer_appear", -16f, 1, 0f, 0f, 0f, AudioBus.Ui, false, false),
                E("module_accept", -10f, 1, 0f, 0f, 0f, AudioBus.Ui, false, false),
                E("module_reject_throw", -10f, 1, 0.65f, 0f, 0f, AudioBus.Ui, false, false),
                E("module_eject", -10f, 1, 0.65f, 0f, 0f, AudioBus.Ui, false, false),
                E("devil_appear", -8f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("contract_yes", -9f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("contract_no", -11f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("revive_shockwave", -5f, 1, 0f, 0f, 0f, AudioBus.Sfx, false, false),
                E("ending_sting_no_first", -8f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("ending_sting_no_late", -8f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("ending_sting_all_yes", -8f, 1, 0f, 0f, 0f, AudioBus.Ui, true, false),
                E("ui_click", -16f, 2, 0.04f, 0.02f, 0f, AudioBus.Ui, true, false),
            };
            return list.ToArray();
        }

        private static SoundEvent E(string name, float db, int voices, float gap, float pitchJitter, float volJitterDb,
            AudioBus bus, bool unscaled, bool pan, params string[] files)
        {
            if (files == null || files.Length == 0) files = new[] { name };
            var clips = new AudioClip[files.Length];
            for (int i = 0; i < files.Length; i++) clips[i] = Load(SfxFolder + "/" + files[i] + ".wav");
            return new SoundEvent(name, clips, db, voices, gap, pitchJitter, volJitterDb, bus, unscaled, pan);
        }

        // ------------------------------------------------------------------ SFX recipes (spec §1)

        private static List<Sfx> BuildSounds()
        {
            var s = new List<Sfx>();
            int seed = 100;
            void Add(string file, float dur, int bits, int ds, params L[] layers) =>
                s.Add(new Sfx { File = file, Dur = dur, Bits = bits, Ds = ds, Seed = seed++, Layers = layers });

            Add("rifle_shot", 70, 8, 2,
                new L { Wave = W.Sqr, Duty = 0.25f, F0 = 1200, F1 = 500, A = 1, D = 60 },
                new L { Wave = W.Noise, Weight = 0.3f, Lp0 = 4000, A = 0, D = 25 });
            Add("rocket_launch", 350, 8, 2,
                new L { Wave = W.Noise, Lp0 = 800, Lp1 = 3000, A = 10, D = 340 },
                new L { Wave = W.Saw, Weight = 0.4f, F0 = 180, F1 = 90, A = 5, D = 200 });
            Add("explosion", 900, 6, 3,
                new L { Wave = W.Noise, Lp0 = 2500, Lp1 = 200, A = 2, D = 880 },
                new L { Wave = W.Sin, Weight = 0.7f, F0 = 90, F1 = 35, A = 1, D = 400 });
            Add("machete_swing", 160, 8, 1,
                new L { Wave = W.Noise, Lp0 = 1000, Lp1 = 3000, LpLin = true, A = 40, D = 120 },
                new L { Wave = W.Sin, Weight = 0.15f, F0 = 300, F1 = 500, A = 40, D = 120 });
            Add("machete_hit", 180, 6, 2,
                new L { Wave = W.Sqr, Weight = 0.6f, F0 = 220, F1 = 110, A = 1, D = 120 },
                new L { Wave = W.Noise, Weight = 0.4f, Lp0 = 3500, A = 0, D = 80 });
            Add("bullet_hit_enemy", 60, 8, 2,
                new L { Wave = W.Sin, F0 = 700, F1 = 350, A = 0, D = 50 },
                new L { Wave = W.Noise, Weight = 0.5f, Lp0 = 2500, A = 0, D = 30 });
            Add("bullet_hit_wall", 50, 8, 3,
                new L { Wave = W.Noise, Lp0 = 6000, A = 0, D = 35 },
                new L { Wave = W.Sqr, Weight = 0.3f, F0 = 2400, A = 0, D = 15 });
            Add("echo_bounce", 260, 12, 1,
                new L { Wave = W.Sin, F0 = 1760, F1 = 1800, A = 1, D = 250 },
                new L { Wave = W.Sin, Weight = 0.6f, F0 = 2637, A = 1, D = 250 },
                new L { Wave = W.Sin, Weight = 0.3f, F0 = 4400, A = 1, D = 250 });
            Add("enemy_shot", 140, 6, 2,
                new L { Wave = W.Saw, F0 = 520, F1 = 260, A = 2, D = 130, Lp0 = 2000 },
                new L { Wave = W.Noise, Weight = 0.2f, Lp0 = 2000, A = 0, D = 40 });
            Add("enemy_tell_chaser", 450, 6, 2,
                new L { Wave = W.Sqr, F0 = 300, F1 = 700, Lin = true, SweepMs = 400, A = 10, S = 1, R = 40, Lp0 = 2500 });
            Add("enemy_tell_shooter", 800, 6, 2,
                new L { Wave = W.Saw, F0 = 400, F1 = 900, Lin = true, Start = 0, End = 550, A = 0, S = 1, Amp0 = 0.4f, Amp1 = 0.8f, Lp0 = 2200 },
                new L { Wave = W.Sqr, F0 = 900, Start = 550, End = 800, A = 0, S = 1, R = 20, GateHz = 16, Weight = 0.9f });
            Add("spawn_warning", 800, 8, 2,
                new L { Wave = W.Tri, F0 = 110, TremR = 8, TremD = 0.8f, A = 50, S = 1, R = 100 },
                new L { Wave = W.Sin, Weight = 0.5f, F0 = 220, F1 = 330, Lin = true, A = 50, S = 1, R = 100 });
            Add("enemy_die", 220, 5, 3,
                new L { Wave = W.Noise, Lp0 = 3000, Lp1 = 400, A = 0, D = 200 },
                new L { Wave = W.Sqr, Weight = 0.5f, F0 = 330, F1 = 80, A = 0, D = 180 });
            Add("player_hurt", 200, 6, 2,
                new L { Wave = W.Sqr, F0 = 180, F1 = 90, A = 1, D = 190 },
                new L { Wave = W.Noise, Weight = 0.3f, Lp0 = 1500, A = 0, D = 80 });
            Add("player_dodge", 180, 8, 2,
                new L { Wave = W.Noise, Lp0 = 400, Lp1 = 2500, A = 30, D = 150 },
                new L { Wave = W.Sin, Weight = 0.2f, F0 = 200, F1 = 400, A = 30, D = 150 });
            Add("dodge_ready", 60, 8, 1,
                new L { Wave = W.Sin, F0 = 1320, A = 1, D = 55 });
            float[] grunts = { 200f, 220f, 185f };
            for (int i = 0; i < grunts.Length; i++)
            {
                float b = grunts[i];
                Add("defne_voice_hurt_" + (i + 1), 230, 8, 2,
                    new L { Wave = W.Saw, F0 = b * 1.05f, F1 = b * 0.75f, VibR = 6, VibP = 0.03f, Lp0 = 900, A = 15, D = 200 },
                    new L { Wave = W.Noise, Weight = 0.15f, Lp0 = 900, A = 15, D = 200 });
            }
            Add("module_offer_appear", 600, 8, 2,
                new L { Wave = W.Noise, Lp0 = 1800, TremR = 11, TremD = 0.7f, A = 150, D = 450 },
                new L { Wave = W.Sin, Weight = 0.15f, F0 = 880, F1 = 660, A = 150, D = 450 });
            Add("module_accept", 300, 8, 1,
                new L { Wave = W.Sqr, F0 = 523.25f, Start = 0, End = 60, A = 1, D = 55 },
                new L { Wave = W.Sqr, F0 = 783.99f, Start = 80, End = 140, A = 1, D = 55 },
                new L { Wave = W.Sin, F0 = 1046.5f, Start = 140, End = 300, A = 1, D = 150 });
            Add("module_reject_throw", 300, 8, 1,
                new L { Wave = W.Sqr, F0 = 783.99f, Start = 0, End = 60, A = 1, D = 55 },
                new L { Wave = W.Sqr, F0 = 523.25f, Start = 80, End = 140, A = 1, D = 55 },
                new L { Wave = W.Noise, Lp0 = 1500, Start = 140, End = 300, A = 0, D = 120 });
            Add("module_eject", 350, 4, 2,
                new L { Wave = W.Saw, F0 = 600, F1 = 150, A = 2, D = 330 },
                new L { Wave = W.Noise, Weight = 0.3f, Lp0 = 1200, Lp1 = 3000, A = 0, D = 100 });
            Add("devil_appear", 1400, 8, 2,
                new L { Wave = W.Saw, F0 = 55f, Lp0 = 200, Lp1 = 1200, A = 300, D = 200, S = 0.8f, R = 600 },
                new L { Wave = W.Saw, F0 = 58.27f, Lp0 = 200, Lp1 = 1200, A = 300, D = 200, S = 0.8f, R = 600 },
                new L { Wave = W.Noise, Weight = 0.25f, Lp0 = 600, A = 900, D = 500 });
            Add("contract_yes", 700, 8, 1,
                new L { Wave = W.Tri, F0 = 293.66f, A = 5, D = 600, GlideAt = 300, GlideRatio = 0.944f },
                new L { Wave = W.Tri, F0 = 369.99f, A = 5, D = 600, GlideAt = 300, GlideRatio = 0.944f },
                new L { Wave = W.Tri, F0 = 440f, A = 5, D = 600, GlideAt = 300, GlideRatio = 0.944f });
            Add("contract_no", 450, 16, 1,
                new L { Wave = W.Sin, F0 = 146.83f, A = 5, D = 440 },
                new L { Wave = W.Sin, Weight = 0.3f, F0 = 293.66f, A = 5, D = 440 });
            Add("revive_shockwave", 800, 6, 2,
                new L { Wave = W.Sin, F0 = 60, F1 = 30, A = 5, D = 700 },
                new L { Wave = W.Noise, Weight = 0.6f, Lp0 = 4000, Lp1 = 300, A = 0, D = 600 },
                new L { Wave = W.Sqr, Weight = 0.2f, F0 = 220, F1 = 880, SweepMs = 150, A = 0, D = 150 });
            Add("ending_sting_no_first", 2500, 8, 2, Arpeggio(369.99f, 1f));
            Add("ending_sting_no_late", 2500, 8, 2, Arpeggio(349.23f, 1f));
            var allYes = new List<L>(Arpeggio(349.23f, 0.983f))
            {
                new L { Wave = W.Saw, Weight = 0.3f, F0 = 36.7f, A = 400, D = 2000 },
            };
            Add("ending_sting_all_yes", 2500, 4, 2, allYes.ToArray());
            Add("ui_click", 30, 8, 1,
                new L { Wave = W.Sqr, F0 = 1000, A = 0, D = 25 });
            return s;
        }

        private static L[] Arpeggio(float x, float detune)
        {
            float[] notes = { 146.83f, 220f, 293.66f, x };
            var layers = new L[notes.Length];
            for (int i = 0; i < notes.Length; i++)
                layers[i] = new L { Wave = W.Tri, F0 = notes[i] * detune, Start = i * 300, A = 5, D = 1500, Lp0 = 2000 };
            return layers;
        }

        // ------------------------------------------------------------------ synthesis

        private static float[] RenderSfx(Sfx sfx)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(sfx.Dur * 0.001f * Rate));
            var buffer = new float[n];
            for (int i = 0; i < sfx.Layers.Length; i++)
            {
                L layer = sfx.Layers[i];
                float end = layer.End > 0f ? layer.End : sfx.Dur;
                float[] part = RenderLayer(layer, end - layer.Start, new System.Random(sfx.Seed * 31 + i));
                int offset = Mathf.RoundToInt(layer.Start * 0.001f * Rate);
                for (int k = 0; k < part.Length && offset + k < n; k++) buffer[offset + k] += part[k];
            }
            Crush(buffer, sfx.Bits, sfx.Ds);
            Normalize(buffer);
            int fade = Mathf.Min(n, Rate * 2 / 1000); // 2 ms click guard
            for (int k = 0; k < fade; k++) buffer[n - 1 - k] *= k / (float)fade;
            return buffer;
        }

        /// <summary>Renders one layer of <paramref name="lengthMs"/> (already weighted, filtered, enveloped).</summary>
        private static float[] RenderLayer(L l, float lengthMs, System.Random rng)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(lengthMs * 0.001f * Rate));
            var output = new float[n];
            float lengthS = lengthMs * 0.001f;
            float sweepS = l.SweepMs > 0f ? l.SweepMs * 0.001f : lengthS;
            float f1 = l.F1 > 0f ? l.F1 : l.F0;
            float lp1 = l.Lp1 > 0f ? l.Lp1 : l.Lp0;
            float relAt = l.R > 0f ? (l.RelAt >= 0f ? l.RelAt : lengthMs - l.R) : float.MaxValue;
            double phase = 0.0;
            float lpState = 0f, held = 0f;
            int holdEvery = Mathf.Max(1, Mathf.RoundToInt(Rate / Mathf.Max(1f, l.SnoiseRate)));

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float tMs = t * 1000f;

                // Pitch: sweep (exp or lin), glide, vibrato.
                float u = Mathf.Clamp01(t / sweepS);
                float f = l.Lin ? Mathf.Lerp(l.F0, f1, u) : l.F0 * Mathf.Pow(f1 / l.F0, u);
                if (l.GlideAt >= 0f && tMs > l.GlideAt)
                    f *= Mathf.Pow(l.GlideRatio, Mathf.Clamp01((tMs - l.GlideAt) / Mathf.Max(1f, lengthMs - l.GlideAt)));
                if (l.VibR > 0f && tMs >= l.VibDelay) f *= 1f + l.VibP * Mathf.Sin(2f * Mathf.PI * l.VibR * t);
                phase += f / Rate;
                phase -= Math.Floor(phase);
                float p = (float)phase;

                float x;
                switch (l.Wave)
                {
                    case W.Sin: x = Mathf.Sin(2f * Mathf.PI * p); break;
                    case W.Tri: x = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                    case W.Saw: x = 2f * p - 1f; break;
                    case W.Sqr: x = p < l.Duty ? 1f : -1f; break;
                    case W.Noise: x = (float)(rng.NextDouble() * 2.0 - 1.0); break;
                    default:
                        if (i % holdEvery == 0) held = (float)(rng.NextDouble() * 2.0 - 1.0);
                        x = held;
                        break;
                }

                if (l.Lp0 > 0f)
                {
                    float c = l.LpLin ? Mathf.Lerp(l.Lp0, lp1, t / lengthS) : l.Lp0 * Mathf.Pow(lp1 / l.Lp0, Mathf.Clamp01(t / lengthS));
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * c / Rate);
                    lpState += a * (x - lpState);
                    x = lpState;
                }

                float amp = Envelope(l, tMs, relAt);
                amp *= Mathf.Lerp(l.Amp0, l.Amp1, t / lengthS);
                if (l.TremR > 0f) amp *= 1f - l.TremD * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * l.TremR * t));
                if (l.GateHz > 0f && (t * l.GateHz) % 1f >= 0.5f) amp = 0f;
                output[i] = x * amp * l.Weight;
            }
            return output;
        }

        /// <summary>Linear attack, exponential decay toward S (−5 time constants at D), linear release from relAt.</summary>
        private static float Envelope(L l, float tMs, float relAt)
        {
            float level;
            if (l.A > 0f && tMs < l.A) level = tMs / l.A;
            else if (l.D > 0f) level = l.S + (1f - l.S) * Mathf.Exp(-5f * (tMs - l.A) / l.D);
            else level = l.S > 0f ? l.S : (tMs <= l.A ? 1f : 0f);
            if (l.A <= 0f && l.D <= 0f && l.S <= 0f) level = 1f;
            if (tMs >= relAt) level *= Mathf.Clamp01(1f - (tMs - relAt) / l.R);
            return level;
        }

        private static void Crush(float[] buffer, int bits, int ds)
        {
            if (bits < 16)
            {
                float steps = Mathf.Pow(2f, bits - 1);
                for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Round(buffer[i] * steps) / steps;
            }
            if (ds > 1)
                for (int i = 0; i < buffer.Length; i++) buffer[i] = buffer[i - i % ds];
        }

        private static void Normalize(float[] buffer)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            if (peak <= 1e-6f) return;
            float gain = Mathf.Pow(10f, -1f / 20f) / peak;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
        }

        // ------------------------------------------------------------------ music (spec §2)

        private const int LoopSamples = 940800;   // 8 bars at 90 BPM
        private const int Sixteenth = 7350;
        private const float LoopSeconds = LoopSamples / (float)Rate;

        private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
        private static float Snap(float f) => Mathf.Max(1f, Mathf.Round(f * LoopSeconds)) / LoopSeconds;

        /// <summary>Adds a rendered layer at <paramref name="start"/>, wrapping at the loop length.</summary>
        private static void AddWrapped(float[] loop, float[] part, int start, float gain)
        {
            for (int k = 0; k < part.Length; k++) loop[(start + k) % LoopSamples] += part[k] * gain;
        }

        private static float[] RenderMusicBase()
        {
            var loop = new float[LoopSamples];
            var rng = new System.Random(7);
            float loopMs = LoopSeconds * 1000f;

            // Drone: rendered over two loops, second kept (filter warmed up); snapped frequencies loop seamlessly.
            L[] drone =
            {
                new L { Wave = W.Saw, F0 = Snap(73.42f), Lp0 = 380, S = 1, TremR = 0.1875f, TremD = 0.3f, Weight = 0.35f },
                new L { Wave = W.Saw, F0 = Snap(73.42f * 1.004f), Lp0 = 380, S = 1, TremR = 0.1875f, TremD = 0.3f, Weight = 0.35f },
                new L { Wave = W.Sin, F0 = Snap(110f), Lp0 = 380, S = 1, TremR = 0.1875f, TremD = 0.3f, Weight = 0.175f },
            };
            foreach (L layer in drone)
            {
                float[] part = RenderLayer(layer, loopMs * 2f + 20f, rng); // + margin: never shorter than 2 loops
                for (int i = 0; i < LoopSamples; i++) loop[i] += part[LoopSamples + i];
            }

            int[] roots = { 38, 38, 34, 34, 31, 31, 33, 33 };
            float sixteenthMs = Sixteenth * 1000f / Rate;
            for (int bar = 0; bar < 8; bar++)
            {
                int barStart = bar * 16 * Sixteenth;
                for (int step = 0; step < 16; step += 2)
                {
                    float accent = step == 0 || step == 8 ? 1.3f : 1f;
                    var bass = new L { Wave = W.Sqr, F0 = Midi(roots[bar]), Lp0 = 500, A = 3, S = 0.7f, R = 60, RelAt = 2 * sixteenthMs };
                    AddWrapped(loop, RenderLayer(bass, 2 * sixteenthMs + 60f, rng), barStart + step * Sixteenth, 0.5f * accent);
                }

                AddHeart(loop, rng, barStart, 1f);
                AddHeart(loop, rng, barStart + 3 * Sixteenth, 0.6f);
                if (bar == 7) AddHeart(loop, rng, barStart + 12 * Sixteenth, 0.5f);

                for (int k = 0; k < 2; k++)
                {
                    var tick = new L { Wave = W.SNoise, SnoiseRate = 6000f, Lp0 = 5000, A = 0, D = 25 };
                    AddWrapped(loop, RenderLayer(tick, 60f, rng), barStart + (k == 0 ? 4 : 12) * Sixteenth, 0.12f);
                }
            }

            Crush(loop, 10, 2);
            Normalize(loop);
            return loop;
        }

        private static void AddHeart(float[] loop, System.Random rng, int start, float gain)
        {
            var thump = new L { Wave = W.Sin, F0 = 110, F1 = 45, SweepMs = 90, A = 1, D = 140 };
            var noise = new L { Wave = W.Noise, Weight = 0.1f, Lp0 = 300, A = 0, D = 20 };
            AddWrapped(loop, RenderLayer(thump, 200f, rng), start, 0.8f * gain);
            AddWrapped(loop, RenderLayer(noise, 60f, rng), start, 0.8f * gain);
        }

        private static float[] RenderMusicMelody()
        {
            // (bar, step, midi, length in 16ths), spec §2.2.
            int[,] notes =
            {
                { 0, 0, 69, 4 }, { 0, 6, 65, 2 }, { 0, 8, 64, 2 }, { 0, 10, 62, 6 },
                { 1, 4, 65, 2 }, { 1, 6, 67, 2 }, { 1, 8, 69, 8 },
                { 2, 0, 70, 4 }, { 2, 6, 69, 2 }, { 2, 8, 65, 2 }, { 2, 10, 62, 6 },
                { 3, 4, 74, 4 }, { 3, 8, 72, 2 }, { 3, 10, 70, 6 },
                { 4, 0, 67, 4 }, { 4, 6, 70, 2 }, { 4, 8, 74, 4 }, { 4, 12, 70, 4 },
                { 5, 0, 69, 8 }, { 5, 10, 67, 2 }, { 5, 12, 65, 4 },
                { 6, 0, 64, 4 }, { 6, 6, 73, 2 }, { 6, 8, 76, 4 }, { 6, 12, 74, 2 }, { 6, 14, 73, 2 },
                { 7, 0, 69, 8 }, { 7, 12, 61, 2 }, { 7, 14, 64, 2 },
            };
            var dry = new float[LoopSamples];
            var rng = new System.Random(11);
            float sixteenthMs = Sixteenth * 1000f / Rate;
            for (int i = 0; i < notes.GetLength(0); i++)
            {
                int start = (notes[i, 0] * 16 + notes[i, 1]) * Sixteenth;
                float f = Midi(notes[i, 2]);
                float noteMs = notes[i, 3] * sixteenthMs;
                float total = noteMs + 180f;
                var tri = new L { Wave = W.Tri, F0 = f, VibR = 5, VibP = 0.006f, VibDelay = 150, A = 4, D = 250, S = 0.35f, R = 180, RelAt = noteMs, Lp0 = 3200 };
                var bite = new L { Wave = W.Sqr, Duty = 0.125f, Weight = 0.25f, F0 = f, VibR = 5, VibP = 0.006f, VibDelay = 150, A = 4, D = 250, S = 0.35f, R = 180, RelAt = noteMs, Lp0 = 3200 };
                AddWrapped(dry, RenderLayer(tri, total, rng), start, 1f);
                AddWrapped(dry, RenderLayer(bite, total, rng), start, 1f);
            }

            // Feedback echo (delay 3 sixteenths, fb 0.35, wet 0.3), circular so the loop seam carries the tail.
            const int delay = 3 * Sixteenth;
            var echo = new float[LoopSamples];
            for (int i = 0; i < LoopSamples * 4; i++)
            {
                int idx = i % LoopSamples;
                int src = (idx - delay + LoopSamples) % LoopSamples;
                echo[idx] = dry[src] + 0.35f * echo[src];
            }
            for (int i = 0; i < LoopSamples; i++) dry[i] += 0.3f * echo[i];

            Crush(dry, 8, 2);
            Normalize(dry);
            return dry;
        }

        // ------------------------------------------------------------------ WAV

        private static void WriteWav(string assetPath, float[] samples)
        {
            string full = Path.GetFullPath(assetPath);
            using (var stream = new FileStream(full, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);       // PCM
                writer.Write((short)1);       // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);       // byte rate
                writer.Write((short)2);       // block align
                writer.Write((short)16);      // bits
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
            }
        }
    }
}
