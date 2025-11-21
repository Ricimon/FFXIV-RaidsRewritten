use crate::system_messages::MessageToEcs;
use crate::game::role;
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket};
use std::sync::mpsc::Receiver;
use std::time::Duration;
use tokio::time;
use tracing::info;

#[derive(Component)]
pub struct TestComponent;

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

struct CommonQueries<'a> {
    query_socket: Query<&'a Socket>,
}

#[allow(clippy::let_underscore_future)]
pub fn run_ecs_container(rx_from_ws: Receiver<MessageToEcs>, io: &SocketIo) {
    let world = World::new();

    let common_queries = CommonQueries {
        query_socket: world.query::<&Socket>().build(),
    };

    // Example
    let io1 = io.clone();
    system!(world, TestComponent).each_iter(move |it, index, _test| {
        info!("ECS tick on TestComponent");
        let e = it.entity(index);
        e.destruct();

        // This is an example of how the webserver's SocketIo can be cheaply cloned and used in the ECS system
        let io1 = io1.clone();
        tokio::spawn(async move {
            io1.emit("message", "This is from ECS").await.unwrap();
        });
    });

    let _ = tokio::spawn(async move {
        let mut interval = time::interval(Duration::from_micros(1_000_000 / 64));

        loop {
            interval.tick().await;
            tick(&world, &common_queries, &rx_from_ws);
        }
    });
}

fn tick(world: &World, queries: &CommonQueries, rx_from_ws: &Receiver<MessageToEcs>) {
    // Receive messages from the webserver system per game tick
    for message in rx_from_ws.try_iter() {
        match message {
            MessageToEcs::Test => {
                world.entity().add(TestComponent);
            }

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
                        "Updating Player in ECS"
                    );
                    player_entity = e;
                } else {
                    info!(
                        socket_str = socket_id.as_str(),
                        content_id,
                        name,
                        role_str = Into::<&str>::into(&role),
                        "Adding Player to ECS"
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
                        "Updating PlayerStatus in ECS"
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
                                    "Removing Player from ECS {:?} {:?}", player, role
                                );
                            });
                            e.destruct();
                        }
                    });
                });
            }
        }
    }

    world.progress();
}

fn find_socket<'a>(query: &Query<&'a Socket>, socket_id: socket::Sid) -> Option<EntityView<'a>> {
    query.find(|socket| socket.id == socket_id)
}
