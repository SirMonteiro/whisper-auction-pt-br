using System;

namespace AuctionEngine.Core.Phonetics
{
    /// <summary>
    /// Computes Levenshtein edit distance and normalized similarity metrics.
    /// Uses single/double row dynamic programming to avoid heavy heap allocations.
    /// </summary>
    public static class LevenshteinDistance
    {
        public static int Calculate(string s1, string s2)
        {
            if (s1 == null) s1 = string.Empty;
            if (s2 == null) s2 = string.Empty;

            if (s1.Length < s2.Length)
            {
                // Ensure s1 is always the longer or equal string
                string tmp = s1;
                s1 = s2;
                s2 = tmp;
            }

            int n = s2.Length;
            if (n == 0) return s1.Length;

            int m = s1.Length;
            int[] prev = new int[n + 1];
            int[] curr = new int[n + 1];

            for (int j = 0; j <= n; j++)
            {
                prev[j] = j;
            }

            for (int i = 0; i < m; i++)
            {
                curr[0] = i + 1;
                char c1 = s1[i];

                for (int j = 0; j < n; j++)
                {
                    char c2 = s2[j];
                    int cost = (c1 == c2) ? 0 : 1;

                    int insertion = curr[j] + 1;
                    int deletion = prev[j + 1] + 1;
                    int substitution = prev[j] + cost;

                    int min = insertion < deletion ? insertion : deletion;
                    if (substitution < min) min = substitution;

                    curr[j + 1] = min;
                }

                // Swap rows
                int[] swap = prev;
                prev = curr;
                curr = swap;
            }

            return prev[n];
        }

        public static double Similarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int maxLen = Math.Max(s1.Length, s2.Length);
            int dist = Calculate(s1, s2);
            return Math.Max(0.0, 1.0 - ((double)dist / maxLen));
        }
    }
}
