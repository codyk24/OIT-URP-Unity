using NUnit.Framework;
using OIT;

namespace OIT.Tests
{
    public class OITInsertionSortTests
    {
        // UNIT-SORT-01: Sort 1 fragment → output equals input
        [Test]
        public void UNIT_SORT_01_SingleFragment_Unchanged()
        {
            var fragments = new[] { new OITFragment { PackedColor = 0xFF0000FFu, Depth = 0.5f } };
            OITInsertionSort.Sort(fragments);
            Assert.That(fragments[0].Depth, Is.EqualTo(0.5f));
            Assert.That(fragments[0].PackedColor, Is.EqualTo(0xFF0000FFu));
        }

        // UNIT-SORT-02: 2 fragments, near in front → correct front-to-back order
        [Test]
        public void UNIT_SORT_02_TwoFragments_NearFirst_OrderPreserved()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = 0xFF0000FFu, Depth = 0.2f },
                new OITFragment { PackedColor = 0x0000FFFFu, Depth = 0.8f }
            };
            OITInsertionSort.Sort(fragments);
            Assert.That(fragments[0].Depth, Is.LessThan(fragments[1].Depth));
            Assert.That(fragments[0].Depth, Is.EqualTo(0.2f));
        }

        // UNIT-SORT-03: 2 fragments, far in front (reversed) → swapped to correct order
        [Test]
        public void UNIT_SORT_03_TwoFragments_FarFirst_SwappedToCorrectOrder()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = 0x0000FFFFu, Depth = 0.8f },
                new OITFragment { PackedColor = 0xFF0000FFu, Depth = 0.2f }
            };
            OITInsertionSort.Sort(fragments);
            Assert.That(fragments[0].Depth, Is.EqualTo(0.2f), "Near fragment must be first");
            Assert.That(fragments[1].Depth, Is.EqualTo(0.8f), "Far fragment must be second");
        }

        // UNIT-SORT-04: 24 fragments, pre-sorted → output equals input
        [Test]
        public void UNIT_SORT_04_TwentyFourFragments_PreSorted_Unchanged()
        {
            var fragments = new OITFragment[24];
            for (int i = 0; i < 24; i++)
                fragments[i] = new OITFragment { PackedColor = (uint)i, Depth = i * 0.04f };

            OITInsertionSort.Sort(fragments);

            for (int i = 0; i < 24; i++)
                Assert.That(fragments[i].Depth, Is.EqualTo(i * 0.04f).Within(1e-6f),
                    $"Fragment {i} depth changed after sorting pre-sorted input");
        }

        // UNIT-SORT-05: 24 fragments, reverse-sorted → monotonically non-decreasing depths
        [Test]
        public void UNIT_SORT_05_TwentyFourFragments_ReverseSorted_BecomesAscending()
        {
            var fragments = new OITFragment[24];
            for (int i = 0; i < 24; i++)
                fragments[i] = new OITFragment { PackedColor = (uint)i, Depth = (23 - i) * 0.04f };

            OITInsertionSort.Sort(fragments);

            for (int i = 1; i < 24; i++)
                Assert.That(fragments[i].Depth, Is.GreaterThanOrEqualTo(fragments[i - 1].Depth),
                    $"Depth at index {i} is less than at {i - 1}");
        }

        // UNIT-SORT-06: 24 fragments, random depths → monotonically non-decreasing depths
        [Test]
        public void UNIT_SORT_06_TwentyFourFragments_RandomDepths_BecomesAscending()
        {
            float[] depths = {
                0.73f, 0.12f, 0.55f, 0.88f, 0.03f, 0.66f, 0.41f, 0.97f,
                0.29f, 0.84f, 0.15f, 0.62f, 0.48f, 0.91f, 0.07f, 0.76f,
                0.34f, 0.59f, 0.21f, 0.93f, 0.44f, 0.08f, 0.71f, 0.37f
            };
            var fragments = new OITFragment[24];
            for (int i = 0; i < 24; i++)
                fragments[i] = new OITFragment { PackedColor = (uint)i, Depth = depths[i] };

            OITInsertionSort.Sort(fragments);

            for (int i = 1; i < 24; i++)
                Assert.That(fragments[i].Depth, Is.GreaterThanOrEqualTo(fragments[i - 1].Depth),
                    $"Depth at index {i} is less than at {i - 1}");
        }

        // UNIT-SORT-07: Equal depth values → original order preserved (stable)
        [Test]
        public void UNIT_SORT_07_EqualDepths_OriginalOrderPreserved()
        {
            var fragments = new[]
            {
                new OITFragment { PackedColor = 0xAAu, Depth = 0.5f },
                new OITFragment { PackedColor = 0xBBu, Depth = 0.5f },
                new OITFragment { PackedColor = 0xCCu, Depth = 0.5f }
            };
            OITInsertionSort.Sort(fragments);
            Assert.That(fragments[0].PackedColor, Is.EqualTo(0xAAu), "First equal-depth fragment must stay first");
            Assert.That(fragments[1].PackedColor, Is.EqualTo(0xBBu), "Second equal-depth fragment must stay second");
            Assert.That(fragments[2].PackedColor, Is.EqualTo(0xCCu), "Third equal-depth fragment must stay third");
        }

        // UNIT-SORT-08: Sort 0 fragments → no exception, empty output
        [Test]
        public void UNIT_SORT_08_EmptyArray_NoException()
        {
            var fragments = new OITFragment[0];
            Assert.DoesNotThrow(() => OITInsertionSort.Sort(fragments));
            Assert.That(fragments.Length, Is.EqualTo(0));
        }
    }
}
