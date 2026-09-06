use crate::{
    game::{
        components::*,
        condition::{self, apply_condition},
        utils::*,
    },
    webserver::message::PlayActorVfxOnTargetPayload,
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use rand::seq::IndexedRandom;
use std::collections::HashMap;
use tracing::info;

#[derive(Component, Debug)]
pub struct SuperJumpEnumeration {
    time_to_snapshot: f32,
    effect_delay: f32,
    radius: f32,
    omen_vfx_path: String,
    attack_vfx_paths: [String; 2],
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(SuperJumpEnumeration {
        time_to_snapshot: 6.0,
        effect_delay: 0.2,
        radius: 3.0,
        omen_vfx_path: "vfx/lockon/eff/2tagup_3m_6s_x.avfx".to_string(),
        attack_vfx_paths: [
            "vfx/monster/gimmick4/eff/z5fb_b_g10c0x.avfx".to_string(),
            "vfx/monster/gimmick4/eff/z5fb_b_g10c1x.avfx".to_string(),
        ],
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(
            &Mechanic,
            &mut SuperJumpEnumeration,
            &ExtraMechanicData,
            &Party,
        )>()
        .each_iter(|it, index, (mechanic, enumeration, extra_data, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            if !entity.has(Targets::id()) {
                let mut targets: Vec<u64> = Vec::new();
                for t in extra_data.value.split(',') {
                    if let Ok(n) = t.parse::<u64>() {
                        targets.push(n);
                    }
                }

                // Only pick 1 target
                let target = targets.choose(&mut rand::rng());

                if target.is_none() {
                    finish_mechanic(entity, mechanic, party);
                    return;
                }

                let targets = vec![*target.unwrap()];

                // Assign targets
                let mut target_players: Vec<Entity> = Vec::new();

                let player_query = world.query::<(&Player, &State)>().build();
                player_query.each_entity(|entity, (player, state)| {
                    if !target_players.is_empty() {
                        return;
                    }
                    if player.content_id == *target.unwrap() && state.is_alive {
                        target_players.push(*entity);
                    }
                });

                if target_players.is_empty() {
                    finish_mechanic(entity, mechanic, party);
                    return;
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
                                    vfx_path: enumeration.omen_vfx_path.clone(),
                                    content_id_targets: targets.clone(),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                }
                return;
            }

            enumeration.time_to_snapshot -= it.delta_time();

            if enumeration.time_to_snapshot > 0.0 {
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
                            e1.try_get::<(&Player, &Position)>(|(pl1, p1)| {
                                // affect all players within radius
                                let mut enumeration_success = false;
                                pc.each_child(|c| {
                                    c.try_get::<(&Player, &Position, &State)>(|(pl2, p2, s2)| {
                                        if !s2.is_alive {
                                            return;
                                        }

                                        let p1 = Vector2::new(p1.x, p1.z);
                                        let p2 = Vector2::new(p2.x, p2.z);

                                        if p1.metric_distance(&p2) <= enumeration.radius {
                                            if pl1.content_id != pl2.content_id {
                                                enumeration_success = true;
                                            }
                                            // TODO: Check for vuln
                                            add_affect(&mut affects, &c, 1);
                                        }
                                    });
                                });

                                if !enumeration_success {
                                    add_affect(&mut affects, &e1, 1);
                                }
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
                            for vfx in &enumeration.attack_vfx_paths {
                                send_play_actor_vfx_on_target(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnTargetPayload {
                                        vfx_path: vfx.clone(),
                                        content_id_targets: targets.clone(),
                                        ..Default::default()
                                    },
                                );
                            }
                        });
                    });
                }
            }

            enumeration.effect_delay -= it.delta_time();

            if enumeration.effect_delay > 0.0 {
                return;
            }

            // Apply conditions
            entity.try_get::<&Affects>(|a| {
                for (e, affect_count) in &a.player_entities {
                    if *affect_count > 1
                        && let Some(player) = get_entity_view(e, &it.world())
                    {
                        apply_condition(
                            &player,
                            condition::Condition::Stun as u128,
                            condition::Condition::Stun,
                            15.0,
                            false,
                        );
                    }
                }
            });

            finish_mechanic(entity, mechanic, party);
        });
}

fn finish_mechanic(entity: EntityView<'_>, mechanic: &Mechanic, party: &Party) {
    info!(
        mechanic.request_id,
        mechanic.mechanic_id, party.id, "Completing Mechanic"
    );
    entity.remove(SuperJumpEnumeration::id());
}
