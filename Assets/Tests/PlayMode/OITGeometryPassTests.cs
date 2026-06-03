using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OIT;

namespace OIT.Tests
{
    /// <summary>
    /// Integration tests for the OIT Geometry Pass (INT-GEO-01 through INT-GEO-05).
    ///
    /// BUFFER READBACK STRATEGY:
    ///   OITGeometryPass writes to three GraphicsBuffers (HeadBuffer, NodeBuffer,
    ///   AtomicCounter) via fragment-shader UAV writes. Tests use
    ///   <see cref="GraphicsBuffer.GetData"/> after <c>WaitForEndOfFrame</c> to
    ///   synchronously read back the GPU results. This stalls the CPU until the
    ///   GPU has finished the frame, which is acceptable in test contexts.
    ///
    /// NODE LAYOUT (12 bytes per element, matching OITBufferManager stride):
    ///   [0] packedColor  — RGBA8 packed uint (R in bits 31-24)
    ///   [1] depth        — fragment depth as asuint(float)
    ///   [2] next         — index of previous head (sentinel = 0xFFFFFFFF)
    ///
    /// CSG DEPENDENCY (INT-GEO-04):
    ///   OITGeometryPass uses Stencil { Ref 0; Comp Equal } so it skips pixels
    ///   where CSGStencilPass has written a non-zero value. The test creates a
    ///   CSGSystem + CSGCutter that encloses the OIT object, verifying that no
    ///   nodes are appended when all visible pixels are masked.
    /// </summary>
    public class OITGeometryPassTests
    {
        // k_TexSize must satisfy: k_TexSize² ≤ Screen.width × Screen.height so that
        // pixel indices written by the shader (py * k_TexSize + px) never exceed
        // HeadBuffer.count (which is Screen.width × Screen.height).
        // 128 × 128 = 16 384 — safe even at a 200 × 200 game-view resolution.
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
            // OITBufferManager allocates HeadBuffer / NodeBuffer / AtomicCounter.
            // Sizes are based on Screen.width × Screen.height so buffer indices
            // are always in-bounds when the shader uses cameraData.pixelWidth.
            _bufferManagerGo = new GameObject("OIT_GEO_BufferManager");
            _bufferManager   = _bufferManagerGo.AddComponent<OITBufferManager>();

            _oitSystemGo = new GameObject("OIT_GEO_OITSystem");
            _oitSystemGo.AddComponent<OITSystem>();

            // Explicit targetTexture with a 24-bit depth/stencil buffer.
            // This ensures URP renders directly to this RT so that
            // resourceData.activeDepthTexture wraps the same depth surface that
            // OITGeometryPass binds in its render func via CameraTargetRT.depthBuffer.
            // Without targetTexture, URP's active depth and BuiltinRenderTextureType.Depth
            // can point to different surfaces, breaking the stencil test (INT-GEO-04).
            _testRT = new RenderTexture(k_TexSize, k_TexSize, 24, RenderTextureFormat.ARGB32)
                { name = "OIT_GEO_TestRT" };
            _testRT.Create();

            _cameraGo = new GameObject("OIT_GEO_Camera");
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

            if (_cameraGo          != null) Object.DestroyImmediate(_cameraGo);
            if (_oitSystemGo       != null) Object.DestroyImmediate(_oitSystemGo);
            if (_bufferManagerGo   != null) Object.DestroyImmediate(_bufferManagerGo);
            if (_testRT            != null) { _testRT.Release(); Object.DestroyImmediate(_testRT); }
        }

        // -----------------------------------------------------------------------
        // Helper: create a sphere with an OITObject component and OITGeometry material.
        // -----------------------------------------------------------------------
        private GameObject CreateOITSphere(Vector3 pos, Color color, float alpha = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            _extraGos.Add(go);

            var mat = new Material(Shader.Find("OIT/OITGeometry"))
            {
                name = "OIT_GEO_TestMat"
            };
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            _extraMats.Add(mat);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            go.AddComponent<OITObject>().alpha = alpha;
            return go;
        }

        // -----------------------------------------------------------------------
        // Helper: read back AtomicCounter as uint[1].
        // -----------------------------------------------------------------------
        private uint ReadCounter()
        {
            var data = new uint[1];
            _bufferManager.AtomicCounter.GetData(data);
            return data[0];
        }

        // -----------------------------------------------------------------------
        // Helper: read all node data as flat uint array (3 uints per node).
        // -----------------------------------------------------------------------
        private uint[] ReadNodeData()
        {
            var data = new uint[_bufferManager.NodeBuffer.count * 3];
            _bufferManager.NodeBuffer.GetData(data);
            return data;
        }

        // -----------------------------------------------------------------------
        // Helper: read HeadBuffer.
        // -----------------------------------------------------------------------
        private uint[] ReadHeadBuffer()
        {
            var data = new uint[_bufferManager.HeadBuffer.count];
            _bufferManager.HeadBuffer.GetData(data);
            return data;
        }

        // -----------------------------------------------------------------------
        // Helper: traverse linked list starting at headIndex; return node count.
        // -----------------------------------------------------------------------
        private int TraverseLinkedList(uint headIndex, uint[] nodeData)
        {
            int count  = 0;
            uint cur   = headIndex;
            const int  maxIterations = 256;
            while (cur != OITBufferManager.HeadSentinel && count < maxIterations)
            {
                count++;
                cur = nodeData[cur * 3 + 2]; // .next field
            }
            return count;
        }

