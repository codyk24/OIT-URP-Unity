using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for the CSG Stencil Pass (GPU z-fail inside-outside detection).
    /// Tests INT-CSG-01 through INT-CSG-05.
    ///
    /// STENCIL READBACK STRATEGY (INT-CSG-01 through 04):
    ///   URP's RenderGraph stores depth/stencil in resourceData.activeDepthTexture — an
    ///   internal transient texture. It is never copied to camera.targetTexture.depthBuffer,
    ///   so post-frame CommandBuffer probing of the camera RT's depth yields no stencil data.
    ///
    ///   Instead, StencilCapturePass runs WITHIN the same URP frame immediately after
    ///   CSGStencilPass. It binds the live activeDepthTexture as its depth attachment,
    ///   paints white where stencil != 0, and writes the result to StencilCapturePass.ActiveCapture
    ///   (a plain colour RenderTexture set by tests before the frame). After yield return null
    ///   the capture texture holds the stencil visualisation and can be ReadPixels'd directly.
    ///
    /// INT-CSG-05 reads the main scene colour RT to verify no colour was written by the
    ///   CSGStencil shader's ColorMask 0.
    /// </summary>
    public class CSGStencilPassTests
    {
        private const int k_TexSize = 256;

        private GameObject    _cameraGo;
        private Camera        _mainCamera;
        private RenderTexture _sceneRT;    // colour-only target for main camera (INT-CSG-05)
        private RenderTexture _captureRT;  // written by StencilCapturePass during frame
        private GameObject    _targetCubeGo;
        private GameObject    _csgSystemGo;

        private System.Collections.Generic.List<GameObject> _extraGos =
            new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // Colour RT with depth buffer — URP's RenderGraph requires camera.targetTexture
            // to have a depth buffer (Depth Stencil Format != None). We no longer read
            // stencil from this buffer (StencilCapturePass uses the internal activeDepthTexture),
            // so a plain 24-bit depth is sufficient.
            _sceneRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "TestSceneRT" };
            _sceneRT.Create();

            // Colour-only RT — StencilCapturePass writes stencil-as-colour here each frame.
            // Must be created and assigned BEFORE yield return null so the pass sees it.
            _captureRT = new RenderTexture(k_TexSize, k_TexSize, 0, RenderTextureFormat.ARGB32)
                { name = "StencilCaptureRT" };
            _captureRT.Create();
            StencilCapturePass.ActiveCapture = _captureRT;

            // Main camera.
            _cameraGo   = new GameObject("CSG_TestCamera");
            _mainCamera = _cameraGo.AddComponent<Camera>();
            _mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = Color.black;
            _mainCamera.targetTexture   = _sceneRT;
            // 90° FOV gives a horizontal half-extent of 5 × tan(45°) = 5 world units at z=0,
            // ensuring objects up to x=±4 are visible from (0,0,-5). The default 60° FOV
            // only reaches ≈2.9 units, which clips cubeB at x=4 in INT-CSG-04.
            _mainCamera.fieldOfView     = 90f;
            _mainCamera.transform.position = new Vector3(0f, 0f, -5f);
            _mainCamera.transform.LookAt(Vector3.zero);

            // Default opaque target at origin.
            _targetCubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _targetCubeGo.transform.position = Vector3.zero;

            // CSGSystem singleton — must exist before CSGCutter OnEnable fires.
            _csgSystemGo = new GameObject("CSGSystem");
            _csgSystemGo.AddComponent<CSGSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            StencilCapturePass.ActiveCapture = null;

            foreach (var go in _extraGos)
                if (go != null) Object.DestroyImmediate(go);
            _extraGos.Clear();

            if (_cameraGo     != null) Object.DestroyImmediate(_cameraGo);
            if (_targetCubeGo != null) Object.DestroyImmediate(_targetCubeGo);
            if (_csgSystemGo  != null) Object.DestroyImmediate(_csgSystemGo);

            if (_sceneRT   != null) { _sceneRT.Release();   Object.DestroyImmediate(_sceneRT); }
            if (_captureRT != null) { _captureRT.Release(); Object.DestroyImmediate(_captureRT); }
        }

        // -----------------------------------------------------------------------
        // Helper: read from _captureRT (written by StencilCapturePass during the frame).
        // Returns white (r ≈ 1) at pixels that had stencil != 0, black elsewhere.
        // -----------------------------------------------------------------------
        private Color SampleStencilAtWorldPos(Vector3 worldPos)
        {
            var vp = _mainCamera.WorldToViewportPoint(worldPos);

            var prevRT = RenderTexture.active;
            RenderTexture.active = _captureRT;
            var tex = new Texture2D(k_TexSize, k_TexSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, k_TexSize, k_TexSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prevRT;

            int px    = Mathf.Clamp(Mathf.RoundToInt(vp.x * (k_TexSize - 1)), 0, k_TexSize - 1);
            int py    = Mathf.Clamp(Mathf.RoundToInt(vp.y * (k_TexSize - 1)), 0, k_TexSize - 1);
            Color col = tex.GetPixel(px, py);
            Object.DestroyImmediate(tex);
            return col;
        }

        // -----------------------------------------------------------------------
        // Helper: sphere primitive configured as a cutter. MeshRenderer disabled
        // so the opaque pass does not render its colour into the scene.
        // -----------------------------------------------------------------------
        private CSGCutter CreateCutter(Vector3 worldPos, float radius = 1.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TestCutter";
            go.transform.position   = worldPos;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.GetComponent<MeshRenderer>().enabled = false;
            var cutter = go.AddComponent<CSGCutter>();
            _extraGos.Add(go);
            return cutter;
        }

        // ====================================================================
        // INT-CSG-01: Stencil != 0 at pixel inside cutter projection
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CSG_01_PixelInsideCutter_StencilNonZero()
        {
            // Cutter sphere (r=2) centred at origin encloses the target cube.
            // Camera is at z=-5. Cube at z=0. Cube is inside cutter → stencil > 0.
            CreateCutter(Vector3.zero, 2f);

            LogAssert.NoUnexpectedReceived();
            yield return null; // CSGStencilPass writes stencil; StencilCapturePass writes capture RT.

            Color sample = SampleStencilAtWorldPos(Vector3.zero);
            Assert.Greater(sample.r, 0.5f,
                "INT-CSG-01: Expected stencil > 0 at pixel inside cutter projection " +
                $"but capture RT returned r={sample.r:F3} (black = stencil was 0). " +
                "Check that OITRenderFeature is on URP-Balanced-Renderer.asset and " +
                "StencilCapturePass.ActiveCapture was set before the frame.");
        }

        // ====================================================================
        // INT-CSG-02: Stencil == 0 at pixel outside cutter projection
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CSG_02_PixelOutsideCutter_StencilZero()
        {
            // Cutter placed far to the side — does not enclose the target cube at origin.
            CreateCutter(new Vector3(5f, 0f, 0f), 1f);

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color sample = SampleStencilAtWorldPos(Vector3.zero);
            Assert.Less(sample.r, 0.1f,
                "INT-CSG-02: Expected stencil == 0 at pixel outside cutter projection " +
                $"but capture RT returned r={sample.r:F3}.");
        }

        // ====================================================================
        // INT-CSG-03: Non-target object pixel never marked by cutter
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CSG_03_NonTargetObject_NotMarkedByCutter()
        {
            // Cutter encloses only the main cube at origin.
            // A second cube placed 4 units to the right must remain unstencilled.
            CreateCutter(Vector3.zero, 1.2f);

            var sideCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideCube.transform.position = new Vector3(4f, 0f, 0f);
            _extraGos.Add(sideCube);

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color sample = SampleStencilAtWorldPos(new Vector3(4f, 0f, 0f));
            Assert.Less(sample.r, 0.1f,
                "INT-CSG-03: Non-target object pixel was stencil-marked — cutter leaked to " +
                $"geometry outside its volume (capture r={sample.r:F3}).");
        }

        // ====================================================================
        // INT-CSG-04: Two cutters on two objects — independent stencil, no crossover
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CSG_04_TwoCutters_TwoObjects_IndependentStencil_NoCrossover()
        {
            // CutterA surrounds cube at origin; cubeB at x=4 with cutterB around it.
            // The midpoint at x=2 (between the two objects) should have stencil == 0.
            CreateCutter(Vector3.zero, 1.2f);

            var cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeB.transform.position = new Vector3(4f, 0f, 0f);
            _extraGos.Add(cubeB);

            CreateCutter(new Vector3(4f, 0f, 0f), 1.2f);

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color colorA   = SampleStencilAtWorldPos(Vector3.zero);
            Color colorB   = SampleStencilAtWorldPos(new Vector3(4f, 0f, 0f));
            Color colorMid = SampleStencilAtWorldPos(new Vector3(2f, 0f, 0f));

            Assert.Greater(colorA.r,   0.5f, "INT-CSG-04: CubeA pixel should be stencil > 0 (inside cutterA).");
            Assert.Greater(colorB.r,   0.5f, "INT-CSG-04: CubeB pixel should be stencil > 0 (inside cutterB).");
            Assert.Less   (colorMid.r, 0.1f, "INT-CSG-04: Mid-gap pixel should be stencil == 0 (no crossover).");
        }

        // ====================================================================
        // INT-CSG-05: Cutter ColorWriteMask=0 — no colour written to render target
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CSG_05_CutterColorWriteMask0_NoColorWritten()
        {
            // A very large cutter sphere (r=10) with its MeshRenderer enabled and coloured
            // bright red. The camera is inside the sphere, so the opaque pass renders nothing
            // from the sphere at the centre pixel (backfaces culled from inside).
            // CSGStencilPass then runs — its ColorMask 0 must not write the red to colour.
            // The centre pixel should therefore NOT be bright red.
            var cutterGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cutterGo.name = "HugeCutter";
            cutterGo.transform.position   = Vector3.zero;
            cutterGo.transform.localScale = Vector3.one * 20f; // radius = 10 world units
            var mr = cutterGo.GetComponent<MeshRenderer>();
            mr.enabled        = true; // deliberately left on — would paint red if ColorMask != 0
            mr.material.color = Color.red;
            cutterGo.AddComponent<CSGCutter>();
            _extraGos.Add(cutterGo);

            _mainCamera.backgroundColor = Color.black;

            LogAssert.NoUnexpectedReceived();
            yield return null;

            // Read colour directly from _sceneRT (the camera's colour output).
            var prevRT = RenderTexture.active;
            RenderTexture.active = _sceneRT;
            var tex = new Texture2D(k_TexSize, k_TexSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, k_TexSize, k_TexSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prevRT;

            Color center = tex.GetPixel(k_TexSize / 2, k_TexSize / 2);
            Object.DestroyImmediate(tex);

            // If CSGStencil.shader had ColorMask != 0, the cutter fragments would write the
            // sphere's red material colour (r ≈ 1, g ≈ 0, b ≈ 0) to the render target.
            //
            // CapFacePass legitimately runs here (the cutter IS registered with CSGSystem)
            // and paints capColor (Color.white = r=g=b=1) at stencil-marked pixels. Pure
            // white is NOT a failure — it means stencil worked correctly and CapFace did its
            // job. Only pure red (r ≈ 1, g ≈ 0, b ≈ 0) indicates CSGStencil wrote colour.
            bool isPureRed = center.r > 0.8f && center.g < 0.2f && center.b < 0.2f;
            Assert.IsFalse(isPureRed,
                $"INT-CSG-05: Centre pixel is pure red (r={center.r:F3} g={center.g:F3} " +
                $"b={center.b:F3}), indicating CSGStencil.shader wrote colour despite ColorMask 0. " +
                "White (from CapFacePass capColor) would be acceptable; pure red is not.");
        }
    }
}
