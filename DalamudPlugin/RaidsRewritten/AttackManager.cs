﻿using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten;

public sealed class AttackManager : IDalamudHook
{
    private readonly DalamudServices dalamud;
    private readonly World world;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private readonly Dictionary<Type, Func<World, Entity>> entityCreationFunctions = [];

    public AttackManager(
        DalamudServices dalamud,
        EcsContainer container,
        Configuration configuration,
        IAttack[] attacks,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.world = container.World;
        this.configuration = configuration;
        this.logger = logger;

        // Register all attacks
        foreach (var attack in attacks)
        {
            entityCreationFunctions.Add(attack.GetType(), attack.Create);
        }
    }

    public void HookToDalamud()
    {
        this.dalamud.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        this.dalamud.ClientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public bool TryCreateAttackEntity<T>(out Entity entity)
    {
        if (this.configuration.EverythingDisabled) { entity = default; return false; }

        if (this.entityCreationFunctions.TryGetValue(typeof(T), out var createFunc))
        {
            entity = createFunc.Invoke(this.world);
            return true;
        }
        this.logger.Error("Attack of type {0} is not registered, cannot create attack entity", typeof(T));
        entity = default;
        return false;
    }

    public void ClearAllAttacks()
    {
        this.world.DeleteWith<Attack>();
    }

    private void OnTerritoryChanged(ushort obj)
    {
        ClearAllAttacks();
    }
}
