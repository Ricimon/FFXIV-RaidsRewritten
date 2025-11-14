use crate::MessageToEcs;
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

pub fn run_ecs_container(rx_from_ws: Receiver<MessageToEcs>, io: &SocketIo) {
    let world = World::new();

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
            tick(&world, &rx_from_ws);
        }
    });
}

fn tick(world: &World, rx_from_ws: &Receiver<MessageToEcs>) {
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
                info!(
                    socket_str = socket_id.as_str(),
                    content_id,
                    name,
                    role_str = Into::<&str>::into(&role),
                    "Adding Player to ECS"
                );
                world
                    .entity()
                    .set(Socket { id: socket_id })
                    .set(Player {
                        content_id: content_id,
                        name: name,
                    })
                    .set(Role { role: role });
            }
            MessageToEcs::RemovePlayer { socket_id } => {
                world.defer(|| {
                    world
                        .query::<(&Socket, Option<&Player>, Option<&Role>)>()
                        .build()
                        .each_entity(|e, (socket, player, role)| {
                            if socket.id == socket_id {
                                info!(
                                    socket_str = socket_id.as_str(),
                                    "Removing Player from ECS {:?} {:?}", player, role
                                );
                                e.destruct();
                            }
                        });
                });
            }
        }
    }

    world.progress();
}
