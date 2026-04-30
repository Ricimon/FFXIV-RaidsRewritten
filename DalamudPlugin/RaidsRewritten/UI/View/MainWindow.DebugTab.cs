using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons.Hooks;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Flecs.NET.Core;
using RaidsRewritten.Game;
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
    private int debugModelCharaId = 292;
    private Entity debugSpawnedModel = default;

    private void DebugSpawnModel()
    {
        var player = this.dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }
        if (debugSpawnedModel.IsValid()) debugSpawnedModel.Destruct();
        debugSpawnedModel = World.Entity()
            .Set(new Model(debugModelCharaId))
            .Set(new Position(player.Position))
            .Set(new Rotation(player.Rotation))
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new TimelineBase(0))
            .Add<Attack>();
    }

    private static void SameLineIfFits(string nextLabel)
    {
        var style = ImGui.GetStyle();
        var displayLabel = nextLabel.Contains("##") ? nextLabel[..nextLabel.IndexOf("##")] : nextLabel;
        var nextWidth =  ImGui.CalcTextSize(displayLabel).X + style.FramePadding.X * 2;
        var maxWidth = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        if (ImGui.GetItemRectMax().X + style.ItemSpacing.X + nextWidth < maxWidth)
            ImGui.SameLine();
    }

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
            SameLineIfFits("Clear All Statuses");
            if (ImGui.Button("Clear All Statuses"))
            {
                this.World.DeleteWith<Condition.Component>();
            }
            SameLineIfFits("Clear All Models");
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
        SameLineIfFits("Print Target Data");
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
            SameLineIfFits("Knockback");
            if (ImGui.Button("Knockback"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    var angle = random.NextSingle() * 2 * MathF.PI;
                    var direction = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                    Knockback.ApplyToTarget(e, direction, 2.0f, true);
                });
            }
            SameLineIfFits("Stun");
            if (ImGui.Button("Stun"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Stun.ApplyToTarget(e, 3.0f);
                });
            }
            SameLineIfFits("Paralysis");
            if (ImGui.Button("Paralysis"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Paralysis.ApplyToTarget(e, 5.0f, 3.0f, 1.0f);
                });
            }
            SameLineIfFits("Heavy");
            if (ImGui.Button("Heavy"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Heavy.ApplyToTarget(e, 5.0f);
                });
            }
            SameLineIfFits("Pacify");
            if (ImGui.Button("Pacify"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Pacify.ApplyToTarget(e, 5.0f);
                });
            }
            SameLineIfFits("Blind");
            if (ImGui.Button("Blind"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Blind.ApplyToTarget(e, 5.0f);
                });
            }
            SameLineIfFits("Blind");
            if (ImGui.Button("Sleep"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Sleep.ApplyToTarget(e, 3.0f);
                });
            }
            SameLineIfFits("Hysteria");
            if (ImGui.Button("Hysteria"))
            {
                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                {
                    Hysteria.ApplyToTarget(e, 8.0f, 3.0f);
                });
            }
            SameLineIfFits("Heavy (e)");
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
            SameLineIfFits("Fan Omen");
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
            SameLineIfFits("Rect Omen");
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
            SameLineIfFits("Star Omen");
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
            SameLineIfFits("One Third Donut Omen");
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

        ImGui.Text("Test Attacks");

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
                SameLineIfFits("Spawn Ball");
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
                SameLineIfFits("LightningCorridor");
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
                SameLineIfFits("Row of Exaflares");
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
                SameLineIfFits("Dreadknight With Tether");
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
                SameLineIfFits("ADS Stepped Leader");
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

                SameLineIfFits("Far Tether to Target");

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
                SameLineIfFits("Star");
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

                SameLineIfFits("Donut Tornado");

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

                SameLineIfFits("Transition Melusine");

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

                SameLineIfFits("Transition Kaliya");

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
                SameLineIfFits("Incr Heat");
                if (ImGui.Button("Incr Heat"))
                {
                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        Temperature.HeatChangedEvent(e, 50);
                    });
                }
                SameLineIfFits("Decr Heat");
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
                if (ImGui.Button("Clear Networked Mechanics"))
                {
                    this.networkClient.SendAsync(new Message
                    {
                        action = Message.Action.ClearMechanics,
                    }).SafeFireAndForget();
                }

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
                SameLineIfFits("Enum");
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

                if (ImGui.Button("Place Trap"))
                {
                    var placeEntity = World.Entity()
                        .Set(new PlaceMechanicWithMouse(3));
                    World.Entity()
                        .Set(new Message.StartMechanicPayload
                        {
                            requestId = Guid.NewGuid().ToString(),
                            mechanicId = 20,
                        })
                        .ChildOf(placeEntity);
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

                ImGui.SetNextItemWidth(120);
                ImGui.InputInt("ModelCharaId", ref debugModelCharaId);
                ImGui.SameLine();
                if (ImGui.ArrowButton("##mcharaDec", ImGuiDir.Left)) { debugModelCharaId--; DebugSpawnModel(); }
                ImGui.SameLine();
                if (ImGui.ArrowButton("##mcharaInc", ImGuiDir.Right)) { debugModelCharaId++; DebugSpawnModel(); }
                ImGui.SameLine();
                if (ImGui.Button("Spawn Model")) DebugSpawnModel();
                ImGui.SameLine();
                if (ImGui.Button("Despawn") && debugSpawnedModel.IsValid())
                {
                    debugSpawnedModel.Destruct();
                    debugSpawnedModel = default;
                }
            }

            if (ImGui.CollapsingHeader("Encounter Override"))
            {
                if (ImGui.Button("Clear Override"))
                {
                    encounterManager.ForceActivateEncounter(null);
                }

                foreach (var enc in encounterManager.Encounters)
                {
                    SameLineIfFits(enc.Name);
                    if (ImGui.Button(enc.Name))
                    {
                        encounterManager.ForceActivateEncounter(enc);
                    }
                }

                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Active: " + (encounterManager.ActiveEncounter?.Name ?? "None"));
            }

            if (ImGui.CollapsingHeader("Mechanic Triggers"))
            {
                if (encounterManager.ActiveEncounter == null)
                {
                    ImGui.Text("No active encounter");
                }
                else
                {
                    ImGui.Text("Global Events:");
                    if (ImGui.Button("Combat Start"))
                    {
                        foreach (var mechanic in encounterManager.ActiveEncounter.GetMechanics())
                        {
                            mechanic.OnCombatStart();
                        }
                    }
                    SameLineIfFits("Combat End");
                    if (ImGui.Button("Combat End"))
                    {
                        foreach (var mechanic in encounterManager.ActiveEncounter.GetMechanics())
                        {
                            mechanic.OnCombatEnd();
                        }
                    }
                    SameLineIfFits("Director: Commence");
                    if (ImGui.Button("Director: Commence"))
                    {
                        encounterManager.ActiveEncounter.IncrementRngSeed();
                        foreach (var mechanic in encounterManager.ActiveEncounter.GetMechanics())
                        {
                            mechanic.OnDirectorUpdate(DirectorUpdateCategory.Commence);
                        }
                    }
                    SameLineIfFits("Director: Wipe");
                    if (ImGui.Button("Director: Wipe"))
                    {
                        foreach (var mechanic in encounterManager.ActiveEncounter.GetMechanics())
                        {
                            mechanic.OnDirectorUpdate(DirectorUpdateCategory.Wipe);
                        }
                    }

                    ImGui.Separator();
                    ImGui.Text("Individual Mechanics:");
                    foreach (var mechanic in encounterManager.ActiveEncounter.GetMechanics())
                    {
                        var name = mechanic.GetType().Name;
                        if (ImGui.TreeNode(name))
                        {
                            if (ImGui.Button($"Simulate##{name}"))
                            {
                                mechanic.DebugSimulate();
                            }
                            SameLineIfFits($"OnCombatStart##{name}");
                            if (ImGui.Button($"OnCombatStart##{name}"))
                            {
                                mechanic.OnCombatStart();
                            }
                            SameLineIfFits($"OnCombatEnd##{name}");
                            if (ImGui.Button($"OnCombatEnd##{name}"))
                            {
                                mechanic.OnCombatEnd();
                            }
                            SameLineIfFits($"Reset##{name}");
                            if (ImGui.Button($"Reset##{name}"))
                            {
                                mechanic.Reset();
                            }
                            ImGui.TreePop();
                        }
                    }
                }
            }
        }
    }
}
