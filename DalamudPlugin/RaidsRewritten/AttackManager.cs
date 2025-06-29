using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Flecs.NET.Core;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Systems;

namespace RaidsRewritten;

public sealed class AttackManager : IDisposable
{
    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private readonly Dictionary<Type, Func<World, Entity>> entityCreationFunctions = [];
    private readonly World world;

    public AttackManager(
        DalamudServices dalamud,
        IAttack[] attacks,
        ISystem[] systems,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        this.world = World.Create();

        // Register all attacks
        foreach(var attack in attacks)
        {
            entityCreationFunctions.Add(attack.GetType(), attack.Create);
        }

        // Register all systems
        foreach(var system in systems)
        {
            system.Register(this.world);
        }

        this.dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.dalamud.Framework.Update -= OnFrameworkUpdate;
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

    private void OnFrameworkUpdate(IFramework framework)
    {
        this.world.Progress((float)framework.UpdateDelta.TotalSeconds);
    }
}
