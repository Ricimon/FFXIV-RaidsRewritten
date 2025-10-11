using Dalamud.Plugin.Services;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;
using System;
using System.Text;

namespace RaidsRewritten;

public class MapManager : IDisposable
{
    public ushort CurrentTerritoryId => this.clientState.TerritoryType;
    public uint CurrentMapId => this.clientState.MapId;

    public event System.Action? OnMapChanged;

    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly ILogger logger;

    public MapManager(IClientState clientState, IDataManager dataManager, ILogger logger)
    {
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.logger = logger;

        this.clientState.TerritoryChanged += OnTerritoryChanged;
        OnTerritoryChanged(this.clientState.TerritoryType);
    }

    public void Dispose()
    {
        this.clientState.TerritoryChanged -= OnTerritoryChanged;
        GC.SuppressFinalize(this);
    }

    public unsafe bool InSharedWorldMap()
    {
        var territoryIntendedUse = (TerritoryIntendedUseEnum)GameMain.Instance()->CurrentTerritoryIntendedUseId;

        switch (territoryIntendedUse)
        {
            case TerritoryIntendedUseEnum.City_Area:
            case TerritoryIntendedUseEnum.Open_World:
            case TerritoryIntendedUseEnum.Quest_Area_2: // Raid Public Area
            case TerritoryIntendedUseEnum.Residential_Area:
            case TerritoryIntendedUseEnum.Gold_Saucer:
                return true;
            case TerritoryIntendedUseEnum.Housing_Instances:
                return HousingManager.Instance()->GetCurrentIndoorHouseId().IsValid();
            case TerritoryIntendedUseEnum.Inn:
            case TerritoryIntendedUseEnum.Dungeon:
            case TerritoryIntendedUseEnum.Variant_Dungeon:
            case TerritoryIntendedUseEnum.Gaol:
            case TerritoryIntendedUseEnum.Starting_Area:
            case TerritoryIntendedUseEnum.Quest_Area: // Pre Trial Dungeon
            case TerritoryIntendedUseEnum.Alliance_Raid:
            case TerritoryIntendedUseEnum.Quest_Battle: // Open World Instance Battle
            case TerritoryIntendedUseEnum.Trial:
            case TerritoryIntendedUseEnum.Quest_Area_3: // MSQ Private Area
            case TerritoryIntendedUseEnum.Raid: // need to check
            case TerritoryIntendedUseEnum.Raid_2:
            case TerritoryIntendedUseEnum.Frontline:
            case TerritoryIntendedUseEnum.Chocobo_Square: // need to check
            case TerritoryIntendedUseEnum.Restoration_Event:
            case TerritoryIntendedUseEnum.Sanctum: // Wedding
            case TerritoryIntendedUseEnum.Lord_of_Verminion: // need to check
            case TerritoryIntendedUseEnum.Diadem:
            case TerritoryIntendedUseEnum.Hall_of_the_Novice:
            case TerritoryIntendedUseEnum.Crystalline_Conflict:
            case TerritoryIntendedUseEnum.Quest_Battle_2: // MSQ Event Area, need to check
            case TerritoryIntendedUseEnum.Barracks:
            case TerritoryIntendedUseEnum.Deep_Dungeon:
            case TerritoryIntendedUseEnum.Seasonal_Event:
            case TerritoryIntendedUseEnum.Treasure_Map_Duty:
            case TerritoryIntendedUseEnum.Seasonal_Event_Duty:
            case TerritoryIntendedUseEnum.Battlehall:
            case TerritoryIntendedUseEnum.Crystalline_Conflict_2:
            case TerritoryIntendedUseEnum.Diadem_2:
            case TerritoryIntendedUseEnum.Rival_Wings:
            case TerritoryIntendedUseEnum.Unknown_1: // need to check
            case TerritoryIntendedUseEnum.Eureka:
            case TerritoryIntendedUseEnum.Leap_of_Faith:
            case TerritoryIntendedUseEnum.Masked_Carnivale:
            case TerritoryIntendedUseEnum.Ocean_Fishing:
            case TerritoryIntendedUseEnum.Diadem_3:
            case TerritoryIntendedUseEnum.Bozja:
            case TerritoryIntendedUseEnum.Island_Sanctuary:
            case TerritoryIntendedUseEnum.Battlehall_2:
            case TerritoryIntendedUseEnum.Battlehall_3:
            case TerritoryIntendedUseEnum.Large_Scale_Raid:
            case TerritoryIntendedUseEnum.Large_Scale_Savage_Raid:
            case TerritoryIntendedUseEnum.Quest_Area_4:
            case TerritoryIntendedUseEnum.Tribal_Instance:
            case TerritoryIntendedUseEnum.Criterion_Duty:
            case TerritoryIntendedUseEnum.Criterion_Savage_Duty:
            case TerritoryIntendedUseEnum.Blunderville:
            case TerritoryIntendedUseEnum.Occult_Crescent:
            default:
                return false;
        }

        //var currentCfc = this.dataManager.GetExcelSheet<ContentFinderCondition>().GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
        //return currentCfc.RowId is 0;
    }

    public unsafe string GetCurrentMapPublicRoomName()
    {
        var s = new StringBuilder("public");
        if (InSharedWorldMap())
        {
            s.Append('_');
            if (this.clientState.LocalPlayer != null)
            {
                s.Append(this.clientState.LocalPlayer.CurrentWorld.Value.Name.ToString());
            }
            else
            {
                s.Append("Unknown");
            }
        }
        else
        {
            s.Append("_Instance");
        }
        s.Append("_t"); s.Append(CurrentTerritoryId);
        s.Append("_m"); s.Append(CurrentMapId);

        var instance = UIState.Instance()->PublicInstance;
        if (instance.IsInstancedArea())
        {
            s.Append("_i"); s.Append(instance.InstanceId);
        }

        var territoryIntendedUse = (TerritoryIntendedUseEnum)GameMain.Instance()->CurrentTerritoryIntendedUseId;
        if (territoryIntendedUse == TerritoryIntendedUseEnum.Residential_Area ||
            territoryIntendedUse == TerritoryIntendedUseEnum.Housing_Instances)
        {
            var housingManager = HousingManager.Instance();
            var houseId = housingManager->GetCurrentIndoorHouseId();
            if (houseId.IsValid())
            {
                s.Append("_h"); s.Append(houseId);
            }
            else
            {
                var ward = housingManager->GetCurrentWard();
                if (ward != -1)
                {
                    s.Append("_w"); s.Append(ward);
                    var division = housingManager->GetCurrentDivision();
                    if (division != 0)
                    {
                        s.Append("_d"); s.Append(division);
                    }
                    //s.Append("_r"); s.Append(housingManager->GetCurrentRoom());
                    //s.Append("_p"); s.Append(housingManager->GetCurrentPlot());
                }
            }
        }
        return s.ToString();
    }

    private void OnTerritoryChanged(ushort obj)
    {
        OnMapChanged?.Invoke();
    }
}
