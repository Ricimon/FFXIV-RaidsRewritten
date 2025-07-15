using System.Numerics;

namespace RaidsRewritten.Utility;

public static class RandomUtilities
{
    public static int HashToRngSeed(string s)
    {
        int seed = 0;
        unchecked
        {
            for (var i = 0; i < s.Length; i++)
            {
                seed += (int)BitOperations.RotateLeft(s[i], i);
            }
        }
        return seed;
    }
}
