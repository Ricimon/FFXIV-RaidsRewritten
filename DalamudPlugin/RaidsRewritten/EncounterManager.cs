using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ObjectLifeTracker;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts;
using RaidsRewritten.Utility;

namespace RaidsRewritten;

public class EncounterManager : IDisposable
{
    private readonly IGameInteropProvider gameInteropProvider;
    private readonly ISigScanner sigScanner;
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly MapEffectProcessor mapEffectProcessor;
    private readonly ObjectEffectProcessor objectEffectProcessor;
    private readonly ActorControlProcessor actorControlProcessor;
    private readonly ILogger logger;

    private readonly List<string> BlacklistedPcVfx = [
        "vfx/common/eff/dk02ht_zan0m.avfx",
        "vfx/common/eff/dk03ht_bct0m.avfx",
        "vfx/common/eff/dk03ht_mct0s.avfx",
        "vfx/common/eff/dk04ht_cur0h.avfx",
        "vfx/common/eff/dk04ht_ele0h.avfx",
        "vfx/common/eff/dk04ht_hpt0h.avfx",
        "vfx/common/eff/dk04ht_win0h.avfx",
    ];

    private List<Mechanic> activeMechanics = [];

    public EncounterManager(
        IGameInteropProvider gameInteropProvider,
        ISigScanner sigScanner,
        IObjectTable objectTable,
        IClientState clientState,
        IDataManager dataManager,
        IFramework framework,
        MapEffectProcessor mapEffectProcessor,
        ObjectEffectProcessor objectEffectProcessor,
        ActorControlProcessor actorControlProcessor,
        ILogger logger)
    {
        this.gameInteropProvider = gameInteropProvider;
        this.sigScanner = sigScanner;
        this.objectTable = objectTable;
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.framework = framework;
        this.mapEffectProcessor = mapEffectProcessor;
        this.objectEffectProcessor = objectEffectProcessor;
        this.actorControlProcessor = actorControlProcessor;
        this.logger = logger;
    }

    public void Init()
    {
        this.mapEffectProcessor.Init(OnMapEffect);
        this.objectEffectProcessor.Init(OnObjectEffect);
        this.actorControlProcessor.Init(OnActorControl);
        AttachedInfo.Init(logger, OnStartingCast, OnVFXSpawn);
        DirectorUpdate.Init(OnDirectorUpdate, logger);
        ObjectLife.Init(gameInteropProvider, sigScanner, objectTable, logger);
        ObjectLife.OnObjectCreation += OnObjectCreation;
        ActionEffect.ActionEffectEntryEvent += OnActionEffect;
        ActionEffect.ActionEffectEvent += OnActionEffectEvent;
    }

    public void Dispose()
    {
        AttachedInfo.Dispose();
        DirectorUpdate.Dispose();
        ObjectLife.Dispose();
        ActionEffect.Dispose();
        GC.SuppressFinalize(this);
    }

    public void AddMechanic(Mechanic mechanic)
    {
        activeMechanics.Add(mechanic);
    }

    public void RemoveMechanic(Mechanic mechanic)
    {
        activeMechanics.Remove(mechanic);
    }

    private void OnMapEffect(uint Position, ushort Param1, ushort Param2)
    {
        var text = $"MAP_EFFECT: {Position}, {Param1}, {Param2}";
        this.logger.Debug(text);
    }

    private void OnObjectEffect(uint Target, ushort Param1, ushort Param2)
    {
        var gameObject = this.objectTable.SearchByEntityId(Target);
        if (gameObject == null) { return; }

        var text = $"OBJECT_EFFECT: on {gameObject.Name.TextValue} 0x{Target:X}/0x{gameObject.GameObjectId:X} data {Param1}, {Param2}";
        this.logger.Debug(text);
    }

    private void OnStartingCast(uint source, uint castId)
    {
        var sourceObject = this.objectTable.SearchByEntityId(source);
        if (sourceObject == null) { return; }
        if (sourceObject is not IBattleChara battleChara) { return; }

        if (sourceObject is IPlayerCharacter)
        {
            return;
        }

        var actionName = "<Unknown>";
        var actionSheet = this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(this.clientState.ClientLanguage);
        if (actionSheet.TryGetRow(battleChara.CastActionId, out var action))
        {
            actionName = action.Name.ExtractText();
        }
        var text = $"CAST: {battleChara.Name} (0x{battleChara.EntityId:X}|{battleChara.Position}) starts casting {actionName} ({battleChara.NameId}>{battleChara.CastActionId})";
        this.logger.Debug(text);
    }

