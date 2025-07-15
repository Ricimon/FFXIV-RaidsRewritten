using System.Numerics;

namespace RaidsRewritten.Utility;

public static class RandomUtilities
{
    public static int HashToRngSeed(string s)
    {
        unchecked
        {
            int seed = 0;
            for(var i = 0; i < s.Length; i++)
            {
                s += BitOperations.RotateLeft(s[i], i);
            }
            return seed;
        }
    }
}
