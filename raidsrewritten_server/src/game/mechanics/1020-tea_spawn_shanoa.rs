use crate::{
    game::{components::*, mechanics::create_generic_mechanic, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector3;
use std::collections::HashSet;
use tracing::warn;

#[derive(Component, Debug)]
pub struct TeaShanoa {
    visible: bool,
    available_markers: HashSet<u8>,
    fire_tornado: Entity,
}

#[derive(Component, Debug)]
struct FireTornado {
    spawned_shanoa: bool,
}

const ARENA_CENTER: Vector3<f32> = Vector3::new(100.0, 0.0, 100.0);

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(FireTornado {
        spawned_shanoa: false,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(
            &Mechanic,
            &mut TeaShanoa,
            &mut Position,
            &mut Rotation,
            &Party,
        )>()
        .each_iter(|it, index, (mechanic, shanoa, position, rotation, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            // info!(
            //     mechanic.request_id,
            //     mechanic.mechanic_id, party.id, "Completing Mechanic"
            // );
            // entity.remove(TeaShanoa::id());
        });

    world
        .system::<(&Mechanic, &mut FireTornado, &Position, &Party)>()
        .each_iter(|it, index, (mechanic, fire_tornado, position, party)| {
            if fire_tornado.spawned_shanoa {
                return;
            }

            let entity = it.entity(index);
            let world = &it.world();

            if let Some(shanoa) = world
                .query::<(&TeaShanoa, &Party)>()
                .build()
                .find(|(_, p)| p.id == party.id)
            {
                warn!("Shanoa Entity already exists! Updating fire tornado position instead.");
                shanoa.try_get::<&mut TeaShanoa>(|shanoa| {
                    let fire_tornado_entity = shanoa.fire_tornado.entity_view(world);
                    if fire_tornado_entity.is_valid() {
                        fire_tornado_entity.destruct();
                    }
                    shanoa.fire_tornado = *entity;
                });
            } else {
                // Show Shanoa for the first time
                let fire_tornado_position = Vector3::new(position.x, position.y, position.z);
                let towards_center = Vector3::normalize(&(ARENA_CENTER - fire_tornado_position));
                let distance_towards_center = 6.0;
                let shanoa_position =
                    fire_tornado_position + distance_towards_center * towards_center;
                let shanoa_rotation = vector_to_rotation(towards_center.x, towards_center.z);

                let shanoa_entity = create_generic_mechanic(
                    world,
                    mechanic.request_id.clone(),
                    mechanic.mechanic_id,
                    party.id.clone(),
                );
                shanoa_entity
                    .set(TeaShanoa {
                        visible: true,
                        available_markers: HashSet::from([1, 2, 3, 4, 5, 6, 7, 8]),
                        fire_tornado: *entity,
                    })
                    .set(Position {
                        x: shanoa_position.x,
                        y: shanoa_position.y,
                        z: shanoa_position.z,
                    })
                    .set(Rotation {
                        value: shanoa_rotation,
                    });

                if let Some(pc) = find_party_container(world, &party.id) {
                    let io = get_socket_io(world);
                    pc.each_child(|c| {
                        c.try_get::<(&Socket, &Player)>(|(s, _)| {
                            send_run_mechanic_command(
                                io.clone(),
                                s.id,
                                RunMechanicCommandPayload {
                                    mechanic_command_id: NetworkMechanicCommand::TeaShowShanoa
                                        as i32,
                                    world_position_x: Some(shanoa_position.x),
                                    world_position_y: Some(shanoa_position.y),
                                    world_position_z: Some(shanoa_position.z),
                                    rotation: Some(shanoa_rotation),
                                    extra_data: None,
                                },
                            );
                        });
                    });
                }
            }

            fire_tornado.spawned_shanoa = true;
        });
}
