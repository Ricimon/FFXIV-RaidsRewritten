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
use std::collections::HashMap;
use tracing::info;

#[derive(Component, Debug)]
pub struct ApocalypticRaySpreads {
    time_to_snapshot: f32,
    effect_delay: f32,
    radius: f32,
    omen_vfx_path: String,
    attack_vfx_path: String,
}

#[derive(Clone, Copy)]
struct Target {
    entity: Entity,
    content_id: u64,
    distance: f32,
}

const TARGET_COUNT: usize = 6;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(ApocalypticRaySpreads {
        time_to_snapshot: 5.0,
        effect_delay: 0.2,
        radius: 6.0,
        omen_vfx_path: "vfx/lockon/eff/target_ae_s5f.avfx".to_string(),
        attack_vfx_path: "vfx/monster/gimmick4/eff/n5r8_b_g15_t0k1.avfx".to_string(),
    })
}

pub fn create_systems(world: &World) {
    // Assign target
    world
        .system::<(&Mechanic, &mut ApocalypticRaySpreads, &Position, &Party)>()
        .without(Targets::id())
        .each_iter(|it, index, (mechanic, spread, position, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            // Assign targets
            let mut targets: Vec<Target> = Vec::new();
            if let Some(pc) = find_party_container(world, &party.id) {
                let p1 = Vector2::new(position.x, position.z);
                pc.each_child(|c| {
                    c.try_get::<(&Player, &Position, &State)>(|(pl, p, s)| {
                        if !s.is_alive {
                            return;
                        }

                        let p2 = Vector2::new(p.x, p.z);
                        let distance: f32 = p1.metric_distance(&p2);

                        targets.push(Target {
                            entity: *c,
                            content_id: pl.content_id,
                            distance,
                        });
                    });
                });

                targets.sort_unstable_by(|a, b| a.distance.total_cmp(&b.distance));
                targets.reverse();
                targets.truncate(TARGET_COUNT);
            }

            if targets.is_empty() {
                finish_mechanic(entity, mechanic, party);
                return;
            }

            // Double-up
            let target_count = targets.len();
            let mut i = 0;
            while targets.len() < TARGET_COUNT {
                targets.push(targets[i]);
                i = (i + 1) % target_count;
            }

            entity.set(Targets {
                player_entities: targets.iter().map(|t| t.entity).collect(),
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
                                content_id_targets: targets.iter().map(|t| t.content_id).collect(),
                                ..Default::default()
                            },
                        );
                    });
                });
            }
        });

    // Assign affects
    world
        .system::<(&mut ApocalypticRaySpreads, &Party, &mut Targets)>()
        .without(Affects::id())
        .each_iter(|it, index, (spread, party, targets)| {
            let entity = it.entity(index);

            spread.time_to_snapshot -= it.delta_time();

            if spread.time_to_snapshot > 0.0 {
                return;
            }

            let mut affects: HashMap<Entity, u8> = HashMap::new();

            // Snapshot
            // Prune valid targets
            targets.player_entities.retain(|e| {
                let mut valid_target = false;
                if let Some(ev) = get_entity_view(e, &it.world()) {
                    ev.try_get::<&State>(|s| valid_target = s.is_alive);
                }
                valid_target
            });

            if let Some(pc) = find_party_container(&it.world(), &party.id) {
                for e in &targets.player_entities {
                    // For every target player,
                    let e1 = e.entity_view(it.world());
                    e1.try_get::<(&Player, &Position)>(|(_, p1)| {
                        // affect all players within radius
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &Position, &State)>(|(_, p2, s2)| {
                                if !s2.is_alive {
                                    return;
                                }

                                let p1 = Vector2::new(p1.x, p1.z);
                                let p2 = Vector2::new(p2.x, p2.z);

                                if p1.metric_distance(&p2) <= spread.radius {
                                    add_affect(&mut affects, &c, 1);
                                }
                            });
                        });
                    });
                }
            }

            entity.set(Affects {
                player_entities: affects,
            });

            // Send attack vfx
            let mut target_ids: Vec<u64> = Vec::new();
            for e in &targets.player_entities {
                if let Some(ev) = get_entity_view(e, &entity.world()) {
                    ev.try_get::<&Player>(|pl| {
                        target_ids.push(pl.content_id);
                    });
                }
            }
            let io = get_socket_io(&it.world());
            if let Some(pc) = find_party_container(&it.world(), &party.id) {
                pc.each_child(|c| {
                    c.try_get::<&Socket>(|s| {
                        send_play_actor_vfx_on_target(
                            io.clone(),
                            s.id,
                            PlayActorVfxOnTargetPayload {
                                vfx_path: spread.attack_vfx_path.clone(),
                                content_id_targets: target_ids.clone(),
                                ..Default::default()
                            },
                        );
                    });
                });
            }
        });

    // Resolve effects
    world
        .system::<(&Mechanic, &mut ApocalypticRaySpreads, &Party, &mut Affects)>()
        .each_iter(|it, index, (mechanic, spread, party, affects)| {
            let entity = it.entity(index);
            let world = &it.world();

            if affects.player_entities.is_empty() {
                finish_mechanic(entity, mechanic, party);
                return;
            }

            spread.effect_delay = f32::max(spread.effect_delay - it.delta_time(), 0.0);

            // Apply condition to 1 target at a time so as to apply and read magic vulns
            let extract: Vec<(Entity, u8)> = affects
                .player_entities
                .extract_if(|_, _| true)
                .take(1)
                .collect();

            for (e, mut affect_count) in extract {
                if let Some(player) = get_entity_view(&e, world) {
                    let mut has_vuln = false;
                    player.each_child(|c| {
                        c.try_get::<&Condition>(|condition| {
                            if condition.condition == condition::Condition::MagicVulnerabilityUp {
                                has_vuln = true;
                            }
                        });
                    });

                    if has_vuln {
                        let stun = apply_condition(
                            &player,
                            condition::Condition::Stun as u128,
                            condition::Condition::Stun,
                            15.0,
                            false,
                        );
                        if let Some(stun) = get_entity_view(&stun, world) {
                            stun.set(BroadcastDelay {
                                value: spread.effect_delay,
                            });
                        }
                    }

                    let magic_vuln = apply_condition(
                        &player,
                        condition::Condition::MagicVulnerabilityUp as u128,
                        condition::Condition::MagicVulnerabilityUp,
                        2.0,
                        false,
                    );
                    if let Some(magic_vuln) = get_entity_view(&magic_vuln, world) {
                        magic_vuln.set(BroadcastDelay {
                            value: spread.effect_delay,
                        });
                    }
                }

                affect_count -= 1;
                if affect_count > 0 {
                    affects.player_entities.insert(e, affect_count);
                }
            }
        });
}

fn finish_mechanic(entity: EntityView<'_>, mechanic: &Mechanic, party: &Party) {
    info!(
        mechanic.request_id,
        mechanic.mechanic_id, party.id, "Completing Mechanic"
    );
    entity.remove(ApocalypticRaySpreads::id());
}
