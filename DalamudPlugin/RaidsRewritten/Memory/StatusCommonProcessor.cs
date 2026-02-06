// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/CommonProcessor.cs
// 346527d
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusCommonProcessor(
    Configuration configuration,
    DalamudServices dalamudServices,
    ResourceLoader resourceLoader,
    CommonQueries commonQueries,
    Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor) : IDisposable
{
    public nint HoveringOver = 0;

    private readonly Configuration configuration = configuration;
    private readonly DalamudServices dalamudServices = dalamudServices;
    private readonly ResourceLoader resourceLoader = resourceLoader;
    private readonly CommonQueries commonQueries = commonQueries;
    private readonly Lazy<StatusFlyPopupTextProcessor> statusFlyPopupTextProcessor = statusFlyPopupTextProcessor;

    private readonly nint TooltipMemory = Marshal.AllocHGlobal(2 * 1024);
    private int ActiveTooltip = -1;

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

    public void SetIcon(AtkUnitBase* addon, ref Condition.Status status, ref Condition.Component condition, AtkResNode* container)
    {
        if (configuration.UseLegacyStatusRendering) { return; }
        if (!container->IsVisible())
        {
            container->NodeFlags ^= NodeFlags.Visible;
        }
        resourceLoader.LoadIconByID(container->GetAsAtkComponentNode()->Component, status.Icon);

        //var dispelNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[0];

        // timer
        var textNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[2];
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
            AtkStage.Instance()->TooltipManager.ShowTooltip((ushort)addon->Id, container, (byte*)TooltipMemory);
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

    private unsafe ByteColor CreateColor(uint color)
    {
        color = BinaryPrimitives.ReverseEndianness(color);
        var ptr = &color;
        return *(ByteColor*)ptr;
    }

    private string GetTimerText(float rem)
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
    public AtkUnitBase* GetAddon(string addonName) => (AtkUnitBase*)this.dalamudServices.GameGui.GetAddonByName(addonName).Address;
}
