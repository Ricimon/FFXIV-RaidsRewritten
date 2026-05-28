using System;
using Flecs.NET.Core;
using RaidsRewritten.Log;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class CommonQueries(ILogger logger) : IDisposable
{
    public Query<Player.Component> LocalPlayerQuery;
    public Query<Player.Component> AllPlayersQuery;
    public Query<Player.Component> AllOtherPlayersQuery;

    public Query<Condition.Component, Condition.Status, Condition.StatusTooltip> StatusQuery;
    public Query<Condition.Component, Condition.Status, Condition.StatusTooltip> StatusEnhancementQuery;
    public Query<Condition.Component, Condition.Status, Condition.StatusTooltip> StatusEnfeeblementQuery;
    public Query<Condition.Component, Condition.Status, Condition.StatusTooltip> StatusOtherQuery;
    public Query<FlyText, FlyTextReady> StatusFlyTextReadyQuery;

    public void CreateQueries(World world)
    {
        LocalPlayerQuery = world.QueryBuilder<Player.Component>().With<Player.LocalPlayer>().Cached().Build();
        AllPlayersQuery = world.QueryBuilder<Player.Component>().Cached().Build();
        AllOtherPlayersQuery = world.QueryBuilder<Player.Component>().Without<Player.LocalPlayer>().Cached().Build();

        // these are here because I crash on dispose if I put these in StatusCustomProcessor
        StatusQuery = StatusCommonProcessor.QueryForStatus(world);
        StatusEnhancementQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusEnhancement>(world);
        StatusEnfeeblementQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusEnfeeblement>(world);
        StatusOtherQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusOther>(world);
        StatusFlyTextReadyQuery = world.QueryBuilder<FlyText, FlyTextReady>().Cached().Build();
    }

    public void Dispose()
    {
        LocalPlayerQuery.SafeDispose();
        AllPlayersQuery.SafeDispose();
        AllOtherPlayersQuery.SafeDispose();

        StatusQuery.SafeDispose();
        StatusEnhancementQuery.SafeDispose();
        StatusEnfeeblementQuery.SafeDispose();
        StatusOtherQuery.SafeDispose();
        StatusFlyTextReadyQuery.SafeDispose();
    }
}
