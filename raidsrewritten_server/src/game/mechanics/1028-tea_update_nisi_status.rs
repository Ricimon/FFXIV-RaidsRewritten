use crate::{
    game::{
        components::*,
        mechanics::{create_generic_mechanic, m1020_tea_spawn_shanoa::TeaShanoa},
        utils::*,
    },
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::info;

#[derive(Component)]
struct UpdateNisiStatus;

#[derive(Component, Debug)]
pub struct Nisi {
    pub type_: u8,
}

const SHANOA_INTERACTION_RADIUS: f32 = 1.5;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(UpdateNisiStatus)
}

pub fn create_systems(world: &World) {
    // Update player Nisi statuses
    world
        .system::<(&Mechanic, &ExtraMechanicData, &Party)>()
        .with(UpdateNisiStatus)
        .each_iter(|it, index, (mechanic, extra_data, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            if let Ok(nisi_type) = extra_data.value.parse::<u8>() {
                let mut found_nisi = false;
                world
                    .query::<(&mut Nisi, &Socket, &Player)>()
                    .term_at(1)
                    .up()
                    .term_at(2)
                    .up()
                    .build()
                    .each(|(nisi, socket, _)| {
                        if socket.id == mechanic.requester_socket_id {
                            nisi.type_ = nisi_type;
                            found_nisi = true;
                        }
                    });

                if !found_nisi
                    && let Some(player_entity) = world
                        .query::<(&Socket, &Player)>()
                        .build()
                        .find(|(socket, _)| socket.id == mechanic.requester_socket_id)
                {
                    // This is given its own entity and Mechanic component so it gets cleared on ClearMechanics
                    create_generic_mechanic(
                        world,
                        mechanic.request_id.clone(),
                        mechanic.mechanic_id,
                        mechanic.requester_socket_id,
                        party.id.clone(),
                    )
                    .set(Nisi { type_: nisi_type })
                    .child_of(player_entity);
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(UpdateNisiStatus);
        });

    // Shanoa and Nisi interaction
    world.system::<(&TeaShanoa, &Position, &Party)>().each_iter(
        |it, index, (shanoa, shanoa_position, party)| {
            if !shanoa.active { return; }

            let entity = it.entity(index);
            let world = &it.world();

            let mut shanoa_nisi: u8 = 0;
            entity.try_get::<&Nisi>(|nisi| shanoa_nisi = nisi.type_);

            let mut new_nisi: u8 = shanoa_nisi;
            world
                .query::<(&Nisi, &Player, &Position, &Party)>()
                .term_at(1)
                .up()
                .term_at(2)
                .up()
                .term_at(3)
                .up()
                .build()
                .each(|(nisi, _, player_position, player_party)| {
                    if player_party.id != party.id {
                        return;
                    }
                    if new_nisi != shanoa_nisi {
                        return;
                    }
                    if nisi.type_ == shanoa_nisi {
                        return;
                    }

                    let shanoa_position = Vector2::new(shanoa_position.x, shanoa_position.z);
                    let player_position = Vector2::new(player_position.x, player_position.z);
                    if shanoa_position.metric_distance(&player_position) <= SHANOA_INTERACTION_RADIUS {
                        new_nisi = nisi.type_;
                    }
                });

            if new_nisi != shanoa_nisi {
                if shanoa_nisi != 0 {
                    // Destruct Shanoa entity
                    entity.destruct();

                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                send_run_mechanic_command(
                                    io.clone(),
                                    s.id,
                                    RunMechanicCommandPayload {
                                        mechanic_command_id:
                                            NetworkMechanicCommand::TeaShanoaRunsAway as i32,
                                        extra_data: Some(format!(
                                            "{},{}",
                                            shanoa.movement_speed, shanoa.rotation_speed
                                        )),
                                        ..Default::default()
                                    },
                                );
                            });
                        });
                    }
                } else {
                    entity.set(Nisi {type_: new_nisi});

                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                send_run_mechanic_command(
                                    io.clone(),
                                    s.id,
                                    RunMechanicCommandPayload {
                                        mechanic_command_id:
                                            NetworkMechanicCommand::TeaUpdateShanoaNisiStatus as i32,
                                        extra_data: Some(new_nisi.to_string()),
                                        ..Default::default()
                                    },
                                );
                            });
                        });
                    }
                }
            }
        },
    );
}
