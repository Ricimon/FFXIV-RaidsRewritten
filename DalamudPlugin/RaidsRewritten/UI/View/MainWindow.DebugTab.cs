using System;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.UI.View;

public partial class MainWindow
{
    private void DrawDebugTab()
    {
        using var debugTab = ImRaii.TabItem("Debug");
        if (!debugTab) return;

        var debug = false;
#if DEBUG
        debug = true;
#endif

        if (debug)
        {
            bool punishmentImmunity = configuration.PunishmentImmunity;
            if (ImGui.Checkbox("Punishment Immunity", ref punishmentImmunity))
            {
                configuration.PunishmentImmunity = punishmentImmunity;
                configuration.Save();
            }
        }

        if (ImGui.Button("Clear All Attacks"))
        {
            this.World.DeleteWith<Attack>();
        }
        if (debug)
        {
            ImGui.SameLine();
            if (ImGui.Button("Clear All Statuses"))
            {
                this.World.DeleteWith<Condition.Component>();
            }
            ImGui.SameLine();
            if (ImGui.Button("Clear All Models"))
            {
                this.World.DeleteWith<Model>();
            }
        }

        if (ImGui.Button("Print Player Data"))
        {
            var player = this.dalamud.ObjectTable.LocalPlayer;
            if (player != null)
            {
                this.logger.Info($"Player position:{player.Position}, address:0x{player.Address:X}, entityId:0x{player.EntityId:X}, gameObjectId:0x{player.GameObjectId:X}");
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Print Target Data"))
        {
            var player = this.dalamud.ObjectTable.LocalPlayer;
            if (player != null && player.TargetObject != null)
            {
                var target = player.TargetObject;
                this.logger.Info($"Target position:{target.Position}, address:0x{target.Address:X}, entityId:0x{target.EntityId:X}, gameObjectId:0x{target.GameObjectId:X}, baseId:0x{target.BaseId:X}");
            }
        }

        if (ImGui.Button("Print Weather/Time Data"))
        {
            unsafe
            {
                var weatherManager = WeatherManager.Instance();
                var framework = Framework.Instance();
                if (weatherManager != null && framework != null)
                {
                    var weather = weatherManager->GetCurrentWeather();
                    var et = framework->ClientTime.GetEorzeaTimeOfDay();
                    this.logger.Info($"Weather: {weather}, Eorzea Time: {et}");
                }
            }
        }

        if (ImGui.CollapsingHeader("Fake Statuses"))
        {
            if (ImGui.Button("Bind"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Bind.ApplyToTarget(e, 3.0f);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Knockback"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    var angle = random.NextSingle() * 2 * MathF.PI;
                    var direction = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                    Knockback.ApplyToTarget(e, direction, 2.0f, true);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Stun"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Stun.ApplyToTarget(e, 3.0f);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Paralysis"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Paralysis.ApplyToTarget(e, 5.0f, 3.0f, 1.0f);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Heavy"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Heavy.ApplyToTarget(e, 5.0f);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Pacify"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Pacify.ApplyToTarget(e, 5.0f);
                });
            }

            if (ImGui.Button("Sleep"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Sleep.ApplyToTarget(e, 3.0f);
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Hysteria"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Hysteria.ApplyToTarget(e, 8.0f, 3.0f);
                });
            }

            ImGui.SameLine();

            if (ImGui.Button("Heavy (e)"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Heavy.ApplyToTarget(e, 5.0f, true);
                });
            }
        }

        if (ImGui.CollapsingHeader("Test Omens"))
        {
            if (ImGui.Button("Circle Omen"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<CircleOmen>(out var circle))
                    {
                        circle.Set(new Position(player.Position));
                        circle.Set(new Rotation(player.Rotation));
                        circle.Set(new Scale(Vector3.One));
                    }
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Fan Omen"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<Fan90Omen>(out var fan))
                    {
                        fan.Set(new Position(player.Position));
                        fan.Set(new Rotation(player.Rotation));
                        fan.Set(new Scale(Vector3.One));
                    }
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Rect Omen"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<RectangleOmen>(out var rect))
                    {
                        rect.Set(new Position(player.Position));
                        rect.Set(new Rotation(player.Rotation));
                        rect.Set(new Scale(Vector3.One));
                    }
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Star Omen"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<ShortStarOmen>(out var star))
                    {
                        star.Set(new Position(player.Position));
                        star.Set(new Rotation(player.Rotation));
                        star.Set(new Scale(ShortStarOmen.ScaleMultiplier * Vector3.One));
                    }
                }
            }

