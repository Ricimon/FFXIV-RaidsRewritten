using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using RaidsRewritten.Network;
using RaidsRewritten.UI.Util;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class TeaRewritten : IEncounter
{
    public uint TerritoryId => 887;

    public string Name => "TEA Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string BlindProgModeKey => $"{Name}.BlindProgMode";
    private string FireTornadoKey => $"{Name}.FireTornado";
    private string PickyDollsKey => $"{Name}.PickyDolls";
    private string SurfsUpKey => $"{Name}.SurfsUp";
    private string HawkTowerKey => $"{Name}.HawkTower";
    private string KKickKey => $"{Name}.KKick";
    private string ShanoaParkKey => $"{Name}.ShanoaPark";
    private string ChakramBallsKey => $"{Name}.ChakramBalls";
    private string IcePlusKey => $"{Name}.IcePlus";
    private string MoreChakramsKey => $"{Name}.MoreChakrams";
    private string SuperJumpPlusKey => $"{Name}.SuperJumpPlus";
    private string GearBeamKey => $"{Name}.GearBeam";
    private string ScrewdriverKey => $"{Name}.Screwdriver";
    private string JWaveSpreadsKey => $"{Name}.JWaveSpreads";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly DalamudServices dalamud;
    private readonly Configuration configuration;
    private readonly NetworkClient networkClient;
    private readonly NetworkClientUi networkClientUi;

    private readonly List<Mechanic> mechanics = [];
    private readonly Dictionary<string, bool> defaultBoolSettings;
    private readonly Dictionary<string, int> defaultIntSettings;
    private readonly HashSet<string> networkedMechanics;

    public TeaRewritten(Mechanic.Factory mechanicFactory, DalamudServices dalamud, Configuration configuration, NetworkClient networkClient, NetworkClientUi networkClientUi)
    {
        this.mechanicFactory = mechanicFactory;
        this.dalamud = dalamud;
        this.configuration = configuration;
        this.networkClient = networkClient;
        this.networkClientUi = networkClientUi;

        this.defaultBoolSettings = new()
        {
            { FireTornadoKey, true },
            { PickyDollsKey, true },
            { SurfsUpKey, true },
            { HawkTowerKey, true },
            { KKickKey, true },
            { ShanoaParkKey, true },
            { ChakramBallsKey, true },
            { IcePlusKey, true },
            { MoreChakramsKey, true },
            { SuperJumpPlusKey, true },
            { GearBeamKey, true },
            { ScrewdriverKey, true },
            { JWaveSpreadsKey, true },
        };

        this.defaultIntSettings = new()
        {
        };

        this.networkedMechanics = [
            FireTornadoKey,
            HawkTowerKey,
            ShanoaParkKey,
            SuperJumpPlusKey,
            ScrewdriverKey,
            JWaveSpreadsKey,
            ];
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return mechanics;
    }

    public void RefreshMechanics()
    {
        Unload();

        var rngSeedString = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        int rngSeed = RandomUtilities.HashToRngSeed(rngSeedString);

        if (GetEncounterSetting(FireTornadoKey))
        {
            mechanics.Add(mechanicFactory.Create<FireTornado>());
        }
        if (GetEncounterSetting(PickyDollsKey))
        {
            var pickyDolls = mechanicFactory.Create<PickyDolls>();
            pickyDolls.RngSeed = rngSeed;
            mechanics.Add(pickyDolls);
        }
        if (GetEncounterSetting(SurfsUpKey))
        {
            var surfsUp = mechanicFactory.Create<SurfsUp>();
            mechanics.Add(surfsUp);
        }
        if (GetEncounterSetting(HawkTowerKey))
        {
            var hawkTower = mechanicFactory.Create<HawkTower>();
            mechanics.Add(hawkTower);
        }
        if (GetEncounterSetting(KKickKey))
        {
            var kkick = mechanicFactory.Create<KKick>();
            mechanics.Add(kkick);
        }
        if (GetEncounterSetting(ShanoaParkKey))
        {
            var shanoaPark = mechanicFactory.Create<ShanoaPark>();
            mechanics.Add(shanoaPark);
            var shanoaAndNisi = mechanicFactory.Create<ShanoaAndNisi>();
            shanoaAndNisi.RngSeed = rngSeed;
            shanoaAndNisi.ShanoaPark = shanoaPark;
            mechanics.Add(shanoaAndNisi);
            var shanoaAndAlexPrime = mechanicFactory.Create<ShanoaAndAlexPrime>();
            shanoaAndAlexPrime.ShanoaPark = shanoaPark;
            mechanics.Add(shanoaAndAlexPrime);
        }
        if (GetEncounterSetting(ShanoaParkKey) && GetEncounterSetting(FireTornadoKey))
        {
            var shanoaAndFireTornado = mechanicFactory.Create<ShanoaAndFireTornado>();
            mechanics.Add(shanoaAndFireTornado);
        }
        if (GetEncounterSetting(ChakramBallsKey))
        {
            var chakramBalls = mechanicFactory.Create<ChakramBalls>();
            chakramBalls.RngSeed = rngSeed;
            mechanics.Add(chakramBalls);
        }
        if (GetEncounterSetting(IcePlusKey))
        {
            var icePlus = mechanicFactory.Create<IcePlus>();
            icePlus.RngSeed = rngSeed;
            mechanics.Add(icePlus);
        }
        if (GetEncounterSetting(MoreChakramsKey))
        {
            var moreChakrams = mechanicFactory.Create<MoreChakrams>();
            moreChakrams.RngSeed = rngSeed;
            mechanics.Add(moreChakrams);
        }
        if (GetEncounterSetting(SuperJumpPlusKey))
        {
            mechanics.Add(mechanicFactory.Create<SuperJumpPlus>());
        }
        if (GetEncounterSetting(GearBeamKey))
        {
            var gearBeam = mechanicFactory.Create<GearBeams>();
            gearBeam.RngSeed = rngSeed;
            mechanics.Add(gearBeam);
        }
        if (GetEncounterSetting(ScrewdriverKey))
        {
            var screwdriver = mechanicFactory.Create<Screwdriver>();
            screwdriver.RngSeed = rngSeed;
            mechanics.Add(screwdriver);
        }
        if (GetEncounterSetting(JWaveSpreadsKey))
        {
            mechanics.Add(mechanicFactory.Create<JWaveSpreads>());
        }
    }

    public void Unload()
    {
        foreach (var mechanic in mechanics)
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
        this.dalamud.ChatGui.PrintSystemMessage($"RNG seed is now {rngSeed}", PluginInitializer.Name);
        RefreshMechanics();
    }

    public void DrawConfig()
    {
        networkClientUi.DrawConfig();
        ImGui.Spacing();

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

        if (configuration.GetEncounterSetting(BlindProgModeKey, true))
        {
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                var errorIcon = FontAwesomeIcon.EyeSlash.ToIconString();
                ImGui.Text(errorIcon);
            }
            ImGui.SameLine();
            ImGui.Text("Blind prog mode, mechanic names hidden");

            var intendedDifficultyActive = true;
            foreach(var setting in defaultBoolSettings)
            {
                if (configuration.GetEncounterSetting(setting.Key, setting.Value) != setting.Value)
                {
                    intendedDifficultyActive = false;
                    break;
                }
            }
            if (!intendedDifficultyActive)
            {
                using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    var errorIcon = FontAwesomeIcon.ExclamationTriangle.ToIconString();
                    ImGui.TextColored(Vector4Colors.Yellow, errorIcon);
                }
                ImGui.SameLine();
                ImGui.Text("Intended difficulty is not active");
            }

            if (!networkClient.IsConnected)
            {
                using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    var errorIcon = FontAwesomeIcon.ExclamationTriangle.ToIconString();
                    ImGui.TextColored(Vector4Colors.Yellow, errorIcon);
                }
                ImGui.SameLine();
                ImGui.Text("A server connection is required for some mechanics");
            }
            if (ImGui.Button("Show Mechanic Toggles"))
            {
                configuration.EncounterSettings[BlindProgModeKey] = bool.FalseString;
                configuration.Save();
            }
            return;
        }

        bool fireTornado = GetEncounterSetting(FireTornadoKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("Fire Tornado", ref fireTornado))
            {
                configuration.EncounterSettings[FireTornadoKey] =
                    fireTornado ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        bool pickyDolls = GetEncounterSetting(PickyDollsKey);
        if (ImGui.Checkbox("Picky Dolls", ref pickyDolls))
        {
            configuration.EncounterSettings[PickyDollsKey] =
                pickyDolls ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool surfsUp = GetEncounterSetting(SurfsUpKey);
        if (ImGui.Checkbox("Surfs Up!", ref surfsUp))
        {
            configuration.EncounterSettings[SurfsUpKey] =
                surfsUp ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool hawkTower = GetEncounterSetting(HawkTowerKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("Hawk Tuawer (tower)", ref hawkTower))
            {
                configuration.EncounterSettings[HawkTowerKey] =
                    hawkTower ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        bool kkick = GetEncounterSetting(KKickKey);
        if (ImGui.Checkbox("K-Kick", ref kkick))
        {
            configuration.EncounterSettings[KKickKey] =
                kkick ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool shanoaPark = GetEncounterSetting(ShanoaParkKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("Shanoa Park", ref shanoaPark))
            {
                configuration.EncounterSettings[ShanoaParkKey] =
                    shanoaPark ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        bool chakramBalls = GetEncounterSetting(ChakramBallsKey);
        if (ImGui.Checkbox("Rolling Balls", ref chakramBalls))
        {
            configuration.EncounterSettings[ChakramBallsKey] =
                chakramBalls ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool icePlus = GetEncounterSetting(IcePlusKey);
        if (ImGui.Checkbox("Ice+", ref icePlus))
        {
            configuration.EncounterSettings[IcePlusKey] =
                icePlus ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool moreChakrams = GetEncounterSetting(MoreChakramsKey);
        if (ImGui.Checkbox("More Chakrams", ref moreChakrams))
        {
            configuration.EncounterSettings[MoreChakramsKey] =
                moreChakrams ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool superJumpPlus = GetEncounterSetting(SuperJumpPlusKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("Super Super Jump", ref superJumpPlus))
            {
                configuration.EncounterSettings[SuperJumpPlusKey] =
                    superJumpPlus ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        bool gearBeam = GetEncounterSetting(GearBeamKey);
        if (ImGui.Checkbox("Gear Weal", ref gearBeam))
        {
            configuration.EncounterSettings[GearBeamKey] =
                gearBeam ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool screwdriver = GetEncounterSetting(ScrewdriverKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("Screwdriver", ref screwdriver))
            {
                configuration.EncounterSettings[ScrewdriverKey] =
                    screwdriver ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        bool jWaveSpreads = GetEncounterSetting(JWaveSpreadsKey);
        using (ImRaii.Disabled(!networkClient.IsConnected))
        {
            if (ImGui.Checkbox("JS-Waves", ref jWaveSpreads))
            {
                configuration.EncounterSettings[JWaveSpreadsKey] =
                    jWaveSpreads ? bool.TrueString : bool.FalseString;
                configuration.Save();
                RefreshMechanics();
            }
        }
        ServerRequiredTooltip();

        if (!configuration.GetEncounterSetting(BlindProgModeKey, true) &&
            ImGui.Button("Hide Mechanic Toggles (blind prog mode)"))
        {
            configuration.EncounterSettings[BlindProgModeKey] = bool.TrueString;
            configuration.Save();
        }
    }

    private bool GetEncounterSetting(string key)
    {
        if (networkedMechanics.Contains(key))
        {
            return configuration.GetEncounterSetting(key, defaultBoolSettings[key]) &&
                networkClient.IsConnected;
        }
        else
        {
            return configuration.GetEncounterSetting(key, defaultBoolSettings[key]);
        }
    }

    private void ServerRequiredTooltip()
    {
        if (!networkClient.IsConnected && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Requires a server connection");
        }
    }

    private void ApplyIntendedFightSettings()
    {
        foreach (var setting in defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        foreach (var setting in defaultIntSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        configuration.Save();
        RefreshMechanics();
    }

    private void DisableEverything()
    {
        foreach (var setting in defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = bool.FalseString;
        }

        configuration.Save();
        RefreshMechanics();
    }
}
