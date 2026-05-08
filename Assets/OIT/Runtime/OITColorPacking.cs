using UnityEngine;

namespace OIT
{
    public static class OITColorPacking
    {
        // Packs a Color into a uint as RGBA8: R in bits 31-24, G in 23-16, B in 15-8, A in 7-0.
        // (0,0,0,0) → 0x00000000, (1,1,1,1) → 0xFFFFFFFF.
        public static uint PackRGBA(Color color)
        {
            uint r = (uint)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            uint g = (uint)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            uint b = (uint)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            uint a = (uint)Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        public static Color UnpackRGBA(uint packed)
        {
            float r = ((packed >> 24) & 0xFF) / 255f;
            float g = ((packed >> 16) & 0xFF) / 255f;
            float b = ((packed >> 8) & 0xFF) / 255f;
            float a = (packed & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }
    }
}
