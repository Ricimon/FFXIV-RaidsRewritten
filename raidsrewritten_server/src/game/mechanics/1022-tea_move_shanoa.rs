use crate::{
    game::{
        components::*,
        mechanics::m1020_tea_spawn_shanoa::{TeaShanoa, TeaShanoaTargetPosition},
        utils::*,
    },
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector3;
use tracing::info;

#[derive(Component, Debug)]
struct MoveShanoa;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(MoveShanoa)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &Position, &Rotation, &ExtraMechanicData, &Party)>()
        .with(MoveShanoa)
        .each_iter(
            |it, index, (mechanic, position, rotation, extra_data, party)| {
                let entity = it.entity(index);
                let world = &it.world();

                let mut can_move = false;
                let mut movement_speed = 0.0;
                let mut rotation_speed = 0.0;
                if let Ok(marker_id) = extra_data.value.parse::<u8>() {
                    world
                        .query::<(&mut TeaShanoa, &mut Rotation, &Party)>()
                        .build()
                        .each_entity(|e, (shanoa, r, p)| {
                            if p.id != party.id {
                                return;
                            }
                            if shanoa.navigation_markers.remove(&marker_id) {
                                can_move = true;
                                e.set(TeaShanoaTargetPosition {
                                    value: Vector3::new(position.x, position.y, position.z),
                                });
                                r.value = rotation.value; // insta-set the rotation because this value doesn't really matter on the server
                                shanoa.absorbed_markers.insert(marker_id);
                                movement_speed = shanoa.movement_speed;
                                rotation_speed = shanoa.rotation_speed;
                            }
                        });
                }

                if can_move && let Some(pc) = find_party_container(world, &party.id) {
                    let io = get_socket_io(world);
                    pc.each_child(|c| {
                        c.try_get::<(&Socket, &Player)>(|(s, _)| {
                            send_run_mechanic_command(
                                io.clone(),
                                s.id,
                                RunMechanicCommandPayload {
                                    mechanic_command_id: NetworkMechanicCommand::TeaMoveShanoa
                                        as i32,
                                    world_position_x: Some(position.x),
                                    world_position_y: Some(position.y),
                                    world_position_z: Some(position.z),
                                    rotation: Some(rotation.value),
                                    extra_data: Some(format!("{movement_speed},{rotation_speed}")),
                                },
                            );
                        });
                    });
                }

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Completing Mechanic"
                );
                entity.remove(MoveShanoa);
            },
        );
}
