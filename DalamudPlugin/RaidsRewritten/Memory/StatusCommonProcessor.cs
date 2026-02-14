// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/CommonProcessor.cs
// 346527d
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Data;
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
        var seconds = MathF.Round(rem);
        if (seconds <= 0) { return ""; }
        if (seconds < 60) { return seconds.ToString(); }
        var minutes = MathF.Floor(seconds / 60);
        if (minutes < 60) { return $"{minutes}m"; }
        var hours = MathF.Floor(seconds / 3600);
        if (hours < 24) { return $"{hours}h"; }
        var days = MathF.Floor(seconds / 86400);
        if (days < 10) { return $"{days}d"; }
        return ">9d";
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
