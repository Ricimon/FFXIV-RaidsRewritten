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
        // Deprecated: 51
        ApplyCondition = 52,
        UpdatePartyStatus = 53,
        PlayStaticVfx = 54,
        StopStaticVfx = 55,
        PlayActorVfxOnTarget = 56,
        PlayActorVfxOnPosition = 57,
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
        public ulong contentId;
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
        [JsonProperty(PropertyName = "ri")]
        public string requestId;
        [JsonProperty(PropertyName = "mi")]
        public uint mechanicId;
    }
    [JsonProperty(PropertyName = "sm")]
    public StartMechanicPayload? startMechanic;

    // To client ============

    public struct ApplyConditionPayload
    {
        public enum Condition : uint
        {
            None = 0,
            Stun = 1,
            Paralysis = 2,
            Bind = 3,
            Heavy = 4,
            Hysteria = 5,
            Pacify = 6,
            Sleep = 7,
            Knockback = 8,
        }
        [JsonProperty(PropertyName = "c")]
        public Condition condition;
        [JsonProperty(PropertyName = "d")]
        public float duration;
        [JsonProperty(PropertyName = "kbx")]
        public float? knockbackDirectionX;
        [JsonProperty(PropertyName = "kbz")]
        public float? knockbackDirectionZ;
    }
    [JsonProperty(PropertyName = "ac")]
    public ApplyConditionPayload? applyCondition;

    public struct UpdatePartyStatusPayload
    {
        [JsonProperty(PropertyName = "c")]
        public byte connectedPlayersInParty;
    }
    [JsonProperty(PropertyName = "ups")]
    public UpdatePartyStatusPayload? updatePartyStatus;

    public struct PlayStaticVfxPayload
    {
        public string id;
        [JsonProperty(PropertyName = "v")]
        public string vfxPath;
        [JsonProperty(PropertyName = "x")]
        public float worldPositionX;
        [JsonProperty(PropertyName = "y")]
        public float worldPositionY;
        [JsonProperty(PropertyName = "z")]
        public float worldPositionZ;
    }
    [JsonProperty(PropertyName = "psv")]
    public PlayStaticVfxPayload? playStaticVfx;

    public struct StopStaticVfxPayload
    {
        public string id;
    }
    [JsonProperty(PropertyName = "ssv")]
    public StopStaticVfxPayload? stopStaticVfx;

    public struct PlayActorVfxOnTargetPayload
    {
        [JsonProperty(PropertyName = "v")]
        public string vfxPath;
        [JsonProperty(PropertyName = "ct")]
        public ulong[] contentIdTargets;
        [JsonProperty(PropertyName = "it")]
        public string[] customIdTargets;
    }
    [JsonProperty(PropertyName = "pavt")]
    public PlayActorVfxOnTargetPayload? playActorVfxOnTarget;

    public struct PlayActorVfxOnPositionPayload
    {
        [JsonProperty(PropertyName = "v")]
        public string vfxPath;
        [JsonProperty(PropertyName = "x")]
        public float worldPositionX;
        [JsonProperty(PropertyName = "y")]
        public float worldPositionY;
        [JsonProperty(PropertyName = "z")]
        public float worldPositionZ;
    }
    [JsonProperty(PropertyName = "pavp")]
    public PlayActorVfxOnPositionPayload? playActorVfxOnPosition;
}
