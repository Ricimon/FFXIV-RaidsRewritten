using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
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
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Encounters;
using RaidsRewritten.Utility;
using Dalamud.Game.ClientState.Conditions;

namespace RaidsRewritten;

public sealed class EncounterManager : IDalamudHook
{
    public IEncounter? ActiveEncounter { get; private set; }

    private readonly DalamudServices dalamud;
    private readonly MapEffectProcessor mapEffectProcessor;
    private readonly ObjectEffectProcessor objectEffectProcessor;
    private readonly ActorControlProcessor actorControlProcessor;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private readonly List<string> BlacklistedPcVfx = [
        "vfx/common/eff/dk02ht_zan0m.avfx",
        "vfx/common/eff/dk03ht_bct0m.avfx",
        "vfx/common/eff/dk03ht_mct0s.avfx",
        "vfx/common/eff/dk04ht_cur0h.avfx",
        "vfx/common/eff/dk04ht_ear0h.avfx",
        "vfx/common/eff/dk04ht_ele0h.avfx",
        "vfx/common/eff/dk04ht_hpt0h.avfx",
        "vfx/common/eff/dk04ht_win0h.avfx",
        "vfx/common/eff/cmat_aoz0f.avfx",
    ];
    private readonly Dictionary<ushort, IEncounter> encounters;

    private bool CombatStarted = false;

    public EncounterManager(
        DalamudServices dalamud,
        MapEffectProcessor mapEffectProcessor,
        ObjectEffectProcessor objectEffectProcessor,
        ActorControlProcessor actorControlProcessor,
        Configuration configuration,
        IEncounter[] encounters,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.mapEffectProcessor = mapEffectProcessor;
        this.objectEffectProcessor = objectEffectProcessor;
        this.actorControlProcessor = actorControlProcessor;
        this.configuration = configuration;
        this.logger = logger;

        this.encounters = encounters.ToDictionary(e => e.TerritoryId, e => e);
    }

