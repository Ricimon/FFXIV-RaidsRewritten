using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten;

public sealed class AttackManager : IDisposable
{
    private readonly DalamudServices dalamud;
    private readonly World world;
    private readonly ILogger logger;

    private readonly Dictionary<Type, Func<World, Entity>> entityCreationFunctions = [];

    public AttackManager(
        DalamudServices dalamud,
        EcsContainer container,
        IAttack[] attacks,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.world = container.World;
        this.logger = logger;

        // Register all attacks
        foreach(var attack in attacks)
        {
            entityCreationFunctions.Add(attack.GetType(), attack.Create);
        }

        this.dalamud.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        this.dalamud.ClientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public bool TryCreateAttackEntity<T>(out Entity entity)
    {
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
