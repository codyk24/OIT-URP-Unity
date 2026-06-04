using UnityEngine;
using UnityEngine.Rendering;

namespace OIT
{
    public static class OITResources
    {
        public static GraphicsBuffer   HeadBuffer;
        public static GraphicsBuffer   NodeBuffer;
        public static GraphicsBuffer   AtomicCounter;

        /// <summary>
        /// Screen-sized R8 <see cref="RenderTexture"/> that <see cref="StencilCapturePass"/>
        /// writes into each frame: white = CSG-masked pixel, black = unmasked.
        /// Sampled by <see cref="OITGeometryPass"/> to discard masked fragments in lieu of
        /// hardware stencil, which is inaccessible from an UnsafePass in URP RenderGraph.
        /// </summary>
        public static RenderTexture    StencilMaskRT;

        /// <summary>Imported RTHandle for <see cref="StencilMaskRT"/>; used by RenderGraph.</summary>
        public static RTHandle         StencilMaskHandle;

        /// <summary>
        /// The active <see cref="OITBufferManager"/> in the scene.
        /// Set by <see cref="OITBufferManager.Awake"/> and cleared by
        /// <see cref="OITBufferManager.OnDestroy"/>.
        /// </summary>
        public static OITBufferManager BufferManager;
    }
}
