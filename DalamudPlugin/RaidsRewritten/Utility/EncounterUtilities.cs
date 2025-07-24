using System.Text.RegularExpressions;

namespace RaidsRewritten.Utility;

public static class EncounterUtilities
{
    public static string IncrementRngSeed(string seed)
    {
        uint number = 0;

        string regex = @"[0-9]+$";
        var match = Regex.Match(seed, regex);
        if (match.Success)
        {
            var numberString = match.Value;
            if (uint.TryParse(numberString, out number))
            {
                seed = seed[..^numberString.Length];
            }
        }

        number++;
        seed += number.ToString();
        return seed;
    }
}
