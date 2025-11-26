use crate::game::role;
use crate::system_messages::MessageToEcs;
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket};
use std::sync::mpsc::Receiver;
use std::time::Duration;
use tokio::time;
use tracing::info;

#[derive(Component)]
pub struct Socket {
    id: socket::Sid,
}

#[derive(Component, Debug)]
pub struct Player {
    content_id: u64,
    name: String,
}

#[derive(Component, Debug)]
pub struct Role {
    role: role::Role,
}

#[derive(Component, Debug)]
pub struct Party {
    id: String,
}

#[derive(Component, Debug)]
pub struct Position {
    x: f32,
    y: f32,
    z: f32,
}

#[derive(Component, Debug)]
pub struct State {
    is_alive: bool,
}

#[derive(Component, Debug)]
pub struct Mechanic {
    request_id: String,
    mechanic_id: u32,
    time_remaining: f32,
}

struct CommonQueries<'a> {
    query_socket: Query<&'a Socket>,
    query_mechanic: Query<(&'a Mechanic, &'a Party)>,
}

pub fn create_world() -> World {
    World::new()
}

#[allow(clippy::let_underscore_future)]
pub fn run_world(world: World, rx_from_ws: Receiver<MessageToEcs>, io: &SocketIo) {
    let common_queries = CommonQueries {
        query_socket: world.query::<&Socket>().set_cached().build(),
        query_mechanic: world.query::<(&Mechanic, &Party)>().set_cached().build(),
    };

    create_systems(&world);
    create_observers(&world);

    let _ = tokio::spawn(async move {
        let mut interval = time::interval(Duration::from_micros(1_000_000 / 64));

        loop {
            interval.tick().await;
            process_messages(&world, &common_queries, &rx_from_ws);
            world.progress();
        }
    });
}

fn process_messages(world: &World, queries: &CommonQueries, rx_from_ws: &Receiver<MessageToEcs>) {
    // Receive messages from the webserver system per game tick
    for message in rx_from_ws.try_iter() {
        match message {
            MessageToEcs::UpdatePlayer {
                socket_id,
                content_id,
                name,
                role,
                party,
            } => {
                let player_entity;
                if let Some(e) = find_socket(&queries.query_socket, socket_id) {
                    info!(
                        socket_str = socket_id.as_str(),
                        content_id,
                        name,
                        role_str = Into::<&str>::into(&role),
                        "Updating Player"
                    );
                    player_entity = e;
                } else {
                    info!(
                        socket_str = socket_id.as_str(),
                        content_id,
                        name,
                        role_str = Into::<&str>::into(&role),
                        "Adding Player"
                    );
                    player_entity = world.entity();
                }

                player_entity
                    .set(Socket { id: socket_id })
                    .set(Player { content_id, name })
                    .set(Role { role })
                    .set(Party { id: party });
            }

            MessageToEcs::UpdateStatus {
                socket_id,
                world_position_x,
                world_position_y,
                world_position_z,
                is_alive,
            } => {
                if let Some(e) = find_socket(&queries.query_socket, socket_id) {
                    info!(
                        socket_str = socket_id.as_str(),
                        world_position_x,
                        world_position_y,
                        world_position_z,
                        is_alive,
                        "Updating PlayerStatus"
                    );
                    e.set(Position {
                        x: world_position_x,
                        y: world_position_y,
                        z: world_position_z,
                    })
                    .set(State { is_alive });
                }
            }

            MessageToEcs::RemovePlayer { socket_id } => {
                world.defer(|| {
                    queries.query_socket.each_entity(|e, socket| {
                        if socket.id == socket_id {
                            e.get::<(Option<&Player>, Option<&Role>)>(|(player, role)| {
                                info!(
                                    socket_str = socket_id.as_str(),
                                    "Removing Player {:?} {:?}", player, role
                                );
                            });
                            e.destruct();
                        }
                    });
                });
            }

            MessageToEcs::StartMechanic {
                socket_id,
                request_id,
                mechanic_id,
            } => {
                let Some(e) = find_socket(&queries.query_socket, socket_id) else {
                    return;
                };
                e.try_get::<&Party>(|party| {
                    if queries
                        .query_mechanic
                        .find(|(m, p)| m.request_id == request_id && p.id == party.id)
                        .is_none()
                    {
                        info!(
                            socket_str = socket_id.as_str(),
                            request_id, mechanic_id, "Adding Mechanic"
                        );
                        world
                            .entity()
                            .set(Mechanic {
                                request_id,
                                mechanic_id,
                                time_remaining: 1.0,
                            })
                            .set(Party {
                                id: party.id.clone(),
                            });
                    }
                });
            }
        }
    }
}

fn create_systems(world: &World) {
    world
        .system::<(&mut Mechanic, &Party)>()
        .each_iter(|it, index, (mechanic, party)| {
            mechanic.time_remaining -= it.delta_time();
            if mechanic.time_remaining <= 0.0 {
                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Removing Mechanic"
                );
                it.entity(index).destruct();
            }
        });
}

fn create_observers(world: &World) {
    world
        .observer::<flecs::OnRemove, (&Player, &Party)>()
        .each_iter(|it, _, (player, party)| {
            let last_player = it
                .world()
                .query::<(&Player, &Party)>()
                .build()
                .find(|(pl, pa)| pa.id == party.id && pl.content_id != player.content_id)
                .is_none();
            info!(player.name, last_player, "Player removed");
            if last_player {
                // Cleanup any party entities
                it.world().query::<&Party>().build().each_entity(|e, p| {
                    if p.id == party.id {
                        e.destruct();
                    }
                });
            }
        });
}

fn find_socket<'a>(query: &Query<&'a Socket>, socket_id: socket::Sid) -> Option<EntityView<'a>> {
    query.find(|socket| socket.id == socket_id)
}
