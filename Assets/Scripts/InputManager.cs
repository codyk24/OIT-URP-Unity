using UnityEngine;
using OIT;

namespace OITViewer
{
    /// <summary>
    /// Translates raw mouse/touch input into camera and cutter actions.
    ///
    ///  LMB drag (on empty space)  → orbit  (delegates to OrbitCamera)
    ///  RMB drag                   → pan pivot
    ///  Scroll wheel               → zoom dolly
    ///  LMB drag on CutterHandle   → raycast to world-space plane → translate cutter
    ///
    /// Cutter translation is exposed via InjectCutterDrag for performance testing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private OrbitCamera orbitCamera;
        [SerializeField] private Transform scenePivot;

        [Header("Input Sensitivity")]
        [SerializeField] private float orbitSensitivity = 0.3f;
        [SerializeField] private float panSensitivity   = 0.01f;
        [SerializeField] private float zoomSensitivity  = 2f;

        [Header("Cutter Bounds")]
        [Tooltip("World-space bounds to which cutter positions are clamped.")]
        [SerializeField] private Bounds sceneBounds = new Bounds(Vector3.zero, Vector3.one * 10f);

        // ── Cutter drag state ────────────────────────────────────────────
        private Transform _activeCutter;
        private Plane     _dragPlane;
        private Vector3   _dragHitOffset;   // world-space offset from hit point to cutter pivot

        // ── Pan drag state ───────────────────────────────────────────────
        private Vector3 _lastPanScreenPos;

        // ── Orbit drag state ─────────────────────────────────────────────
        private Vector2 _lastOrbitScreenPos;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Update()
        {
            HandleCutterOrOrbit();
            HandlePan();
            HandleZoom();
        }

        // ────────────────────────────────────────────────────────────────
        //  LMB: cutter drag OR camera orbit
        // ────────────────────────────────────────────────────────────────

        private void HandleCutterOrOrbit()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (TryBeginCutterDrag(Input.mousePosition))
                    return;

                _lastOrbitScreenPos = Input.mousePosition;
            }

            if (Input.GetMouseButton(0))
            {
                if (_activeCutter != null)
                {
                    ContinueCutterDrag(Input.mousePosition);
                    return;
                }

                Vector2 current = Input.mousePosition;
                Vector2 delta   = current - _lastOrbitScreenPos;
                _lastOrbitScreenPos = current;

                if (orbitCamera != null && delta.sqrMagnitude > 0f)
                    orbitCamera.Orbit(delta.x * orbitSensitivity, delta.y * orbitSensitivity);
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_activeCutter != null)
                    SnapCutterToBounds(_activeCutter);
                _activeCutter = null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  RMB: pan pivot
        // ────────────────────────────────────────────────────────────────

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(1))
                _lastPanScreenPos = Input.mousePosition;

            if (Input.GetMouseButton(1) && orbitCamera != null)
            {
                Vector3 delta = (Vector3)Input.mousePosition - _lastPanScreenPos;
                _lastPanScreenPos = Input.mousePosition;

                if (delta.sqrMagnitude > 0f)
                {
                    // Convert screen-space pixel delta to camera-relative world-space.
                    // Negate so the scene follows the drag direction (content tracks the cursor).
                    Vector3 worldDelta = (-mainCamera.transform.right * delta.x
                                         - mainCamera.transform.up    * delta.y)
                                        * panSensitivity;
                    orbitCamera.PanPivot(worldDelta);
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  Scroll: zoom dolly
        // ────────────────────────────────────────────────────────────────

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f && orbitCamera != null)
                orbitCamera.Zoom(scroll * zoomSensitivity);
        }

        // ────────────────────────────────────────────────────────────────
        //  Cutter drag internals
        // ────────────────────────────────────────────────────────────────

        private bool TryBeginCutterDrag(Vector3 screenPos)
        {
            if (mainCamera == null)
                return false;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return false;

            var handle = hit.collider.GetComponentInParent<CutterHandle>();
            if (handle == null)
                return false;

            _activeCutter  = handle.transform;
            // Drag plane: perpendicular to camera forward at cutter depth.
            _dragPlane     = new Plane(-mainCamera.transform.forward, _activeCutter.position);
            _dragHitOffset = _activeCutter.position - hit.point;
            return true;
        }

        private void ContinueCutterDrag(Vector3 screenPos)
        {
            if (mainCamera == null || _activeCutter == null)
                return;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            if (!_dragPlane.Raycast(ray, out float enter))
                return;

            Vector3 worldHit    = ray.GetPoint(enter);
            Vector3 newPosition = worldHit + _dragHitOffset;
            InjectCutterDrag(newPosition - _activeCutter.position);
        }

        // ────────────────────────────────────────────────────────────────
        //  Public testable API
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Directly applies a world-space translation to <paramref name="cutter"/>, then clamps
        /// it inside <see cref="sceneBounds"/>.  Call this instead of simulating mouse events in
        /// performance tests (PERF-INPUT-01, PERF-INPUT-02).
        /// </summary>
        public void InjectCutterDrag(Vector3 worldSpaceDelta)
        {
            if (_activeCutter == null)
                return;

            _activeCutter.position += worldSpaceDelta;
            SnapCutterToBounds(_activeCutter);
        }

        /// <summary>Overload that targets an explicit transform (used by performance tests).</summary>
        public void InjectCutterDrag(Transform cutter, Vector3 worldSpaceDelta)
        {
            cutter.position += worldSpaceDelta;
            SnapCutterToBounds(cutter);
        }

        private void SnapCutterToBounds(Transform cutter)
        {
            if (!sceneBounds.size.Equals(Vector3.zero))
                cutter.position = sceneBounds.ClosestPoint(cutter.position);
        }
    }
}
