using UnityEditor;
using UnityEngine;
using OIT;

namespace OITViewer.Editor
{
    /// <summary>
    /// Editor utility that creates a CSG cutter prefab with a trapezoidal-prism mesh
    /// and an interaction handle gizmo child. Run via the menu item once; the resulting
    /// prefab can then be tagged with the Addressables "cutters" label.
    ///
    /// Trapezoidal prism dimensions (default): top-width 0.5, bottom-width 1.0, height 1.0, depth 1.0.
    /// </summary>
    public static class CutterPrefabBuilder
    {
        private const string PrefabSavePath = "Assets/Prefabs/Cutters/CSGCutter.prefab";

        [MenuItem("OIT/Build Cutter Prefab")]
        public static void BuildCutterPrefab()
        {
            var mesh = BuildTrapezoidalPrismMesh(
                topWidth: 0.5f,
                bottomWidth: 1.0f,
                height: 1.0f,
                depth: 1.0f);

            mesh.name = "TrapezoidalPrism";

            // Save mesh asset
            const string meshPath = "Assets/Prefabs/Cutters/TrapezoidalPrism.mesh";
            EnsureDirectory("Assets/Prefabs/Cutters");
            AssetDatabase.CreateAsset(mesh, meshPath);

            // Build prefab hierarchy
            var root = new GameObject("CSGCutter");
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            // Invisible renderer (stencil pass renders the mesh via Graphics.DrawMesh)
            var mr = root.AddComponent<MeshRenderer>();
            mr.enabled = false;

            root.AddComponent<CSGCutter>();

            // Interaction handle gizmo child
            var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "InteractionHandle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localScale = Vector3.one * 0.1f;

            // Mark handle as editor-only gizmo by adding a tag
            handle.tag = "EditorOnly";

            // Destroy the default collider — handle is visual only
            Object.DestroyImmediate(handle.GetComponent<SphereCollider>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabSavePath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CutterPrefabBuilder] Prefab saved to {PrefabSavePath}. " +
                      "Add it to the Addressables group with label 'cutters'.");

            Selection.activeObject = prefab;
        }

        /// <summary>
        /// Builds a trapezoidal prism (frustum with rectangular cross-section) centered at origin.
        /// Top face has width <paramref name="topWidth"/>, bottom face has width <paramref name="bottomWidth"/>.
        /// The mesh is convex, satisfying the CSG stencil z-fail requirement.
        /// </summary>
        private static Mesh BuildTrapezoidalPrismMesh(
            float topWidth, float bottomWidth, float height, float depth)
        {
            float hw = bottomWidth * 0.5f;
            float hwT = topWidth * 0.5f;
            float hh = height * 0.5f;
            float hd = depth * 0.5f;

            // 8 vertices: bottom-front/back (wide), top-front/back (narrow)
            var verts = new Vector3[]
            {
                // Bottom face (y = -hh)
                new(-hw,  -hh, -hd), // 0 BL-front
                new( hw,  -hh, -hd), // 1 BR-front
                new( hw,  -hh,  hd), // 2 BR-back
                new(-hw,  -hh,  hd), // 3 BL-back

                // Top face (y = +hh)
                new(-hwT,  hh, -hd), // 4 TL-front
                new( hwT,  hh, -hd), // 5 TR-front
                new( hwT,  hh,  hd), // 6 TR-back
                new(-hwT,  hh,  hd), // 7 TL-back
            };

            // 6 faces × 2 triangles × 3 indices = 36
            var tris = new int[]
            {
                // Bottom (winding: outward = downward = CW from below)
                0, 3, 2,  0, 2, 1,
                // Top
                4, 5, 6,  4, 6, 7,
                // Front (z = -hd)
                0, 1, 5,  0, 5, 4,
                // Back (z = +hd)
                2, 3, 7,  2, 7, 6,
                // Left (x = -hw / -hwT)
                3, 0, 4,  3, 4, 7,
                // Right (x = +hw / +hwT)
                1, 2, 6,  1, 6, 5,
            };

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureDirectory(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
