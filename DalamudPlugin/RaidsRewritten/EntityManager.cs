using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten;

public sealed class EntityManager : IDalamudHook
{
    private readonly DalamudServices dalamud;
    private readonly World world;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private readonly Dictionary<Type, Func<World, Entity>> entityCreationFunctions = [];

    public EntityManager(
        DalamudServices dalamud,
        EcsContainer container,
        Configuration configuration,
        IEntity[] entities,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.world = container.World;
        this.configuration = configuration;
        this.logger = logger;

        // Register all entities
        foreach (var entity in entities)
        {
            entityCreationFunctions[entity.GetType()] = entity.Create;
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

    public bool TryCreateEntity<T>(out Entity entity)
    {
        if (this.configuration.EverythingDisabled) { entity = default; return false; }

        if (this.entityCreationFunctions.TryGetValue(typeof(T), out var createFunc))
        {
            entity = createFunc.Invoke(this.world);
            return true;
        }
        this.logger.Error("Entity of type {0} is not registered, cannot create entity", typeof(T));
        entity = default;
        return false;
    }

    public void ClearAllManagedEntities()
    {
        this.world.DeleteWith<Model>();
        this.world.DeleteWith<Attack>();
    }

    private void OnTerritoryChanged(ushort obj)
    {
        ClearAllManagedEntities();
    }
}
