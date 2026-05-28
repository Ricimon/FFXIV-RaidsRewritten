// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/FlyPopupTextProcessor.cs
// 37e76d3
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameFunctions;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe sealed class StatusFlyPopupTextProcessor : IDalamudHook, IDisposable
{
    public class FlyPopupTextData(Scripts.Conditions.Condition.Status status, bool isAddition, FileReplacement? replacement = null)
    {
        public Scripts.Conditions.Condition.Status Status = status;
        public FileReplacement? Replacement = replacement;
        public bool IsAddition = isAddition;
    }

    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly ResourceLoader resourceLoader;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    public FlyPopupTextData? CurrentElement = null;

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
    }

    public void HookToDalamud()
    {
        this.dalamudServices.Framework.Update += Framework_Update;
    }

    public void Dispose()
    {
        this.dalamudServices.Framework.Update -= Framework_Update;
    }

    private unsafe void Framework_Update(IFramework framework)
    {
        ProcessPopupText();
        ProcessFlyText();
        if (CurrentElement != null) CurrentElement = null!;

        Entity? toDestroy = null;
        Entity? toRemove = null;

        var executedThisFrame = false;
        commonQueries.StatusFlyTextReadyQuery.Each((Entity entity, ref FlyText flytext, ref FlyTextReady flytextData) => {
            if (executedThisFrame) return;
            var data = flytextData.Data;
            Character* target = null;
            foreach (var pc in this.dalamudServices.ObjectTable.PlayerObjects)
            {
                GameObject* obj = pc.GameObject();
                if (obj == null) continue;
                if (obj->EntityId != flytext.OwnerEntityId) continue;
                if (!obj->IsCharacter()) continue;

                target = (Character*)obj;
                break; // Break out of loop once found.
            }

            // Process logic for non-null target.
            if (target != null)
            {
                //PluginLog.Debug($"Processing {e.Status.Title} at {Utils.Frame} for {target->NameString}...");
                if (entity.TryGet<FileReplacement>(out var replacement))
                {
                    data.Replacement = replacement;
                }

                CurrentElement = data;
                //var isMine = e.Status.Applier == LocalPlayer.NameWithWorld && e.IsAddition;
                FlyTextKind kind;
                if (flytext.IsEnfeeblement)
                {
                    kind = data.IsAddition ? FlyTextKind.Debuff : FlyTextKind.DebuffFading;
                }
                else
                {
                    kind = data.IsAddition ? FlyTextKind.Buff : FlyTextKind.BuffFading;
                }
                if (statusCommonProcessor.StatusData.TryGetValue((uint)data.Status.Icon, out var statusData))
                {
                    resourceLoader.BattleLog_AddToScreenLogWithScreenLogKind((nint)target, (nint)target, kind, 5, 0, 0, (int)statusData.StatusId, (int)statusData.StackCount, 0);
                }
                else
                {
                    PluginLog.Error($"[FlyPopupTextProcessor] Error retrieving data for icon {data.Status.Icon}, please report to developer.");
                }

                if (!flytextData.Data.IsAddition) { toDestroy = entity; }
                else { toRemove = entity; }

                executedThisFrame = true;
            }
            else
            {
                PluginLog.Debug($"Skipping {data.Status.Title} for {flytext.OwnerEntityId:X8}, not found...");
            }
        });

        if (toRemove.HasValue) { toRemove.Value.Remove<FlyTextReady>(); }
        if (toDestroy.HasValue)
        {
            toDestroy.Value.Remove<FlyTextReady>()
                .Remove<FlyText>();
            DelayedAction.Create(ecsContainer.World, () =>
            {
                toDestroy.Value.Destruct();
            }, 5, false);
        }
    }

    // flytext for other players
    private void ProcessPopupText()
    {
        if (CurrentElement == null) { return; }
        var addon = statusCommonProcessor.GetAddon("_PopUpText");
        if (addon == null) { return; }

        for (var i = 1; i < addon->UldManager.NodeListCount; i++)
        {
            var candidate = addon->UldManager.NodeList[i];
            if (IsCandidateValid(candidate, CurrentElement))
            {
                var c = candidate->GetAsAtkComponentNode()->Component;
                var sestr = new SeStringBuilder().AddText(CurrentElement.IsAddition ? "+ " : "- ").Append(CurrentElement.Status.Title);
                c->UldManager.NodeList[1]->GetAsAtkTextNode()->SetText(sestr.Encode());

                if (CurrentElement.Replacement != null)
                {
                    //logger.Debug("DALMAUD LOAD ICON");
                    c->UldManager.NodeList[2]->GetAsAtkImageNode()->LoadTexture(CurrentElement.Replacement.Value.OriginalPath);
                }

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
            if (IsCandidateValid(candidate, CurrentElement))
            {
                //logger.Debug("valid");
                var c = candidate->GetAsAtkComponentNode()->Component;
                var sestr = new SeStringBuilder().AddText(CurrentElement.IsAddition ? "+ " : "- ").Append(CurrentElement.Status.Title);
                c->UldManager.NodeList[1]->GetAsAtkTextNode()->SetText(sestr.Encode());

                if (CurrentElement.Replacement != null)
                {
                    //logger.Debug("DALMAUD LOAD ICON");
                    c->UldManager.NodeList[2]->GetAsAtkImageNode()->LoadTexture(CurrentElement.Replacement.Value.OriginalPath);
                }

                CurrentElement = null!;
                return;
            }
        }
    }

    private bool IsCandidateValid(AtkResNode* node, FlyPopupTextData e)
    {
        if (!node->IsVisible()) { return false; }
        var c = node->GetAsAtkComponentNode()->Component;
        if (c->UldManager.NodeListCount < 3 || c->UldManager.NodeListCount > 4) return false;
        if (c->UldManager.NodeList[1]->Type != NodeType.Text) return false;
        if (!c->UldManager.NodeList[1]->IsVisible()) return false;
        if (c->UldManager.NodeList[2]->Type != NodeType.Image) return false;
        if (!c->UldManager.NodeList[2]->IsVisible()) return false;

        var text = c->UldManager.NodeList[1]->GetAsAtkTextNode()->NodeText.Read().GetText();
        if (text is null || e.IsAddition ? text!.StartsWith('-') : text.StartsWith('+')) return false;

        if (statusCommonProcessor.StatusData.TryGetValue((uint)CurrentElement.Status.Icon, out var data))
        {
            if (!text.Contains(data.Name)) return false;
        } else
        {
            return false;
        }
        return true;
    }
}