            if (ImGui.Button("One Third Donut Omen"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<OneThirdDonutOmen>(out var donut))
                    {
                        donut.Set(new Position(player.Position));
                        donut.Set(new Rotation(player.Rotation));
                        donut.Set(new Scale(Vector3.One));
                    }
                }
            }
        }

        if (ImGui.CollapsingHeader("Test Attacks (Local)"))
        {
            if (ImGui.Button("Spawn Twister"))
            {
                var player = this.dalamud.ObjectTable.LocalPlayer;
                if (player != null)
                {
                    if (this.entityManager.TryCreateEntity<Twister>(out var twister))
                    {
                        twister.Set(new Position(player.Position));
                        twister.Set(new Rotation(player.Rotation));
                    }
                }
            }

            if (debug)
            {
                if (ImGui.Button("Spawn Ball"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<RollingBall>(out var ball))
                        {
                            ball.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation))
                                .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(player.Rotation)))
                                .Set(new RollingBall.CircleArena(player.Position.ToVector2(), 10.0f));
                            //.Set(new RollingBall.ShowOmen());
                        }
                    }
                }
                if (ImGui.Button("LightningCorridor"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<LightningCorridor>(out var attack))
                        {
                            attack.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }

                if (ImGui.Button("Exaflare"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Exaflare>(out var exaflare))
                        {
                            exaflare.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Row of Exaflares"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<ExaflareRow>(out var exaflare))
                        {
                            exaflare.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }

                if (ImGui.Button("Jumpwave"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<JumpableShockwave>(out var jumpwave))
                        {
                            jumpwave.Set(new Position(player.Position + 0.0f * Vector3.UnitX))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }

                if (ImGui.Button("Dreadknight"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Dreadknight>(out var dreadknight))
                        {
                            dreadknight.Set(new Position(player.Position));
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Dreadknight With Tether"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Dreadknight>(out var dreadknight))
                        {
                            dreadknight.Set(new Position(player.Position));
                            Dreadknight.ApplyTarget(dreadknight, player);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                Stun.ApplyToTarget(dreadknight, 1f);
                            }, 4f).ChildOf(dreadknight);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                Bind.ApplyToTarget(dreadknight, 3f);
                            }, 6f).ChildOf(dreadknight);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                Dreadknight.RemoveCancellableCC(dreadknight);
                            }, 7f).ChildOf(dreadknight);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                Sleep.ApplyToTarget(dreadknight, 1f);
                            }, 8f).ChildOf(dreadknight);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                Heavy.ApplyToTarget(dreadknight, 5f);
                                Dreadknight.SetTemporaryRelativeSpeed(dreadknight, .1f);
                            }, 10f).ChildOf(dreadknight);
                            DelayedAction.Create(dreadknight.CsWorld(), () =>
                            {
                                dreadknight.DestructChildEntity<Heavy.Component>();
                            }, 12f).ChildOf(dreadknight);
                        }
                    }
                }

                if (ImGui.Button("ADS"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<ADS>(out var ads))
                        {
                            var originalPosition = player.Position;
                            ads.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                            DelayedAction.Create(ads.CsWorld(), () =>
                            {
                                var player = this.dalamud.ObjectTable.LocalPlayer;
                                if (player != null)
                                {
                                    ADS.CastLineAoe(ads, MathUtilities.GetAbsoluteAngleFromSourceToTarget(originalPosition, player.Position));
                                }
                            }, 3f).ChildOf(ads);
                            DelayedAction.Create(ads.CsWorld(), () =>
                            {
                                var player = this.dalamud.ObjectTable.LocalPlayer;
                                if (player != null)
                                {
                                    ADS.CastLineAoe(ads, MathUtilities.GetAbsoluteAngleFromSourceToTarget(originalPosition, player.Position));
                                }
                            }, 9f).ChildOf(ads);
                            DelayedAction.Create(ads.CsWorld(), ads.Destruct, 15f).ChildOf(ads);
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("ADS Stepped Leader"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<ADS>(out var ads))
                        {
                            var originalPosition = player.Position;
                            ads.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                            DelayedAction.Create(ads.CsWorld(), () =>
                            {
                                var player = this.dalamud.ObjectTable.LocalPlayer;
                                if (player != null)
                                {
                                    ADS.CastSteppedLeader(ads, player.Position);
                                }
                            }, 3f).ChildOf(ads);
                            DelayedAction.Create(ads.CsWorld(), () =>
                            {
                                var player = this.dalamud.ObjectTable.LocalPlayer;
                                if (player != null)
                                {
                                    ADS.CastSteppedLeader(ads, player.Position);
                                }
                            }, 9f).ChildOf(ads);
                            DelayedAction.Create(ads.CsWorld(), ads.Destruct, 15f).ChildOf(ads);
                        }
                    }
                }

                if (ImGui.Button("Close Tether to Target"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        var target = player.TargetObject;
                        if (target != null)
                        {
                            if (this.entityManager.TryCreateEntity<DistanceSnapshotTether>(out var tether))
                            {
                                DistanceSnapshotTether.SetTetherVfx(tether, TetherOmen.TetherVfx.ActivatedClose, player, target)
                                    .Set(new DistanceSnapshotTether.VfxOnFail(["vfx/monster/m0005/eff/m0005sp_15t0t.avfx"]))
                                    .Set(new DistanceSnapshotTether.Tether((e) => { Stun.ApplyToTarget(e, 5); }))
                                    .Set(new DistanceSnapshotTether.FailWhenFurtherThan(10));

                                DelayedAction.Create(tether.CsWorld(), () =>
                                {
                                    tether.Add<DistanceSnapshotTether.Activated>();
                                }, 3f).ChildOf(tether);
                            }
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Far Tether to Target"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        var target = player.TargetObject;
                        if (target != null)
                        {
                            if (this.entityManager.TryCreateEntity<DistanceSnapshotTether>(out var tether))
                            {
                                DistanceSnapshotTether.SetTetherVfx(tether, TetherOmen.TetherVfx.ActivatedFar, player, target)
                                    .Set(new DistanceSnapshotTether.VfxOnFail(["vfx/monster/m0005/eff/m0005sp_15t0t.avfx"]))
                                    .Set(new DistanceSnapshotTether.Tether((e) => { Stun.ApplyToTarget(e, 5); }))
                                    .Set(new DistanceSnapshotTether.FailWhenCloserThan(10));

                                DelayedAction.Create(tether.CsWorld(), () =>
                                {
                                    tether.Add<DistanceSnapshotTether.Activated>();
                                }, 3f).ChildOf(tether);
                            }
                        }
                    }
                }

                if (ImGui.Button("Expanding Puddle"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<ExpandingPuddle>(out var puddle))
                        {
                            puddle.Set(new ExpandingPuddle.Component(
                                "bgcommon/world/common/vfx_for_btl/b0801/eff/b0801_yuka_o.avfx",
                                0.5f,
                                10.0f,
                                1.0f,
                                10.0f));
                            puddle.Set(new Position(player.Position));
                            puddle.Set(new Rotation(player.Rotation));
                            puddle.Set(new Scale(Vector3.One));
                        }
                    }
                }

                if (ImGui.Button("Star"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Star>(out var star))
                        {
                            star.Set(new Star.Component(
                                Type: Star.Type.Long,
                                OmenTime: 3.0f,
                                VfxPath: "vfx/monster/gimmick5/eff/x6r7_b3_g08_c0p.avfx",
                                OnHit: e => { Stun.ApplyToTarget(e, 2.0f); }));
                            star.Set(new Position(player.Position));
                            star.Set(new Rotation(player.Rotation));
                        }
                    }
                }

                if (ImGui.Button("Tornado"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Tornado>(out var tornado))
                        {
                            tornado.Set(new Position(player.Position));
                            DelayedAction.Create(tornado.CsWorld(), () =>
                            {
                                tornado.Destruct();
                            }, 10f).ChildOf(tornado);
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Donut Tornado"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<OctetDonut>(out var tornado))
                        {
                            tornado.Set(new Position(player.Position));
                            DelayedAction.Create(tornado.CsWorld(), () =>
                            {
                                tornado.Destruct();
                            }, 26f).ChildOf(tornado);
                        }
                    }
                }

                if (ImGui.Button("Transition ADS"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<RepellingCannonADS>(out var ads))
                        {
                            ads.Set(new Position(player.Position));
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Transition Melusine"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<CircleBladeMelusine>(out var melusine))
                        {
                            melusine.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Transition Kaliya"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;

                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<NerveGasKaliya>(out var kaliya))
                        {
                            kaliya.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }

                ImGui.Text("Heat Stuff");
                if (ImGui.Button("Add Temperature"))
                {
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        Temperature.SetTemperature(e);
                    });
                }
                ImGui.SameLine();
                if (ImGui.Button("Incr Heat"))
                {
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        Temperature.HeatChangedEvent(e, 50);
                    });
                }
                ImGui.SameLine();
                if (ImGui.Button("Decr Heat"))
                {
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        Temperature.HeatChangedEvent(e, -50);
                    });
                }
                if (ImGui.Button("Spawn Liquid Heaven"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<LiquidHeaven>(out var LiquidHeaven))
                        {
                            LiquidHeaven.Set(new Position(player.Position))
                                        .Set(new Rotation(player.Rotation));
                        }
                    }
                }
            }
        }

        if (ImGui.CollapsingHeader("Test Attacks (Networked)"))
        {
            this.networkClientUi.DrawConfig();
            using (ImRaii.Disabled(!this.networkClient.IsConnected))
            {
                if (ImGui.Button("Spread"))
                {
                    this.networkClient.SendAsync(new Message
                    {
                        action = Message.Action.StartMechanic,
                        startMechanic = new Message.StartMechanicPayload
                        {
                            requestId = Guid.NewGuid().ToString(),
                            mechanicId = 1,
                        }
                    }).SafeFireAndForget();
                }
                ImGui.SameLine();
                if (ImGui.Button("Enum"))
                {
                    this.networkClient.SendAsync(new Message
                    {
                        action = Message.Action.StartMechanic,
                        startMechanic = new Message.StartMechanicPayload
                        {
                            requestId = Guid.NewGuid().ToString(),
                            mechanicId = 10,
                        }
                    }).SafeFireAndForget();
                }
            }
        }

        if (debug)
        {
            if (ImGui.CollapsingHeader("Models"))
            {
                if (ImGui.Button("Chefbingus"))
                {
                    var player = this.dalamud.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        if (this.entityManager.TryCreateEntity<Chefbingus>(out var carby))
                        {
                            carby.Set(new Position(player.Position))
                                .Set(new Rotation(player.Rotation));
                        }
                    }
                }
            }
        }
    }
}
