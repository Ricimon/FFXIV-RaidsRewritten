using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Encounters.TEA;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest(
    Mechanic.Factory mechanicFactory,
    DalamudServices dalamud,
    Configuration configuration,
    NetworkClientUi networkClientUi) : IEncounter
{
    public uint TerritoryId => 853;

    public string Name => "Eden Prime Test";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string PermaTwisterKey => $"{Name}.PermaTwister";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string DreadknightKey => $"{Name}.Dreadknight";
    private string ShanoaParkKey => $"{Name}.ShanoaPark";

    private readonly List<Mechanic> mechanics = [];

    public IEnumerable<Mechanic> GetMechanics()
    {
        return mechanics;
    }

    public void RefreshMechanics()
    {
        mechanics.Clear();

        if (configuration.GetEncounterSetting(PermaTwisterKey, true))
        {
            mechanics.Add(mechanicFactory.Create<PermanentViceOfApathyTest>());
        }

        if (configuration.GetEncounterSetting(RollingBallKey, true))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnViceOfApathy>();
            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);
            mechanics.Add(rollingBall);
        }
 
        if (configuration.GetEncounterSetting(DreadknightKey, true))
        {
            mechanics.Add(mechanicFactory.Create<DreadknightTest>());
        }

        if (configuration.GetEncounterSetting(ShanoaParkKey, true))
        {
            mechanics.Add(mechanicFactory.Create<ShanoaParkTest>());
            mechanics.Add(mechanicFactory.Create<ShanoaAndFireTornadoTest>());
            mechanics.Add(mechanicFactory.Create<ShanoaAndNisi>());
        }
    }

    public void Unload()
    {
        foreach(var mechanic in mechanics)
        {
            mechanic.Reset();
        }
        mechanics.Clear();
    }

    public void IncrementRngSeed()
    {
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        rngSeed = EncounterUtilities.IncrementRngSeed(rngSeed);
        configuration.EncounterSettings[RngSeedKey] = rngSeed;
        configuration.Save();
        dalamud.ChatGui.PrintSystemMessage($"RNG seed is now {rngSeed}", PluginInitializer.Name);
        RefreshMechanics();
    }

    public void DrawConfig()
    {
        networkClientUi.DrawConfig();

        ImGui.PushItemWidth(120);
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeed, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeed;
            configuration.Save();
            RefreshMechanics();
        }
        ImGui.PopItemWidth();

        bool permaTwister = configuration.GetEncounterSetting(PermaTwisterKey, true);
        if (ImGui.Checkbox("Permanent Twister", ref permaTwister))
        {
            configuration.EncounterSettings[PermaTwisterKey] =
                permaTwister ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

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

        bool shanoaPark = configuration.GetEncounterSetting(ShanoaParkKey, true);
        if (ImGui.Checkbox("Shanoa Park", ref shanoaPark))
        {
            configuration.EncounterSettings[ShanoaParkKey] =
                shanoaPark ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }
}
