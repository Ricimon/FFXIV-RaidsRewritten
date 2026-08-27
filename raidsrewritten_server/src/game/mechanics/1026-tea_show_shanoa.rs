use crate::{
    game::{components::*, mechanics::m1020_tea_spawn_shanoa::TeaShanoa, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component)]
struct ShowShanoa;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(ShowShanoa)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &Position, &Rotation, &Party)>()
        .with(ShowShanoa)
        .each_iter(|it, index, (mechanic, position, rotation, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            let mut shanoa_found = false;
            world
                .query::<(&mut TeaShanoa, &mut Position, &mut Rotation, &Party)>()
                .build()
                .each(|(shanoa, shanoa_position, shanoa_rotation, p)| {
                    if p.id != party.id {
                        return;
                    }
                    shanoa_found = true;
                    shanoa.active = true;
                    shanoa_position.x = position.x;
                    shanoa_position.y = position.y;
                    shanoa_position.z = position.z;
                    shanoa_rotation.value = rotation.value;
                });

            if shanoa_found {
                let mut extra_data: Option<String> = None;
                entity.try_get::<&ExtraMechanicData>(|d| {
                    extra_data = Some(d.value.clone());
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
                                    world_position_x: Some(position.x),
                                    world_position_y: Some(position.y),
                                    world_position_z: Some(position.z),
                                    rotation: Some(rotation.value),
                                    extra_data: extra_data.clone(),
                                },
                            );
                        });
                    });
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(ShowShanoa);
        });
}
