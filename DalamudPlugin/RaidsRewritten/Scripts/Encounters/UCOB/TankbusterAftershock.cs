using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class TankbusterAftershock : Mechanic
{
    private struct AftershockData
    {
        public float TelegraphScale;
        public int TelegraphDegrees;
        public float OmenVisibleSeconds;
        public string VfxPath;

        public float DelaySeconds;
        public float VfxDelaySeconds;
        public float StatusDelaySeconds;
    }

    private readonly Dictionary<uint, AftershockData> AftershockDict = new Dictionary<uint, AftershockData>
    {
        // flare breath
        {
            9940, new AftershockData
            {
                TelegraphDegrees = 90,
                TelegraphScale = 30f,
                OmenVisibleSeconds = 0.65f,
                VfxPath = "vfx/monster/m0117/eff/baha_earth_90c0s.avfx",
                DelaySeconds = 0.65f,
                VfxDelaySeconds = 0.15f,
                StatusDelaySeconds = 0.25f
            }
        },
        // plummet
        {
            9896, new AftershockData
            {
                TelegraphDegrees = 120,
                TelegraphScale = 10f,
                OmenVisibleSeconds = 0.5f,
                VfxPath = "vfx/monster/m0389/eff/m389sp_05c0n.avfx",
                DelaySeconds = 0.8f,
                VfxDelaySeconds = 0.05f,
                StatusDelaySeconds = 0.15f
            }
        }
    };

    private const float OmenSnapshotDelay = 0.25f;
    private const float TurnDelaySeconds = 0.5f;
    private const float HeavyDurationSeconds = 20f;
    private const float PacifyDurationSeconds = 20f;
    private readonly List<List<Entity>> attacks = [];

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            foreach (var ToDestruct in attacks)
            {
                Reset(ToDestruct);
            }
            attacks.Clear();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (!AftershockDict.TryGetValue(set.Action.Value.RowId, out var Aftershock)) { return; }


        List<Entity> ToDestruct = [];
        void execAttack() => ExecuteAttack(Aftershock, set.Source, ToDestruct);
        var delayedAction = DelayedAction.Create(this.World, execAttack, TurnDelaySeconds);
        ToDestruct.Add(delayedAction);
        attacks.Add(ToDestruct);
    }

    private void ExecuteAttack(AftershockData aftershockData, IGameObject source, List<Entity> ToDestruct)
    {
        var originalPosition = source.Position;
        var originalRotation = source.Rotation;
        var backAngle = MathUtilities.ClampRadians(originalRotation + MathF.PI);

        // create fake actors to keep vfxes in place if boss turns/moves
        var fakeActorFront = FakeActor.Create(this.World)
            .Set(new Position(originalPosition))
            .Set(new Rotation(originalRotation));
        ToDestruct.Add(fakeActorFront);

        var fakeActorBack = FakeActor.Create(this.World)
            .Set(new Position(originalPosition))
            .Set(new Rotation(backAngle));
        ToDestruct.Add(fakeActorBack);


        // show a quick telegraph
        var delayedAction = DelayedAction.Create(this.World, () => Telegraph(aftershockData, originalPosition, originalRotation, ToDestruct), aftershockData.DelaySeconds);
        ToDestruct.Add(delayedAction);
        delayedAction = DelayedAction.Create(this.World, () => Telegraph(aftershockData, originalPosition, backAngle, ToDestruct), aftershockData.DelaySeconds);
        ToDestruct.Add(delayedAction);

        // check if player is in telegraph
        delayedAction = DelayedAction.Create(this.World, () => 
            Aftershock(aftershockData, fakeActorFront, originalPosition, originalRotation, ToDestruct),
            aftershockData.DelaySeconds + aftershockData.OmenVisibleSeconds + OmenSnapshotDelay
        );
        ToDestruct.Add(delayedAction);

        delayedAction = DelayedAction.Create(this.World, () =>
            Aftershock(aftershockData, fakeActorBack, originalPosition, backAngle, ToDestruct),
            aftershockData.DelaySeconds + aftershockData.OmenVisibleSeconds + OmenSnapshotDelay
        );
        ToDestruct.Add(delayedAction);

        // cleanup
        delayedAction = DelayedAction.Create(this.World, () => Reset(ToDestruct), 5);
        ToDestruct.Add(delayedAction);
    }

    private void Reset(List<Entity> ToDestruct)
    {
        foreach (var e in ToDestruct)
        {
            e.Destruct();
        }
        ToDestruct.Clear();
        attacks.Remove(ToDestruct);
    }

    private void Telegraph(AftershockData aftershockData, Vector3 position, float rotation, List<Entity> ToDestruct)
    {
        Entity FanOmen;
        bool ret = false;
        if (aftershockData.TelegraphDegrees == 90)
        {
            ret = SpawnOmen<Fan90Omen>(out FanOmen, aftershockData, position, rotation, ToDestruct);
        } else
        {
            ret = SpawnOmen<Fan120Omen>(out FanOmen, aftershockData, position, rotation, ToDestruct);
        }

        if (!ret) { return; }

        void DestroyTelegraph()
        {
            ToDestruct.Remove(FanOmen);
            FanOmen.Destruct();
        }

        var delayedAction = DelayedAction.Create(this.World, DestroyTelegraph, aftershockData.OmenVisibleSeconds);
    }

    private bool SpawnOmen<T>(out Entity FanOmen, AftershockData aftershockData, Vector3 position, float rotation, List<Entity> ToDestruct)
    {
        if (this.EntityManager.TryCreateEntity<T>(out Entity tempFanOmen))
        {
            tempFanOmen.Set(new Position(position))
                   .Set(new Rotation(rotation))
                   .Set(new Scale(new Vector3(aftershockData.TelegraphScale)));

            ToDestruct.Add(tempFanOmen);
        } else
        {
            Reset(ToDestruct);
            FanOmen = default;
            return false;
        }
        FanOmen = tempFanOmen;
        return true;
    }

    void Aftershock(AftershockData aftershockData, Entity fakeActor, Vector3 position, float rotation, List<Entity> ToDestruct)
    {
        if (this.EntityManager.TryCreateEntity<Fan>(out var AftershockAoE))
        {
            void OnHit(Entity e)
            {
                var player = this.Dalamud.ClientState.LocalPlayer;
                if (player == null || player.IsDead) { return; }
                if (player.HasTranscendance())
                {
                    DelayedAction.Create(e.CsWorld(), () =>
                    {
                        this.VfxSpawn.PlayInvulnerabilityEffect(player);
                    }, aftershockData.StatusDelaySeconds);
                }
                else
                {
                    DelayedAction.Create(e.CsWorld(), () =>
                    {
                        Heavy.ApplyToTarget(e, HeavyDurationSeconds, true);
                        Pacify.ApplyToTarget(e, PacifyDurationSeconds, true);
                    }, aftershockData.StatusDelaySeconds);
                }
            }

            AftershockAoE.Set(new Position(position))
                              .Set(new Rotation(rotation))
                              .Set(new Scale(new Vector3(aftershockData.TelegraphScale)))
                              .Set(new Fan.Component(OnHit, aftershockData.TelegraphDegrees));

            ToDestruct.Add(AftershockAoE);

            var delayedAction = DelayedAction.Create(this.World, () => fakeActor.Set(new ActorVfx(aftershockData.VfxPath)), aftershockData.VfxDelaySeconds);
            ToDestruct.Add(delayedAction);
        }
    }
}
