using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest(Mechanic.Factory mechanicFactory, DalamudServices dalamud, Configuration configuration) : IEncounter
{
    public ushort TerritoryId => 853;

    public string Name => "Eden Prime Test";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string DreadknightKey => $"{Name}.Dreadknight";

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
            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);
            this.mechanics.Add(rollingBall);
        }
 
        if (configuration.GetEncounterSetting(DreadknightKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<DreadknightTest>());
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

    public void IncrementRngSeed()
    {
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        rngSeed = EncounterUtilities.IncrementRngSeed(rngSeed);
        configuration.EncounterSettings[RngSeedKey] = rngSeed;
        configuration.Save();
        dalamud.ChatGui.Print($"RNG seed is now {rngSeed}", PluginInitializer.Name);
        RefreshMechanics();
    }

    public void DrawConfig()
    {
        ImGui.PushItemWidth(120);
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeed, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeed;
            configuration.Save();
            RefreshMechanics();
        }
        ImGui.PopItemWidth();

        bool rollingBall = configuration.GetEncounterSetting(RollingBallKey, true);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool dreadknight = configuration.GetEncounterSetting(DreadknightKey, true);
        if (ImGui.Checkbox("Dreadknight", ref dreadknight))
        {
            configuration.EncounterSettings[DreadknightKey] =
                dreadknight ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }
}
