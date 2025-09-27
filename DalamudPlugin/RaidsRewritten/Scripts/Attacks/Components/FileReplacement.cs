namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct FileReplacement(string OriginalPath, string ReplacementPath, int FramesSinceApplication = -1);
