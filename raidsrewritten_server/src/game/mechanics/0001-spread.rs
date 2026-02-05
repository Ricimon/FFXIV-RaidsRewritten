use crate::{
    game::{components::*, condition::Condition, utils::*},
    webserver::message::{ApplyConditionPayload, PlayActorVfxOnTargetPayload},
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use std::collections::HashMap;
use tracing::info;

// This spread is placed on every player (no doubling-up) and does not go off on dead bodies.

#[derive(Component, Debug)]
pub struct Spread {
    time_to_snapshot: f32,
    effect_delay: f32,
    radius: f32,
    omen_vfx_path: String,
    attack_vfx_path: String,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(Spread {
        time_to_snapshot: 5.0,
        effect_delay: 0.2,
        radius: 6.0,
        omen_vfx_path: "vfx/lockon/eff/target_ae_s5f.avfx".to_string(),
        attack_vfx_path: "vfx/monster/gimmick4/eff/n5r8_b_g15_t0k1.avfx".to_string(),
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut Spread, &Party)>()
        .each_iter(|it, index, (mechanic, spread, party)| {
            let entity = it.entity(index);

            if !entity.has(Targets::id()) {
                // Assign targets
                let mut target_players: Vec<Entity> = Vec::new();
                let mut targets: Vec<u64> = Vec::new();

                if let Some(pc) = find_party_container(&it.world(), &party.id) {
                    pc.each_child(|c| {
                        c.try_get::<&Player>(|p| {
                            target_players.push(*c);
                            targets.push(p.content_id);
                        });
                    });
                }

                entity.set(Targets {
                    player_entities: target_players,
                });

                // Send omen vfx
                if let Some(pc) = find_party_container(&it.world(), &party.id) {
                    let io = get_socket_io(&it.world());
                    pc.each_child(|c| {
                        c.try_get::<&Socket>(|s| {
                            send_play_actor_vfx_on_target(
                                io.clone(),
                                s.id,
                                PlayActorVfxOnTargetPayload {
                                    vfx_path: spread.omen_vfx_path.clone(),
                                    content_id_targets: targets.clone(),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                }
                return;
            }

            spread.time_to_snapshot -= it.delta_time();

            if spread.time_to_snapshot > 0.0 {
                return;
            }

            if !entity.has(Affects::id()) {
                let mut affects: HashMap<Entity, u8> = HashMap::new();

                // Snapshot
                entity.try_get::<&mut Targets>(|t| {
                    // Prune valid targets
                    t.player_entities.retain(|e| {
                        let mut valid_target = false;
                        if let Some(ev) = get_entity_view(e, &it.world()) {
                            ev.try_get::<&State>(|s| valid_target = s.is_alive);
                        }
                        valid_target
                    });

                    if let Some(pc) = find_party_container(&it.world(), &party.id) {
                        for e in &t.player_entities {
                            // For every target player,
                            let e1 = e.entity_view(it.world());
                            e1.try_get::<(&Player, &Position)>(|(_, p1)| {
                                // affect all players within radius
                                pc.each_child(|c| {
                                    c.try_get::<(&Player, &Position, &State)>(|(_, p2, s2)| {
                                        if !s2.is_alive {
                                            return;
                                        }

                                        let p1 = [p1.x, p1.z];
                                        let p2 = [p2.x, p2.z];
                                        let distance_sq: f32 = euclidean_sq(&p1, &p2);

                                        if distance_sq <= f32::powi(spread.radius, 2) {
                                            add_affect(&mut affects, &c, 1);
                                        }
                                    });
                                });
                            });
                        }
                    }
                });

                entity.set(Affects {
                    player_entities: affects,
                });

                // Send attack vfx
                let targets = get_target_ids(&entity);
                let io = get_socket_io(&it.world());
                if let Some(pc) = find_party_container(&it.world(), &party.id) {
                    pc.each_child(|c| {
                        c.try_get::<&Socket>(|s| {
                            send_play_actor_vfx_on_target(
                                io.clone(),
                                s.id,
                                PlayActorVfxOnTargetPayload {
                                    vfx_path: spread.attack_vfx_path.clone(),
                                    content_id_targets: targets.clone(),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                }
            }

            spread.effect_delay -= it.delta_time();

            if spread.effect_delay > 0.0 {
                return;
            }

            // Send conditions
            entity.try_get::<&Affects>(|a| {
                let io = get_socket_io(&it.world());
                for (e, affect_count) in &a.player_entities {
                    let condition_duration = (affect_count - 1) as f32 * 5.0;
                    if condition_duration > 0.0
                        && let Some(ev) = get_entity_view(e, &it.world())
                    {
                        ev.try_get::<&Socket>(|s| {
                            send_apply_condition(
                                io.clone(),
                                s.id,
                                ApplyConditionPayload {
                                    condition: Condition::Stun,
                                    duration: condition_duration,
                                    ..Default::default()
                                },
                            );
                        });
                    }
                }
            });

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.destruct();
        });
}
