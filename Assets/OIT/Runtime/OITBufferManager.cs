using UnityEngine;
using UnityEngine.Rendering;

namespace OIT
{
    public sealed class OITBufferManager : MonoBehaviour
    {
        // Sentinel written to every HeadBuffer element to mark "no node at this pixel".
        public const uint HeadSentinel = 0xFFFFFFFF;

        // Set before SetActive(true) in tests to simulate a specific VRAM amount.
        // When <= 0, real SystemInfo.graphicsMemorySize is used.
        public long simulatedGraphicsMemoryBytes;

        public GraphicsBuffer HeadBuffer    { get; private set; }
        public GraphicsBuffer NodeBuffer    { get; private set; }
        public GraphicsBuffer AtomicCounter { get; private set; }
        public int            MaxLayers     { get; private set; }

        private void Awake()
        {
            OITResources.BufferManager = this;
            long vram = simulatedGraphicsMemoryBytes > 0
                ? simulatedGraphicsMemoryBytes
                : (long)SystemInfo.graphicsMemorySize * 1024L * 1024L;

            MaxLayers = OITBufferSizing.SelectMaxLayers(vram);
            int w = Screen.width;
            int h = Screen.height;

            HeadBuffer    = new GraphicsBuffer(GraphicsBuffer.Target.Structured, w * h,             4);
            NodeBuffer    = new GraphicsBuffer(GraphicsBuffer.Target.Structured, w * h * MaxLayers, 12);
            AtomicCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1,                 4);

            ClearBuffers();

            OITResources.HeadBuffer    = HeadBuffer;
            OITResources.NodeBuffer    = NodeBuffer;
            OITResources.AtomicCounter = AtomicCounter;
            // StencilMaskRT is created and sized per-camera by StencilCapturePass.
        }

        // Resets all buffers to frame-start state. Called at Awake and by the OIT Geometry Pass each frame.
        public void ClearBuffers()
        {
            var heads = new uint[HeadBuffer.count];
            System.Array.Fill(heads, HeadSentinel);
            HeadBuffer.SetData(heads);

            // NodeBuffer stride is 12 bytes = 3 uints per element; pass uint[] of length count*3.
            var nodes = new uint[NodeBuffer.count * 3];
            NodeBuffer.SetData(nodes);

            AtomicCounter.SetData(new uint[] { 0 });
        }

        private void OnDestroy()
        {
            if (OITResources.BufferManager == this)
                OITResources.BufferManager = null;

            HeadBuffer?.Release();    HeadBuffer    = null;
            NodeBuffer?.Release();    NodeBuffer    = null;
            AtomicCounter?.Release(); AtomicCounter = null;

            OITResources.HeadBuffer    = null;
            OITResources.NodeBuffer    = null;
            OITResources.AtomicCounter = null;

            // StencilMaskRT lifecycle is managed by StencilCapturePass.
        }
    }
}
