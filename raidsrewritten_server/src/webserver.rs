pub mod message;

use crate::ecs_container;
use crate::system_messages::MessageToEcs;
use axum::routing::get;
use flecs_ecs::prelude::*;
use rmpv::Value;
use socketioxide::{
    SocketIo,
    extract::{AckSender, Data, SocketRef},
    layer::SocketIoLayer,
    socket::DisconnectReason,
};
use std::sync::mpsc::Sender;
use std::sync::{Arc, Mutex};
use tracing::info;

async fn on_connect_impl(
    socket: SocketRef,
    Data(_data): Data<Value>,
    tx_to_ecs: Sender<MessageToEcs>,
) {
    info!(ns = socket.ns(), ?socket.id, "Socket.IO connected");

    let tx = tx_to_ecs.clone();
    let on_message = async |socket: SocketRef, Data(data): Data<message::Message>| {
        on_message_impl(socket, Data(data), tx).await;
    };
    socket.on("message", on_message);
    socket.on("message-with-ack", on_message_with_ack);

    let tx = tx_to_ecs.clone();
    let on_disconnect = async |socket: SocketRef, reason: DisconnectReason| {
        on_disconnect_impl(socket, reason, tx).await;
    };
    socket.on_disconnect(on_disconnect);
}

async fn on_message_impl(
    socket: SocketRef,
    Data(message): Data<message::Message>,
    tx: Sender<MessageToEcs>,
) {
    info!(?socket.id, "Received message\n{:#?}", message);
    socket.emit("message-back", &message).ok();

    match message.action {
        message::Action::UpdatePlayer => {
            if let Some(update_player) = message.update_player {
                tx.send(MessageToEcs::UpdatePlayer {
                    socket_id: socket.id,
                    content_id: update_player.id,
                    name: update_player.name,
                    role: update_player.role,
                    party: update_player.party,
                })
                .unwrap();
            }
        }
        message::Action::UpdateStatus => {
            if let Some(update_status) = message.update_status {
                tx.send(MessageToEcs::UpdateStatus {
                    socket_id: socket.id,
                    world_position_x: update_status.world_position_x,
                    world_position_y: update_status.world_position_y,
                    world_position_z: update_status.world_position_z,
                    is_alive: update_status.is_alive,
                })
                .unwrap();
            }
        }
        _ => {}
    }
}

async fn on_message_with_ack(socket: SocketRef, Data(data): Data<Value>, ack: AckSender) {
    info!(?socket.id, ?data, "Received message-with-ack");
    ack.send(&data).ok();
}

async fn on_disconnect_impl(socket: SocketRef, reason: DisconnectReason, tx: Sender<MessageToEcs>) {
    info!(?socket.id, ?reason, "Socket disconnected");

    tx.send(MessageToEcs::RemovePlayer {
        socket_id: socket.id,
    })
    .unwrap();
}

pub fn create_layer() -> (SocketIoLayer, SocketIo) {
    SocketIo::new_layer()
}

pub async fn run_webserver(
    layer: SocketIoLayer,
    io: SocketIo,
    tx_to_ecs: Sender<MessageToEcs>,
    world: World,
) -> Result<(), Box<dyn std::error::Error>> {
    // https://doc.rust-lang.org/book/ch16-03-shared-state.html#atomic-reference-counting-with-arct
    let world = Arc::new(Mutex::new(world));

    let on_connect = async |socket: SocketRef, Data::<Value>(data)| {
        on_connect_impl(socket, Data(data), tx_to_ecs).await;
    };

    io.ns("/", on_connect);

    let app = axum::Router::new()
        .route(
            "/",
            get(|| async move {
                let mut players = 0;
                let world = world.lock().unwrap();
                world
                    .query::<&ecs_container::Player>()
                    .build()
                    .each_entity(|_, _| {
                        players += 1;
                    });
                format!("Players connected: {players}")
            }),
        )
        .layer(layer);

    info!("Starting server.");

    let listener = tokio::net::TcpListener::bind("0.0.0.0:3000").await.unwrap();
    axum::serve(listener, app).await.unwrap();

    Ok(())
}
