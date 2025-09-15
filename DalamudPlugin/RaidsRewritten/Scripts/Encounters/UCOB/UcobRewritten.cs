using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class UcobRewritten(Mechanic.Factory mechanicFactory, Configuration configuration, EcsContainer ecsContainer) : IEncounter
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
    private string TemperatureControlKey => $"{Name}.TemperatureControl";
    private string TemperatureControlXKey => Temperature.GaugeXPositionConfig;
    private string TemperatureControlYKey => Temperature.GaugeYPositionConfig;
    private string MoreExaflaresDifficultyKey => $"{Name}.MoreExaflaresDifficulty";
    private string JumpableShockwavesKey => $"{Name}.JumpableShockwaves";
    private string DreadknightKey => $"{Name}.Dreadknight";
    private string ADSSquaredKey => $"{Name}.ADS^2";
    private string TethersKey => $"{Name}.Tethers";
    private string EarthShakerStarKey => $"{Name}.EarthShakerStar";
    private string OctetCourseKey => $"{Name}.OctetCourse";

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
        foreach(var mechanic in this.mechanics)
        {
            mechanic.Reset();
        }
        this.mechanics.Clear();

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

        if (configuration.GetEncounterSetting(TemperatureControlKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<TemperatureControl>());
            this.mechanics.Add(mechanicFactory.Create<LiquidHeaven>());
        }

        if (configuration.GetEncounterSetting(DreadknightKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<DreadknightInUCoB>());
        }

        if (configuration.GetEncounterSetting(ADSSquaredKey, true))
        {
            var adsSquared = mechanicFactory.Create<ADSSquared>();

            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            adsSquared.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(adsSquared);
        }

        if (configuration.GetEncounterSetting(TethersKey, true))
        {
            var tethers = mechanicFactory.Create<Tethers>();

            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            tethers.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(tethers);
        }

        if (configuration.GetEncounterSetting(EarthShakerStarKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<EarthShakerStar>());
        }

        if (configuration.GetEncounterSetting(OctetCourseKey, true))
        {
            var octetCourse = mechanicFactory.Create<OctetObstacleCourse>();

            var seed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
            octetCourse.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(octetCourse);
        }

        // Meme mechanics

        if (configuration.GetEncounterSetting(PermanentTwistersKey, false))
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
        if (ImGui.Button("Apply Intended Difficulty"))
        {
            ApplyIntendedFightSettings();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Everything"))
        {
            DisableEverything();
        }

        ImGui.SetNextItemWidth(140);
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeed, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeed;
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
        if (ImGui.Checkbox("J. Shockwave", ref jumpableShockwaves))
        {
            configuration.EncounterSettings[JumpableShockwavesKey] =
                jumpableShockwaves ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawTemperatureControlConfig();

        bool dreadknight = configuration.GetEncounterSetting(DreadknightKey, true);
        if (ImGui.Checkbox("Dreadknight", ref dreadknight))
        {
            configuration.EncounterSettings[DreadknightKey] =
                dreadknight ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool adsSquared = configuration.GetEncounterSetting(ADSSquaredKey, true);
        if (ImGui.Checkbox("ADS²", ref adsSquared))
        {
            configuration.EncounterSettings[ADSSquaredKey] =
                adsSquared ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool tethers = configuration.GetEncounterSetting(TethersKey, true);
        if (ImGui.Checkbox("Tethers", ref tethers))
        {
            configuration.EncounterSettings[TethersKey] =
                tethers ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool earthShakerStar = configuration.GetEncounterSetting(EarthShakerStarKey, true);
        if (ImGui.Checkbox("Earth Shaker Star", ref earthShakerStar))
        {
            configuration.EncounterSettings[EarthShakerStarKey] =
                earthShakerStar ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool octetCourse = configuration.GetEncounterSetting(OctetCourseKey, true);
        if (ImGui.Checkbox("Octet Course", ref octetCourse))
        {
            configuration.EncounterSettings[OctetCourseKey] =
                octetCourse ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool permanentTwisters = configuration.GetEncounterSetting(PermanentTwistersKey, false);
        if (ImGui.Checkbox("Permanent Twisters", ref permanentTwisters))
        {
            configuration.EncounterSettings[PermanentTwistersKey] =
                permanentTwisters ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawRollingBallConfig();
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

    private void DrawTemperatureControlConfig()
    {
        bool temperatureControl = configuration.GetEncounterSetting(TemperatureControlKey, true);
        if (ImGui.Checkbox("Temperature Control", ref temperatureControl))
        {
            configuration.EncounterSettings[TemperatureControlKey] =
                temperatureControl ? bool.TrueString : bool.FalseString;
            configuration.Save();
            if (!temperatureControl)
            {
                ecsContainer.World.DeleteWith<Temperature.Component>();
            }
            RefreshMechanics();
        }
        
        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!temperatureControl))
        {
            ImGui.PushItemWidth(120);
            int temperatureControlX = configuration.GetEncounterSetting(TemperatureControlXKey, 0);
            int temperatureControlY = configuration.GetEncounterSetting(TemperatureControlYKey, 0);

            // Auto-position
            if (temperatureControlX == 0 && temperatureControlY == 0)
            {
                var viewport = ImGui.GetMainViewport();
                int x = (int)(viewport.Pos.X + viewport.Size.X / 2.0f);
                int y = (int)(viewport.Pos.Y + viewport.Size.Y / 2.0f);

                temperatureControlX = x;
                temperatureControlY = y;

                configuration.EncounterSettings[TemperatureControlXKey] = temperatureControlX.ToString();
                configuration.EncounterSettings[TemperatureControlYKey] = temperatureControlY.ToString();
                configuration.Save();
            }

            if (ImGui.InputInt("Gauge X", ref temperatureControlX, 5))
            {
                configuration.EncounterSettings[TemperatureControlXKey] = temperatureControlX.ToString();
                configuration.Save();
            }
            if (ImGui.InputInt("Gauge Y", ref temperatureControlY, 5))
            {
                configuration.EncounterSettings[TemperatureControlYKey] = temperatureControlY.ToString();
                configuration.Save();
            }
            ImGui.PopItemWidth();
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

    private void ApplyIntendedFightSettings()
    {
        configuration.EncounterSettings[TankbusterAftershockKey] = bool.TrueString;
        configuration.EncounterSettings[LightningCorridorKey] = bool.TrueString;
        configuration.EncounterSettings[MoreExaflaresKey] = bool.TrueString;
        configuration.EncounterSettings[MoreExaflaresDifficultyKey] = ((int)MoreExaflares.Difficulties.Low).ToString();
        configuration.EncounterSettings[JumpableShockwavesKey] = bool.TrueString;
        configuration.EncounterSettings[TemperatureControlKey] = bool.TrueString;
        configuration.EncounterSettings[DreadknightKey] = bool.TrueString;
        configuration.EncounterSettings[ADSSquaredKey] = bool.TrueString;
        configuration.EncounterSettings[TethersKey] = bool.TrueString;
        configuration.EncounterSettings[EarthShakerStarKey] = bool.TrueString;
        configuration.EncounterSettings[OctetCourseKey] = bool.TrueString;

        configuration.EncounterSettings[PermanentTwistersKey] = bool.FalseString;
        configuration.EncounterSettings[RollingBallKey] = bool.FalseString;

        configuration.Save();
        RefreshMechanics();
    }

    private void DisableEverything()
    {
        configuration.EncounterSettings[TankbusterAftershockKey] = bool.FalseString;
        configuration.EncounterSettings[LightningCorridorKey] = bool.FalseString;
        configuration.EncounterSettings[MoreExaflaresKey] = bool.FalseString;
        configuration.EncounterSettings[JumpableShockwavesKey] = bool.FalseString;
        configuration.EncounterSettings[TemperatureControlKey] = bool.FalseString;
        configuration.EncounterSettings[DreadknightKey] = bool.FalseString;
        configuration.EncounterSettings[ADSSquaredKey] = bool.FalseString;
        configuration.EncounterSettings[TethersKey] = bool.FalseString;
        configuration.EncounterSettings[EarthShakerStarKey] = bool.FalseString;
        configuration.EncounterSettings[OctetCourseKey] = bool.FalseString;
        configuration.EncounterSettings[PermanentTwistersKey] = bool.FalseString;
        configuration.EncounterSettings[RollingBallKey] = bool.FalseString;

        configuration.Save();
        RefreshMechanics();
    }
}