    public void HookToDalamud()
    {
        this.mapEffectProcessor.Init(OnMapEffect);
        this.objectEffectProcessor.Init(OnObjectEffect);
        this.actorControlProcessor.Init(OnActorControl);
        AttachedInfo.Init(logger, OnStartingCast, OnVFXSpawn);
        DirectorUpdate.Init(OnDirectorUpdate, logger);
        ObjectLife.Init(dalamud.GameInteropProvider, dalamud.SigScanner, dalamud.ObjectTable, logger);
        ObjectLife.OnObjectCreation += OnObjectCreation;
        ActionEffect.ActionEffectEntryEvent += OnActionEffect;
        ActionEffect.ActionEffectEvent += OnActionEffectEvent;

        this.dalamud.ClientState.TerritoryChanged += this.OnTerritoryChanged;
        OnTerritoryChanged(this.dalamud.ClientState.TerritoryType);
        this.dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        AttachedInfo.Dispose();
        DirectorUpdate.Dispose();
        ObjectLife.Dispose();
        ActionEffect.Dispose();
        this.dalamud.ClientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.dalamud.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnTerritoryChanged(ushort obj)
    {
        ActiveEncounter?.Unload();

        if (this.encounters.TryGetValue(obj, out var encounter))
        {
            ActiveEncounter = encounter;
            encounter.RefreshMechanics();
            this.logger.Info("Active encounter set to {0}", encounter.Name);
        }
        else
        {
            ActiveEncounter = null;
        }
    }

    private void OnMapEffect(uint Position, ushort Param1, ushort Param2)
    {
        var text = $"MAP_EFFECT: {Position}, {Param1}, {Param2}";
        this.logger.Debug(text);

        if (this.configuration.EverythingDisabled) { return; }
    }

    private void OnObjectEffect(uint Target, ushort Param1, ushort Param2)
    {
        var gameObject = this.dalamud.ObjectTable.SearchByEntityId(Target);
        if (gameObject == null) { return; }

        var text = $"OBJECT_EFFECT: on {gameObject.Name.TextValue} 0x{Target:X}/0x{gameObject.GameObjectId:X} data {Param1}, {Param2}";
        this.logger.Debug(text);

        if (this.configuration.EverythingDisabled) { return; }
    }

    private void OnStartingCast(uint source, uint castId)
    {
        var sourceObject = this.dalamud.ObjectTable.SearchByEntityId(source);
        if (sourceObject == null) { return; }
        if (sourceObject is not IBattleChara battleChara) { return; }

        if (sourceObject is IPlayerCharacter)
        {
            return;
        }

        var actionName = "<Unknown>";
        var actionSheet = this.dalamud.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(this.dalamud.ClientState.ClientLanguage);
        if (actionSheet.TryGetRow(battleChara.CastActionId, out var action))
        {
            actionName = action.Name.ExtractText();
        }
        var text = $"CAST: {battleChara.Name} (0x{battleChara.EntityId:X}|{battleChara.Position}) starts casting {actionName} ({battleChara.NameId}>{battleChara.CastActionId})";
        this.logger.Debug(text);

        if (this.configuration.EverythingDisabled) { return; }

        if (ActiveEncounter != null)
        {
            foreach (var mechanic in ActiveEncounter.GetMechanics())
            {
                mechanic.OnStartingCast(action, battleChara);
            }
        }
    }

    private void OnVFXSpawn(uint target, string vfxPath)
    {
        if (Utils.BlacklistedVFX.Contains(vfxPath)) { return; }

        var text = new StringBuilder($"VFX: {vfxPath}");

        var obj = this.dalamud.ObjectTable.SearchByEntityId(target);
        if (obj == null)
        {
            this.logger.Debug(text.ToString());

            if (this.configuration.EverythingDisabled) { return; }
            if (ActiveEncounter != null)
            {
                foreach (var mechanic in ActiveEncounter.GetMechanics())
                {
                    mechanic.OnVFXSpawn(obj, vfxPath);
                }
            }
            return;
        }

        if (obj is ICharacter c)
        {
            if (c is IPlayerCharacter && BlacklistedPcVfx.Contains(vfxPath)) { return; }
            var targetText = c.AddressEquals(this.dalamud.ClientState.LocalPlayer) ? "me" : (c is IPlayerCharacter pc ? pc.GetJob().ToString() : c.DataId.ToString() ?? "Unknown");
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

        if (this.configuration.EverythingDisabled) { return; }
        if (ActiveEncounter != null)
        {
            foreach (var mechanic in ActiveEncounter.GetMechanics())
            {
                mechanic.OnVFXSpawn(obj, vfxPath);
            }
        }
    }

    private void OnDirectorUpdate(long a1, long a2, DirectorUpdateCategory a3, uint a4, uint a5, int a6, int a7)
    {
        var text = $"DIRECTOR_UPDATE: {a3}, {a4:X8}, {a5:X8}, {a6:X8}, {a7:X8}";
        this.logger.Debug(text);

        if (this.configuration.EverythingDisabled) { return; }
        if (ActiveEncounter != null)
        {
            if (a3 == DirectorUpdateCategory.Commence ||
                a3 == DirectorUpdateCategory.Recommence)
            {
                ActiveEncounter.IncrementRngSeed();
            }
            foreach (var mechanic in ActiveEncounter.GetMechanics())
            {
                mechanic.OnDirectorUpdate(a3);
            }
        }
    }

    private unsafe void OnObjectCreation(nint newObjectPointer)
    {
        this.dalamud.Framework.Run(() =>
        {
            var text = new StringBuilder("OBJECT_CREATED: ");
            var obj = this.dalamud.ObjectTable.FirstOrDefault(x => x.Address == newObjectPointer);
            if (obj == null)
            {
                text.Append($"0x{newObjectPointer:X}");
                this.logger.Debug(text.ToString());

                if (this.configuration.EverythingDisabled) { return; }

                if (ActiveEncounter != null)
                {
                    foreach (var mechanic in ActiveEncounter.GetMechanics())
                    {
                        mechanic.OnObjectCreation(newObjectPointer, null);
                    }
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
            text.Append($" EntityId 0x{obj.EntityId:X}");
            this.logger.Debug(text.ToString());

            if (this.configuration.EverythingDisabled) { return; }

            if (ActiveEncounter != null)
            {
                foreach (var mechanic in ActiveEncounter.GetMechanics())
                {
                    mechanic.OnObjectCreation(newObjectPointer, obj);
                }
            }
        });
    }

    private void OnActionEffect(uint ActionID, ushort animationID, ActionEffectType type, uint sourceID, ulong targetIOD, uint damage)
    {

    }

    private void OnActionEffectEvent(ActionEffectSet set)
    {
        var text = new StringBuilder("ACTION: ");

        // Ignore actions from other players
        var source = set.Source;
        var target = set.Target;
        if (source != null &&
            source.ObjectKind == ObjectKind.Player &&
            source.EntityId != this.dalamud.ClientState.LocalPlayer?.EntityId)
        {
            return;
        }

        text.Append(set.ToString());
        this.logger.Debug(text.ToString());

        if (this.configuration.EverythingDisabled) { return; }

        if (ActiveEncounter != null)
        {
            foreach (var mechanic in ActiveEncounter.GetMechanics())
            {
                mechanic.OnActionEffectEvent(set);
            }
        }
    }

    private void OnActorControl(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, ulong targetId, byte replaying)
    {
        var source = this.dalamud.ObjectTable.SearchByEntityId(sourceId);
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

        if (this.configuration.EverythingDisabled) { return; }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (this.dalamud.Condition[ConditionFlag.InCombat])
        {
            if (!this.CombatStarted)
            {
                this.CombatStarted = true;
                this.logger.Debug("COMBAT STARTED");
                if (this.configuration.EverythingDisabled) { return; }
                
                if (ActiveEncounter != null)
                {
                    foreach (var mechanic in ActiveEncounter.GetMechanics())
                    {
                        mechanic.OnCombatStart();
                    }
                }
            }
        } else
        {
            if (this.CombatStarted)
            {
                this.CombatStarted = false;
                this.logger.Debug("COMBAT ENDED");
                if (this.configuration.EverythingDisabled) { return; }

                if (ActiveEncounter != null)
                {
                    foreach (var mechanic in ActiveEncounter.GetMechanics())
                    {
                        mechanic.OnCombatEnd();
                    }
                }
            }
        }
    }
}
