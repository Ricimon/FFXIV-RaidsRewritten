using System;
using System.Linq;
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
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Utility;

namespace RaidsRewritten;

public class EncounterManager : IDisposable
{
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly ILogger logger;

    public EncounterManager(
        IGameInteropProvider gameInteropProvider,
        ISigScanner sigScanner,
        IObjectTable objectTable,
        IClientState clientState,
        MapEffectProcessor mapEffectProcessor,
        ObjectEffectProcessor objectEffectProcessor,
        ActorControlProcessor actorControlProcessor,
        ILogger logger)
    {
        this.objectTable = objectTable;
        this.clientState = clientState;
        this.logger = logger;

        mapEffectProcessor.Init(OnMapEffect);
        objectEffectProcessor.Init(OnObjectEffect);
        actorControlProcessor.Init(OnActorControl);
        AttachedInfo.Init(logger, OnStartingCast, OnVFXSpawn);
        ObjectLife.Init(gameInteropProvider, sigScanner, objectTable, logger);
        ObjectLife.OnObjectCreation += OnObjectCreation;
        ActionEffect.ActionEffectEntryEvent += OnActionEffect;
        ActionEffect.ActionEffectEvent += OnActionEffectEvent;
    }

    public void Dispose()
    {
        AttachedInfo.Dispose();
        ObjectLife.Dispose();
        ActionEffect.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnMapEffect(uint Position, ushort Param1, ushort Param2)
    {
        var text = $"MapEffect: {Position}, {Param1}, {Param2}";
        this.logger.Info(text);
    }

    private void OnObjectEffect(uint Target, ushort Param1, ushort Param2)
    {
        var gameObject = this.objectTable.SearchByEntityId(Target);
        if (gameObject == null) { return; }

        var text = $"ObjectEffect: on {gameObject.Name.TextValue} 0x{Target:X}/0x{gameObject.GameObjectId:X} data {Param1}, {Param2}";
        this.logger.Info(text);
    }

    private void OnStartingCast(uint source, uint castId)
    {
        var sourceObject = this.objectTable.SearchByEntityId(source);
        if (sourceObject == null) { return; }
        if (sourceObject is not IBattleChara battleChara) { return; }

        var text = $"{battleChara.Name} ({battleChara.Position}) starts casting {battleChara.CastActionId} ({battleChara.NameId}>{battleChara.CastActionId})";
        this.logger.Info(text);
    }

    private void OnVFXSpawn(uint target, string vfxPath)
    {
        var obj = this.objectTable.SearchByEntityId(target);
        if (obj == null) { return; }

        if (!Utils.BlacklistedVFX.Contains(vfxPath))
        {
            if (obj is ICharacter c)
            {
                var targetText = c.AddressEquals(this.clientState.LocalPlayer) ? "me" : (c is IPlayerCharacter pc ? pc.GetJob().ToString() : c.DataId.ToString() ?? "Unknown");
                unsafe
                {
                    var text = $"VFX {vfxPath} spawned on {targetText} npc id={c.NameId}, model id={c.Struct()->ModelContainer.ModelCharaId}, name npc id={c.NameId}, position={c.Position}, name={c.Name}";
                    this.logger.Info(text);
                }
            }
            else
            {
                unsafe
                {
                    var text = $"VFX {vfxPath} spawned on {obj.DataId} npc id={obj.Struct()->GetNameId()}, position={obj.Position}";
                    this.logger.Info(text);
                }
            }
        }
    }

    private void OnObjectCreation(nint newObjectPointer)
    {
        this.logger.Info("Object created: 0x{0}", newObjectPointer.ToString("X"));
    }

    private void OnActionEffect(uint ActionID, ushort animationID, ActionEffectType type, uint sourceID, ulong targetIOD, uint damage)
    {

    }

    private void OnActionEffectEvent(ActionEffectSet set)
    {
        this.logger.Debug($"--- source actor: {set.SourceCharacter?.GameObject.EntityId}, action id {set.Header.ActionID}, anim id {set.Header.AnimationId} numTargets: {set.Header.TargetCount} ---");
    }

    private void OnActorControl(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, ulong targetId, byte replaying)
    {
        this.logger.Info("OnActorControl sourceId {0}, command {1}, {2}, {3}, {4}, {5}, {6}, {7}, targetId {8}, replaying {9}",
            sourceId, command, p1, p2, p3, p4, p5, p6, targetId, replaying);
    }
}
