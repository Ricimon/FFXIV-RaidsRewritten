use super::{Affect, Mechanic, Target};
use crate::{
    ecs_container::{Party, Player, Position, Socket, SocketIoSingleton, State},
    game::{condition::Condition, utils::*},
    webserver::message::{ApplyConditionPayload, PlayActorVfxOnTargetPayload},
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use tracing::info;

// This spread is placed on every player (no doubling-up) and goes off on dead bodies.

#[derive(Component, Debug)]
pub struct Spread {
    time_remaining: f32,
    effect_delay: f32,
    radius: f32,
    omen_vfx_path: String,
    attack_vfx_path: String,
    targets_assigned: bool,
    started: bool,
    snapshotted: bool,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(Spread {
        time_remaining: 5.2,
        effect_delay: 0.2,
        radius: 6.0,
        omen_vfx_path: "vfx/lockon/eff/target_ae_s5f.avfx".to_string(),
        attack_vfx_path: "vfx/monster/gimmick4/eff/n5r8_b_g15_t0k1.avfx".to_string(),
        targets_assigned: false,
        started: false,
        snapshotted: false,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut Spread, &Party)>()
        .each_iter(|it, index, (mechanic, spread, party)| {
            let entity = it.entity(index);

            if !spread.targets_assigned {
                spread.targets_assigned = true;

                // Assign targets
                it.world()
                    .query::<(&Player, &Party)>()
                    .build()
                    .each_entity(|e, (_pl, pa)| {
                        if party.id == pa.id {
                            entity.add((Target, e));
                        }
                    });
                return;
            }

            if !spread.started {
                spread.started = true;

                // Send omen vfx
                it.world().get::<&SocketIoSingleton>(|sio| {
                    let targets = get_target_ids(&entity);
                    entity.each_target(Target, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_play_actor_vfx_on_target(
                                sio.io.clone(),
                                s.id,
                                PlayActorVfxOnTargetPayload {
                                    vfx_path: spread.omen_vfx_path.clone(),
                                    content_id_targets: targets.clone(),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                });
            }

            spread.time_remaining -= it.delta_time();

            if !spread.snapshotted && spread.time_remaining <= spread.effect_delay {
                spread.snapshotted = true;

                // Snapshot
                entity.each_target(Target, |e1| {
                    let mut affected = false;
                    // For every target player, check their position against every other target player for overlap
                    e1.try_get::<(&Player, &Position, &State)>(|(pl1, p1, s1)| {
                        if !s1.is_alive {
                            return;
                        }

                        entity.each_target(Target, |e2| {
                            e2.try_get::<(&Player, &Position)>(|(pl2, p2)| {
                                if !affected && pl1.content_id != pl2.content_id {
                                    let p1 = [p1.x, p1.z];
                                    let p2 = [p2.x, p2.z];
                                    let distance_sq: f32 = euclidean_sq(&p1, &p2);

                                    if distance_sq <= f32::powi(spread.radius, 2) {
                                        entity.add((Affect, e1));
                                        affected = true;
                                    }
                                }
                            });
                        });
                    });
                });

                // Send attack vfx
                it.world().get::<&SocketIoSingleton>(|sio| {
                    let targets = get_target_ids(&entity);
                    entity.each_target(Target, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_play_actor_vfx_on_target(
                                sio.io.clone(),
                                s.id,
                                PlayActorVfxOnTargetPayload {
                                    vfx_path: spread.attack_vfx_path.clone(),
                                    content_id_targets: targets.clone(),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                });
            }
            // Snapshot must run before the mechanic can be completed
            else if spread.time_remaining <= 0.0 {
                it.world().get::<&SocketIoSingleton>(|sio| {
                    // Send effects
                    entity.each_target(Affect, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_apply_condition(
                                sio.io.clone(),
                                s.id,
                                ApplyConditionPayload {
                                    condition: Condition::Stun,
                                    duration: 5.0,
                                    ..Default::default()
                                },
                            );
                        });
                    });
                });

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Completing Mechanic"
                );
                entity.destruct();
            }
        });
}
