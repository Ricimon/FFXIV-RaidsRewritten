﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class DreadknightInUCoB : Mechanic
{
    private const uint NeurolinkDataId = 0x1E88FF;
    private readonly Vector3 ArenaCenter = new(0,0,0);
    private const int GenerateId = 9902;
    private const int HatchId = 9903;
    private const float BaseSpeedIncrement = 0.5f;
    private const float TwintaniaId = 0x1FDF;
    private const byte AddsWeather = 31;
    private const float AddsDreadknightSpawnDelay = 10f;
    private const string SwappableTetherVfx = "vfx/channeling/eff/chn_light01f.avfx";
    private const int SecondsUntilSwappable = 30;

    private readonly List<uint> actionIds = [
        7538,  // interject
        7551,  // head graze
        7554,  // leg graze
        7540,  // low blow
        7863,  // leg sweep
        25880, // sleep
        16560, // repose
    ]; 

    private Entity? dreadknight;
    private readonly List<Entity> attacks = [];

    private int neurolinksSpawned = 0;
    private int oviformsOnField = 0;
    private DateTime lastTargetSwap = DateTime.MinValue;
    private bool tetherVfxChanged = true;

    public override void Reset()
    {
        SoftReset();
        neurolinksSpawned = 0;
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
    }

    private void SoftReset()
    {
        this.dreadknight?.Destruct();
        this.dreadknight = null;
        oviformsOnField = 0;
        lastTargetSwap = DateTime.MinValue;
        tetherVfxChanged = true;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnCombatStart()
    {
        if (dreadknight.HasValue)
        {
            dreadknight.Value.Destruct();
            dreadknight = null;
        }

        SpawnDreadknight();
    }

    public override void OnWeatherChange(byte weather)
    {
        if (weather == AddsWeather)
        {
            attacks.Add(DelayedAction.Create(World, SpawnDreadknight, AddsDreadknightSpawnDelay));
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NeurolinkDataId) { return; }

        neurolinksSpawned++;

        if (neurolinksSpawned > 2)
        {
            SoftReset();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!dreadknight.HasValue) { return; }

        if (set.Action.Value.RowId == GenerateId)
        {
            oviformsOnField = neurolinksSpawned;
        } else if (set.Action.Value.RowId == HatchId) {
            oviformsOnField--;
            if (oviformsOnField <= 0 && dreadknight.HasValue)
            {
                var speedIncrement = BaseSpeedIncrement;
                if (neurolinksSpawned >= 3)
                {
                    speedIncrement *= 2;
                }
                Dreadknight.IncrementSpeed(dreadknight.Value, speedIncrement);
            }
        } else if (actionIds.Contains(set.Action.Value.RowId)) {
            var timeDiff = DateTime.Now - lastTargetSwap;
            if (timeDiff.TotalSeconds < SecondsUntilSwappable && Dreadknight.HasTarget(dreadknight.Value)) { return; }
            if (set.Source == null) { return; }

            Dreadknight.ApplyTarget(dreadknight.Value, set.Source);
            lastTargetSwap = DateTime.Now;
            tetherVfxChanged = false;
        }
    }

    public override void OnVFXSpawn(IGameObject? target, string vfxPath)
    {
        if (vfxPath.Equals("vfx/channeling/eff/chn_kosoku1f.avfx"))
        {
            Reset();
        }
    }

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (dreadknight.HasValue && !tetherVfxChanged && (DateTime.Now - lastTargetSwap).Seconds > SecondsUntilSwappable)
        {
            Dreadknight.ChangeTetherVfx(dreadknight.Value, SwappableTetherVfx);
            tetherVfxChanged = true;
        }
    }

    private void SpawnDreadknight()
    {
        if (this.AttackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
        {
            Dalamud.ToastGui.ShowNormal("Twintania channels energy to the Dreadknight...");
            dreadknight.Set(new Position(ArenaCenter));
            this.dreadknight = dreadknight;

            foreach (var obj in Dalamud.ObjectTable)
            {
                if (obj.DataId == TwintaniaId)
                {
                    dreadknight.Set(new Dreadknight.BackupTarget(obj));
                    break;
                }
            }
        }
    }
}
