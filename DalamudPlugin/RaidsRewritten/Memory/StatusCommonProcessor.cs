// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/CommonProcessor.cs
// 346527d
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusCommonProcessor(
    Configuration configuration,
    DalamudServices dalamudServices,
    ResourceLoader resourceLoader,
    CommonQueries commonQueries,
    ILogger logger) : IDisposable
{
    public nint HoveringOver = 0;

    public readonly nint TooltipMemory = Marshal.AllocHGlobal(2 * 1024);
    private int ActiveTooltip = -1;

    public struct Status
    {
        public uint Id;
        public uint IconId;
        public string Name;
        public string Description;
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

    public readonly List<List<Status>> SortedStatusList = [[], [], [], [], [], [], [], []];

    public void Dispose()
    {
        Marshal.FreeHGlobal(TooltipMemory);
    }

    public void DisableActiveTooltip()
    {
        if (ActiveTooltip != -1)
        {
            AtkStage.Instance()->TooltipManager.HideTooltip((ushort)ActiveTooltip);
            ActiveTooltip = -1;
        }
    }

    public void SetIcon(AtkUnitBase* addon, ref Condition.Status status, ref Condition.Component condition, AtkResNode* container, FileReplacement? replacement = null)
    {
        if (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled) { return; }
        if (!container->IsVisible())
        {
            container->NodeFlags ^= NodeFlags.Visible;
        }

        if (replacement == null)
        {
            resourceLoader.LoadIconByID(container->GetAsAtkComponentNode()->Component, status.Icon);
        } else
        {
            container->GetAsAtkComponentNode()->Component->GetImageNodeById(3)->LoadTexture(replacement.Value.OriginalPath);
            // these are sometimes hidden for whatever reason
            // visibility of component node will take care of hiding, so force these to be visible
            container->GetAsAtkComponentNode()->Component->GetImageNodeById(3)->ToggleVisibility(true);
        }

        //var dispelNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[0];09:56:24.378 | DBG | [RaidsRewritten] 2A726060DF0


        // timer
        var textNode = container->GetAsAtkComponentNode()->Component->GetTextNodeById(2);
        var timerText = "";
        if (!float.IsInfinity(condition.TimeRemaining))
        {
            timerText = condition.TimeRemaining > 0 ? GetTimerText(condition.TimeRemaining) : "";
        }

        if (timerText != null)
        {
            if (!textNode->IsVisible()) { textNode->NodeFlags ^= NodeFlags.Visible; }
        }

        var t = textNode->GetAsAtkTextNode();
        t->SetText((timerText ?? SeString.Empty).Encode());

        t->TextColor = CreateColor(0xffffffff);
        t->EdgeColor = CreateColor(0x333333ff);
        t->BackgroundColor = CreateColor(0);

        // tooltip
        var addr = (nint)container->GetAsAtkComponentNode()->Component;
        if (HoveringOver == addr && status.TooltipShown == -1)
        {
            commonQueries.StatusQuery.Each((ref _, ref status) =>
            {
                status.TooltipShown = -1;
            });
            status.TooltipShown = addon->Id;
            ActiveTooltip = addon->Id;
            AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            var str = status.Title;
            if (status.Description != "")
            {
                str += $"\n{status.Description}";
            }
            str += "\0";
            MemoryHelper.WriteSeString(TooltipMemory, str);
            AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, container, (byte*)TooltipMemory);
        }
        if (status.TooltipShown == addon->Id && HoveringOver != addr)
        {
            status.TooltipShown = -1;
            if (HoveringOver == 0)
            {
                AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            }
        }
    }

    public unsafe static ByteColor CreateColor(uint color)
    {
        color = BinaryPrimitives.ReverseEndianness(color);
        var ptr = &color;
        return *(ByteColor*)ptr;
    }

    public static string GetTimerText(float rem)
    {
        var seconds = MathF.Ceiling(rem);
        if (seconds <= 59) return seconds.ToString();
        var minutes = MathF.Floor(seconds / 60f);
        if (minutes <= 59) return $"{minutes}m";
        var hours = MathF.Floor(minutes / 60f);
        if (hours <= 59) return $"{hours}h";
        var days = MathF.Floor(hours / 24f);
        if (days <= 9) return $"{days}d";
        return $">9d";
    }

    public static Query<Condition.Component, Condition.Status> QueryForStatus(World world) => world.QueryBuilder<Condition.Component, Condition.Status>().Up().Cached().Build();
    public static Query<Condition.Component, Condition.Status> QueryForStatusType<T>(World world)
    {
        return world.QueryBuilder<Condition.Component, Condition.Status>().With<T>().With<Player.LocalPlayer>().Up().Cached().Build();
    }

    // adapted from https://github.com/NightmareXIV/ECommons/blob/master/ECommons/GenericHelpers/AddonHelpers.cs
    // 2d8d2f4
    public static unsafe bool IsAddonReady(AtkUnitBase* addon)
    {
        if ((IntPtr)addon == IntPtr.Zero)
        {
            return false;
        } else
        {
            return addon->IsVisible && addon->UldManager.LoadedState == AtkLoadState.Loaded && addon->IsFullyLoaded();
        }
    }

    public static unsafe bool LocalPlayerAvailable() => Control.Instance()->LocalPlayer is not null;
    public static unsafe nint LocalPlayer() => (nint)Control.Instance()->LocalPlayer;
    public static Query<Condition.Component, Condition.Status> GetAllStatusesOfEntity(Entity e) => e.CsWorld().QueryBuilder<Condition.Component, Condition.Status>().With(flecs.EcsChildOf, e).Build();
    public AtkUnitBase* GetAddon(string addonName) => (AtkUnitBase*)dalamudServices.GameGui.GetAddonByName(addonName).Address;
}
