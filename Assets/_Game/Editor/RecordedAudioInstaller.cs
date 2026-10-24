using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>Connects the team's recorded effects without regenerating music or other SFX.</summary>
    public static class RecordedAudioInstaller
    {
        private const string Folder = "Assets/_Game/Audio/Recorded/";

        [MenuItem("Dance/Audio/Apply Recorded Sounds")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before applying recordings.");
            AssetDatabase.Refresh();
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>("Assets/_Game/Audio/SoundBank.asset");
            if (bank == null) throw new System.InvalidOperationException("SoundBank missing.");
            var events = new List<SoundEvent>(bank.Events);
            Set(events, "rocket_launch", "roket_ucus", -9f, 2, 0.08f);
            Set(events, "bullet_hit_wall", "duvar_isabet", -20f, 3, 0.04f, pan: true);
            Set(events, "machete_swing", "pala_savurma", -12f, 1, 0f);
            Set(events, "explosion", "patlama", -4f, 3, 0.04f);
            Set(events, "player_dodge", "kacinma", -12f, 1, 0.1f);
            Set(events, "dodge_ready", "kacinma_hazir", -14f, 1, 0.1f);
            Set(events, "devil_appear", "seytan_belirme", -8f, 1, 0f, unscaled: true, bus: AudioBus.Ui);
            Set(events, "defne_voice_hurt", "defne_hasar", -10f, 1, 0.15f, bus: AudioBus.Voice);
            Set(events, "spawn_warning", "düşman_belirme", -18f, 2, 0.15f, pan: true);
            Set(events, "skeleton_hit", "iskelet_isabet", -16f, 3, 0.06f, pan: true);
            Set(events, "mummy_hit", "mumya_isabet", -16f, 3, 0.06f, pan: true);
            Set(events, "skeleton_die", "iskelet_olum", -13f, 3, 0.06f, pan: true);
            Set(events, "mummy_die", "mumya_olum", -13f, 3, 0.06f, pan: true);
            Set(events, "skeleton_tell", "iskelet_saldiri_uyari", -14f, 3, 0.05f, pan: true);
            Set(events, "mummy_tell", "mumya_saldiri_uyari", -14f, 3, 0.05f, pan: true);
            Set(events, "skeleton_move", "iskelet_hareket", -22f, 2, 0.4f, pan: true);
            Set(events, "mummy_move", "mumya_hareket", -22f, 2, 0.4f, pan: true);
            bank.Set(events.ToArray(), bank.MusicBase, bank.MusicMelody, bank.MusicBaseDb, bank.MusicMelodyDb);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log("Recorded audio: 17 recordings connected; rifle and music retained.");
        }

        private static void Set(List<SoundEvent> events, string name, string file, float db, int voices,
            float gap, bool pan = false, bool unscaled = false, AudioBus bus = AudioBus.Sfx)
        {
            string path = Folder + file + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new System.InvalidOperationException("Recording missing: " + path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var replacement = new SoundEvent(name, new[] { clip }, db, voices, gap, 0.025f, 0f, bus, unscaled, pan);
            int index = events.FindIndex(e => e.Name == name);
            if (index >= 0) events[index] = replacement;
            else events.Add(replacement);
        }
    }
}
