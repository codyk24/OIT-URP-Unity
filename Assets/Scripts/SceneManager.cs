using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using OIT;

namespace OITViewer
{
    /// <summary>
    /// Bootstraps the scene: loads OIT objects and CSG cutter prefabs from Addressables,
    /// wires the OrbitCamera pivot, and hides the loading screen when ready.
    /// Must complete within 5 s to satisfy PERF-STARTUP-01.
    /// </summary>
    public sealed class SceneManager : BaseMonoSingleton<SceneManager>
    {
        [Header("Addressable Keys")]
        [Tooltip("Addressable label or keys for transparent OIT objects to spawn.")]
        [SerializeField] private AssetLabelReference oitObjectsLabel;

        [Tooltip("Addressable label or keys for CSG cutter prefabs to spawn.")]
        [SerializeField] private AssetLabelReference cutterPrefabsLabel;

        [Header("Scene References")]
        [Tooltip("Parent transform used as the orbit-camera and cutter pivot.")]
        [SerializeField] private Transform scenePivot;

        [Tooltip("OrbitCamera to wire to the scene pivot after load.")]
        [SerializeField] private OrbitCamera orbitCamera;

        [Tooltip("Loading screen to hide once all assets are ready.")]
        [SerializeField] private LoadingScreen loadingScreen;

        private readonly List<GameObject> _spawnedObjects = new();

        /// <summary>True once all Addressable loads have completed (success or failure).</summary>
        public bool IsReady { get; private set; }

        private void Start()
        {
            StartCoroutine(LoadScene());
        }

        private IEnumerator LoadScene()
        {
            if (loadingScreen != null)
                loadingScreen.Show();

            // Load OIT objects
            if (oitObjectsLabel != null)
            {
                var oitHandle = Addressables.LoadAssetsAsync<GameObject>(oitObjectsLabel, null);
                yield return oitHandle;

                if (oitHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    foreach (var prefab in oitHandle.Result)
                    {
                        var go = Instantiate(prefab, scenePivot);
                        _spawnedObjects.Add(go);
                        go.transform.position = new Vector3(_spawnedObjects.Count, go.transform.position.y, go.transform.position.z); // offset cutters to avoid overlapping with OIT objects
                    }
                }
                else
                {
                    Debug.LogError("[SceneManager] Failed to load OIT objects: " + oitHandle.OperationException);
                }

                Addressables.Release(oitHandle);
            }

            // Load cutter prefabs
            if (cutterPrefabsLabel != null)
            {
                var cutterHandle = Addressables.LoadAssetsAsync<GameObject>(cutterPrefabsLabel, null);
                yield return cutterHandle;

                if (cutterHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    foreach (var prefab in cutterHandle.Result)
                    {
                        var go = Instantiate(prefab, scenePivot);
                        _spawnedObjects.Add(go);
                        go.transform.position = new Vector3(_spawnedObjects.Count, go.transform.position.y, go.transform.position.z); // offset cutters to avoid overlapping with OIT objects
                    }
                }
                else
                {
                    Debug.LogError("[SceneManager] Failed to load cutter prefabs: " + cutterHandle.OperationException);
                }

                Addressables.Release(cutterHandle);
            }

            //WireOrbitCamera();

            if (loadingScreen != null)
                loadingScreen.Hide();
            IsReady = true;
        }

        private void WireOrbitCamera()
        {
            if (orbitCamera == null || scenePivot == null)
                return;

            // Expose pivot wiring via reflection-safe public API if available,
            // otherwise fall back to serialized field injection at runtime.
            var pivotField = typeof(OrbitCamera)
                .GetField("lookAtTransform",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

            if (pivotField != null)
                pivotField.SetValue(orbitCamera, scenePivot);
            else
                Debug.LogWarning("[SceneManager] Could not wire OrbitCamera pivot: 'lookAtTransform' field not found.");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                    Destroy(go);
            }
            _spawnedObjects.Clear();
        }
    }
}
