using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using OITViewer;
using Debug = UnityEngine.Debug;

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

        // ────────────────────────────────────────────────────────────────────────────
        //  PERF-INPUT-01 & PERF-INPUT-02 — Input Latency
        // ────────────────────────────────────────────────────────────────────────────

        private const double InputEventBudgetMs  = 16.0;   // PERF-INPUT-01: each drag event
        private const double FrameBudgetMs       = 33.3;   // PERF-INPUT-02: no frame exceeds this
        private const int    DragEventCount      = 100;
        private const int    ContinuousDragFrames = 60;

        /// <summary>
        /// PERF-INPUT-01: 100 simulated cutter drag events (input → transform update) must each
        /// complete in under 16 ms.
        ///
        /// Uses InputManager.InjectCutterDrag(Transform, Vector3) to bypass the Input API so the
        /// test is deterministic. The cutter starts at the origin; small incremental deltas are
        /// applied, each within sceneBounds, to keep SnapCutterToBounds from doing extra work.
        /// </summary>
        [UnityTest]
        public IEnumerator PERF_INPUT_01_CutterDragEvent_Under16ms()
        {
            var (inputManager, cutterTransform, root) = BuildInputManagerFixture();

            var sw = new Stopwatch();
            var delta = new Vector3(0.01f, 0f, 0f);

            for (int i = 0; i < DragEventCount; i++)
            {
                sw.Restart();
                inputManager.InjectCutterDrag(cutterTransform, delta);
                sw.Stop();

                double elapsedMs = sw.Elapsed.TotalMilliseconds;
                Assert.Less(elapsedMs, InputEventBudgetMs,
                    $"[PERF-INPUT-01] Event {i + 1}/{DragEventCount} took {elapsedMs:F3} ms " +
                    $"(budget: {InputEventBudgetMs} ms).");
            }

            Debug.Log($"[PERF-INPUT-01] All {DragEventCount} cutter drag events completed within {InputEventBudgetMs} ms each.");

            Object.Destroy(root);
            yield return null;
        }

        /// <summary>
        /// PERF-INPUT-02: During a 60-frame simulated continuous drag no frame may take more
        /// than 33.3 ms (i.e. no frame drop below 30 fps).
        ///
        /// Each frame: record wall-clock time at frame start, inject one cutter drag event, yield
        /// one frame, then measure elapsed wall-clock time. The measured window includes Unity's
        /// own frame overhead, giving a conservative real-world budget.
        /// </summary>
        [UnityTest]
        public IEnumerator PERF_INPUT_02_ContinuousDrag_NoFrameExceeds33ms()
        {
            var (inputManager, cutterTransform, root) = BuildInputManagerFixture();

            var sw    = new Stopwatch();
            var delta = new Vector3(0.01f, 0f, 0f);

            for (int frame = 0; frame < ContinuousDragFrames; frame++)
            {
                sw.Restart();

                inputManager.InjectCutterDrag(cutterTransform, delta);

                yield return null;   // advance one Unity frame

                sw.Stop();
                double frameMs = sw.Elapsed.TotalMilliseconds;

                Assert.Less(frameMs, FrameBudgetMs,
                    $"[PERF-INPUT-02] Frame {frame + 1}/{ContinuousDragFrames} took {frameMs:F3} ms " +
                    $"(budget: {FrameBudgetMs} ms).");
            }

            Debug.Log($"[PERF-INPUT-02] All {ContinuousDragFrames} drag frames completed within {FrameBudgetMs} ms each.");

            Object.Destroy(root);
            yield return null;
        }

        /// <summary>
        /// Builds a minimal InputManager + cutter Transform hierarchy for input performance tests.
        /// sceneBounds covers [−5,5] on each axis so SnapCutterToBounds is exercised without
        /// clamping (cutter stays near origin with the small deltas used in the tests).
        /// </summary>
        private static (InputManager inputManager, Transform cutter, GameObject root) BuildInputManagerFixture()
        {
            var root = new GameObject("InputPerfTest_Root");

            // Cutter handle
            var cutterGo = new GameObject("Cutter");
            cutterGo.transform.SetParent(root.transform, false);
            cutterGo.AddComponent<CutterHandle>();

            // InputManager — no camera or OrbitCamera needed for InjectCutterDrag path.
            var imGo = new GameObject("InputManager");
            imGo.transform.SetParent(root.transform, false);
            var im = imGo.AddComponent<InputManager>();

            // Wire sceneBounds via reflection (mirrors the pattern in BuildSceneManagerFixture).
            var boundsField = typeof(InputManager).GetField(
                "sceneBounds",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            boundsField?.SetValue(im, new Bounds(Vector3.zero, Vector3.one * 10f));

            return (im, cutterGo.transform, root);
        }
    }
}
