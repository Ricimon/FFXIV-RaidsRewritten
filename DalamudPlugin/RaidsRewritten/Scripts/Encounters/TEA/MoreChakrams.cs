using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class MoreChakrams : Mechanic
{
    public int RngSeed { get; set; }

    private const uint PhotonActionId = 18486;
    private const float ChakramCastDelay = 0.75f;
    private const float ChakramCastDuration = 5.0f;
    private const float OmenDuration = 1.0f;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private int photonsCasted = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        photonsCasted = 0;
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

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    {
        if (action.RowId == PhotonActionId)
        {
            photonsCasted++;
            if (photonsCasted == 2)
            {
                SpawnChakrams();
            }
        }
    }

    public override void DebugSimulate()
    {
        SpawnChakrams();
    }

    private void SpawnChakrams()
    {
        var seed = RngSeed;
        unchecked
        {
            seed += 0xC84C54E;
        }
        var random = new Random(seed);
        var rotationOffset = random.Next(8) * 0.25f * MathF.PI;

        var width = 5.0f;
        var length = 43.0f;
        for (var i = 0; i < 3; i++)
        {
            var position = 0.5f * length * MathUtilities.RotationToUnitVector(rotationOffset).ToVector3(arenaMiddle.Y);
            position += arenaMiddle;

            var r = rotationOffset + 0.5f * MathF.PI;
            var sign = i % 2 == 0 ? 1 : -1;
            var offset = sign * (i / 2 + 0.5f) * 2.0f * width * MathUtilities.RotationToUnitVector(r).ToVector3(arenaMiddle.Y);
            position += offset;

            if (EntityManager.TryCreateEntity<SteamChakram>(out var chakram))
            {
                chakram
                    .Set(new Position(position))
                    .Set(new Rotation(rotationOffset + MathF.PI));
                attacks.Add(chakram);

                // Cast VFX
                var action1 = DelayedAction.Create(World, () =>
                {
                    var castVfx = World.Entity()
                        .Set(new ActorVfx("vfx/common/eff/cmml_castx1f.avfx"))
                        .ChildOf(chakram);

                    // Delete Cast VFX
                    var action = DelayedAction.Create(World, () =>
                    {
                        castVfx.SafeDestruct();
                    }, ChakramCastDuration);
                    attacks.Add(action);
                }, ChakramCastDelay);
                attacks.Add(action1);

                // Omen
                var action2 = DelayedAction.Create(World, () =>
                {
                    if (EntityManager.TryCreateEntity<RectangleOmen>(out var rect))
                    {
                        rect.Set(new Position(position));
                        rect.Set(new Scale(new Vector3(width, 1, length)));
                        rect.Set(new Rotation(rotationOffset + MathF.PI));
                        rect.Set(new OmenDuration(OmenDuration, false));
                        attacks.Add(rect);

                        // Snapshot
                        var action = DelayedAction.Create(World, () =>
                        {
                            var player = Dalamud.ObjectTable.LocalPlayer;
                            if (player == null || player.IsDead) { return; }

                            // Effect
                            if (RectangleOmen.IsInOmen(rect, player.Position))
                            {
                                Action action;
                                if (player.HasTranscendance())
                                {
                                    action = () => VfxSpawn.PlayInvulnerabilityEffect(player);
                                }
                                else
                                {
                                    action = () => CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                    {
                                        Pacify.ApplyToTarget(e, 60.0f);
                                        Knockback.ApplyToTarget(e, MathUtilities.RotationToUnitVector(rotationOffset + MathF.PI).ToVector3(position.Y), 5.0f, false);
                                    });
                                }
                                var delayedAction = DelayedAction.Create(World, action, 0.5f);
                                attacks.Add(delayedAction);
                            }
                        }, OmenDuration);
                        attacks.Add(action);
                    }
                }, ChakramCastDelay + ChakramCastDuration - OmenDuration);
                attacks.Add(action2);

                // Attack
                var action3 = DelayedAction.Create(World, () =>
                {
                    if (chakram.IsValid())
                    {
                        chakram.Set(new SteamChakram.AttackAnimation());
                    }
                }, ChakramCastDelay + ChakramCastDuration);
                attacks.Add(action3);

                // Cleanup
                var action4 = DelayedAction.Create(World, () =>
                {
                    chakram.SafeDestruct();
                }, ChakramCastDelay + ChakramCastDuration + 2.0f);
                attacks.Add(action4);
            }
        }
    }
}
