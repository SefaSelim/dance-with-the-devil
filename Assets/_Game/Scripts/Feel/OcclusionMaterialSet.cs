using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Pairs each URP Lit scenery material with its "Dance/Lit Occlusion" copy (same properties and keywords), for
    /// <see cref="OcclusionFader"/>. Lives at Resources/OcclusionMaterials so the copies and their shader variants are
    /// in builds; filled by the editor menu Dance/Arena/Build Occlusion Materials from the open scene.
    /// </summary>
    public sealed class OcclusionMaterialSet : ScriptableObject
    {
        public List<Material> Sources = new List<Material>();
        public List<Material> Copies = new List<Material>();
    }
}
