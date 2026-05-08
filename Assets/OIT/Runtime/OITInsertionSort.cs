namespace OIT
{
    public static class OITInsertionSort
    {
        // Sorts fragments in-place by depth ascending (front-to-back). Stable.
        public static void Sort(OITFragment[] fragments)
        {
            if (fragments == null || fragments.Length <= 1)
                return;

            for (int i = 1; i < fragments.Length; i++)
            {
                OITFragment key = fragments[i];
                int j = i - 1;
                while (j >= 0 && fragments[j].Depth > key.Depth)
                {
                    fragments[j + 1] = fragments[j];
                    j--;
                }
                fragments[j + 1] = key;
            }
        }
    }
}
