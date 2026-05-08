using UnityEngine;

namespace OIT
{
    public static class OITUnderCompositing
    {
        // Maximum fragments processed per pixel; matches GPU MAX_LAYERS.
        public const int MaxFragmentsPerPixel = 24;

        // Composites sorted (front-to-back) fragments over a background using under-compositing.
        // Formula per fragment: acc.rgb += frag.rgb * frag.alpha * (1 - acc.alpha)
        //                       acc.alpha += frag.alpha * (1 - acc.alpha)
        // Final blend: result = acc.rgb + background.rgb * (1 - acc.alpha)
        public static Color Composite(OITFragment[] sortedFragments, Color background)
        {
            float accR = 0f, accG = 0f, accB = 0f, accAlpha = 0f;

            int count = sortedFragments == null ? 0 : sortedFragments.Length;
            if (count > MaxFragmentsPerPixel)
                count = MaxFragmentsPerPixel;

            for (int i = 0; i < count; i++)
            {
                Color frag = OITColorPacking.UnpackRGBA(sortedFragments[i].PackedColor);
                float weight = frag.a * (1f - accAlpha);
                accR += frag.r * weight;
                accG += frag.g * weight;
                accB += frag.b * weight;
                accAlpha += weight;
            }

            float transmittance = 1f - accAlpha;
            return new Color(
                accR + background.r * transmittance,
                accG + background.g * transmittance,
                accB + background.b * transmittance,
                1f
            );
        }
    }
}
