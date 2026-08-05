use crate::{
    game::{
        components::*,
        condition,
        utils::*,
    },
    webserver::message::*,
};
use distances::vectors::euclidean;
use flecs_ecs::prelude::*;
use std::collections::HashMap;
use tracing::info;
use uuid::Uuid;

#[derive(Component, Debug)]
pub struct HawkBlasterTower {
    time_to_snapshot: f32,
    attack_delay: f32,
    effect_delay: f32,
    failure_attack_delay: f32,
    failure_effect_delay: f32,
    radius: f32,
    tower_vfx: String,
    attack_vfx: String,
    failure_attack_vfx: String,
    phase: Phase,
}

#[derive(Debug)]
enum Phase {
    Omen,
    Snapshot,
    Attack,
    Failure,
    FailureAttack,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(HawkBlasterTower {
        time_to_snapshot: 3.0,
        attack_delay: 0.25,
        effect_delay: 0.2,
        failure_attack_delay: 1.0,
        failure_effect_delay: 0.1,
        radius: 3.0,
        tower_vfx: "vfx/omen/eff/general_trap_o2x.avfx".to_string(),
        attack_vfx: "vfx/monster/gimmick2/eff/d2ac2_b4_g01c0c.avfx".to_string(),
        failure_attack_vfx: "vfx/monster/gimmick3/eff/n4g7_b3_g21c0x.avfx".to_string(),
        phase: Phase::Omen,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut HawkBlasterTower, &Position, &Rotation, &Party)>()
        .each_iter(|it, index, (mechanic, tower, position, rotation, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            match tower.phase {
                Phase::Omen => {
                    // Send all players the tower vfx
                    if !entity.has(Vfx::id()) {
                        let vfx_id = Uuid::new_v4().as_u128();
                        entity.set(Vfx { id: vfx_id });

                        if let Some(pc) = find_party_container(world, &party.id) {
                            let io = get_socket_io(world);
                            pc.each_child(|c| {
                                c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                    send_play_static_vfx(
                                        io.clone(),
                                        s.id,
                                        PlayStaticVfxPayload {
                                            id: vfx_id,
                                            vfx_path: tower.tower_vfx.clone(),
                                            is_omen: true,
                                            world_position_x: position.x,
                                            world_position_y: position.y,
                                            world_position_z: position.z,
                                            rotation: rotation.value,
                                            scale_x: Some(tower.radius),
                                            scale_y: Some(tower.radius),
                                            scale_z: Some(tower.radius),
                                        },
                                    );
                                });
                            });
                        }
                    }

                    tower.time_to_snapshot -= it.delta_time();
                    if tower.time_to_snapshot > 0.0 {
                        return;
                    }

                    // Snapshot
                    entity.remove(Vfx::id());

                    let mut affects: HashMap<Entity, u8> = HashMap::new();

                    if let Some(pc) = find_party_container(world, &party.id) {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &Position, &State)>(|(_, p, s)| {
                                if !s.is_alive {
                                    return;
                                }

                                let p1 = [position.x, position.z];
                                let p2 = [p.x, p.z];
                                let distance: f32 = euclidean(&p1, &p2);

                                if distance <= tower.radius {
                                    // TODO: check vuln
                                    add_affect(&mut affects, &c, 1);
                                }
                            });
                        });
                    }

                    entity.set(Affects {
                        player_entities: affects,
                    });

                    tower.phase = Phase::Snapshot;
                    return;
                }

                Phase::Snapshot => {
                    tower.attack_delay -= it.delta_time();
                    if tower.attack_delay > 0.0 {
                        return;
                    }

                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<&Socket>(|s| {
                                // Play attack vfx
                                send_play_actor_vfx_on_position(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnPositionPayload {
                                        vfx_path: tower.attack_vfx.clone(),
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: rotation.value,
                                    },
                                );
                            });
                        });
                    }

                    tower.phase = Phase::Attack;
                    return;
                }

                Phase::Attack => {
                    tower.effect_delay -= it.delta_time();
                    if tower.effect_delay > 0.0 {
                        return;
                    }

                    let mut affect_count = 0;
                    entity.try_get::<&Affects>(|a| {
                        affect_count = a.player_entities.len();
                        for e in a.player_entities.keys() {
                            world
                                .entity()
                                .set(Condition {
                                    id: condition::Condition::Stun as u128,
                                    condition: condition::Condition::Stun,
                                    time_remaining: 1.0,
                                })
                                .child_of(e.entity_view(world));
                        }

                        if affect_count > 0 {
                            
                        }
                    });

                    if affect_count < 2 {
                        tower.phase = Phase::Failure;
                        return;
                    }
                }

                Phase::Failure => {
                    tower.failure_attack_delay -= it.delta_time();
                    if tower.failure_attack_delay > 0.0 {
                        return;
                    }

                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<&Socket>(|s| {
                                // Play attack vfx
                                send_play_actor_vfx_on_position(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnPositionPayload {
                                        vfx_path: tower.failure_attack_vfx.clone(),
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: rotation.value,
                                    },
                                );
                            });
                        });
                    }

                    tower.phase = Phase::FailureAttack;
                    return;
                }

                Phase::FailureAttack => {
                    tower.failure_effect_delay -= it.delta_time();
                    if tower.failure_effect_delay > 0.0 {
                        return;
                    }

                    if let Some(pc) = find_party_container(world, &party.id) {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &State)>(|(_, s)| {
                                if !s.is_alive {
                                    return;
                                }
                                world
                                    .entity()
                                    .set(Condition {
                                        id: condition::Condition::Hysteria as u128,
                                        condition: condition::Condition::Hysteria,
                                        time_remaining: 15.0,
                                    })
                                    .set(conditions::Hysteria {
                                        redirection_interval: 5.0,
                                    })
                                    .child_of(c);
                            });
                        });
                    }
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(HawkBlasterTower::id());
        });
}
