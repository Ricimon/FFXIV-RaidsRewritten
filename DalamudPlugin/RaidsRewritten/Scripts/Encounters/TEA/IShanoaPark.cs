using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public interface IShanoaPark
{
    bool EasyMode { get; set; }
    bool OverrideAllMarkersAbsorbed { get; set; }

    Entity Shanoa { get; }

    void AttackMarkers(string mechanicName);
}
