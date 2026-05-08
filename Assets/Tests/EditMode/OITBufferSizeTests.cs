using NUnit.Framework;
using OIT;

namespace OIT.Tests
{
    public class OITBufferSizeTests
    {
        // UNIT-BUF-01: HeadBuffer at 1920×1080 = 8,294,400 bytes (exact)
        [Test]
        public void UNIT_BUF_01_HeadBuffer_1080p_ExactSize()
        {
            int bytes = OITBufferSizing.HeadBufferBytes(1920, 1080);
            Assert.That(bytes, Is.EqualTo(8_294_400));
        }

        // UNIT-BUF-02: NodeBuffer at 1920×1080, MAX_LAYERS=24 — correct formula-derived size
        // Formula: w * h * layers * 12 bytes/node = 1920 * 1080 * 24 * 12 = 596,428,800
        [Test]
        public void UNIT_BUF_02_NodeBuffer_1080p_MaxLayers24_CorrectSize()
        {
            int bytes = OITBufferSizing.NodeBufferBytes(1920, 1080, 24);
            int expected = 1920 * 1080 * 24 * 12;
            Assert.That(bytes, Is.EqualTo(expected));
        }

        // UNIT-BUF-03: NodeBuffer at 1920×1080, MAX_LAYERS=16 — correct formula-derived size
        // Formula: 1920 * 1080 * 16 * 12 = 397,619,200
        [Test]
        public void UNIT_BUF_03_NodeBuffer_1080p_MaxLayers16_CorrectSize()
        {
            int bytes = OITBufferSizing.NodeBufferBytes(1920, 1080, 16);
            int expected = 1920 * 1080 * 16 * 12;
            Assert.That(bytes, Is.EqualTo(expected));
        }

        // UNIT-BUF-04: NodeBuffer at 1280×720, MAX_LAYERS=24 — correct scaled value
        [Test]
        public void UNIT_BUF_04_NodeBuffer_720p_ScalesCorrectly()
        {
            int bytes = OITBufferSizing.NodeBufferBytes(1280, 720, 24);
            int expected = 1280 * 720 * 24 * 12;
            Assert.That(bytes, Is.EqualTo(expected));
        }

        // UNIT-BUF-05: AtomicCounter = 4 bytes (exact)
        [Test]
        public void UNIT_BUF_05_AtomicCounter_IsFourBytes()
        {
            int bytes = OITBufferSizing.AtomicCounterBytes();
            Assert.That(bytes, Is.EqualTo(4));
        }

        // UNIT-BUF-06: Memory pressure fallback → SelectMaxLayers returns 16 below threshold
        [Test]
        public void UNIT_BUF_06_MemoryPressure_FallsBackToSixteenLayers()
        {
            // 500 MB < 1 GB threshold → should return 16
            long lowMemory = 500L * 1024L * 1024L;
            int layers = OITBufferSizing.SelectMaxLayers(lowMemory);
            Assert.That(layers, Is.EqualTo(16));
        }

        [Test]
        public void UNIT_BUF_06b_SufficientMemory_ReturnsTwentyFourLayers()
        {
            // 4 GB > 1 GB threshold → should return 24
            long highMemory = 4L * 1024L * 1024L * 1024L;
            int layers = OITBufferSizing.SelectMaxLayers(highMemory);
            Assert.That(layers, Is.EqualTo(24));
        }
    }
}
