namespace OIT
{
    public static class OITBufferSizing
    {
        // 4 bytes per pixel (one uint head pointer).
        public static int HeadBufferBytes(int width, int height) => width * height * 4;

        // 12 bytes per node: 4 packed RGBA8 color + 4 float depth + 4 uint next pointer.
        public static int NodeBufferBytes(int width, int height, int maxLayers) => width * height * maxLayers * 12;

        // Single uint counter for atomic node allocation.
        public static int AtomicCounterBytes() => 4;

        // Returns 24 if graphics memory >= 1 GB, otherwise 16 (memory pressure fallback).
        public static int SelectMaxLayers(long graphicsMemoryBytes)
        {
            const long threshold = 1L * 1024L * 1024L * 1024L; // 1 GB
            return graphicsMemoryBytes >= threshold ? 24 : 16;
        }
    }
}
