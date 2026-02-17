using Dalamud.Utility;
using ECommons.DalamudServices;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Data;

public struct Status
{
    public uint Id;
    public uint IconId;
    public string Name = "";
    public string Description = "";
    public float TimeRemaining;
    public byte MaxStacks;
    public byte StatusCategory;
    public byte PartyListPriority;
    public byte CanIncreaseRewards;
    public byte ParamEffect;

    public float RemainingTime;
    public bool SourceIsSelf;
    public int TooltipShown = -1;

    public string OriginalPath = "";

    public readonly bool IsEnhancement => StatusCategory == 1 && CanIncreaseRewards == 0;
    public readonly bool IsOtherEnhancement => StatusCategory == 1 && CanIncreaseRewards == 1;
    public readonly bool IsConditionalEnhancement => StatusCategory == 1 && CanIncreaseRewards == 2;
    public readonly bool IsEnfeeblement => StatusCategory == 2;
    public readonly bool IsOtherEnfeeblement => IsEnfeeblement && CanIncreaseRewards == 1;
    public readonly bool IsCustom = false;
    // Some statuses are invisible and are in the status list but are not shown in any UI
    // Class-based damage buff in Criterion (ParamEffect 31)
    // Hoofing It in Occult Crescent (Id 1778)
    public readonly bool IsVisible => ParamEffect != 31 && Id != 1778;

    public Status(Condition.Status status, Condition.Component condition, StatusType statusType, FileReplacement? replacement = null)
    {
        IsCustom = true;
        IconId = (uint)status.Icon;
        Name = status.Title;
        if (replacement != null)
        {
            OriginalPath = replacement.Value.OriginalPath;
        }
        Description = status.Description;
        TimeRemaining = condition.TimeRemaining;
        StatusCategory = 2;  // TODO: think about logic for enhancement buffs later, just gonna make them all enfeeblements for now
        PartyListPriority = byte.MaxValue;
        CanIncreaseRewards = 0;
        ParamEffect = 0;
    }

    public Status(Lumina.Excel.Sheets.Status luminaStatus) => InitLumina(luminaStatus);

    public Status(FFXIVClientStructs.FFXIV.Client.Game.Status status)
    {
        var luminaStatusSheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>(Svc.ClientState.ClientLanguage);
        TimeRemaining = status.RemainingTime;
        if (!luminaStatusSheet.TryGetRow(status.StatusId, out var luminaStatus)) { InitDefault(); return; }
        InitLumina(luminaStatus);
        if (luminaStatus.MaxStacks > 0)
        {
            this.IconId += (uint)(status.Param - 1);
        }
    }

    private void InitDefault()
    {
        Id = 0;
        Name = "";
        Description = "";
    }

    private void InitLumina(Lumina.Excel.Sheets.Status luminaStatus)
    {
        Id = luminaStatus.RowId;
        IconId = luminaStatus.Icon;
        Name = luminaStatus.Name.ToDalamudString().TextValue;
        Description = luminaStatus.Description.ToDalamudString().TextValue;
        MaxStacks = luminaStatus.MaxStacks;
        StatusCategory = luminaStatus.StatusCategory;
        PartyListPriority = luminaStatus.PartyListPriority;
        CanIncreaseRewards = luminaStatus.CanIncreaseRewards;
        ParamEffect = luminaStatus.ParamEffect;
    }
}

public enum StatusType
{
    None = 0,

    SelfEnhancement = 1,
    SelfEnfeeblement = 2,
    SelfOther = 3,
    SelfConditionalEnhancement = 4,

    PartyListStatus = 10,
    TargetStatus = 11,
}