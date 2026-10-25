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
        /// <summary>
        /// Keskin Göz (user decision 2026-10-25, production/design/crit-difficulty.md §3): rifle crit chance +20 %;
        /// cost: Defne walks ×0.85 (6 → 5.1 m/s) while the fire button is held.
        /// </summary>
        CritChance,
        /// <summary>
        /// Delici (user decision 2026-10-25): rifle bullets pass through 2 more enemies (adds to Contract 3's +2, max 4);
        /// cost: each enemy passed through cuts that bullet's damage by 30 % (×0.7 per pierce).
        /// </summary>
        Pierce,
    }

    /// <summary>Fixed per-type data shared by gameplay and UI (MAIN_GDD §7 / first GDD §7 colours).</summary>
    public static class ModuleCatalog
    {
        /// <summary>Number of module types.</summary>
        public const int Count = 6;

        /// <summary>Modules the player can have equipped at once (user decision 2026-10-25, K-23).</summary>
        public const int SlotCount = 3;

        /// <summary>Triple Shot turquoise #3FE0D0.</summary>
        public static readonly Color TripleShotColor = new Color(0x3F / 255f, 0xE0 / 255f, 0xD0 / 255f, 1f);
        /// <summary>Echo purple #9B5CFF.</summary>
        public static readonly Color EchoColor = new Color(0x9B / 255f, 0x5C / 255f, 0xFF / 255f, 1f);
        /// <summary>Density amber #FFB23F.</summary>
        public static readonly Color DensityColor = new Color(0xFF / 255f, 0xB2 / 255f, 0x3F / 255f, 1f);
        /// <summary>Haste lime #B8FF3F (placeholder; clear of the other three, of health red and of the Devil's red).</summary>
        public static readonly Color HasteColor = new Color(0xB8 / 255f, 0xFF / 255f, 0x3F / 255f, 1f);
        /// <summary>Keskin Göz cold steel white #D6EEFF (placeholder; paler and bluer than Triple Shot's turquoise).</summary>
        public static readonly Color CritChanceColor = new Color(0xD6 / 255f, 0xEE / 255f, 0xFF / 255f, 1f);
        /// <summary>
        /// Delici copper #E0703A (placeholder; a bone white would blur into Keskin Göz's pale steel and the cream
        /// "TAKILI" marker, so it sits between Density amber and the Devil's red, clearly orange).
        /// </summary>
        public static readonly Color PierceColor = new Color(0xE0 / 255f, 0x70 / 255f, 0x3A / 255f, 1f);

        public static Color ColorOf(ModuleType type)
        {
            switch (type)
            {
                case ModuleType.TripleShot: return TripleShotColor;
                case ModuleType.Echo: return EchoColor;
                case ModuleType.Haste: return HasteColor;
                case ModuleType.CritChance: return CritChanceColor;
                case ModuleType.Pierce: return PierceColor;
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

        /// <summary>
        /// The module's icon from primitives, ~0.6 m across at scale 1, built inactive-safe under
        /// <paramref name="parent"/>: the offer's floating core and the Q selection ring show the same shape.
        /// </summary>
        public static Transform BuildIcon(ModuleType type, Transform parent, Material m)
        {
            var icon = new GameObject("Shape_" + type).transform;
            icon.SetParent(parent, false);
            switch (type)
            {
                case ModuleType.TripleShot:
                    // Three bullets fanned out from one muzzle (the ±12° salvo), tips up.
                    CreatePart("Muzzle", PrimitiveType.Cube, icon, new Vector3(0f, -0.06f, 0f), new Vector3(0.14f, 0.06f, 0.06f), m);
                    for (int i = -1; i <= 1; i++)
                    {
                        float angle = i * 24f;
                        Quaternion turn = Quaternion.Euler(0f, 0f, -angle);
                        Transform bullet = CreatePart("Bullet_" + (i + 1), PrimitiveType.Capsule, icon,
                            new Vector3(0f, -0.2f, 0f) + turn * new Vector3(0f, 0.3f, 0f), new Vector3(0.09f, 0.11f, 0.09f), m);
                        bullet.localRotation = turn;
                    }
                    break;
                case ModuleType.Echo:
                    // A small core inside a square ring.
                    CreatePart("Body", PrimitiveType.Cube, icon, Vector3.zero, Vector3.one * 0.2f, m);
                    CreatePart("Ring_N", PrimitiveType.Cube, icon, new Vector3(0f, 0f, 0.3f), new Vector3(0.6f, 0.1f, 0.1f), m);
                    CreatePart("Ring_S", PrimitiveType.Cube, icon, new Vector3(0f, 0f, -0.3f), new Vector3(0.6f, 0.1f, 0.1f), m);
                    CreatePart("Ring_E", PrimitiveType.Cube, icon, new Vector3(0.3f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.6f), m);
                    CreatePart("Ring_W", PrimitiveType.Cube, icon, new Vector3(-0.3f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.6f), m);
                    break;
                case ModuleType.Haste:
                    // A double chevron ">>" (speed), thin bars.
                    for (int c = 0; c < 2; c++)
                    {
                        float tip = 0.2f - c * 0.24f;
                        Transform upper = CreatePart("Chevron" + c + "_Up", PrimitiveType.Cube, icon,
                            new Vector3(tip - 0.09f, 0.09f, 0f), new Vector3(0.28f, 0.08f, 0.08f), m);
                        upper.localRotation = Quaternion.Euler(0f, 0f, -45f);
                        Transform lower = CreatePart("Chevron" + c + "_Down", PrimitiveType.Cube, icon,
                            new Vector3(tip - 0.09f, -0.09f, 0f), new Vector3(0.28f, 0.08f, 0.08f), m);
                        lower.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    }
                    break;
                case ModuleType.CritChance:
                    // Keskin Göz: an upright crosshair (four thin arms with a gap) around a small diamond (aim).
                    CreatePart("Arm_Up", PrimitiveType.Cube, icon, new Vector3(0f, 0.24f, 0f), new Vector3(0.07f, 0.2f, 0.07f), m);
                    CreatePart("Arm_Down", PrimitiveType.Cube, icon, new Vector3(0f, -0.24f, 0f), new Vector3(0.07f, 0.2f, 0.07f), m);
                    CreatePart("Arm_Left", PrimitiveType.Cube, icon, new Vector3(-0.24f, 0f, 0f), new Vector3(0.2f, 0.07f, 0.07f), m);
                    CreatePart("Arm_Right", PrimitiveType.Cube, icon, new Vector3(0.24f, 0f, 0f), new Vector3(0.2f, 0.07f, 0.07f), m);
                    CreatePart("Dot", PrimitiveType.Cube, icon, Vector3.zero, Vector3.one * 0.1f, m)
                        .localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case ModuleType.Pierce:
                    // Delici: a long spike skewering two thin plates, with a pointed tip (a bullet through two enemies).
                    CreatePart("Shaft", PrimitiveType.Cube, icon, Vector3.zero, new Vector3(0.66f, 0.07f, 0.07f), m);
                    CreatePart("Tip", PrimitiveType.Cube, icon, new Vector3(0.36f, 0f, 0f), Vector3.one * 0.12f, m)
                        .localRotation = Quaternion.Euler(0f, 0f, 45f);
                    CreatePart("Plate_A", PrimitiveType.Cube, icon, new Vector3(-0.12f, 0f, 0f), new Vector3(0.05f, 0.34f, 0.3f), m);
                    CreatePart("Plate_B", PrimitiveType.Cube, icon, new Vector3(0.12f, 0f, 0f), new Vector3(0.05f, 0.34f, 0.3f), m);
                    break;
                default:
                    // Density: one big cube.
                    CreatePart("Body", PrimitiveType.Cube, icon, Vector3.zero, Vector3.one * 0.5f, m);
                    break;
            }
            return icon;
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
