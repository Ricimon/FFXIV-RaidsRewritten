namespace RaidsRewritten.Data;

public struct HudElementInfo
{
    public enum Type
    {
        None,
        Status,
        Hp,
        Mp,
    }

    public Type ElementType;
    public Status Status;
    public GaugeValue Hp;
    public GaugeValue Mp;

    public string? OwnerName;
    public bool IsOnSelf;
    public bool IsOnPartyMember;
    public bool IsOnHostile;
}

public struct GaugeValue
{
    public uint Value;
    public uint MaxValue;
}
