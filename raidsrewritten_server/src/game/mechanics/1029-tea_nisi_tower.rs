use crate::{
    game::{
        components::*,
        condition::{self, apply_condition},
        mechanics::{m1020_tea_spawn_shanoa::TeaShanoa, m1028_tea_update_nisi_status::Nisi},
        utils::*,
    },
    webserver::{
        message::{PlayActorVfxOnPositionPayload, PlaySfxPayload, RunMechanicCommandPayload},
        network_mechanic::NetworkMechanicCommand,
    },
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::{info, warn};
use uuid::Uuid;

#[derive(Component, Debug)]
struct NisiTower {
    radius: f32,
    time_to_snapshot: f32,
    success_vfx: String,
    success_sfx: String,
    failure_vfx: String,
    failure_sfx: String,
    nisi_type: u8,
    last_in_tower_count_broadcasted: i8,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(NisiTower {
        radius: 3.0,
        time_to_snapshot: 8.2,
        success_vfx: "vfx/monster/gimmick2/eff/z3oe_b3_g03c0i.avfx".to_string(),
        success_sfx: "sound/vfx/monster6/SE_Vfx_Monster_OIIIBOSS3_TrapAE_c.scd".to_string(),
        failure_vfx: "vfx/monster/gimmick2/eff/z3oe_b3_g04c0i.avfx".to_string(),
        failure_sfx: "sound/vfx/monster6/SE_Vfx_Monster_OIIIBOSS3_TrapAE_penalty_c.scd".to_string(),
        nisi_type: 0,
        last_in_tower_count_broadcasted: -1,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(
            &Mechanic,
            &mut NisiTower,
            &Position,
            &Rotation,
            &ExtraMechanicData,
            &Party,
        )>()
        .each_iter(
            |it, index, (mechanic, nisi_tower, position, rotation, extra_data, party)| {
                let entity = it.entity(index);
                let world = &it.world();

                if nisi_tower.nisi_type == 0 {
                    if let Ok(nisi_type) = extra_data.value.parse::<u8>() {
                        nisi_tower.nisi_type = nisi_type;
                    }
                    if nisi_tower.nisi_type == 0 {
                        warn!(
                            mechanic.request_id,
                            mechanic.mechanic_id,
                            party.id,
                            "Invalid Nisi type, completing mechanic"
                        );
                        entity.remove(NisiTower::id());
                        return;
                    }
                }

                if !entity.has(Vfx::id()) {
                    let vfx_id = Uuid::new_v4().as_u128();
                    entity.set(Vfx { id: vfx_id });
                }

                // Calculate this based on Shanoa position and Nisi state
                let mut in_tower_count = 0;
                world
                    .query::<(&TeaShanoa, &Position, &Nisi, &Party)>()
                    .build()
                    .each(|(_, shanoa_position, shanoa_nisi, p)| {
                        if p.id != party.id {
                            return;
                        }
                        let shanoa_position = Vector2::new(shanoa_position.x, shanoa_position.z);
                        let tower_position = Vector2::new(position.x, position.z);
                        if shanoa_nisi.type_ == nisi_tower.nisi_type
                            && shanoa_position.metric_distance(&tower_position) <= nisi_tower.radius
                        {
                            in_tower_count = 1;
                        }
                    });

                if nisi_tower.last_in_tower_count_broadcasted != in_tower_count {
                    let mut vfx_id: u128 = 0;
                    entity.try_get::<&Vfx>(|vfx| {
                        vfx_id = vfx.id;
                    });

                    if vfx_id != 0 {
                        nisi_tower.last_in_tower_count_broadcasted = in_tower_count;

                        if let Some(pc) = find_party_container(world, &party.id) {
                            let io = get_socket_io(world);
                            pc.each_child(|c| {
                                c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                    send_run_mechanic_command(
                                        io.clone(),
                                        s.id,
                                        RunMechanicCommandPayload {
                                            mechanic_command_id:
                                                NetworkMechanicCommand::TeaUpdateNisiTower as i32,
                                            world_position_x: Some(position.x),
                                            world_position_y: Some(position.y),
                                            world_position_z: Some(position.z),
                                            rotation: Some(rotation.value),
                                            extra_data: Some(format!(
                                                "{},{},{}",
                                                vfx_id, nisi_tower.nisi_type, in_tower_count
                                            )),
                                        },
                                    );
                                });
                            });
                        }
                    }
                }

                nisi_tower.time_to_snapshot -= it.delta_time();

                if nisi_tower.time_to_snapshot > 0.0 {
                    return;
                }

                let failure = in_tower_count == 0;
                let mut vfx_path = &nisi_tower.failure_vfx;
                let mut sfx_path = &nisi_tower.failure_sfx;
                if !failure {
                    vfx_path = &nisi_tower.success_vfx;
                    sfx_path = &nisi_tower.success_sfx;

                    // Take Nisi off of Shanoa
                    world
                        .query::<(&TeaShanoa, &mut Nisi, &Party)>()
                        .build()
                        .each(|(_, shanoa_nisi, p)| {
                            if p.id != party.id {
                                return;
                            }
                            shanoa_nisi.type_ = 0;
                        });
                }

                if let Some(pc) = find_party_container(world, &party.id) {
                    let io = get_socket_io(world);
                    // send VFX/SFX
                    pc.each_child(|c| {
                        c.try_get::<(&Socket, &Player)>(|(s, _)| {
                            send_play_actor_vfx_on_position(
                                io.clone(),
                                s.id,
                                PlayActorVfxOnPositionPayload {
                                    vfx_path: vfx_path.to_string(),
                                    world_position_x: position.x,
                                    world_position_y: position.y,
                                    world_position_z: position.z,
                                    rotation: rotation.value,
                                },
                            );

                            send_play_sfx(
                                io.clone(),
                                s.id,
                                PlaySfxPayload {
                                    sfx_path: sfx_path.to_string(),
                                    sfx_index: 0,
                                },
                            );

                            if !failure {
                                send_run_mechanic_command(
                                    io.clone(),
                                    s.id,
                                    RunMechanicCommandPayload {
                                        mechanic_command_id:
                                            NetworkMechanicCommand::TeaUpdateShanoaNisiStatus as i32,
                                        extra_data: Some(0.to_string()),
                                        ..Default::default()
                                    },
                                );
                            }
                        });
                    });

                    if failure {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &State)>(|(_, s)| {
                                if !s.is_alive {
                                    return;
                                }
                                let player = c.entity_view(world);
                                apply_condition(
                                    &player,
                                    condition::Condition::Pacify as u128,
                                    condition::Condition::Pacify,
                                    60.0,
                                    false,
                                );
                            });
                        });
                    }
                }

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Completing Mechanic"
                );
                entity.remove(NisiTower::id());
            },
        );
}
