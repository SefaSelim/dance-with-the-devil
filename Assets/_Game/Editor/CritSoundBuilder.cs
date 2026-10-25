using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Synthesizes the crit hit sound (two round-robin variations) into Assets/_Game/Audio/Sfx/crit_hit_1.wav / _2.wav
    /// and adds or replaces ONLY the "crit_hit" event in SoundBank.asset; every other event and the music stay as they
    /// are (do not use Dance/Audio/Bake All, which would overwrite the recorded sounds).
    /// Design (≈0.36 s, mono 44.1 kHz 16-bit, normalised to -1 dBFS):
    /// 0–4 ms high-passed noise click (the "crack"), a struck-bar "ting" (partials 1 : 1.007 : 2.756 : 5.404, the
    /// upper ones die first, 2.35 / 2.62 kHz fundamental per variation), a short low-passed noise snap, and a low thump
    /// (sine falling from ~150 Hz to 50 Hz, 60 ms decay) for weight; soft-clipped with tanh so it hits loud but rounds.
    /// Runtime mix (event): -3 dB (rifle sits at -17 dB after its trim), 3 voices, 0.05 s gap, ±4% pitch, Sfx bus.
    /// </summary>
    public static class CritSoundBuilder
    {
        private const int Rate = 44100;
        private const string BankPath = "Assets/_Game/Audio/SoundBank.asset";
        private const string Folder = "Assets/_Game/Audio/Sfx/";
        private const string EventName = "crit_hit";

        [MenuItem("Dance/Audio/Build Crit Sound")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before building the crit sound.");
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null) throw new System.InvalidOperationException("SoundBank missing: " + BankPath);

            var clips = new AudioClip[2];
            for (int v = 0; v < clips.Length; v++)
            {
                string path = Folder + "crit_hit_" + (v + 1) + ".wav";
                WriteWav(path, Render(v));
                ConfigureImporter(path);
                clips[v] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[v] == null) throw new System.InvalidOperationException("Import failed: " + path);
            }

            var events = new List<SoundEvent>(bank.Events ?? new SoundEvent[0]);
            var crit = new SoundEvent(EventName, clips, -3f, 3, 0.05f, 0.04f, 0.5f, AudioBus.Sfx, false, true);
            int index = events.FindIndex(e => e != null && e.Name == EventName);
            if (index >= 0) events[index] = crit;
            else events.Add(crit);
            bank.Set(events.ToArray(), bank.MusicBase, bank.MusicMelody, bank.MusicBaseDb, bank.MusicMelodyDb);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"Crit sound: crit_hit_1/2.wav written, event '{EventName}' {(index >= 0 ? "replaced" : "added")} " +
                      $"({events.Count} events, others untouched).");
        }

        private static float[] Render(int variant)
        {
            const float duration = 0.36f;
            int n = Mathf.RoundToInt(Rate * duration);
            var buffer = new float[n];
            var rng = new System.Random(7919 + variant * 131);

            float f0 = variant == 0 ? 2350f : 2620f;
            float thumpStart = variant == 0 ? 150f : 136f;
            float[] ratios = { 1f, 1.0072f, 2.756f, 5.404f };
            float[] amps = { 0.55f, 0.35f, variant == 0 ? 0.3f : 0.36f, 0.16f };
            float[] taus = { 0.11f, 0.095f, 0.045f, 0.022f };
            float[] phases = { 0f, 1.3f, 2.1f, 0.7f };

            float thumpPhase = 0f, hpIn = 0f, hpOut = 0f, lp = 0f;
            const float twoPi = Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;

                float ting = 0f;
                for (int k = 0; k < ratios.Length; k++)
                    ting += amps[k] * Mathf.Sin(twoPi * f0 * ratios[k] * t + phases[k]) * Mathf.Exp(-t / taus[k]);
                ting *= Mathf.Clamp01(t / 0.0015f);

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hpOut = 0.7f * (hpOut + noise - hpIn); // one-pole high-pass, ~3 kHz: bright click
                hpIn = noise;
                float crack = hpOut * Mathf.Exp(-t / 0.007f) * 0.9f;
                lp += 0.25f * (noise - lp);            // low-passed noise: the short "snap" body
                float snap = lp * Mathf.Exp(-t / 0.018f) * 0.55f;

                float f = 50f + (thumpStart - 50f) * Mathf.Exp(-t / 0.025f);
                thumpPhase += twoPi * f / Rate;
                float thump = Mathf.Sin(thumpPhase) * Mathf.Exp(-t / 0.06f) * Mathf.Clamp01(t / 0.002f) * 0.9f;

                buffer[i] = (float)System.Math.Tanh((ting + crack + snap + thump) * 1.4f);
            }

            int fade = Mathf.RoundToInt(Rate * 0.012f);
            for (int i = 0; i < fade; i++) buffer[n - 1 - i] *= i / (float)fade;

            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            if (peak > 1e-6f)
            {
                float gain = Mathf.Pow(10f, -1f / 20f) / peak;
                for (int i = 0; i < n; i++) buffer[i] *= gain;
            }
            return buffer;
        }

        private static void ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

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
                writer.Write((short)1);  // PCM
                writer.Write((short)1);  // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);  // byte rate
                writer.Write((short)2);  // block align
                writer.Write((short)16); // bits
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
            }
        }
    }
}
