use super::{Affect, Mechanic, Target};
use crate::{
    ecs_container::{Party, Player, Position, Socket, State},
    game::{condition::Condition, utils::*},
    webserver::message::{ApplyConditionPayload, PlayVfxPayload},
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use rand::seq::IndexedRandom;
use tracing::info;

// This enumeration is placed on one random player and does not go off on dead bodies.
// 2+ players successfully resolve this enumeration.

#[derive(Component, Debug)]
pub struct Enumeration {
    time_remaining: f32,
    effect_delay: f32,
    radius: f32,
    omen_vfx_path: String,
    attack_vfx_paths: [String; 2],
    targets_assigned: bool,
    omen_vfx_sent: bool,
    snapshotted: bool,
    attack_vfx_sent: bool,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(Enumeration {
        time_remaining: 6.2,
        effect_delay: 0.2,
        radius: 3.0,
        omen_vfx_path: "vfx/lockon/eff/2tagup_3m_6s_x.avfx".to_string(),
        attack_vfx_paths: [
            "vfx/monster/gimmick4/eff/z5fb_b_g10c0x.avfx".to_string(),
            "vfx/monster/gimmick4/eff/z5fb_b_g10c1x.avfx".to_string(),
        ],
        targets_assigned: false,
        omen_vfx_sent: false,
        snapshotted: false,
        attack_vfx_sent: false,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut Enumeration, &Party)>()
        .each_iter(|it, index, (mechanic, enumeration, party)| {
            let entity = it.entity(index);

            if !enumeration.targets_assigned {
                enumeration.targets_assigned = true;

                // Assign targets
                let player_query = it.world().query::<(&Player, &Party)>().build();
                let mut players: Vec<u64> = Vec::new();
                player_query.each(|(pl, pa)| {
                    if party.id == pa.id {
                        players.push(pl.content_id);
                    }
                });
                // Pick one random target
                if let Some(target) = players.choose(&mut rand::rng()) {
                    player_query.each_entity(|e, (pl, pa)| {
                        if party.id == pa.id && *target == pl.content_id {
                            entity.add((Target, e));
                        }
                    });
                }
                return;
            }

            if !enumeration.omen_vfx_sent {
                enumeration.omen_vfx_sent = true;

                // Send omen vfx
                let io = get_socket_io(&it.world());
                let targets = get_target_ids(&entity);
                it.world()
                    .query::<(&Socket, &Party)>()
                    .build()
                    .each(|(s, pa)| {
                        if party.id == pa.id {
                            send_play_vfx(
                                io.clone(),
                                s.id,
                                PlayVfxPayload {
                                    vfx_path: enumeration.omen_vfx_path.clone(),
                                    targets: targets.clone(),
                                },
                            );
                        }
                    });
            }

            enumeration.time_remaining -= it.delta_time();

            if enumeration.time_remaining > enumeration.effect_delay {
                return;
            }

            if !enumeration.snapshotted {
                enumeration.snapshotted = true;

                // Snapshot
                entity.each_target(Target, |e1| {
                    let mut affected = true;
                    // For every target player, check their position against every other target player for overlap
                    e1.try_get::<(&Player, &Position, &State)>(|(pl1, p1, s1)| {
                        // Attacks do not go off on dead bodies
                        if !s1.is_alive {
                            entity.remove((Target, e1));
                            return;
                        }

                        entity.each_target(Target, |e2| {
                            e2.try_get::<(&Player, &Position, &State)>(|(pl2, p2, s2)| {
                                if !s2.is_alive {
                                    return;
                                }
                                if affected && pl1.content_id != pl2.content_id {
                                    let p1 = [p1.x, p1.z];
                                    let p2 = [p2.x, p2.z];
                                    let distance_sq: f32 = euclidean_sq(&p1, &p2);

                                    if distance_sq <= f32::powi(enumeration.radius, 2) {
                                        affected = false;
                                    }
                                }
                            });
                        });

                        if affected {
                            entity.add((Affect, e1));
                        }
                    });
                });
                return;
            }

            if !enumeration.attack_vfx_sent {
                enumeration.attack_vfx_sent = true;

                // Send attack vfx
                let io = get_socket_io(&it.world());
                let targets = get_target_ids(&entity);
                it.world()
                    .query::<(&Socket, &Party)>()
                    .build()
                    .each(|(s, pa)| {
                        if party.id == pa.id {
                            for vfx in &enumeration.attack_vfx_paths {
                                send_play_vfx(
                                    io.clone(),
                                    s.id,
                                    PlayVfxPayload {
                                        vfx_path: vfx.clone(),
                                        targets: targets.clone(),
                                    },
                                );
                            }
                        }
                    });
            }

            if enumeration.time_remaining > 0.0 {
                return;
            }

            // Send effects
            let io = get_socket_io(&it.world());
            entity.each_target(Affect, |e| {
                e.try_get::<&Socket>(|s| {
                    send_apply_condition(
                        io.clone(),
                        s.id,
                        ApplyConditionPayload {
                            condition: Condition::Stun,
                            duration: 5.0,
                            ..Default::default()
                        },
                    );
                });
            });

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.destruct();
        });
}
