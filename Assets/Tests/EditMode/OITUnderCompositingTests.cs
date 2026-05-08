using NUnit.Framework;
using UnityEngine;
using OIT;

namespace OIT.Tests
{
    public class OITUnderCompositingTests
    {
        private const float ColorTolerance = 0.005f;

        // UNIT-COMP-01: 1 fully opaque fragment over black → output equals fragment color
        [Test]
        public void UNIT_COMP_01_OpaqueFragment_OverBlack_OutputsFragmentColor()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 1f)), Depth = 0.5f }
            };
            Color result = OITUnderCompositing.Composite(fragments, Color.black);

            Assert.That(result.r, Is.EqualTo(1f).Within(ColorTolerance), "R");
            Assert.That(result.g, Is.EqualTo(0f).Within(ColorTolerance), "G");
            Assert.That(result.b, Is.EqualTo(0f).Within(ColorTolerance), "B");
        }

        // UNIT-COMP-02: 1 fully transparent fragment → output equals background
        [Test]
        public void UNIT_COMP_02_TransparentFragment_OutputsBackground()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0f)), Depth = 0.5f }
            };
            Color background = new Color(0f, 0f, 1f, 1f);
            Color result = OITUnderCompositing.Composite(fragments, background);

            Assert.That(result.r, Is.EqualTo(0f).Within(ColorTolerance), "R should match background");
            Assert.That(result.g, Is.EqualTo(0f).Within(ColorTolerance), "G should match background");
            Assert.That(result.b, Is.EqualTo(1f).Within(ColorTolerance), "B should match background");
        }

        // UNIT-COMP-03: Red α=0.5 over Blue α=0.5 → hand-calculated under-composite
        // Sorted front-to-back: Red first (depth 0.2), Blue second (depth 0.8)
        // Accumulator steps:
        //   acc=(0,0,0,0) + Red(1,0,0)*0.5*(1-0) → acc=(0.5,0,0,0.5)
        //   acc + Blue(0,0,1)*0.5*(1-0.5) → acc=(0.5,0,0.25,0.75)
        //   final = acc.rgb + black*(1-0.75) = (0.5,0,0.25)
        [Test]
        public void UNIT_COMP_03_RedOverBlue_HalfAlpha_MatchesFormula()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0.5f)), Depth = 0.2f },
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(0f, 0f, 1f, 0.5f)), Depth = 0.8f }
            };
            Color result = OITUnderCompositing.Composite(fragments, Color.black);

            Assert.That(result.r, Is.EqualTo(0.5f).Within(ColorTolerance), "R");
            Assert.That(result.g, Is.EqualTo(0f).Within(ColorTolerance), "G");
            Assert.That(result.b, Is.EqualTo(0.25f).Within(ColorTolerance), "B");
        }

        // UNIT-COMP-04: 24 layers at α=0.1 → accumulated alpha within expected bounds
        // Each step: acc.alpha += 0.1 * (1 - acc.alpha)
        // After 24 steps from 0: alpha = 1 - (0.9^24) ≈ 0.919
        [Test]
        public void UNIT_COMP_04_TwentyFourLayers_TenPercentAlpha_AccumulatedAlphaInBounds()
        {
            var fragments = new OITFragment[24];
            for (int i = 0; i < 24; i++)
                fragments[i] = new OITFragment
                {
                    PackedColor = OITColorPacking.PackRGBA(new Color(1f, 1f, 1f, 0.1f)),
                    Depth = i * 0.04f
                };

            Color result = OITUnderCompositing.Composite(fragments, Color.black);

            // Expected alpha accumulation: 1 - 0.9^24 ≈ 0.919
            float expectedAlpha = 1f - Mathf.Pow(0.9f, 24f);
            // result.r should equal expectedAlpha (all white fragments, black background)
            Assert.That(result.r, Is.EqualTo(expectedAlpha).Within(0.02f), "Accumulated contribution should match formula");
            Assert.That(result.r, Is.InRange(0.8f, 1.0f), "Accumulated alpha should be high after 24 layers");
        }

        // UNIT-COMP-05: Red-front vs blue-front produce different outputs (order dependency)
        [Test]
        public void UNIT_COMP_05_OrderDependency_DifferentFrontProducesDifferentResult()
        {
            var redFront = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0.5f)), Depth = 0.2f },
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(0f, 0f, 1f, 0.5f)), Depth = 0.8f }
            };
            var blueFront = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(0f, 0f, 1f, 0.5f)), Depth = 0.2f },
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0.5f)), Depth = 0.8f }
            };

            Color resultRedFront = OITUnderCompositing.Composite(redFront, Color.black);
            Color resultBlueFront = OITUnderCompositing.Composite(blueFront, Color.black);

            Assert.That(resultRedFront, Is.Not.EqualTo(resultBlueFront),
                "Compositing with different fragment order must produce different results");
        }

        // UNIT-COMP-06: All fragments α=0 → output equals opaque background
        [Test]
        public void UNIT_COMP_06_AllTransparentFragments_OutputsBackground()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0f)), Depth = 0.2f },
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(0f, 1f, 0f, 0f)), Depth = 0.5f },
                new OITFragment { PackedColor = OITColorPacking.PackRGBA(new Color(0f, 0f, 1f, 0f)), Depth = 0.8f }
            };
            Color background = new Color(0.4f, 0.6f, 0.8f, 1f);
            Color result = OITUnderCompositing.Composite(fragments, background);

            Assert.That(result.r, Is.EqualTo(background.r).Within(ColorTolerance), "R should match background");
            Assert.That(result.g, Is.EqualTo(background.g).Within(ColorTolerance), "G should match background");
            Assert.That(result.b, Is.EqualTo(background.b).Within(ColorTolerance), "B should match background");
        }

        // UNIT-COMP-07: More fragments than MaxFragmentsPerPixel (overflow) → no crash, truncated result
        [Test]
        public void UNIT_COMP_07_OverflowFragments_NoCrash_TruncatedResult()
        {
            // Create 30 fragments (> MAX_LAYERS=24)
            var fragments = new OITFragment[30];
            for (int i = 0; i < 30; i++)
                fragments[i] = new OITFragment
                {
                    PackedColor = OITColorPacking.PackRGBA(new Color(1f, 0f, 0f, 0.1f)),
                    Depth = i * 0.03f
                };

            Color result = default;
            Assert.DoesNotThrow(() =>
            {
                result = OITUnderCompositing.Composite(fragments, Color.black);
            }, "Overflow must not throw");

            // Result should be a valid color (no NaN/Inf)
            Assert.That(float.IsNaN(result.r), Is.False, "Result R must not be NaN");
            Assert.That(float.IsNaN(result.g), Is.False, "Result G must not be NaN");
            Assert.That(float.IsNaN(result.b), Is.False, "Result B must not be NaN");
        }
    }
}
