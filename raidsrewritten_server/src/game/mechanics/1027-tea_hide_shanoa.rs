use crate::{
    game::{components::*, mechanics::m1020_tea_spawn_shanoa::TeaShanoa, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component)]
struct HideShanoa;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(HideShanoa)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &Party)>()
        .with(HideShanoa)
        .each_iter(|it, index, (mechanic, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            world
                .query::<(&mut TeaShanoa, &Party)>()
                .build()
                .each(|(shanoa, p)| {
                    if p.id != party.id {
                        return;
                    }
                    shanoa.active = false;
                });

            if let Some(pc) = find_party_container(world, &party.id) {
                let io = get_socket_io(world);
                pc.each_child(|c| {
                    c.try_get::<(&Socket, &Player)>(|(s, _)| {
                        send_run_mechanic_command(
                            io.clone(),
                            s.id,
                            RunMechanicCommandPayload {
                                mechanic_command_id: NetworkMechanicCommand::TeaHideShanoa as i32,
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
            entity.remove(HideShanoa);
        });
}
