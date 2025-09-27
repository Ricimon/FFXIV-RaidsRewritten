using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.UI.Util;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class UcobRewritten : IEncounter
{
    public ushort TerritoryId => 733;

    public string Name => "UCOB Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string TankbusterAftershockKey => $"{Name}.TankbusterAftershock";
    private string LightningCorridorKey => $"{Name}.LightningCorridor";
    private string MoreExaflaresKey => $"{Name}.MoreExaflares";
    private string MoreExaflaresDifficultyKey => $"{Name}.MoreExaflaresDifficulty";
    private string JumpableShockwavesKey => $"{Name}.JumpableShockwaves";
    private string TemperatureControlKey => $"{Name}.TemperatureControl";
    private string TemperatureControlXKey => Temperature.GaugeXPositionConfig;
    private string TemperatureControlYKey => Temperature.GaugeYPositionConfig;
    private string DreadknightKey => $"{Name}.Dreadknight";
    private string ADSSquaredKey => $"{Name}.ADS^2";
    private string TethersKey => $"{Name}.Tethers";
    private string EarthShakerStarKey => $"{Name}.EarthShakerStar";
    private string OctetCourseKey => $"{Name}.OctetCourse";
    private string PermanentTwistersKey => $"{Name}.PermanentTwisters";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallMaxBallsKey => $"{Name}.RollingBallMaxBalls";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly Configuration configuration;
    private readonly EcsContainer ecsContainer;

    private readonly List<Mechanic> mechanics = [];
    private readonly string[] moreExaflaresDifficulties = [
        MoreExaflares.Difficulties.Low.ToString(),
        MoreExaflares.Difficulties.Medium.ToString(),
        MoreExaflares.Difficulties.High.ToString(),
    ];
    private readonly Dictionary<string, bool> defaultBoolSettings;
    private readonly Dictionary<string, int> defaultIntSettings;

    public UcobRewritten(Mechanic.Factory mechanicFactory, Configuration configuration, EcsContainer ecsContainer)
    {
        this.mechanicFactory = mechanicFactory;
        this.configuration = configuration;
        this.ecsContainer = ecsContainer;

        this.defaultBoolSettings = new()
        {
            { TankbusterAftershockKey, true },
            { LightningCorridorKey, true },
            { MoreExaflaresKey, true },
            { JumpableShockwavesKey, true },
            { TemperatureControlKey, true },
            { DreadknightKey, true },
            { ADSSquaredKey, true },
            { TethersKey, true },
            { EarthShakerStarKey, true },
            { OctetCourseKey, true },

            { PermanentTwistersKey, false },
            { RollingBallKey, false },

        };

        this.defaultIntSettings = new()
        {
            { MoreExaflaresDifficultyKey, (int)MoreExaflares.Difficulties.Low },
        };
    }

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

        var rngSeedString = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        int rngSeed = RandomUtilities.HashToRngSeed(rngSeedString);

        if (configuration.GetEncounterSetting(TankbusterAftershockKey, this.defaultBoolSettings[TankbusterAftershockKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<TankbusterAftershock>());
        }

        if (configuration.GetEncounterSetting(LightningCorridorKey, this.defaultBoolSettings[LightningCorridorKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<LightningCorridor>());
        }

        if (configuration.GetEncounterSetting(MoreExaflaresKey, this.defaultBoolSettings[MoreExaflaresKey]))
        {
            var moreExaflares = mechanicFactory.Create<MoreExaflares>();
            moreExaflares.RngSeed = rngSeed;

            var difficulty = (MoreExaflares.Difficulties)configuration.GetEncounterSetting(MoreExaflaresDifficultyKey, this.defaultIntSettings[MoreExaflaresDifficultyKey]);
            moreExaflares.Difficulty = difficulty;

            this.mechanics.Add(moreExaflares);
        }

        if (configuration.GetEncounterSetting(JumpableShockwavesKey, this.defaultBoolSettings[JumpableShockwavesKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<JumpableShockwaves>());
        }

        if (configuration.GetEncounterSetting(TemperatureControlKey, this.defaultBoolSettings[TemperatureControlKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<TemperatureControl>());
            this.mechanics.Add(mechanicFactory.Create<LiquidHeaven>());
        }

        if (configuration.GetEncounterSetting(DreadknightKey, this.defaultBoolSettings[DreadknightKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<DreadknightInUCoB>());
        }

        if (configuration.GetEncounterSetting(ADSSquaredKey, this.defaultBoolSettings[ADSSquaredKey]))
        {
            var adsSquared = mechanicFactory.Create<ADSSquared>();
            adsSquared.RngSeed = rngSeed;

            this.mechanics.Add(adsSquared);
        }

        if (configuration.GetEncounterSetting(TethersKey, this.defaultBoolSettings[TethersKey]))
        {
            var tethers = mechanicFactory.Create<Tethers>();
            tethers.RngSeed = rngSeed;

            this.mechanics.Add(tethers);
        }

        if (configuration.GetEncounterSetting(EarthShakerStarKey, this.defaultBoolSettings[EarthShakerStarKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<EarthShakerStar>());
        }

        if (configuration.GetEncounterSetting(OctetCourseKey, this.defaultBoolSettings[OctetCourseKey]))
        {
            var octetCourse = mechanicFactory.Create<OctetObstacleCourse>();
            octetCourse.RngSeed = rngSeed;

            this.mechanics.Add(octetCourse);
        }

        // Meme mechanics

        if (configuration.GetEncounterSetting(PermanentTwistersKey, this.defaultBoolSettings[PermanentTwistersKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<PermanentTwister>());
        }

        if (configuration.GetEncounterSetting(RollingBallKey, this.defaultBoolSettings[RollingBallKey]))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnNeurolink>();
            rollingBall.MaxBalls = configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);
            rollingBall.RngSeed = rngSeed;

            this.mechanics.Add(rollingBall);
        }

        // Rewards

        if (IsAtLeastIntendedFightSettingsApplied())
        {
            var chefbingus = mechanicFactory.Create<ChefbingusOnClear>();
            this.mechanics.Add(chefbingus);
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
        ImGui.SameLine();
        Common.HelpMarker("Make sure all players are on the same RNG seed");

        bool tankbusterAftershock = configuration.GetEncounterSetting(TankbusterAftershockKey, this.defaultBoolSettings[TankbusterAftershockKey]);
        if (ImGui.Checkbox("Tankbuster Aftershocks", ref tankbusterAftershock))
        {
            configuration.EncounterSettings[TankbusterAftershockKey] =
                tankbusterAftershock ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool lightningCorridor = configuration.GetEncounterSetting(LightningCorridorKey, this.defaultBoolSettings[LightningCorridorKey]);
        if (ImGui.Checkbox("Lightning Corridor", ref lightningCorridor))
        {
            configuration.EncounterSettings[LightningCorridorKey] =
                lightningCorridor ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawMoreExaflaresConfig();

        bool jumpableShockwaves = configuration.GetEncounterSetting(JumpableShockwavesKey, this.defaultBoolSettings[JumpableShockwavesKey]);
        if (ImGui.Checkbox("J. Shockwave", ref jumpableShockwaves))
        {
            configuration.EncounterSettings[JumpableShockwavesKey] =
                jumpableShockwaves ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        DrawTemperatureControlConfig();

        bool dreadknight = configuration.GetEncounterSetting(DreadknightKey, this.defaultBoolSettings[DreadknightKey]);
        if (ImGui.Checkbox("Dreadknight", ref dreadknight))
        {
            configuration.EncounterSettings[DreadknightKey] =
                dreadknight ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool adsSquared = configuration.GetEncounterSetting(ADSSquaredKey, this.defaultBoolSettings[ADSSquaredKey]);
        if (ImGui.Checkbox("ADS²", ref adsSquared))
        {
            configuration.EncounterSettings[ADSSquaredKey] =
                adsSquared ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool tethers = configuration.GetEncounterSetting(TethersKey, this.defaultBoolSettings[TethersKey]);
        if (ImGui.Checkbox("Tethers", ref tethers))
        {
            configuration.EncounterSettings[TethersKey] =
                tethers ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool earthShakerStar = configuration.GetEncounterSetting(EarthShakerStarKey, this.defaultBoolSettings[EarthShakerStarKey]);
        if (ImGui.Checkbox("Stars", ref earthShakerStar))
        {
            configuration.EncounterSettings[EarthShakerStarKey] =
                earthShakerStar ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool octetCourse = configuration.GetEncounterSetting(OctetCourseKey, this.defaultBoolSettings[OctetCourseKey]);
        if (ImGui.Checkbox("Octet Course", ref octetCourse))
        {
            configuration.EncounterSettings[OctetCourseKey] =
                octetCourse ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        ImGui.Text("Fun Extras");

        bool permanentTwisters = configuration.GetEncounterSetting(PermanentTwistersKey, this.defaultBoolSettings[PermanentTwistersKey]);
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
        bool moreExaflares = configuration.GetEncounterSetting(MoreExaflaresKey, this.defaultBoolSettings[MoreExaflaresKey]);
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
            var difficulty = configuration.GetEncounterSetting(MoreExaflaresDifficultyKey, this.defaultIntSettings[MoreExaflaresDifficultyKey]);
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
        bool temperatureControl = configuration.GetEncounterSetting(TemperatureControlKey, this.defaultBoolSettings[TemperatureControlKey]);
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
        bool rollingBall = configuration.GetEncounterSetting(RollingBallKey, this.defaultBoolSettings[RollingBallKey]);
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

    private bool IsAtLeastIntendedFightSettingsApplied()
    {
        foreach(var setting in this.defaultBoolSettings)
        {
            if (setting.Value &&
                !configuration.GetEncounterSetting(setting.Key, setting.Value))
            {
                return false;
            }
        }
        foreach(var setting in this.defaultIntSettings)
        {
            if (configuration.GetEncounterSetting(setting.Key, setting.Value) < setting.Value)
            {
                return false;
            }
        }
        return true;
    }

    private void ApplyIntendedFightSettings()
    {
        foreach(var setting in this.defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        foreach(var setting in this.defaultIntSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        configuration.Save();
        RefreshMechanics();
    }

    private void DisableEverything()
    {
        foreach(var setting in this.defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = bool.FalseString;
        }

        configuration.Save();
        RefreshMechanics();
    }
}
