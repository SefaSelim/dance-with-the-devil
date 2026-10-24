using UnityEngine;
using UnityEngine.Rendering;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Builds the runtime tell visuals shared by the enemy brains (GDD §6 ön işaretler): a flat floor wedge and an aim
    /// line. They live at the scene root (not under the enemy) so the enemy's hit flash, hide-on-death and
    /// <see cref="ShatterOnDeath"/> never touch them; the owning brain destroys them in OnDestroy.
    /// Colours are set per object with a MaterialPropertyBlock on _BaseColor, so one material serves every enemy.
    /// </summary>
    public static class EnemyTelegraphs
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Material _fallbackMaterial;
        private static Mesh _wedgeMesh;

        /// <summary>The material to use: the given one, else a shared runtime URP Unlit material.</summary>
        public static Material MaterialOrFallback(Material material)
        {
            if (material != null) return material;
            if (_fallbackMaterial != null) return _fallbackMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _fallbackMaterial = new Material(shader) { name = "EnemyTelegraph" };
            return _fallbackMaterial;
        }

        /// <summary>
        /// A hidden floor wedge: apex at the object's origin, opening along +z to (±0.5, 0, 1). Scale x = width,
        /// z = length. Double-sided, no collider, no shadows.
        /// </summary>
        public static MeshRenderer CreateWedge(string name, Material material, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = WedgeMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialOrFallback(material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
            go.SetActive(false);
            return renderer;
        }

        /// <summary>A hidden two-point world-space line (aim line).</summary>
        public static LineRenderer CreateLine(string name, Material material, Color color, float width)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = MaterialOrFallback(material);
            line.startColor = color;
            line.endColor = color;
            SetColor(line, color);
            go.SetActive(false);
            return line;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        public static void Destroy(Component component)
        {
            if (component != null) Object.Destroy(component.gameObject);
        }

        private static Mesh WedgeMesh()
        {
            if (_wedgeMesh != null) return _wedgeMesh;
            _wedgeMesh = new Mesh { name = "TellWedge" };
            _wedgeMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            };
            _wedgeMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            _wedgeMesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // both sides
            _wedgeMesh.RecalculateBounds();
            return _wedgeMesh;
        }
    }
}
