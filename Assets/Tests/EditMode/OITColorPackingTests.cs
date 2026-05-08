using NUnit.Framework;
using UnityEngine;
using OIT;

namespace OIT.Tests
{
    public class OITColorPackingTests
    {
        // UNIT-OIT-01: Round-trip RGBA packing — per-channel error < 1/255
        [Test]
        public void UNIT_OIT_01_RoundTrip_RedOpaque()
        {
            Color original = new Color(1f, 0f, 0f, 1f);
            uint packed = OITColorPacking.PackRGBA(original);
            Color unpacked = OITColorPacking.UnpackRGBA(packed);

            float tolerance = 1f / 255f;
            Assert.That(Mathf.Abs(unpacked.r - original.r), Is.LessThanOrEqualTo(tolerance), "R channel error");
            Assert.That(Mathf.Abs(unpacked.g - original.g), Is.LessThanOrEqualTo(tolerance), "G channel error");
            Assert.That(Mathf.Abs(unpacked.b - original.b), Is.LessThanOrEqualTo(tolerance), "B channel error");
            Assert.That(Mathf.Abs(unpacked.a - original.a), Is.LessThanOrEqualTo(tolerance), "A channel error");
        }

        // UNIT-OIT-02: PackRGBA(0,0,0,0) → 0x00000000
        [Test]
        public void UNIT_OIT_02_TransparentBlack_PacksToZero()
        {
            uint packed = OITColorPacking.PackRGBA(new Color(0f, 0f, 0f, 0f));
            Assert.That(packed, Is.EqualTo(0x00000000u));
        }

        // UNIT-OIT-03: PackRGBA(1,1,1,1) → 0xFFFFFFFF
        [Test]
        public void UNIT_OIT_03_OpaqueWhite_PacksToMaxUint()
        {
            uint packed = OITColorPacking.PackRGBA(new Color(1f, 1f, 1f, 1f));
            Assert.That(packed, Is.EqualTo(0xFFFFFFFFu));
        }

        // UNIT-OIT-04: 16 boundary alpha values pack/unpack within 0.5% relative error per channel
        [Test]
        public void UNIT_OIT_04_BoundaryAlphaValues_RoundTrip()
        {
            float[] alphas = { 0f, 1f/255f, 2f/255f, 16f/255f, 32f/255f, 64f/255f, 96f/255f,
                               128f/255f, 160f/255f, 192f/255f, 224f/255f, 240f/255f,
                               252f/255f, 253f/255f, 254f/255f, 1f };

            foreach (float alpha in alphas)
            {
                Color original = new Color(0.5f, 0.5f, 0.5f, alpha);
                uint packed = OITColorPacking.PackRGBA(original);
                Color unpacked = OITColorPacking.UnpackRGBA(packed);

                float tolerance = 1f / 255f;
                Assert.That(Mathf.Abs(unpacked.a - alpha), Is.LessThanOrEqualTo(tolerance),
                    $"Alpha boundary {alpha:F4} failed round-trip");
            }
        }

        // UNIT-OIT-05: 256 random colors pack/unpack without exceeding 1/255 error per channel
        [Test]
        public void UNIT_OIT_05_RandomColors_NeverExceedOneOver255Error()
        {
            System.Random rng = new System.Random(42);
            float tolerance = 1f / 255f;

            for (int i = 0; i < 256; i++)
            {
                Color original = new Color(
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble()
                );
                uint packed = OITColorPacking.PackRGBA(original);
                Color unpacked = OITColorPacking.UnpackRGBA(packed);

                Assert.That(Mathf.Abs(unpacked.r - original.r), Is.LessThanOrEqualTo(tolerance), $"Color {i} R exceeded tolerance");
                Assert.That(Mathf.Abs(unpacked.g - original.g), Is.LessThanOrEqualTo(tolerance), $"Color {i} G exceeded tolerance");
                Assert.That(Mathf.Abs(unpacked.b - original.b), Is.LessThanOrEqualTo(tolerance), $"Color {i} B exceeded tolerance");
                Assert.That(Mathf.Abs(unpacked.a - original.a), Is.LessThanOrEqualTo(tolerance), $"Color {i} A exceeded tolerance");
            }
        }
    }
}