        // ====================================================================
        // INT-GEO-01: AtomicCounter > 0 after 1 transparent sphere rendered
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_GEO_01_Counter_GreaterThanZero_AfterOneSphere()
        {
            CreateOITSphere(Vector3.zero, Color.red);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            uint counter = ReadCounter();
            Assert.Greater(counter, 0u,
                "INT-GEO-01: AtomicCounter should be > 0 after rendering one transparent sphere, " +
                "but was 0. Check OITGeometryPass is registered in OITRenderFeature and " +
                "OIT/OITGeometry shader was imported successfully.");
        }

        // ====================================================================
        // INT-GEO-02: AtomicCounter = 0 with no transparent objects
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_GEO_02_Counter_IsZero_NoTransparentObjects()
        {
            // No OIT objects in scene — only the camera and empty buffers.
            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            uint counter = ReadCounter();
            Assert.AreEqual(0u, counter,
                $"INT-GEO-02: AtomicCounter should be 0 with no OIT objects, but was {counter}. " +
                "OITGeometryPass may be appending nodes when no OITObjects are registered.");
        }

        // ====================================================================
        // INT-GEO-03: Node at sphere pixel has correctly packed material colour
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_GEO_03_Node_HasCorrectlyPackedMaterialColor()
        {
            // Create a red sphere. Verify that at least one node in NodeBuffer
            // decodes to approximately red. Avoids pixel-index Y-flip ambiguity
            // by scanning all allocated nodes rather than addressing a specific pixel.
            var expectedColor = new Color(1f, 0f, 0f, 0.5f);
            CreateOITSphere(Vector3.zero, Color.red, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            uint counter  = ReadCounter();
            Assert.Greater(counter, 0u,
                "INT-GEO-03: No nodes were allocated — sphere may not be visible to camera.");

            uint[] nodeData = ReadNodeData();
            bool   found    = false;
            uint   numNodes = System.Math.Min(counter, (uint)_bufferManager.NodeBuffer.count);

            for (uint i = 0; i < numNodes; i++)
            {
                uint packed = nodeData[i * 3]; // [0] = packedColor
                Color decoded = OITColorPacking.UnpackRGBA(packed);

                if (decoded.r > 0.8f && decoded.g < 0.2f && decoded.b < 0.2f)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                "INT-GEO-03: No node found with correctly packed red colour. " +
                "Check PackRGBA packing in OITGeometry.shader matches OITColorPacking.cs " +
                "(R in bits 31-24, G in 23-16, B in 15-8, A in 7-0).");
        }

        // ====================================================================
        // INT-GEO-04: CSG-masked pixel — no node appended for masked object
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_GEO_04_CSGMaskedPixels_NoNodesAppended()
        {
            // Opaque cube at origin — provides depth so CSGStencilPass can mark stencil.
            // Large CSG cutter sphere encloses the entire cube.
            // OIT sphere at the same location: all its visible pixels are stencil-masked,
            // so OITGeometryPass should append zero nodes.

            var opaqueCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            opaqueCube.transform.position   = Vector3.zero;
            opaqueCube.transform.localScale = Vector3.one * 3f;
            _extraGos.Add(opaqueCube);

            var csgSystemGo = new GameObject("OIT_GEO_CSGSystem");
            csgSystemGo.AddComponent<CSGSystem>();
            _extraGos.Add(csgSystemGo);

            // Cutter encloses cube and OIT sphere entirely.
            var cutterGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cutterGo.transform.position   = Vector3.zero;
            cutterGo.transform.localScale = Vector3.one * 10f;
            cutterGo.GetComponent<MeshRenderer>().enabled = false;
            cutterGo.AddComponent<CSGCutter>();
            _extraGos.Add(cutterGo);

            // OIT sphere — same position as cube, should be entirely masked.
            CreateOITSphere(Vector3.zero, Color.blue, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            uint counter = ReadCounter();
            Assert.AreEqual(0u, counter,
                $"INT-GEO-04: Expected 0 nodes (all OIT pixels CSG-masked) but AtomicCounter = {counter}. " +
                "Check OITGeometry.shader Stencil {{ Ref 0; Comp Equal }} and that CSGStencilPass " +
                "runs before OITGeometryPass.");
        }

        // ====================================================================
        // INT-GEO-05: Two overlapping transparent objects — linked list has 2 nodes
        // ====================================================================
        [UnityTest]
        public IEnumerator INT_GEO_05_TwoOverlappingObjects_LinkedListHasTwoNodes()
        {
            // Two large quads at slightly different depths so both pass ZTest.
            // They cover most of the screen, guaranteeing overlap pixels exist.
            // We scan all pixels in HeadBuffer to find at least one with 2 nodes.

            CreateOITSphere(new Vector3(0f, 0f,  0.2f), Color.red,  0.5f);
            CreateOITSphere(new Vector3(0f, 0f, -0.2f), Color.blue, 0.5f);

            LogAssert.NoUnexpectedReceived();
            yield return new WaitForEndOfFrame();

            uint counter = ReadCounter();
            Assert.GreaterOrEqual(counter, 2u,
                $"INT-GEO-05: Expected at least 2 nodes (one per overlapping sphere), got {counter}. " +
                "Both spheres must be visible and append fragments.");

            uint[] heads    = ReadHeadBuffer();
            uint[] nodeData = ReadNodeData();

            bool foundPixelWithTwoNodes = false;
            for (int i = 0; i < heads.Length && !foundPixelWithTwoNodes; i++)
            {
                if (heads[i] == OITBufferManager.HeadSentinel)
                    continue;

                if (TraverseLinkedList(heads[i], nodeData) >= 2)
                    foundPixelWithTwoNodes = true;
            }

            Assert.IsTrue(foundPixelWithTwoNodes,
                "INT-GEO-05: No pixel found with a linked list of length >= 2. " +
                "Two overlapping transparent spheres should produce 2 nodes at their " +
                "overlap pixels. Check per-pixel linked list insertion in OITGeometry.shader.");
        }
    }
}
