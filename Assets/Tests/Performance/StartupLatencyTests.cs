using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using OITViewer;

namespace OIT.Tests.Performance
{
    /// <summary>
    /// PERF-STARTUP-01: Addressables fully loaded from scene open in under 5 seconds.
    ///
    /// The test builds a minimal SceneManager in a fresh scene (no actual prefabs need to
    /// be loaded — the Addressables labels may resolve to empty lists in the test environment,
    /// which is fine; the gate is on elapsed wall time, not asset count).  If you want to
    /// measure against real Addressables content, point the labels at a local group and ensure
    /// the Addressables catalog is built before running.
    /// </summary>
    public class StartupLatencyTests
    {
        private const float StartupBudgetSeconds = 5f;

        private GameObject _root;
        private SceneManager _sceneManager;
        private LoadingScreen _loadingScreen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Ensure Addressables runtime is initialized before the test starts the timer.
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
            yield return null;
        }

        // PERF-STARTUP-01: SceneManager.IsReady within 5 seconds of Start()
        [UnityTest]
        public IEnumerator PERF_STARTUP_01_SceneReady_Within5Seconds()
        {
            BuildSceneManagerFixture();

            float startTime = Time.realtimeSinceStartup;

            // Poll until ready or budget exceeded.
            while (!_sceneManager.IsReady)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                Assert.Less(elapsed, StartupBudgetSeconds,
                    $"SceneManager did not finish loading within {StartupBudgetSeconds}s " +
                    $"(elapsed: {elapsed:F2}s). Check Addressables group build and network availability.");

                yield return null;
            }

            float totalElapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[PERF-STARTUP-01] Scene ready in {totalElapsed:F3}s (budget: {StartupBudgetSeconds}s).");

            Assert.Less(totalElapsed, StartupBudgetSeconds,
                $"SceneManager.IsReady set true but total elapsed {totalElapsed:F2}s exceeded budget.");
        }

        // -------------------------------------------------------------------

        /// <summary>
        /// Constructs the minimal SceneManager + LoadingScreen hierarchy needed for the test.
        /// No Addressable prefabs need to be present; empty label results are handled gracefully.
        /// </summary>
        private void BuildSceneManagerFixture()
        {
            _root = new GameObject("StartupLatencyTest_Root");

            // LoadingScreen requires a Canvas component.
            var canvasGo = new GameObject("LoadingCanvas");
            canvasGo.transform.SetParent(_root.transform, false);
            canvasGo.AddComponent<Canvas>();
            _loadingScreen = canvasGo.AddComponent<LoadingScreen>();

            // SceneManager wired with empty-but-valid Addressable label references.
            var managerGo = new GameObject("SceneManager");
            managerGo.transform.SetParent(_root.transform, false);
            _sceneManager = managerGo.AddComponent<SceneManager>();

            // Inject dependencies via SerializedField reflection (test environment only).
            SetPrivateField(_sceneManager, "loadingScreen", _loadingScreen);

            // oitObjectsLabel and cutterPrefabsLabel intentionally left null:
            // SceneManager must handle null labels without throwing so that the
            // startup-time contract is testable in isolation from content availability.
            // If real content timing is needed, assign labels here and build the catalog.
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field == null)
                throw new System.MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }
    }
}
