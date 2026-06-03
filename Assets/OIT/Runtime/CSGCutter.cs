using UnityEngine;

namespace OIT
{
    /// <summary>
    /// Marks a GameObject as a CSG cutter volume.
    /// Registers itself with <see cref="CSGSystem"/> so <see cref="CSGStencilPass"/>
    /// can draw its mesh each frame.
    ///
    /// Requires a <see cref="MeshFilter"/> on the same GameObject. Attach this to any
    /// convex mesh — the stencil z-fail algorithm works correctly for convex volumes.
    /// <see cref="capColor"/> is reserved for the future CapFace pass.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class CSGCutter : MonoBehaviour
    {
        [Tooltip("Fill colour applied by the CapFace pass at cut-boundary pixels.")]
        public Color capColor = Color.white;

        private MeshFilter _meshFilter;

        /// <summary>The mesh used as the stencil volume. Null if no mesh is assigned.</summary>
        public Mesh CutterMesh => _meshFilter != null ? _meshFilter.sharedMesh : null;

        /// <summary>Object-to-world matrix captured each frame by the render pass.</summary>
        public Matrix4x4 LocalToWorld => transform.localToWorldMatrix;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void OnEnable()
        {
            if (CSGSystem.Instance != null)
                CSGSystem.Instance.RegisterCutter(this);
            else
                Debug.LogWarning(
                    $"[CSGCutter] No CSGSystem found in scene when '{gameObject.name}' enabled. " +
                    "Add a CSGSystem component to a GameObject in this scene.");
        }

        private void OnDisable()
        {
            if (CSGSystem.Instance != null)
                CSGSystem.Instance.UnregisterCutter(this);
        }
    }
}
