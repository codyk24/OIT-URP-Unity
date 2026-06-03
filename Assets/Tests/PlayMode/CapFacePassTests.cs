using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for the Cap Face Pass (INT-CAP-01 through INT-CAP-05).
    ///
    /// COLOUR READBACK STRATEGY:
    ///   The CapFacePass writes its cap colour directly to the active colour attachment,
    ///   which URP resolves to camera.targetTexture at the end of the frame. Tests set
    ///   _sceneRT as the camera's targetTexture before the frame, then ReadPixels from it
    ///   after yield return null to compare cap colour at stencil-marked pixels.
    ///
    ///   This contrasts with CSGStencilPassTests, which uses StencilCapturePass to
    ///   visualise stencil values. For CapFace the cap colour IS the colour output, so
    ///   the scene RT is the direct source of truth.
    ///
    /// STENCIL DEPENDENCY:
    ///   CapFacePass reads the stencil written by CSGStencilPass. Both passes run in the
    ///   same frame; CSGStencilPass fires at AfterRenderingOpaques and CapFacePass fires
    ///   at AfterRenderingOpaques + 1, so the stencil is always populated first.
    /// </summary>
    public class CapFacePassTests
    {
        private const int k_TexSize = 256;

        private GameObject    _cameraGo;
        private Camera        _mainCamera;
        private RenderTexture _sceneRT;
        private GameObject    _csgSystemGo;

        private readonly List<GameObject> _extraGos  = new List<GameObject>();
        private readonly List<Material>   _extraMats = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            // Scene RT with depth/stencil — URP RenderGraph requires a depth buffer on
            // camera.targetTexture. 24-bit depth is sufficient; stencil is read via the
            // internal activeDepthTexture, not this buffer.
            _sceneRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "CapFace_TestSceneRT" };
            _sceneRT.Create();

            _cameraGo   = new GameObject("CapFace_TestCamera");
            _mainCamera = _cameraGo.AddComponent<Camera>();
            _mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = Color.black;
            _mainCamera.targetTexture   = _sceneRT;
            _mainCamera.fieldOfView     = 90f;
            _mainCamera.transform.position = new Vector3(0f, 0f, -5f);
            _mainCamera.transform.LookAt(Vector3.zero);

            _csgSystemGo = new GameObject("CSGSystem");
            _csgSystemGo.AddComponent<CSGSystem>();
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

            if (_cameraGo    != null) Object.DestroyImmediate(_cameraGo);
            if (_csgSystemGo != null) Object.DestroyImmediate(_csgSystemGo);

            if (_sceneRT != null) { _sceneRT.Release(); Object.DestroyImmediate(_sceneRT); }
        }

        // -----------------------------------------------------------------------
        // Helper: read the colour of a world-space point from _sceneRT.
        // -----------------------------------------------------------------------
        private Color SampleSceneRTAtWorldPos(Vector3 worldPos)
        {
            var vp = _mainCamera.WorldToViewportPoint(worldPos);

            var prevRT = RenderTexture.active;
            RenderTexture.active = _sceneRT;
            var tex = new Texture2D(k_TexSize, k_TexSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, k_TexSize, k_TexSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prevRT;

            int px  = Mathf.Clamp(Mathf.RoundToInt(vp.x * (k_TexSize - 1)), 0, k_TexSize - 1);
            int py  = Mathf.Clamp(Mathf.RoundToInt(vp.y * (k_TexSize - 1)), 0, k_TexSize - 1);
            Color c = tex.GetPixel(px, py);
            Object.DestroyImmediate(tex);
            return c;
        }

        // -----------------------------------------------------------------------
        // Helper: sphere configured as a cutter. MeshRenderer disabled so the
        // opaque pass does not paint its colour into the scene.
        // -----------------------------------------------------------------------
        private CSGCutter CreateCutter(Vector3 pos, float radius = 1.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CapFace_TestCutter";
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.GetComponent<MeshRenderer>().enabled = false;
            var cutter = go.AddComponent<CSGCutter>();
            _extraGos.Add(go);
            return cutter;
        }

        // ====================================================================
        // INT-CAP-01: Cap colour visible at known cut-boundary pixel
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CAP_01_CapColor_VisibleAtCutBoundaryPixel()
        {
            // Cube at origin fully enclosed by a cutter sphere (r=2).
            // CapFacePass should paint the cutter's capColor (red) over the stencil-marked pixels.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            _extraGos.Add(cube);

            var cutter = CreateCutter(Vector3.zero, 2f);
            cutter.capColor = Color.red;

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color pixel = SampleSceneRTAtWorldPos(Vector3.zero);
            Assert.Greater(pixel.r, 0.5f,
                "INT-CAP-01: Expected cap colour (red, r > 0.5) at cut-boundary pixel " +
                $"but got r={pixel.r:F3}. Check CapFacePass is registered in OITRenderFeature " +
                "and CapFace.shader was imported successfully.");
            Assert.Less(pixel.g, 0.3f,
                $"INT-CAP-01: Green channel too high (g={pixel.g:F3}) — " +
                "cap colour should be red, not white or yellow.");
        }

        // ====================================================================
        // INT-CAP-02: Outside cut boundary — object colour, not cap colour
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CAP_02_OutsideCutBoundary_NotCapColor()
        {
            // Small cutter at origin; cube placed far to the right is outside the cutter.
            // The pixel at the cube's screen position should keep its opaque colour, not capColor.
            var distantCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            distantCube.transform.position = new Vector3(4f, 0f, 0f);
            _extraGos.Add(distantCube);

            var cutter = CreateCutter(Vector3.zero, 0.5f);
            cutter.capColor = Color.red;

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color pixel = SampleSceneRTAtWorldPos(new Vector3(4f, 0f, 0f));
            // Distant cube is not stencil-marked → should NOT be overwritten with red.
            Assert.Less(pixel.r, 0.5f,
                "INT-CAP-02: Pixel outside cut boundary was painted with cap colour — " +
                $"stencil leaked to geometry outside the cutter volume (r={pixel.r:F3}).");
        }

        // ====================================================================
        // INT-CAP-03: 10 consecutive frames — cap pixel is stable (no z-fighting)
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CAP_03_TenFrames_CapPixelStable_NoZFighting()
        {
            // ZWrite Off in CapFace.shader means depth is not perturbed each frame.
            // Ten frames of the same cut position should produce identical pixel colours.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            _extraGos.Add(cube);

            var cutter = CreateCutter(Vector3.zero, 2f);
            cutter.capColor = Color.red;

            // Warm up one frame before sampling.
            yield return null;

            const int frameCount = 10;
            var samples = new Color[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
                samples[i] = SampleSceneRTAtWorldPos(Vector3.zero);
            }

            Color reference = samples[0];
            for (int i = 1; i < frameCount; i++)
            {
                Assert.AreEqual(reference.r, samples[i].r, 0.02f,
                    $"INT-CAP-03: Red channel drifted between frame 0 and frame {i + 1} " +
                    $"(ref={reference.r:F3}, got={samples[i].r:F3}). Possible z-fighting.");
                Assert.AreEqual(reference.g, samples[i].g, 0.02f,
                    $"INT-CAP-03: Green channel drifted between frame 0 and frame {i + 1}.");
                Assert.AreEqual(reference.b, samples[i].b, 0.02f,
                    $"INT-CAP-03: Blue channel drifted between frame 0 and frame {i + 1}.");
            }
        }

        // ====================================================================
        // INT-CAP-04: Cap face composites correctly under transparent object alpha
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CAP_04_CapFace_CompositesUnderTransparentAlpha()
        {
            // Cap face (red) is written to the colour buffer at AfterRenderingOpaques+1.
            // A transparent blue quad (alpha=0.5) rendered at the transparent queue (>3000)
            // blends over the cap colour, producing a purple-ish (red+blue) mix.
            // This verifies that CapFacePass output IS in the colour buffer for subsequent
            // transparent passes to composite against.

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            _extraGos.Add(cube);

            var cutter = CreateCutter(Vector3.zero, 2f);
            cutter.capColor = Color.red;

            // Transparent quad slightly in front of the cube, large enough to cover the
            // stencil-marked screen area. Sprites/Default is always alpha-blended and
            // renders at renderQueue=3000, after the cap face pass.
            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.transform.position   = new Vector3(0f, 0f, -0.5f);
            quadGo.transform.localScale = Vector3.one * 4f;
            _extraGos.Add(quadGo);

            var transparentMat = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0f, 0f, 1f, 0.5f)
            };
            _extraMats.Add(transparentMat);
            quadGo.GetComponent<MeshRenderer>().material = transparentMat;

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color pixel = SampleSceneRTAtWorldPos(Vector3.zero);

            // After blending: blue (0,0,1)×0.5 + red (1,0,0)×0.5 → (0.5, 0, 0.5).
            // Allow generous tolerance for platform/API differences.
            Assert.Greater(pixel.r, 0.2f,
                $"INT-CAP-04: Red (cap) component too low after transparent blend (r={pixel.r:F3}). " +
                "Cap colour may not be reaching the colour buffer before transparent pass.");
            Assert.Greater(pixel.b, 0.2f,
                $"INT-CAP-04: Blue (transparent) component too low (b={pixel.b:F3}). " +
                "Transparent object may not be blending over cap colour correctly.");
        }

        // ====================================================================
        // INT-CAP-05: Cutter in empty space — cap pixel equals background
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_CAP_05_CutterInEmptySpace_CapPixelIsBackground()
        {
            // Cutter placed where no opaque geometry exists.
            // CSGStencilPass z-fail logic: the cutter back-face ZTest Greater fails against
            // the clear depth (1.0 for non-reverse-Z) because the back face has depth < 1.0.
            // No stencil increment → CapFacePass writes nothing → pixel stays background.
            _mainCamera.backgroundColor = Color.black;

            // Cutter at a position with no scene geometry behind it.
            var cutter = CreateCutter(new Vector3(0f, 0f, 0f), 0.5f);
            cutter.capColor = Color.red;
            // No opaque cube — the cutter floats in empty space.

            LogAssert.NoUnexpectedReceived();
            yield return null;

            Color pixel = SampleSceneRTAtWorldPos(Vector3.zero);
            Assert.Less(pixel.r, 0.1f,
                "INT-CAP-05: Cap colour was painted at an empty-space pixel — stencil should " +
                $"be 0 with no opaque geometry inside the cutter volume (r={pixel.r:F3}).");
        }
    }
}
