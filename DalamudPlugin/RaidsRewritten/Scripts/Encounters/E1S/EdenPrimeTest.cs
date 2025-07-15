using System.Collections.Generic;
using ImGuiNET;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest(Mechanic.Factory mechanicFactory, Configuration configuration) : IEncounter
{
    public ushort TerritoryId => 853;

    public string Name => "Eden Prime Test";

    // Config
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallRngSeedKey => $"{Name}.RollingBallRngSeed";

    private readonly List<Mechanic> mechanics = [];

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }

    public void RefreshMechanics()
    {
        this.mechanics.Clear();

        this.mechanics.Add(mechanicFactory.Create<PermanentViceOfApathyTest>());

        if (configuration.GetEncounterSetting(RollingBallKey, true))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnViceOfApathy>();
            var seed = configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);
            this.mechanics.Add(rollingBall);
        }
    }

    public void Unload()
    {
        foreach(var mechanic in this.mechanics)
        {
            mechanic.Reset();
        }
        this.mechanics.Clear();
    }

    public void DrawConfig()
    {
        bool rollingBall = configuration.GetEncounterSetting(RollingBallKey, true);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        string rollingBallRngSeed = configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
        ImGui.PushItemWidth(150);
        if (ImGui.InputText("Rolling Ball RNG Seed", ref rollingBallRngSeed, 100))
        {
            configuration.EncounterSettings[RollingBallRngSeedKey] = rollingBallRngSeed;
            configuration.Save();
            RefreshMechanics();
        }
        ImGui.PopItemWidth();
    }
}
