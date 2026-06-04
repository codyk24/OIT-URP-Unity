using System.Collections.Generic;

namespace OIT
{
    /// <summary>
    /// Shared per-frame execution log used by <see cref="PassOrderingTests"/>.
    ///
    /// Each pass appends its name to <see cref="Log"/> from inside
    /// <c>SetRenderFunc</c> (GPU timeline). Tests call <see cref="Clear"/> before
    /// rendering and read <see cref="Log"/> after <c>WaitForEndOfFrame</c>.
    ///
    /// Accessing this from multiple threads is safe because URP's RenderGraph
    /// executes all SetRenderFunc callbacks on the main render thread in order.
    /// </summary>
    public static class OITPassOrder
    {
        public static readonly List<string> Log = new List<string>(8);

        public static void Clear() => Log.Clear();

        public static void Record(string passName) => Log.Add(passName);
    }
}
