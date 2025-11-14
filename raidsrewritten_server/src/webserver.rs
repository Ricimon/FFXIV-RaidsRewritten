pub mod message;

use crate::MessageToEcs;
use axum::routing::get;
use rmpv::Value;
use socketioxide::{
    SocketIo,
    extract::{AckSender, Data, SocketRef},
    layer::SocketIoLayer,
    socket::DisconnectReason,
};
use std::sync::mpsc::Sender;
use tracing::info;

async fn on_connect_impl(
    socket: SocketRef,
    Data(_data): Data<Value>,
    tx_to_ecs: Sender<MessageToEcs>,
) {
    info!(ns = socket.ns(), ?socket.id, "Socket.IO connected");

    // socket
    //     .emit(
    //         "message",
    //         &message::Message {
    //             action: message::Action::PlayVfx,
    //             ..Default::default()
    //         },
    //     )
    //     .ok();

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
) -> Result<(), Box<dyn std::error::Error>> {
    let on_connect = async |socket: SocketRef, Data::<Value>(data)| {
        on_connect_impl(socket, Data(data), tx_to_ecs).await;
    };

    io.ns("/", on_connect);

    let app = axum::Router::new()
        .route("/", get(|| async { "Hello, World!" }))
        .layer(layer);

    info!("Starting server.");

    let listener = tokio::net::TcpListener::bind("0.0.0.0:3000").await.unwrap();
    axum::serve(listener, app).await.unwrap();

    Ok(())
}
