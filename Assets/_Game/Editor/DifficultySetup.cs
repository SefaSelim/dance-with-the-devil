using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Writes the bullet-hell difficulty (production/design/crit-difficulty.md §5, user decision 2026-10-25) into an
    /// arena scene: the scene's <see cref="WaveDirector"/> gets the schedule, caps, pack size, spread and late growth,
    /// and every <see cref="EnemyPool"/> its prewarm count. The values are copied from a fresh component's code
    /// defaults, so WaveDirector.cs and EnemyPool.cs stay the single source of the numbers. Idempotent.
    /// Usage: <c>DifficultySetup.Apply(scene);</c> (the caller saves), or menu Dance/Arena/Apply Difficulty (saves).
    /// </summary>
    public static class DifficultySetup
    {
        private static readonly string[] DirectorFields =
        {
            "_schedule", "_maxActiveCap", "_windowSpread", "_maxPackSize", "_minPackSize", "_packSizePerCap", "_lateGrowth",
        };

        private static readonly string[] PoolFields = { "_prewarmCount" };

        [MenuItem("Dance/Arena/Apply Difficulty")]
        private static void ApplyToActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int changed = Apply(scene);
            if (changed > 0) EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Applies the code-default difficulty to <paramref name="scene"/>. Returns the number of components written.</summary>
        public static int Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("DifficultySetup: scene is not loaded.");
                return 0;
            }

            var temp = new GameObject("DifficultySetup_Defaults") { hideFlags = HideFlags.HideAndDontSave };
            int changed = 0;
            try
            {
                // Edit mode: no Awake/Start runs on these, they only carry the serialized defaults.
                var directorDefaults = new SerializedObject(temp.AddComponent<WaveDirector>());
                var poolDefaults = new SerializedObject(temp.AddComponent<EnemyPool>());

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (WaveDirector director in root.GetComponentsInChildren<WaveDirector>(true))
                    {
                        Copy(directorDefaults, director, DirectorFields);
                        changed++;
                    }
                    foreach (EnemyPool pool in root.GetComponentsInChildren<EnemyPool>(true))
                    {
                        Copy(poolDefaults, pool, PoolFields);
                        changed++;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            if (changed == 0) Debug.LogWarning("DifficultySetup: no WaveDirector or EnemyPool in the scene.");
            else EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"DifficultySetup: wrote the difficulty into {changed} component(s) of '{scene.name}'.");
            return changed;
        }

        private static void Copy(SerializedObject source, Object target, string[] fields)
        {
            var destination = new SerializedObject(target);
            foreach (string field in fields)
            {
                SerializedProperty property = source.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"DifficultySetup: field '{field}' not found on {target.GetType().Name}.");
                    continue;
                }
                destination.CopyFromSerializedProperty(property);
            }
            destination.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
