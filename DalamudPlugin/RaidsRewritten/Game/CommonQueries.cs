using System;
using Flecs.NET.Core;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Game;

public sealed class CommonQueries : IDisposable
{
    public Query<Player.Component> LocalPlayerQuery { get; private set; }

    private bool disposed;

    public void CreateQueries(World world)
    {
        if (!this.LocalPlayerQuery.IsValid())
        {
            this.LocalPlayerQuery = Player.QueryForLocalPlayer(world);
        }
    }

    public void Dispose()
    {
        // Anything related to queries cannot be accessed after the world has been destroyed, so only
        // allow this Dispose method to be called once, which should be right before World destruction.
        if (disposed) { return; }

        if (this.LocalPlayerQuery.IsValid()) { this.LocalPlayerQuery.Dispose(); }

        disposed = true;
    }
}
