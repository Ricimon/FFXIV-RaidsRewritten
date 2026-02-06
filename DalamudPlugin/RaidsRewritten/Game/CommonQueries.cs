using System;
using Flecs.NET.Core;
using RaidsRewritten.Memory;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class CommonQueries : IDisposable
{
    public Query<Player.Component> LocalPlayerQuery { get; private set; }
    public Query<Player.Component> AllPlayersQuery { get; private set; }

    public Query<Condition.Component, Condition.Status> StatusQuery { get; private set; }
    public Query<Condition.Component, Condition.Status> StatusEnhancementQuery { get; private set; }
    public Query<Condition.Component, Condition.Status> StatusEnfeeblementQuery { get; private set; }
    public Query<Condition.Component, Condition.Status> StatusOtherQuery { get; private set; }

    private bool disposed;

    public void CreateQueries(World world)
    {
        if (!this.LocalPlayerQuery.IsValid())
        {
            this.LocalPlayerQuery = Player.QueryForLocalPlayer(world);
        }
        if (!this.AllPlayersQuery.IsValid())
        {
            this.AllPlayersQuery = Player.QueryForAllPlayers(world);
        }


        // these are here because I crash on dispose if I put these in StatusCustomProcessor
        if (!this.StatusQuery.IsValid())
        {
            this.StatusQuery = StatusCommonProcessor.QueryForStatus(world);
        }
        if (!this.StatusEnhancementQuery.IsValid())
        {
            this.StatusEnhancementQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusEnhancement>(world);
        }
        if (!this.StatusEnfeeblementQuery.IsValid())
        {
            this.StatusEnfeeblementQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusEnfeeblement>(world);
        }
        if (!this.StatusOtherQuery.IsValid())
        {
            this.StatusOtherQuery = StatusCommonProcessor.QueryForStatusType<Condition.StatusOther>(world);
        }
    }

    public void Dispose()
    {
        // Anything related to queries cannot be accessed after the world has been destroyed, so only
        // allow this Dispose method to be called once, which should be right before World destruction.
        if (disposed) { return; }

        if (this.LocalPlayerQuery.IsValid()) { this.LocalPlayerQuery.Dispose(); }
        if (this.AllPlayersQuery.IsValid()) { this.AllPlayersQuery.Dispose(); }
        if (this.StatusEnhancementQuery.IsValid()) { this.StatusEnhancementQuery.Dispose(); }
        if (this.StatusEnfeeblementQuery.IsValid()) { this.StatusEnfeeblementQuery.Dispose(); }
        if (this.StatusOtherQuery.IsValid()) { this.StatusOtherQuery.Dispose(); }
        disposed = true;
    }
}
