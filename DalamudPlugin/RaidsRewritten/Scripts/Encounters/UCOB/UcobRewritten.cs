using System;
using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class UcobRewritten(Mechanic.Factory mechanicFactory, Configuration configuration) : IEncounter
{
    public ushort TerritoryId => 733;

    public string Name => "UCOB Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string PermanentTwistersKey => $"{Name}.PermanentTwisters";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallMaxBallsKey => $"{Name}.RollingBallMaxBalls";
    private string TankbusterAftershockKey => $"{Name}.TankbusterAftershock";
    private string LightningCorridorKey => $"{Name}.LightningCorridor";
    private string MoreExaflaresKey => $"{Name}.MoreExaflares";
    private string MoreExaflaresDifficultyKey => $"{Name}.MoreExaflaresDifficulty";
    private string JumpableShockwavesKey => $"{Name}.JumpableShockwaves";
    private string DreadknightKey => $"{Name}.Dreadknight";

    private readonly List<Mechanic> mechanics = [];
    private readonly string[] moreExaflaresDifficulties = [
        MoreExaflares.Difficulties.Low.ToString(),
        MoreExaflares.Difficulties.Medium.ToString(),
        MoreExaflares.Difficulties.High.ToString(),
    ];

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }

    public void RefreshMechanics()
    {
        this.mechanics.Clear();

        if (configuration.GetEncounterSetting(PermanentTwistersKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<PermanentTwister>());
        }

        if (configuration.GetEncounterSetting(RollingBallKey, false))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnNeurolink>();

            rollingBall.MaxBalls = configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);

            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(rollingBall);
        }

        if (configuration.GetEncounterSetting(TankbusterAftershockKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<TankbusterAftershock>());
        }

        if (configuration.GetEncounterSetting(LightningCorridorKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<LightningCorridor>());
        }

        if (configuration.GetEncounterSetting(MoreExaflaresKey, true))
        {
            var moreExaflares = mechanicFactory.Create<MoreExaflares>();

            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            moreExaflares.RngSeed = RandomUtilities.HashToRngSeed(seed);

            var difficulty = (MoreExaflares.Difficulties)configuration.GetEncounterSetting(MoreExaflaresDifficultyKey, (int)MoreExaflares.Difficulties.Low);
            moreExaflares.Difficulty = difficulty;

            this.mechanics.Add(moreExaflares);
        }

        if (configuration.GetEncounterSetting(JumpableShockwavesKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<JumpableShockwaves>());
        }

        if (configuration.GetEncounterSetting(DreadknightKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<DreadknightOnNeurolink>());
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
        RefreshMechanics();
    }

    public void DrawConfig()
    {
        ImGui.SetNextItemWidth(140);
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeed, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeed;
            configuration.Save();
            RefreshMechanics();
        }

        bool permanentTwisters = configuration.GetEncounterSetting(PermanentTwistersKey, true);
        if (ImGui.Checkbox("Permanent Twisters", ref permanentTwisters))
        {
            configuration.EncounterSettings[PermanentTwistersKey] =
                permanentTwisters ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool tankbusterAftershock = configuration.GetEncounterSetting(TankbusterAftershockKey, true);
        if (ImGui.Checkbox("Tankbuster Aftershocks", ref tankbusterAftershock))
        {
            configuration.EncounterSettings[TankbusterAftershockKey] =
                tankbusterAftershock ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool lightningCorridor = configuration.GetEncounterSetting(LightningCorridorKey, true);
        if (ImGui.Checkbox("Lightning Corridor", ref lightningCorridor))
        {
            configuration.EncounterSettings[LightningCorridorKey] =
                lightningCorridor ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawMoreExaflaresConfig();

        bool jumpableShockwaves = configuration.GetEncounterSetting(JumpableShockwavesKey, true);
        if (ImGui.Checkbox("J.S.", ref jumpableShockwaves))
        {
            configuration.EncounterSettings[JumpableShockwavesKey] =
                jumpableShockwaves ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawRollingBallConfig();

        bool dreadknight = configuration.GetEncounterSetting(DreadknightKey, true);
        if (ImGui.Checkbox("Dreadknight", ref dreadknight))
        {
            configuration.EncounterSettings[DreadknightKey] =
                dreadknight ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }

    private void DrawRollingBallConfig()
    {
        bool rollingBall = configuration.GetEncounterSetting(RollingBallKey, false);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!rollingBall))
        {
            ImGui.SetNextItemWidth(120);
            int maxBalls = configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);
            if (ImGui.InputInt("Rolling Ball Max Balls", ref maxBalls))
            {
                configuration.EncounterSettings[RollingBallMaxBallsKey] = maxBalls.ToString();
                configuration.Save();
                RefreshMechanics();
            }
        }

    }

    private void DrawMoreExaflaresConfig()
    {
        bool moreExaflares = configuration.GetEncounterSetting(MoreExaflaresKey, true);
        if (ImGui.Checkbox("More Exaflares", ref moreExaflares))
        {
            configuration.EncounterSettings[MoreExaflaresKey] =
                moreExaflares ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!moreExaflares))
        {
            var difficulty = configuration.GetEncounterSetting(MoreExaflaresDifficultyKey, (int)MoreExaflares.Difficulties.Low);
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("M.E. Difficulty", ref difficulty, this.moreExaflaresDifficulties, this.moreExaflaresDifficulties.Length))
            {
                configuration.EncounterSettings[MoreExaflaresDifficultyKey] = difficulty.ToString();
                configuration.Save();
                RefreshMechanics();
            }
        }
    }
}
