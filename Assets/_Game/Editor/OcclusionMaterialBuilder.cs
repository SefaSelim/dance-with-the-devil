using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Menu <c>Dance/Arena/Build Occlusion Materials</c>: for every URP Lit material on the open scene's scenery that
    /// <see cref="OcclusionFader"/> may cut (<see cref="OcclusionFader.IsCandidate"/>), writes a "Dance/Lit Occlusion"
    /// copy to Art/Materials/Occlusion (same properties and keywords) and records the pair in
    /// Resources/OcclusionMaterials, so builds (WebGL) carry the copies and their shader variants. Re-run after the
    /// map's materials change. Existing copies are refreshed in place (same GUIDs).
    /// </summary>
    public static class OcclusionMaterialBuilder
    {
        private const string Folder = "Assets/_Game/Art/Materials/Occlusion";
        private const string SetPath = "Assets/_Game/Resources/OcclusionMaterials.asset";

        [MenuItem("Dance/Arena/Build Occlusion Materials")]
        public static void Build()
        {
            Shader shader = Shader.Find(OcclusionFader.OcclusionShaderName);
            if (shader == null)
            {
                Debug.LogError("OcclusionMaterialBuilder: shader " + OcclusionFader.OcclusionShaderName + " not found.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Art/Materials", "Occlusion");

            var sources = new List<Material>();
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!OcclusionFader.IsCandidate(r)) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !sources.Contains(m) && AssetDatabase.Contains(m)) sources.Add(m);
            }

            var set = AssetDatabase.LoadAssetAtPath<OcclusionMaterialSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<OcclusionMaterialSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.Sources.Clear();
            set.Copies.Clear();
            foreach (Material source in sources)
            {
                string path = Folder + "/" + source.name + "_occ.mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null)
                {
                    copy = new Material(shader);
                    AssetDatabase.CreateAsset(copy, path);
                }
                copy.shader = shader;
                copy.CopyPropertiesFromMaterial(source);
                copy.shaderKeywords = source.shaderKeywords;
                copy.renderQueue = source.renderQueue;
                EditorUtility.SetDirty(copy);
                set.Sources.Add(source);
                set.Copies.Add(copy);
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"OcclusionMaterialBuilder: {sources.Count} occlusion copies in {Folder}, pairs in {SetPath}.");
        }
    }
}
