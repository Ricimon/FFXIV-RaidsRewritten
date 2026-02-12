// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/FlyPopupTextProcessor.cs
// 37e76d3
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusFlyPopupTextProcessor
{
    public class FlyPopupTextData (Entity entity, Scripts.Conditions.Condition.Status status, bool isAddition, uint owner, FileReplacement? replacement = null)
    {
        public Scripts.Conditions.Condition.Status Status = status;
        public FileReplacement? Replacement = replacement;
        public bool IsEnfeeblement = entity.Has<Scripts.Conditions.Condition.StatusEnfeeblement>();
        public bool IsAddition = isAddition;
        public uint OwnerEntityId = owner;
    }

    public record struct IconStatusData(uint StatusId, string Name, uint StackCount);

    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly ResourceLoader resourceLoader;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    private List<FlyPopupTextData> Queue = [];
    private nint LastNode = nint.Zero;
    public FlyPopupTextData CurrentElement = null!;
    public Dictionary<uint, IconStatusData> StatusData = [];

    public StatusFlyPopupTextProcessor(
        Configuration configuration,
        DalamudServices dalamudServices,
        ResourceLoader resourceLoader,
        StatusCommonProcessor statusCommonProcessor,
        EcsContainer ecsContainer,
        CommonQueries commonQueries,
        ILogger logger)
    {
        this.configuration = configuration;
        this.dalamudServices = dalamudServices;
        this.resourceLoader = resourceLoader;
        this.statusCommonProcessor = statusCommonProcessor;
        this.ecsContainer = ecsContainer;
        this.commonQueries = commonQueries;
        this.logger = logger;

        foreach (var x in this.dalamudServices.DataManager.GetExcelSheet<Status>())
        {
            var baseData = new IconStatusData(x.RowId, x.Name.ExtractText(), 0);
            StatusData[x.Icon] = baseData;
            for (var i = 2; i <= x.MaxStacks; i++)
            {
                StatusData[(uint)(x.Icon + i - 1)] = baseData with { StackCount = (uint)i };
            }
        }
        this.dalamudServices.Framework.Update += Framework_Update;
    }

    public void Enqueue(FlyPopupTextData data)
    {
        if (!this.configuration.UseLegacyStatusRendering && !configuration.EverythingDisabled)
        {
            Queue.Add(data);
        }
    }

    private unsafe void Framework_Update(IFramework framework)
    {
        ProcessPopupText();
        ProcessFlyText();
        if (CurrentElement != null) CurrentElement = null!;

        var objManager = GameObjectManager.Instance();

        if (Queue.Count > this.configuration.FlyPopupTextLimit)
        {
            PluginLog.Warning($"FlyPopupTextProcessor Queue is too large! Trimming to {this.configuration.FlyPopupTextLimit} closest entities.");
            var n = Queue.RemoveAll(x =>
            {
                var obj = objManager->Objects.GetObjectByEntityId(x.OwnerEntityId);
                return obj == null || !obj->IsCharacter();
            });

            if (n > 0) PluginLog.Information($"  Removed {n} non-player entities");
            var localPlayer = (Character*)StatusCommonProcessor.LocalPlayer();
            if (localPlayer == null) { return; }

            Queue = Queue
                .OrderBy(x => Vector3.DistanceSquared(localPlayer->Position, objManager->Objects.GetObjectByEntityId(x.OwnerEntityId)->Position))
                .Take(this.configuration.FlyPopupTextLimit)
                .ToList();
        }

        while (Queue.TryDequeue(out var e))
        {
            Character* target = null;
            for (var i = 0; i < 200; i++)
            {
                GameObject* obj = objManager->Objects.IndexSorted[i];
                if (obj == null) continue;
                if (obj->EntityId != e.OwnerEntityId) continue;
                if (!obj->IsCharacter()) continue;

                target = (Character*)obj;
                break; // Break out of loop once found.
            }

            // Process logic for non-null target.
            if (target != null)
            {
                //PluginLog.Debug($"Processing {e.Status.Title} at {Utils.Frame} for {target->NameString}...");
                CurrentElement = e;
                //var isMine = e.Status.Applier == LocalPlayer.NameWithWorld && e.IsAddition;
                FlyTextKind kind;
                if (e.IsEnfeeblement)
                {
                    kind = e.IsAddition ? FlyTextKind.Debuff : FlyTextKind.DebuffFading;
                } else
                {
                    kind = e.IsAddition ? FlyTextKind.Buff : FlyTextKind.BuffFading;
                }
                if (StatusData.TryGetValue((uint)e.Status.Icon, out var data))
                {
                    resourceLoader.BattleLog_AddToScreenLogWithScreenLogKind((nint)target, (nint)target, kind, 5, 0, 0, (int)data.StatusId, (int)data.StackCount, 0);
                } else
                {
                    PluginLog.Error($"[FlyPopupTextProcessor] Error retrieving data for icon {e.Status.Icon}, please report to developer.");
                }
                break;
            } else
            {
                PluginLog.Debug($"Skipping {e.Status.Title} for {e.OwnerEntityId:X8}, not found...");
            }
        }
    }

    private void ProcessPopupText()
    {
        if (CurrentElement == null) { return; }
        var addon = statusCommonProcessor.GetAddon("_PopUpText");
        if (addon == null) { return; }

        for (var i = 1; i < addon->UldManager.NodeListCount; i++)
        {
            var candidate = addon->UldManager.NodeList[i];
            if (IsCandidateValid(candidate))
            {
                var c = candidate->GetAsAtkComponentNode()->Component;
                var sestr = new SeStringBuilder().AddText(CurrentElement.IsAddition ? "+ " : "- ").Append(CurrentElement.Status.Title);
                c->UldManager.NodeList[1]->GetAsAtkTextNode()->SetText(sestr.Encode());
                c->UldManager.NodeList[2]->GetAsAtkImageNode()->LoadTexture(this.dalamudServices.TextureProvider.GetIconPath(CurrentElement.Status.Icon), 1);
                CurrentElement = null!;
                return;
            }
        }
    }

    private void ProcessFlyText()
    {
        if (CurrentElement == null) { return; }

        var addon = statusCommonProcessor.GetAddon("_FlyText");
        if (addon == null) { return; }

        for (var i = 1; i < addon->UldManager.NodeListCount; i++)
        {
            var candidate = addon->UldManager.NodeList[i];
            if (IsCandidateValid(candidate))
            {
                var c = candidate->GetAsAtkComponentNode()->Component;
                var sestr = new SeStringBuilder().AddText(CurrentElement.IsAddition ? "+ " : "- ").Append(CurrentElement.Status.Title);
                c->UldManager.NodeList[1]->GetAsAtkTextNode()->SetText(sestr.Encode());

                if (CurrentElement.Replacement != null)
                {
                    c->UldManager.NodeList[2]->GetAsAtkImageNode()->LoadTexture(CurrentElement.Replacement.Value.OriginalPath);
                }

                CurrentElement = null!;
                return;
            }
        }
    }

    private bool IsCandidateValid(AtkResNode* node)
    {
        if (!node->IsVisible()) {
            if ((nint)node == LastNode)
            {
                LastNode = nint.Zero;
            }
            return false;
        }
        var c = node->GetAsAtkComponentNode()->Component;
        if (c->UldManager.NodeListCount < 3 || c->UldManager.NodeListCount > 4) return false;
        if (c->UldManager.NodeList[1]->Type != NodeType.Text) return false;
        if (!c->UldManager.NodeList[1]->IsVisible()) return false;
        if (c->UldManager.NodeList[2]->Type != NodeType.Image) return false;
        if (!c->UldManager.NodeList[2]->IsVisible()) return false;

        var text = MemoryHelper.ReadSeString(&c->UldManager.NodeList[1]->GetAsAtkTextNode()->NodeText)?.GetText();
        if (text is null || !text.StartsWith('-') && !text.StartsWith('+')) return false;

        if (StatusData.TryGetValue((uint)CurrentElement.Status.Icon, out var data))
        {
            if (!text.Contains(data.Name)) return false;
        } else
        {
            return false;
        }
        LastNode = (nint)node;
        return true;
    }

    public void Dispose()
    {
        this.dalamudServices.Framework.Update -= Framework_Update;
    }

}