    private void OnVFXSpawn(uint target, string vfxPath)
    {
        if (Utils.BlacklistedVFX.Contains(vfxPath)) { return; }

        var text = new StringBuilder($"VFX: {vfxPath}");

        var obj = this.objectTable.SearchByEntityId(target);
        if (obj == null)
        {
            this.logger.Debug(text.ToString());
            return;
        }

        if (obj is ICharacter c)
        {
            if (c is IPlayerCharacter && BlacklistedPcVfx.Contains(vfxPath)) { return; }
            var targetText = c.AddressEquals(this.clientState.LocalPlayer) ? "me" : (c is IPlayerCharacter pc ? pc.GetJob().ToString() : c.DataId.ToString() ?? "Unknown");
            unsafe
            {
                text.Append($" spawned on {targetText}, npc id={c.NameId}, model id={c.Struct()->ModelContainer.ModelCharaId}, name npc id={c.NameId}, position={c.Position}, name={c.Name}");
            }
        }
        else
        {
            unsafe
            {
                text.Append($" spawned on {obj.DataId}, npc id={obj.Struct()->GetNameId()}, position={obj.Position}");
            }
        }
        this.logger.Debug(text.ToString());
    }

    private void OnDirectorUpdate(long a1, long a2, DirectorUpdateCategory a3, uint a4, uint a5, int a6, int a7)
    {
        var text = $"DIRECTOR_UPDATE: {a3}, {a4:X8}, {a5:X8}, {a6:X8}, {a7:X8}";
        this.logger.Debug(text);
    }

    private unsafe void OnObjectCreation(nint newObjectPointer)
    {
        this.framework.Run(() =>
        {
            var text = new StringBuilder("OBJECT_CREATED: ");
            var obj = this.objectTable.FirstOrDefault(x => x.Address == newObjectPointer);
            if (obj == null)
            {
                text.Append($"0x{newObjectPointer:X}");
                this.logger.Debug(text.ToString());

                foreach (var mechanic in activeMechanics)
                {
                    mechanic.OnObjectCreation(newObjectPointer, null);
                }
                return;
            }

            if (obj.Name.TextValue.Length > 0)
            {
                text.Append($"{obj.Name.TextValue} ");
            }
            text.Append($"(0x{newObjectPointer:X}|{obj.Position})");
            text.Append($" Kind {obj.ObjectKind}");
            text.Append($" DataId 0x{obj.DataId:X}");
            this.logger.Debug(text.ToString());

            foreach (var mechanic in activeMechanics)
            {
                mechanic.OnObjectCreation(newObjectPointer, obj);
            }
        });
    }

    private void OnActionEffect(uint ActionID, ushort animationID, ActionEffectType type, uint sourceID, ulong targetIOD, uint damage)
    {

    }

    private void OnActionEffectEvent(ActionEffectSet set)
    {
        var text = new StringBuilder("ACTION:");

        if (set.SourceCharacter.HasValue)
        {
            var source = set.SourceCharacter.Value;
            if (source.GetObjectKind() == ObjectKind.Pc)
            {
                return;
            }
            text.Append($" source: {source.NameString} (0x{source.EntityId:X}),");
        }

        if (!set.Action.HasValue) { return; }

        var actionName = "<Unknown>";
        var actionSheet = this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(this.clientState.ClientLanguage);
        if (actionSheet.TryGetRow(set.Action.Value.RowId, out var action))
        {
            actionName = action.Name.ExtractText();
        }
        text.Append($" action {actionName} ({set.Action.Value.RowId}), anim id {set.Header.AnimationId} numTargets: {set.Header.TargetCount}");
        this.logger.Debug(text.ToString());
    }

    private void OnActorControl(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, ulong targetId, byte replaying)
    {
        var source = this.objectTable.SearchByEntityId(sourceId);
        if (source == null) { return; }
        if (source is not IBattleChara battleChara) { return; }

        if (source is IPlayerCharacter)
        {
            return;
        }

        var text = new StringBuilder($"ACTOR_CONTROL: source {source.Name} (0x{sourceId})");
        text.Append($", command {command}, {p1}, {p2}, {p3}, {p4}, {p5}, {p6}");
        text.Append($", targetId 0x{targetId:X}, replaying {replaying}");
        this.logger.Debug(text.ToString());
    }
}
