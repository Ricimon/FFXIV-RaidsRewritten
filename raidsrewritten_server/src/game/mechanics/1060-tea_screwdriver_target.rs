use crate::{
    game::{components::*, mechanics::create_generic_mechanic, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::info;

#[derive(Component)]
pub struct ScrewdriverTarget;

#[derive(Component, Clone, Debug)]
pub struct Screw {
    target_id: u64,
    direction: i8,
    rotation_state: i8,
    link_to_id: u64,
}

const DEFAULT_ROTATION_STATE: i8 = 3;
const LINK_RADIUS: f32 = 1.9;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(ScrewdriverTarget)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &ExtraMechanicData, &Party)>()
        .with(ScrewdriverTarget)
        .each_iter(|it, index, (mechanic, extra_data, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            let mut target_id: Option<u64> = None;
            let mut direction: Option<i8> = None;
            let mut rotation_state: Option<i8> = None;
            for (i, data) in extra_data.value.split(',').enumerate() {
                if i == 0 {
                    target_id = data.parse::<u64>().ok();
                } else if i == 1 {
                    direction = data.parse::<i8>().ok();
                } else if i == 2 {
                    rotation_state = data.parse::<i8>().ok();
                }
            }

            if let Some(target_id) = target_id
                && let Some(direction) = direction
            {
                let mut screw: Screw = Screw {
                    target_id,
                    direction,
                    rotation_state: rotation_state.unwrap_or(DEFAULT_ROTATION_STATE),
                    link_to_id: 0,
                };
                let mut found_existing = false;
                world
                    .query::<&mut Screw>()
                    .build()
                    .each(|s| {
                        if s.target_id == target_id {
                            s.rotation_state = rotation_state.unwrap_or(s.rotation_state);
                            found_existing = true;
                            screw = s.clone();
                        }
                    });

                if !found_existing {
                    create_generic_mechanic(
                        world,
                        mechanic.request_id.clone(),
                        mechanic.mechanic_id,
                        mechanic.requester_socket_id,
                        party.id.clone(),
                    )
                    .set(screw.clone());
                }

                send_update_screwdriver_state(world, &party.id, &screw);
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(ScrewdriverTarget);
        });

    // Linking
    world
        .system::<(&mut Screw, &Party)>()
        .each_iter(|it, index, (screw, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            if screw.rotation_state <= 0 {
                entity.destruct();
                return;
            }

            let player_query = world.query::<(&Player, &Position)>().build();
            let player = player_query.find(|(p, _)| p.content_id == screw.target_id);

            if player.is_none() {
                entity.destruct();
                return;
            }

            let player = player.unwrap();
            let mut target_position: Option<Vector2<f32>> = None;
            player.try_get::<(&State, &Position)>(|(s, p)| {
                if !s.is_alive {
                    screw.rotation_state = -1;
                    send_update_screwdriver_state(world, &party.id, screw);
                    entity.destruct();
                    return;
                }
                target_position = Some(Vector2::new(p.x, p.z));
            });

            if target_position.is_none() {
                return;
            }
            let target_position = target_position.unwrap();

            let screw_query = world.query::<&Screw>().build();

            if screw.link_to_id == 0 {
                // Look for new link targets
                if let Some(pc) = find_party_container(world, &party.id) {
                    let mut found_link = false;
                    pc.each_child(|c1| {
                        c1.try_get::<(&Player, &Position, &State)>(|(pl, p, s)| {
                            if found_link || !s.is_alive {
                                return;
                            }

                            let mut is_screwed = false;
                            screw_query.each(|s| {
                                if s.target_id == pl.content_id {
                                    is_screwed = true;
                                }
                            });
                            if is_screwed {
                                return;
                            }

                            let player_position = Vector2::new(p.x, p.z);
                            if target_position.metric_distance(&player_position) <= LINK_RADIUS {
                                screw.link_to_id = pl.content_id;
                                send_update_screwdriver_state(world, &party.id, screw);
                                found_link = true;
                            }
                        });
                    });
                }
            } else {
                // Test if existing link needs to be broken
                player_query.each(|(pl, p)| {
                    if pl.content_id == screw.link_to_id {
                        let player_position = Vector2::new(p.x, p.z);
                        if target_position.metric_distance(&player_position) > LINK_RADIUS {
                            screw.link_to_id = 0;
                            send_update_screwdriver_state(world, &party.id, screw);
                        }
                    }
                });
            }
        });
}

fn send_update_screwdriver_state(world: &WorldRef, party_id: &String, screw: &Screw) {
    if let Some(pc) = find_party_container(world, party_id) {
        let io = get_socket_io(world);
        pc.each_child(|c| {
            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                send_run_mechanic_command(
                    io.clone(),
                    s.id,
                    RunMechanicCommandPayload {
                        mechanic_command_id: NetworkMechanicCommand::TeaUpdateScrewdriverState
                            as i32,
                        extra_data: Some(format!(
                            "{},{},{},{}",
                            screw.target_id,
                            screw.direction,
                            screw.rotation_state,
                            screw.link_to_id
                        )),
                        ..Default::default()
                    },
                );
            });
        });
    }
}
