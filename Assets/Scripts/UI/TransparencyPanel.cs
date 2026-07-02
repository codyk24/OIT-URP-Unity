using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OIT;

namespace OITViewer
{
    /// <summary>
    /// Sidebar panel that lists every <see cref="OITObject"/> in the scene.
    /// Spawns one row prefab per object and delegates all slider/toggle logic
    /// to the <see cref="TransparencyRow"/> component on each row.
    /// </summary>
    public sealed class TransparencyPanel : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Scroll-view content rect that rows are spawned into.")]
        [SerializeField] private RectTransform contentRoot;

        [Tooltip("Prefab with a TransparencyRow component plus Slider, Toggle, and TMP_Text children.")]
        [SerializeField] private GameObject rowPrefab;

        private readonly List<TransparencyRow> _rows = new List<TransparencyRow>();

        private void Start()
        {
            StartCoroutine(LateStart());
        }

        private IEnumerator LateStart()
        {
            yield return new WaitUntil(() => SceneManager.Instance.IsReady);
            BuildRows();
        }

        /// <summary>
        /// Clears and rebuilds the row list from all <see cref="OITObject"/> instances
        /// currently in the scene. Safe to call again after scene changes.
        /// </summary>
        public void BuildRows()
        {
            foreach (var row in _rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _rows.Clear();

            if (contentRoot == null || rowPrefab == null)
            {
                Debug.LogWarning("[TransparencyPanel] contentRoot or rowPrefab is not assigned.");
                return;
            }

            var objects = FindObjectsByType<OITObject>(FindObjectsSortMode.InstanceID);
            foreach (var oitObj in objects)
            {
                var go  = Instantiate(rowPrefab, contentRoot);
                var row = go.GetComponent<TransparencyRow>();

                if (row == null)
                {
                    Debug.LogWarning("[TransparencyPanel] Row prefab is missing a TransparencyRow component.");
                    Destroy(go);
                    continue;
                }

                row.Initialize(oitObj);
                _rows.Add(row);
            }
        }
    }
}
