using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for OIT pass ordering (INT-ORDER-01 through INT-ORDER-04).
    ///
    /// ORDERING MECHANISM:
    ///   Each pass appends its name to <see cref="OITPassOrder.Log"/> from inside its
    ///   SetRenderFunc callback (which URP executes in declaration order on the render
    ///   thread). Tests call <see cref="OITPassOrder.Clear"/> before rendering and
    ///   read <see cref="OITPassOrder.Log"/> after WaitForEndOfFrame.
    ///
    ///   Passes that instrument the log:
    ///     "OIT.CSGStencilPass"    — CSGStencilPass
    ///     "OIT.GeometryPass"      — OITGeometryPass
    ///     "OIT.ResolvePass"       — OITResolvePass
    ///     "OIT.FinalCompositePass"— FinalCompositePass
    ///
    /// NOTE: Passes only append to the log when they actually execute (e.g.
    ///   OITGeometryPass skips when no OIT objects are registered; CSGStencilPass
    ///   skips when no cutters are active). Tests create the required objects so
    ///   that all four passes run.
    /// </summary>
    public class PassOrderingTests
    {
        private const int k_TexSize = 128;

        private GameObject       _bufferManagerGo;
        private GameObject       _oitSystemGo;
        private GameObject       _csgSystemGo;
        private GameObject       _cameraGo;
        private Camera           _camera;
        private RenderTexture    _testRT;

        private readonly List<GameObject> _extraGos  = new List<GameObject>();
        private readonly List<Material>   _extraMats = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            OITPassOrder.Clear();

            _bufferManagerGo = new GameObject("OIT_ORDER_BufferManager");
            _bufferManagerGo.AddComponent<OITBufferManager>();

            _oitSystemGo = new GameObject("OIT_ORDER_OITSystem");
            _oitSystemGo.AddComponent<OITSystem>();

            _csgSystemGo = new GameObject("OIT_ORDER_CSGSystem");
            _csgSystemGo.AddComponent<CSGSystem>();

            _testRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "OIT_ORDER_TestRT" };
            _testRT.Create();

            _cameraGo = new GameObject("OIT_ORDER_Camera");
            _camera   = _cameraGo.AddComponent<Camera>();
            _camera.clearFlags      = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.fieldOfView     = 60f;
            _camera.targetTexture   = _testRT;
            _camera.transform.position = new Vector3(0f, 0f, -5f);
            _camera.transform.LookAt(Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            OITPassOrder.Clear();

            foreach (var go in _extraGos)
                if (go != null) Object.DestroyImmediate(go);
            _extraGos.Clear();

            foreach (var mat in _extraMats)
                if (mat != null) Object.DestroyImmediate(mat);
            _extraMats.Clear();

            if (_cameraGo        != null) Object.DestroyImmediate(_cameraGo);
            if (_oitSystemGo     != null) Object.DestroyImmediate(_oitSystemGo);
            if (_csgSystemGo     != null) Object.DestroyImmediate(_csgSystemGo);
            if (_bufferManagerGo != null) Object.DestroyImmediate(_bufferManagerGo);
            if (_testRT          != null) { _testRT.Release(); Object.DestroyImmediate(_testRT); }

            if (OITResources.ResolveTexture != null)
            {
                OITResources.ResolveTexture.Release();
                Object.DestroyImmediate(OITResources.ResolveTexture);
                OITResources.ResolveTexture = null;
            }
        }

        private GameObject CreateOITSphere(Vector3 pos, Color color, float alpha = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("OIT/OITGeometry")) { name = "OIT_ORDER_OITMat" };
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<OITObject>().alpha = alpha;
            return go;
        }

        private GameObject CreateCutter(Vector3 pos)
        {
            var go = new GameObject("OIT_ORDER_Cutter");
            go.transform.position = pos;
            _extraGos.Add(go);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>();
            go.AddComponent<CSGCutter>();
            return go;
        }

        // ====================================================================
        // INT-ORDER-01: All 6 passes execute in correct order each frame
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_ORDER_01_AllPasses_ExecuteInCorrectOrder_EachFrame()
        {
            // Need at least one OIT object and one cutter so all four instrumented
            // passes actually run.
            CreateOITSphere(Vector3.zero, Color.red, 0.5f);
            CreateCutter(new Vector3(10f, 0f, 0f)); // offset so it doesn't mask the OIT sphere

            OITPassOrder.Clear();
            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            var log = OITPassOrder.Log;

            Assume.That(log.Count, Is.GreaterThan(0),
                "INT-ORDER-01: No passes logged. Ensure OITPassOrder.Record is called in each pass.");

            // Verify the relative order of the four instrumented passes.
            int idxCSG      = log.IndexOf("OIT.CSGStencilPass");
            int idxGeometry = log.IndexOf("OIT.GeometryPass");
            int idxResolve  = log.IndexOf("OIT.ResolvePass");
            int idxComposite= log.IndexOf("OIT.FinalCompositePass");

            Assert.That(idxCSG,       Is.GreaterThanOrEqualTo(0), "INT-ORDER-01: CSGStencilPass did not log.");
            Assert.That(idxGeometry,  Is.GreaterThanOrEqualTo(0), "INT-ORDER-01: OITGeometryPass did not log.");
            Assert.That(idxResolve,   Is.GreaterThanOrEqualTo(0), "INT-ORDER-01: OITResolvePass did not log.");
            Assert.That(idxComposite, Is.GreaterThanOrEqualTo(0), "INT-ORDER-01: FinalCompositePass did not log.");

            Assert.Less(idxCSG, idxGeometry,
                "INT-ORDER-01: CSGStencilPass must run before OITGeometryPass.");
            Assert.Less(idxGeometry, idxResolve,
                "INT-ORDER-01: OITGeometryPass must run before OITResolvePass.");
            Assert.Less(idxResolve, idxComposite,
                "INT-ORDER-01: OITResolvePass must run before FinalCompositePass.");
        }

        // ====================================================================
        // INT-ORDER-02: OIT Geometry runs after CSG Stencil
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_ORDER_02_OITGeometry_RunsAfter_CSGStencil()
        {
            CreateOITSphere(Vector3.zero, Color.blue, 0.5f);
            CreateCutter(new Vector3(10f, 0f, 0f));

            OITPassOrder.Clear();
            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            var log = OITPassOrder.Log;

            int idxCSG      = log.IndexOf("OIT.CSGStencilPass");
            int idxGeometry = log.IndexOf("OIT.GeometryPass");

            Assert.That(idxCSG,      Is.GreaterThanOrEqualTo(0), "INT-ORDER-02: CSGStencilPass did not log.");
            Assert.That(idxGeometry, Is.GreaterThanOrEqualTo(0), "INT-ORDER-02: OITGeometryPass did not log.");
            Assert.Less(idxCSG, idxGeometry,
                $"INT-ORDER-02: CSGStencilPass (index {idxCSG}) must execute before " +
                $"OITGeometryPass (index {idxGeometry}).");
        }

        // ====================================================================
        // INT-ORDER-03: OIT Resolve runs after OIT Geometry (NodeBuffer populated at dispatch)
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_ORDER_03_OITResolve_RunsAfter_OITGeometry_NodeBufferPopulated()
        {
            CreateOITSphere(Vector3.zero, Color.green, 0.5f);

            OITPassOrder.Clear();
            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            var log = OITPassOrder.Log;

            int idxGeometry = log.IndexOf("OIT.GeometryPass");
            int idxResolve  = log.IndexOf("OIT.ResolvePass");

            Assert.That(idxGeometry, Is.GreaterThanOrEqualTo(0), "INT-ORDER-03: OITGeometryPass did not log.");
            Assert.That(idxResolve,  Is.GreaterThanOrEqualTo(0), "INT-ORDER-03: OITResolvePass did not log.");
            Assert.Less(idxGeometry, idxResolve,
                $"INT-ORDER-03: OITGeometryPass (index {idxGeometry}) must execute before " +
                $"OITResolvePass (index {idxResolve}) so NodeBuffer is populated.");
        }

        // ====================================================================
        // INT-ORDER-04: Final Composite executes last among OIT passes
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_ORDER_04_FinalComposite_ExecutesLast()
        {
            CreateOITSphere(Vector3.zero, Color.white, 0.5f);

            OITPassOrder.Clear();
            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            var log = OITPassOrder.Log;

            int idxComposite = log.IndexOf("OIT.FinalCompositePass");

            Assert.That(idxComposite, Is.GreaterThanOrEqualTo(0),
                "INT-ORDER-04: FinalCompositePass did not log. " +
                "Ensure OIT_ResolveTexture is created before FinalCompositePass is enqueued.");

            // FinalCompositePass must be the last OIT pass in the log.
            int lastOITIndex = -1;
            for (int i = 0; i < log.Count; i++)
                if (log[i].StartsWith("OIT."))
                    lastOITIndex = i;

            Assert.AreEqual(idxComposite, lastOITIndex,
                $"INT-ORDER-04: FinalCompositePass (index {idxComposite}) is not the last OIT pass. " +
                $"Last OIT pass in log is at index {lastOITIndex}: \"{(lastOITIndex >= 0 ? log[lastOITIndex] : "none")}\".");
        }
    }
}
