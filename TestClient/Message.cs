using Newtonsoft.Json;

namespace TestClient;

public struct Message
{
    public enum Action : uint
    {
        None = 0,

        // To server
        UpdatePlayer = 1,
        UpdateStatus = 2,
        StartMechanic = 3,

        // To client
        PlayVfx = 51,
        ApplyCondition = 52,
    }

    [JsonProperty(PropertyName = "a")]
    public Action action;

    // To server ============

    public struct UpdatePlayerPayload
    {
        public enum Role : uint
        {
            None = 0,
            Tank = 1,
            Healer = 2,
            Dps = 3,
        }
        public ulong id;
        public string name;
        public Role role;
        public string party;
    }
    [JsonProperty(PropertyName = "up")]
    public UpdatePlayerPayload? updatePlayer;

    public struct UpdateStatusPayload
    {
        [JsonProperty(PropertyName = "x")]
        public float worldPositionX;
        [JsonProperty(PropertyName = "y")]
        public float worldPositionY;
        [JsonProperty(PropertyName = "z")]
        public float worldPositionZ;
        [JsonProperty(PropertyName = "a")]
        public bool isAlive;
    }
    [JsonProperty(PropertyName = "us")]
    public UpdateStatusPayload? updateStatus;

    public struct StartMechanicPayload
    {
        [JsonProperty(PropertyName = "i")]
        public uint id;
        [JsonProperty(PropertyName = "mi")]
        public uint mechanicId;
    }
    [JsonProperty(PropertyName = "sm")]
    public StartMechanicPayload? startMechanic;

    // To client ============

    public struct PlayVfxPayload
    {
        [JsonProperty(PropertyName = "v")]
        public string vfxPath;
        [JsonProperty(PropertyName = "t")]
        public ulong[] targets;
    }
    [JsonProperty(PropertyName = "pv")]
    public PlayVfxPayload? playVfx;

    public struct ApplyConditionPayload
    {
        [JsonProperty(PropertyName = "i")]
        public uint conditionId;
        [JsonProperty(PropertyName = "d")]
        public float duration;
    }
    [JsonProperty(PropertyName = "ac")]
    public ApplyConditionPayload? applyCondition;
}
