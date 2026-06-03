using System.Collections.Generic;
using UnityEngine;

namespace OIT
{
    /// <summary>
    /// Scene-level registry of active CSG cutter volumes.
    /// CSGStencilPass queries Cutters each frame to obtain meshes and transforms.
    /// At most <see cref="MaxCutters"/> cutters are tracked; additional registrations
    /// are dropped with a warning (EDGE-CUT-02).
    /// </summary>
    public sealed class CSGSystem : MonoBehaviour
    {
        public const int MaxCutters = 2;

        private static CSGSystem _instance;

        /// <summary>Active CSGSystem in the scene, or null if none exists.</summary>
        public static CSGSystem Instance => _instance;

        private readonly List<CSGCutter> _cutters = new List<CSGCutter>(MaxCutters);

        /// <summary>Read-only snapshot of registered cutters for use by render passes.</summary>
        public IReadOnlyList<CSGCutter> Cutters => _cutters;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[CSGSystem] Duplicate instance detected; destroying the new one.");
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

        /// <summary>
        /// Called by <see cref="CSGCutter.OnEnable"/>. Returns false and logs a warning
        /// when the cutter limit has been reached.
        /// </summary>
        public bool RegisterCutter(CSGCutter cutter)
        {
            if (_cutters.Count >= MaxCutters)
            {
                Debug.LogWarning(
                    $"[CSGSystem] Maximum cutter count ({MaxCutters}) reached. " +
                    $"'{cutter.gameObject.name}' will not be rendered as a cutter. " +
                    "Disable an existing cutter first.");
                return false;
            }
            if (!_cutters.Contains(cutter))
                _cutters.Add(cutter);
            return true;
        }

        /// <summary>Called by <see cref="CSGCutter.OnDisable"/>.</summary>
        public void UnregisterCutter(CSGCutter cutter) => _cutters.Remove(cutter);
    }
}
