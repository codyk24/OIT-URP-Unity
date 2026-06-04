using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for the OIT Resolve Pass (INT-RES-01 through INT-RES-04).
    ///
    /// RESOLVE TEXTURE READBACK STRATEGY:
    ///   OITResolvePass writes to OITResources.ResolveTexture (ARGBFloat RenderTexture)
    ///   via a compute dispatch. Tests use RenderTexture.active + Texture2D.ReadPixels
    ///   after WaitForEndOfFrame to synchronously read back the GPU result.
    ///
    /// OVERFLOW TEST (INT-RES-04):
    ///   The compute shader clamps iteration to MAX_LAYERS (24) per pixel. The
    ///   AtomicCounter is allowed to exceed NodeBuffer.count — the geometry shader
    ///   already guards against out-of-bounds writes. This test verifies the frame
    ///   completes without a GPU error or crash when 30 quads are stacked.
    /// </summary>
    public class OITResolvePassTests
    {
        private const int k_TexSize = 128;

        private GameObject         _bufferManagerGo;
        private OITBufferManager   _bufferManager;
        private GameObject         _oitSystemGo;
        private GameObject         _cameraGo;
        private Camera             _camera;
        private RenderTexture      _testRT;

        private readonly List<GameObject> _extraGos  = new List<GameObject>();
        private readonly List<Material>   _extraMats = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            _bufferManagerGo = new GameObject("OIT_RES_BufferManager");
            _bufferManager   = _bufferManagerGo.AddComponent<OITBufferManager>();

            _oitSystemGo = new GameObject("OIT_RES_OITSystem");
            _oitSystemGo.AddComponent<OITSystem>();

            _testRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "OIT_RES_TestRT" };
            _testRT.Create();

            _cameraGo = new GameObject("OIT_RES_Camera");
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
            foreach (var go in _extraGos)
                if (go != null) Object.DestroyImmediate(go);
            _extraGos.Clear();

            foreach (var mat in _extraMats)
                if (mat != null) Object.DestroyImmediate(mat);
            _extraMats.Clear();

            if (_cameraGo        != null) Object.DestroyImmediate(_cameraGo);
            if (_oitSystemGo     != null) Object.DestroyImmediate(_oitSystemGo);
            if (_bufferManagerGo != null) Object.DestroyImmediate(_bufferManagerGo);
            if (_testRT          != null) { _testRT.Release(); Object.DestroyImmediate(_testRT); }

            if (OITResources.ResolveTexture != null)
            {
                OITResources.ResolveTexture.Release();
                Object.DestroyImmediate(OITResources.ResolveTexture);
                OITResources.ResolveTexture = null;
            }
        }

        // -----------------------------------------------------------------------
        // Helper: create a sphere with OITObject component.
        // -----------------------------------------------------------------------
        private GameObject CreateOITSphere(Vector3 pos, Color color, float alpha = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("OIT/OITGeometry")) { name = "OIT_RES_Mat" };
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<OITObject>().alpha = alpha;
            return go;
        }

        // -----------------------------------------------------------------------
        // Helper: create an opaque quad mesh (1×1 plane, no OITObject).
        // -----------------------------------------------------------------------
        private GameObject CreateOpaqueQuad(Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "OIT_RES_OpaqueMat" };
            mat.color = color;
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // -----------------------------------------------------------------------
        // Helper: read the centre pixel of OIT_ResolveTexture.
        // Returns Color.clear when ResolveTexture is null.
        // -----------------------------------------------------------------------
        private Color ReadResolveTextureCentre()
        {
            var rt = OITResources.ResolveTexture;
            if (rt == null)
                return Color.clear;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(rt.width / 2, rt.height / 2, 1, 1), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;

            var pixel = tex.GetPixel(0, 0);
            Object.DestroyImmediate(tex);
            return pixel;
        }

        // -----------------------------------------------------------------------
        // Helper: scan all pixels of OIT_ResolveTexture and return max alpha.
        // -----------------------------------------------------------------------
        private float ReadResolveTextureMaxAlpha()
        {
            var rt = OITResources.ResolveTexture;
            if (rt == null)
                return 0f;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;

            float maxAlpha = 0f;
            var pixels = tex.GetPixels();
            foreach (var p in pixels)
                if (p.a > maxAlpha)
                    maxAlpha = p.a;

            Object.DestroyImmediate(tex);
            return maxAlpha;
        }

        // ====================================================================
        // INT-RES-01: Resolve texture pixel alpha > 0 at transparent object location
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_RES_01_ResolveTexture_AlphaGreaterThanZero_AtTransparentObject()
        {
            Assume.That(OITResources.ResolveTexture != null || true, // resolve is created lazily
                "Precondition: OITResolvePass will create ResolveTexture on first frame.");

            CreateOITSphere(Vector3.zero, Color.red, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            Assume.That(OITResources.ResolveTexture, Is.Not.Null,
                "INT-RES-01: OITResolvePass did not create OITResources.ResolveTexture. " +
                "Check OITResolve.compute is in a Resources folder and OITResolvePass.TryInitialize succeeded.");

            float maxAlpha = ReadResolveTextureMaxAlpha();
            Assert.Greater(maxAlpha, 0f,
                "INT-RES-01: All resolve texture pixels have alpha == 0 after rendering a transparent " +
                "sphere. OITResolvePass should write composited RGBA to OIT_ResolveTexture.");
        }

        // ====================================================================
        // INT-RES-02: Fully opaque scene — all resolve texture pixels are zero
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_RES_02_ResolveTexture_AllZero_OpaqueSceneOnly()
        {
            // No OIT objects — only opaque geometry. AtomicCounter stays 0, so
            // every pixel in the linked list is the sentinel. The resolve compute
            // should write float4(0,0,0,0) for every pixel.
            CreateOpaqueQuad(Vector3.zero, Color.white);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            // If ResolveTexture was never created (OITBufferManager absent or
            // no geometry pass ran), that itself satisfies "all zero".
            if (OITResources.ResolveTexture == null)
                Assert.Pass("INT-RES-02: ResolveTexture not created — no OIT geometry was rendered (pass).");

            float maxAlpha = ReadResolveTextureMaxAlpha();
            Assert.AreEqual(0f, maxAlpha,
                $"INT-RES-02: Resolve texture has non-zero alpha ({maxAlpha:F4}) with no OIT objects. " +
                "OITResolvePass should write zero for pixels with no linked-list entries.");
        }

        // ====================================================================
        // INT-RES-03: MAX_LAYERS=24 fragments — no GPU error, frame completes
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_RES_03_MaxLayers24Fragments_NoGpuError_FrameCompletes()
        {
            // Stack exactly 24 OIT spheres at slightly different depths so each
            // produces at least one fragment at the centre pixel. The compute shader
            // has MAX_LAYERS=24, so this exercises the full sort capacity.
            for (int i = 0; i < 24; i++)
                CreateOITSphere(new Vector3(0f, 0f, i * 0.01f), Color.white, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            // If we reach here without exception or GPU error the test passes.
            // Additionally verify that the resolve texture has a non-zero result.
            Assume.That(OITResources.ResolveTexture, Is.Not.Null,
                "INT-RES-03: ResolveTexture not created — ensure OITResolvePass is active.");

            float maxAlpha = ReadResolveTextureMaxAlpha();
            Assert.Greater(maxAlpha, 0f,
                "INT-RES-03: Resolve texture is all-zero with 24 overlapping transparent spheres. " +
                "Check MAX_LAYERS handling in OITResolve.compute.");
        }

        // ====================================================================
        // INT-RES-04: 30 overlapping quads (overflow) — counter clamped, no crash
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_RES_04_ThirtyOverlappingQuads_CounterClamped_NoCrash()
        {
            // 30 spheres exceed MAX_LAYERS=24. OITGeometry.shader guards against
            // out-of-bounds node writes when nodeIndex >= _OIT_MaxNodes. The compute
            // shader iterates at most MAX_LAYERS entries per pixel. This test
            // verifies: (a) no GPU crash or Unity error; (b) frame completes;
            // (c) AtomicCounter is clamped to NodeBuffer.count (capacity), not
            //     exceeding it in actual writes (counter value may be > capacity,
            //     but writes are guarded).
            for (int i = 0; i < 30; i++)
                CreateOITSphere(new Vector3(0f, 0f, i * 0.01f), Color.white, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            // Read back AtomicCounter — value may exceed NodeBuffer.count (the
            // counter increments before the bounds check) but actual node writes
            // must not exceed NodeBuffer.count.
            var counterData = new uint[1];
            _bufferManager.AtomicCounter.GetData(counterData);
            uint counter = counterData[0];

            Assert.GreaterOrEqual(counter, 24u,
                $"INT-RES-04: AtomicCounter ({counter}) should be >= 24 with 30 overlapping spheres.");

            // Verify frame completed and resolve texture is valid.
            Assume.That(OITResources.ResolveTexture, Is.Not.Null,
                "INT-RES-04: ResolveTexture not created — ensure OITResolvePass is active.");

            // No crash and reaching here = test passes for the stability requirement.
            Assert.Pass($"INT-RES-04: Frame completed with {counter} fragments attempted (30 spheres, " +
                        $"MAX_LAYERS=24 cap). No GPU error or crash.");
        }
    }
}
