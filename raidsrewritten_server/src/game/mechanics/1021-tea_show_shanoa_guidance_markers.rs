use crate::{
    game::{components::*, mechanics::m1020_tea_spawn_shanoa::TeaShanoa, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component, Debug)]
struct ShowShanoaGuidanceMarkers {
    duration: f32,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(ShowShanoaGuidanceMarkers { duration: 5.0 })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &ShowShanoaGuidanceMarkers, &Party)>()
        .each_iter(
            |it, index, (mechanic, show_shanoa_guidance_markers, party)| {
                let entity = it.entity(index);
                let world = &it.world();

                let mut available_markers_flags: u8 = 0;
                world
                    .query::<(&mut TeaShanoa, &Party)>()
                    .build()
                    .each(|(shanoa, p)| {
                        if p.id == party.id {
                            for i in &shanoa.available_markers {
                                available_markers_flags |= 1 << i;
                            }
                        }
                    });

                if available_markers_flags > 0
                    && let Some(pc) = find_party_container(world, &party.id)
                {
                    let io = get_socket_io(world);
                    pc.each_child(|c| {
                        c.try_get::<(&Socket, &Player)>(|(s, _)| {
                            send_run_mechanic_command(
                                io.clone(),
                                s.id,
                                RunMechanicCommandPayload {
                                    mechanic_command_id:
                                        NetworkMechanicCommand::TeaShowShanoaGuidanceMarkers as i32,
                                    extra_data: Some(format!(
                                        "{available_markers_flags},{}",
                                        show_shanoa_guidance_markers.duration
                                    )),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                }

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Completing Mechanic"
                );
                entity.remove(ShowShanoaGuidanceMarkers::id());
            },
        );
}
