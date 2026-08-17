using System.Collections.Generic;
using UnityEngine;

namespace OIT
{
    /// <summary>
    /// Scene-level registry of active OIT transparent objects.
    /// OITGeometryPass queries Objects each frame to obtain meshes and transforms.
    /// </summary>
    public sealed class OITSystem : MonoBehaviour
    {
        private static OITSystem _instance;

        /// <summary>Active OITSystem in the scene, or null if none exists.</summary>
        public static OITSystem Instance => _instance;

        [SerializeField] private List<OITObject> _objects = new List<OITObject>();

        /// <summary>Read-only list of registered OIT objects for use by render passes.</summary>
        public IReadOnlyList<OITObject> Objects => _objects;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[OITSystem] Duplicate instance detected; destroying the new one.");
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Called by <see cref="OITObject.OnEnable"/>.</summary>
        public void RegisterObject(OITObject obj)
        {
            if (!_objects.Contains(obj))
                _objects.Add(obj);
        }

        /// <summary>Called by <see cref="OITObject.OnDisable"/>.</summary>
        public void UnregisterObject(OITObject obj) => _objects.Remove(obj);
    }
}
