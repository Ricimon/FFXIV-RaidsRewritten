namespace RaidsRewritten.Scripts.Components;

public record struct FileReplacement(string OriginalPath, string ReplacementPath, int FramesSinceApplication = -1);
public record struct FileReplacementReference(FileReplacement Replacement);