using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class PermanentViceOfApathyTest : Mechanic
{
    private const uint ViceOfApathyDataId = 0x1EAE20;
    private const int SpawnDelayMs = 1000;

    private readonly List<Entity> attacks = [];

    private CancellationTokenSource cts = new();

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            Reset();
            cts.Cancel();
            cts = new();
        }
    }

    public override async void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != ViceOfApathyDataId) { return; }

        var ct = this.cts.Token;
        try
        {
            await Task.Delay(SpawnDelayMs, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { }

        var player = this.Dalamud.ClientState.LocalPlayer;
        if (player != null)
        {
            if (this.AttackManager.TryCreateAttackEntity<Twister>(out var twister))
            {
                twister.Set(new Position(newObject.Position));
                twister.Set(new Rotation(newObject.Rotation));
                attacks.Add(twister);
            }
        }
    }

    private void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
    }
}
