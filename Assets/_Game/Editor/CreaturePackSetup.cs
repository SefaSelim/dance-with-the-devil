using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Menu <c>Dance/Enemies/Use Creature Pack Clips</c>: imports the Mixamo "mutant" clips in
    /// <see cref="PackFolder"/> as Humanoid (each with its own avatar, in place, loops where they loop, like
    /// CharacterBuilder does for Defne's clips), puts them in the three enemy anim sets and rebuilds the enemy prefabs
    /// (<see cref="EnemyModelBuilder.BuildAll"/>).
    /// <list type="bullet">
    /// <item>Chaser and Runner (melee): mutant idle / walking / run and the swipe's swing as the attack; the Chaser's
    /// tell is the swipe's windup (arms rising), the Runner keeps its procedural crouch. Their old rifle walking
    /// backwards and strafes are cleared (they always face where they run).</item>
    /// <item>Shooter: the breathing idle, the swipe as the orb throw; keeps the rifle-pack walk, backing off and
    /// strafes because it backs away and circles while facing Defne.</item>
    /// <item>Death: the dying clip goes in the reserved slot (still not played: enemies burst into pixels).</item>
    /// </list>
    /// The swipe take is split in two clips: the arms rising (the melee tell, sped up to the tell's length) and the
    /// swing (the attack, fired at the lunge or the Shooter's shot).
    /// </summary>
    public static class CreaturePackSetup
    {
        public const string PackFolder = "Assets/Character/Creature NPC Pack";
        private const string DataFolder = "Assets/_Game/Data";

        private static readonly string[] LoopClips =
        {
            "mutant idle", "mutant breathing idle", "mutant walking", "mutant run",
        };

        // "mutant swiping" (2.67 s), measured from the hand speed on the Chaser's body: the arms rise from about 12%
        // to 43%, the swing peaks at 45-52%, the follow-through settles by about 76%.
        private const string SwipeFile = "mutant swiping";
        private const string SwipeWindup = "mutant swipe windup";
        private const string SwipeStrike = "mutant swipe strike";
        private const float WindupStart = 0.12f;
        private const float StrikeStart = 0.43f;
        private const float StrikeEnd = 0.76f;

        [MenuItem("Dance/Enemies/Use Creature Pack Clips")]
        public static void Apply()
        {
            ConfigureImports();

            AnimationClip idle = Load("mutant idle");
            AnimationClip breathing = Load("mutant breathing idle");
            AnimationClip walk = Load("mutant walking");
            AnimationClip run = Load("mutant run");
            AnimationClip windup = Load(SwipeFile, SwipeWindup);
            AnimationClip swipe = Load(SwipeFile, SwipeStrike);
            AnimationClip dying = Load("mutant dying");
            if (idle == null || walk == null || run == null || swipe == null)
            {
                Debug.LogError("CreaturePackSetup: the mutant clips are missing from " + PackFolder + ".");
                return;
            }

            EnemyAnimSet chaser = LoadSet("chaser");
            EnemyAnimSet runner = LoadSet("runner");
            EnemyAnimSet shooter = LoadSet("shooter");

            foreach (EnemyAnimSet melee in new[] { chaser, runner })
            {
                if (melee == null) continue;
                melee.Idle = idle;
                melee.Walk = walk;
                melee.Run = run;
                melee.WalkBack = null;
                melee.RunBack = null;
                melee.StrafeLeft = null;
                melee.StrafeRight = null;
                melee.StrafeLeftRun = null;
                melee.StrafeRightRun = null;
                melee.Attack = swipe;
                melee.Death = dying;
                EditorUtility.SetDirty(melee);
            }
            // Chaser: the windup (0.83 s) raises its arms over the 0.45 s tell (GDD: the Chaser opens its arms), the
            // swing peaks within the 0.12 s lunge. The Runner keeps its procedural crouch as the tell (a different
            // read on purpose) and only swings.
            if (chaser != null) { chaser.Tell = windup; chaser.TellSpeed = 1.85f; chaser.AttackSpeed = 1.6f; }
            if (runner != null) { runner.Tell = null; runner.AttackSpeed = 2f; }
            // The mutant run covers only ~1.8 m/s at 1×: play it faster so the feet slide less at the Chaser's 4.2 and
            // the Runner's 7.5 m/s (capped so the stride does not look frantic).
            if (chaser != null) chaser.PlaybackSpeed = 1.5f;
            if (runner != null) runner.PlaybackSpeed = 1.9f;

            if (shooter != null)
            {
                shooter.Idle = breathing != null ? breathing : idle;
                shooter.Attack = swipe;
                shooter.AttackSpeed = 1.6f;
                shooter.Death = dying;
                EditorUtility.SetDirty(shooter);
            }

            AssetDatabase.SaveAssets();
            EnemyModelBuilder.BuildAll();
            Debug.Log("CreaturePackSetup: the Creature NPC Pack clips are on the enemies.");
        }

        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PackFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                string clipName = Path.GetFileNameWithoutExtension(path);
                bool loop = System.Array.IndexOf(LoopClips, clipName) >= 0;

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                if (defaults == null || defaults.Length == 0) continue;
                ModelImporterClipAnimation[] clips;
                if (clipName == SwipeFile)
                {
                    // One take, two clips (from the file's full take each time, so running the menu again does not
                    // trim twice): the arms rising for the tell and the swing for the attack.
                    clips = new[]
                    {
                        Trimmed(defaults[0], SwipeWindup, WindupStart, StrikeStart, false),
                        Trimmed(defaults[0], SwipeStrike, StrikeStart, StrikeEnd, false),
                    };
                }
                else
                {
                    clips = new[] { Trimmed(defaults[0], clipName, 0f, 1f, loop) };
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static ModelImporterClipAnimation Trimmed(ModelImporterClipAnimation take, string name, float from,
            float to, bool loop)
        {
            float first = take.firstFrame, length = take.lastFrame - take.firstFrame;
            // A new clip each time: ModelImporterClipAnimation is a class, and both swipe clips come from one take.
            var clip = new ModelImporterClipAnimation { takeName = take.takeName, name = name };
            clip.firstFrame = Mathf.Round(first + length * from);
            clip.lastFrame = Mathf.Round(first + length * to);
            clip.loopTime = loop;
            clip.loopPose = loop;
            // In place: the NavMeshAgent moves the enemy (root motion off), the clips only animate it.
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = false;
            clip.heightFromFeet = true;
            clip.keepOriginalPositionXZ = false;
            return clip;
        }

        private static EnemyAnimSet LoadSet(string key)
        {
            var set = AssetDatabase.LoadAssetAtPath<EnemyAnimSet>($"{DataFolder}/anim_enemy_{key}.asset");
            if (set == null) Debug.LogWarning($"CreaturePackSetup: anim_enemy_{key}.asset not found (run Build Enemy Models first).");
            return set;
        }

        private static AnimationClip Load(string file, string clipName = null)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"{PackFolder}/{file}.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")
                    && (clipName == null || clip.name == clipName)) return clip;
            Debug.LogWarning($"CreaturePackSetup: no clip {clipName ?? file} in {PackFolder}/{file}.fbx.");
            return null;
        }
    }
}
