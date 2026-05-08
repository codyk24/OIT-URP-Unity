using NUnit.Framework;
using OIT;

namespace OIT.Tests
{
    public class CSGStencilLogicTests
    {
        // UNIT-CSG-01: Pixel inside convex cutter volume → net stencil > 0
        // Simulate: back face depth test passes (cutter geometry hits the scene object)
        [Test]
        public void UNIT_CSG_01_PixelInsideCutter_StencilPositive()
        {
            var stencil = new CSGStencilLogic();
            stencil.OnBackFaceDepthPass();
            Assert.That(stencil.IsInsideCutter(), Is.True);
            Assert.That(stencil.StencilValue, Is.GreaterThan(0));
        }

        // UNIT-CSG-02: Pixel outside cutter volume → net stencil = 0
        // No depth tests pass → stencil remains at zero
        [Test]
        public void UNIT_CSG_02_PixelOutsideCutter_StencilZero()
        {
            var stencil = new CSGStencilLogic();
            Assert.That(stencil.IsInsideCutter(), Is.False);
            Assert.That(stencil.StencilValue, Is.EqualTo(0));
        }

        // UNIT-CSG-03: Two separate cutters on two objects → no cross-contamination
        // Operations on one instance do not affect the other
        [Test]
        public void UNIT_CSG_03_TwoCutters_Independent_NoCrossContamination()
        {
            var cutterA = new CSGStencilLogic();
            var cutterB = new CSGStencilLogic();

            cutterA.OnBackFaceDepthPass();
            cutterA.OnBackFaceDepthPass();

            Assert.That(cutterA.StencilValue, Is.EqualTo(2), "Cutter A should have stencil 2");
            Assert.That(cutterB.StencilValue, Is.EqualTo(0), "Cutter B should be unaffected");
            Assert.That(cutterB.IsInsideCutter(), Is.False, "Cutter B pixel should not be marked");
        }

        // UNIT-CSG-04: Cutter outside scene (no geometry at pixel) → depth test fails → no stencil mark
        // Simulated by not calling any depth pass method
        [Test]
        public void UNIT_CSG_04_CutterOutsideScene_DepthTestFails_StencilUnchanged()
        {
            var stencil = new CSGStencilLogic();
            // Depth test fails — no methods called
            Assert.That(stencil.StencilValue, Is.EqualTo(0));
            Assert.That(stencil.IsInsideCutter(), Is.False);
        }

        // UNIT-CSG-05: Cutter fully enclosing target → all target pixels marked
        // Simulate multiple pixels all receiving a back-face depth pass
        [Test]
        public void UNIT_CSG_05_CutterFullyEnclosesTarget_AllPixelsMarked()
        {
            const int pixelCount = 10;
            var pixels = new CSGStencilLogic[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                pixels[i] = new CSGStencilLogic();
                pixels[i].OnBackFaceDepthPass();
            }

            for (int i = 0; i < pixelCount; i++)
                Assert.That(pixels[i].IsInsideCutter(), Is.True, $"Pixel {i} should be marked inside cutter");
        }
    }
}
