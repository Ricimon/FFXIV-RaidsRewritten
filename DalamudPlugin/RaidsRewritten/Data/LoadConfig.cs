namespace RaidsRewritten.Data;

public class LoadConfig
{
#pragma warning disable IDE1006 // Naming Styles
    public struct ServerEntry
    {
        public string url { get; set; }
        public string label { get; set; }
    }

    public ServerEntry[] serverUrls { get; set; } = [];
#pragma warning restore IDE1006 // Naming Styles
}
