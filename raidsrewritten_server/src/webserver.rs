pub mod message;

use crate::MessageToEcs;
use crate::game::role::Role;
use axum::routing::get;
use rmpv::Value::{self, Map};
use socketioxide::{
    SocketIo,
    extract::{AckSender, Data, SocketRef},
    layer::SocketIoLayer,
    socket::DisconnectReason,
};
use std::convert::{TryFrom, TryInto};
use std::sync::mpsc::Sender;
use tracing::info;

async fn on_connect_impl(
    socket: SocketRef,
    Data(_data): Data<Value>,
    tx_to_ecs: Sender<MessageToEcs>,
) {
    info!(ns = socket.ns(), ?socket.id, "Socket.IO connected");

    let tx = tx_to_ecs.clone();
    let on_message = async |socket: SocketRef, Data::<Value>(data)| {
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

async fn on_message_impl(socket: SocketRef, Data(data): Data<Value>, tx: Sender<MessageToEcs>) {
    info!(?socket.id, "Received message\n{:#?}", data);
    socket.emit("message-back", &data).ok();

    if let Map(vec) = data {
        let mut iter = vec.iter();
        if let Some(action_number) = iter.find_map(|x| {
            if (&x).0.as_str() == Some("a")
                && let Some(i) = (&x).1.as_u64()
            {
                return u32::try_from(i).ok();
            } else {
                return None;
            }
        }) {
            match action_number.try_into() {
                Ok(message::Action::UpdatePlayer) => {
                    info!("Update Player received!");

                    tx.send(MessageToEcs::UpdatePlayer {
                        socket_id: socket.id,
                        content_id: 0,
                        name: "invalid".to_string(),
                        role: Role::None,
                        party: "invalid".to_string(),
                    })
                    .unwrap();
                }
                _ => {}
            }
        }
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
