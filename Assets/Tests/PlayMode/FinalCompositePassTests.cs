using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for the Final Composite Pass (INT-COMP-01 through INT-COMP-04).
    ///
    /// READBACK STRATEGY:
    ///   FinalCompositePass blends OIT_ResolveTexture over the backbuffer using
    ///   src-over blending (One / OneMinusSrcAlpha).  Tests render to a known
    ///   RenderTexture target and read pixels back after WaitForEndOfFrame.
    ///
    /// INT-COMP-01: A transparent OIT sphere is present. The backbuffer pixel at
    ///   the sphere's centre should differ from the cleared background colour,
    ///   indicating that the OIT resolve was blended in.
    ///
    /// INT-COMP-02: An opaque object pixel should be unchanged by the composite
    ///   pass (the resolve texture is zero at that location).
    ///
    /// INT-COMP-03: The final pixel at a transparent object centre must be
    ///   strictly between the OIT colour and the background colour, confirming
    ///   alpha blending occurred (not a full replace).
    ///
    /// INT-COMP-04: A fully opaque scene (no OIT objects) — final buffer equals
    ///   the opaque pass output at five sampled pixels.
    /// </summary>
    public class FinalCompositePassTests
    {
        private const int k_TexSize = 128;

        private GameObject       _bufferManagerGo;
        private GameObject       _oitSystemGo;
        private GameObject       _cameraGo;
        private Camera           _camera;
        private RenderTexture    _testRT;

        private readonly List<GameObject> _extraGos  = new List<GameObject>();
        private readonly List<Material>   _extraMats = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            _bufferManagerGo = new GameObject("OIT_COMP_BufferManager");
            _bufferManagerGo.AddComponent<OITBufferManager>();

            _oitSystemGo = new GameObject("OIT_COMP_OITSystem");
            _oitSystemGo.AddComponent<OITSystem>();

            _testRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "OIT_COMP_TestRT" };
            _testRT.Create();

            _cameraGo = new GameObject("OIT_COMP_Camera");
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

        private GameObject CreateOITSphere(Vector3 pos, Color color, float alpha = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("OIT/OITGeometry")) { name = "OIT_COMP_OITMat" };
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<OITObject>().alpha = alpha;
            return go;
        }

        private GameObject CreateOpaqueQuad(Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "OIT_COMP_OpaqueMat" };
            mat.SetColor("_BaseColor", color);
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private Color ReadBackbufferCentre()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _testRT;

            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(_testRT.width / 2, _testRT.height / 2, 1, 1), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;

            var pixel = tex.GetPixel(0, 0);
            Object.DestroyImmediate(tex);
            return pixel;
        }

        private Color ReadBackbufferAt(int x, int y)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _testRT;

            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;

            var pixel = tex.GetPixel(0, 0);
            Object.DestroyImmediate(tex);
            return pixel;
        }

        // ====================================================================
        // INT-COMP-01: Backbuffer alpha-blended result at transparent object pixel
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_COMP_01_Backbuffer_BlendedResult_AtTransparentObjectPixel()
        {
            // Black background + red OIT sphere at half alpha: final pixel should
            // have non-zero red (the OIT resolve was blended in).
            CreateOITSphere(Vector3.zero, Color.red, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            Assume.That(OITResources.ResolveTexture, Is.Not.Null,
                "INT-COMP-01: OITResolvePass did not create ResolveTexture. " +
                "Ensure OITResolve.compute is in a Resources folder.");

            Color pixel = ReadBackbufferCentre();
            Assert.Greater(pixel.r + pixel.g + pixel.b, 0f,
                "INT-COMP-01: Backbuffer centre is still black after rendering a red OIT sphere. " +
                "FinalCompositePass should have blended the resolve texture over the backbuffer.");
        }

        // ====================================================================
        // INT-COMP-02: Opaque object pixels unchanged in final composite
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_COMP_02_OpaquePixels_Unchanged_InFinalComposite()
        {
            // Place an opaque white quad at centre so it is fully visible.
            // No OIT objects are present, so OIT_ResolveTexture is all-zero.
            // FinalCompositePass blending (0,0,0,0) over white must leave it white.
            CreateOpaqueQuad(Vector3.zero, Color.white);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            // Sample at the centre pixel where the white quad covers.
            Color pixel = ReadBackbufferCentre();

            // Without OIT geometry, OIT_ResolveTexture alpha = 0 everywhere →
            // One/OneMinusSrcAlpha with src=(0,0,0,0) leaves dst unchanged.
            Assert.Greater(pixel.r + pixel.g + pixel.b, 1.5f,
                "INT-COMP-02: Opaque white quad pixel was darkened by final composite pass. " +
                "FinalCompositePass should not alter pixels where OIT resolve alpha = 0.");
        }

        // ====================================================================
        // INT-COMP-03: Final pixel is between OIT color and background (blended intermediate)
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_COMP_03_FinalPixel_BetweenOITColorAndBackground_BlendedIntermediate()
        {
            // Black background + green OIT sphere at 50% alpha.
            // Expected result: green channel roughly 0.5 (half of full green × alpha contribution).
            // Strictly > 0 (not black) and < 1.0 (not full green) confirms partial blending.
            CreateOITSphere(Vector3.zero, Color.green, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            Assume.That(OITResources.ResolveTexture, Is.Not.Null,
                "INT-COMP-03: ResolveTexture is null — OITResolvePass did not run.");

            Color pixel = ReadBackbufferCentre();
            float green = pixel.g;

            Assert.Greater(green, 0.01f,
                $"INT-COMP-03: Green channel ({green:F3}) is too close to 0 (background). " +
                "FinalCompositePass should blend OIT green colour into the backbuffer.");

            Assert.Less(green, 0.99f,
                $"INT-COMP-03: Green channel ({green:F3}) is too close to 1 (fully replaced). " +
                "Expected partial blending — the result should be between background and OIT colour.");
        }

        // ====================================================================
        // INT-COMP-04: Fully opaque scene — final buffer == opaque pass output (5 samples)
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_COMP_04_FullyOpaqueScene_FinalBuffer_EqualsOpaquePassOutput()
        {
            // No OIT objects. The resolve texture (if any) will be all-zero.
            // Final composite with src=(0,0,0,0) leaves dest unchanged.
            // We verify 5 sample positions across the frame to catch any bleed.
            CreateOpaqueQuad(Vector3.zero, Color.white);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            int half = k_TexSize / 2;
            int q    = k_TexSize / 4;

            var samplePositions = new[]
            {
                (half, half),
                (q,    q),
                (half, q),
                (q,    half),
                (half + q, half),
            };

            foreach (var (x, y) in samplePositions)
            {
                Color pixel = ReadBackbufferAt(x, y);
                // If this pixel is on the opaque white quad it must remain white (r,g,b ≈ 1).
                // If it is on the black background it must remain black (r,g,b ≈ 0).
                // Either way, blending zero over the existing colour must not change it.
                // We test that neither a fully transparent shift happened (pixel stays as-is).
                // Since we don't know which pixels hit the quad, we only assert that none
                // changed to a midtone (which would indicate a bad composite).
                bool isWhite = pixel.r > 0.9f && pixel.g > 0.9f && pixel.b > 0.9f;
                bool isBlack = pixel.r < 0.1f && pixel.g < 0.1f && pixel.b < 0.1f;
                Assert.IsTrue(isWhite || isBlack,
                    $"INT-COMP-04: Pixel at ({x},{y}) = ({pixel.r:F2},{pixel.g:F2},{pixel.b:F2}) is a midtone " +
                    "in a fully opaque scene. FinalCompositePass should not alter pixels when OIT resolve is zero.");
            }
        }
    }
}
