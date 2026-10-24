using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>The Devil's in-combat "whisper" modules (MAIN_GDD §7). They affect only the rifle (decision K-7).</summary>
    public enum ModuleType
    {
        TripleShot,
        Echo,
        Density,
        /// <summary>Hız (user decision 2026-10-24, production/design/combo-haste.md §3): rifle fire rate ×1.4, each bullet strays ±6°.</summary>
        Haste,
    }

    /// <summary>Fixed per-type data shared by gameplay and UI (MAIN_GDD §7 / first GDD §7 colours).</summary>
    public static class ModuleCatalog
    {
        /// <summary>Number of module types (and fixed slots).</summary>
        public const int Count = 4;

        /// <summary>Triple Shot turquoise #3FE0D0.</summary>
        public static readonly Color TripleShotColor = new Color(0x3F / 255f, 0xE0 / 255f, 0xD0 / 255f, 1f);
        /// <summary>Echo purple #9B5CFF.</summary>
        public static readonly Color EchoColor = new Color(0x9B / 255f, 0x5C / 255f, 0xFF / 255f, 1f);
        /// <summary>Density amber #FFB23F.</summary>
        public static readonly Color DensityColor = new Color(0xFF / 255f, 0xB2 / 255f, 0x3F / 255f, 1f);
        /// <summary>Haste lime #B8FF3F (placeholder; clear of the other three, of health red and of the Devil's red).</summary>
        public static readonly Color HasteColor = new Color(0xB8 / 255f, 0xFF / 255f, 0x3F / 255f, 1f);

        public static Color ColorOf(ModuleType type)
        {
            switch (type)
            {
                case ModuleType.TripleShot: return TripleShotColor;
                case ModuleType.Echo: return EchoColor;
                case ModuleType.Haste: return HasteColor;
                default: return DensityColor;
            }
        }
    }

    /// <summary>Runtime placeholder geometry for offers, slots and thrown modules (cube primitives, no colliders).</summary>
    internal static class ModuleVisuals
    {
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// One material per <see cref="ModuleType"/> (index = (int)type), plus a dark ring-base material at index
        /// <see cref="ModuleCatalog.Count"/>. Copies of <paramref name="template"/>, or of the pipeline's default
        /// primitive material when it is null. The caller owns (and destroys) them.
        /// </summary>
        public static Material[] CreateMaterials(Material template)
        {
            Material source = template;
            GameObject probe = null;
            if (source == null)
            {
                probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.SetActive(false); // its collider must not exist in the scene until the deferred Destroy
                source = probe.GetComponent<MeshRenderer>().sharedMaterial;
            }

            var materials = new Material[ModuleCatalog.Count + 1];
            for (int i = 0; i < ModuleCatalog.Count; i++)
                materials[i] = CreateTinted(source, ModuleCatalog.ColorOf((ModuleType)i), "mat_module_" + (ModuleType)i);
            materials[ModuleCatalog.Count] = CreateTinted(source, new Color(0.10f, 0.10f, 0.13f, 1f), "mat_module_ring_base");

            if (probe != null) Object.Destroy(probe);
            return materials;
        }

        public static void DestroyMaterials(Material[] materials)
        {
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null) Object.Destroy(materials[i]);
        }

        private static Material CreateTinted(Material source, Color color, string name)
        {
            var material = source != null ? new Material(source) : null;
            if (material == null) return null;
            material.name = name;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            material.color = color;
            return material;
        }

        /// <summary>A primitive with its collider disabled and removed (never blocks bullets or the player).</summary>
        public static Transform CreatePart(string name, PrimitiveType primitive, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = localScale;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (material != null) meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }
    }
}
