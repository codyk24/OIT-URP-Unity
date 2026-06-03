using UnityEngine;

namespace OIT
{
    /// <summary>
    /// Marks a GameObject as an OIT transparent object.
    /// Registers itself with <see cref="OITSystem"/> so <see cref="OITGeometryPass"/>
    /// draws it into the per-pixel linked list each frame.
    ///
    /// Requires a <see cref="MeshFilter"/> and <see cref="MeshRenderer"/> on the same
    /// GameObject. The renderer's material must use the <c>OIT/OITGeometry</c> shader
    /// so the fragment shader appends nodes to the OIT buffers.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class OITObject : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Alpha value forwarded to the OIT geometry shader's _BaseColor.a channel.")]
        public float alpha = 0.5f;

        private MeshFilter   _meshFilter;
        private MeshRenderer _meshRenderer;

        /// <summary>Shared mesh used by the geometry pass.</summary>
        public Mesh      SharedMesh     => _meshFilter  != null ? _meshFilter.sharedMesh          : null;

        /// <summary>Shared material (must use OIT/OITGeometry shader).</summary>
        public Material  SharedMaterial => _meshRenderer != null ? _meshRenderer.sharedMaterial   : null;

        /// <summary>Object-to-world matrix captured each frame by the render pass.</summary>
        public Matrix4x4 LocalToWorld   => transform.localToWorldMatrix;

        private void Awake()
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            if (OITSystem.Instance != null)
                OITSystem.Instance.RegisterObject(this);
            else
                Debug.LogWarning(
                    $"[OITObject] No OITSystem found in scene when '{gameObject.name}' enabled. " +
                    "Add an OITSystem component to a GameObject in this scene.");
        }

        private void OnDisable()
        {
            if (OITSystem.Instance != null)
                OITSystem.Instance.UnregisterObject(this);
        }
    }
}
