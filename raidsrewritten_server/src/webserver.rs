use crate::MessageToEcs;
use axum::routing::get;
use rmpv::Value;
use socketioxide::{
    extract::{AckSender, Data, SocketRef},
    layer::SocketIoLayer,
    socket::DisconnectReason,
    SocketIo,
};
use std::sync::mpsc::Sender;
use tracing::info;

async fn on_connect_impl(
    socket: SocketRef,
    Data(data): Data<Value>,
    tx_to_ecs: Sender<MessageToEcs>,
) {
    info!(ns = socket.ns(), ?socket.id, "Socket.IO connected");

    tx_to_ecs.send(MessageToEcs::Test).unwrap();

    socket.emit("auth", &data).ok();

    socket.on("message", on_message);
    socket.on("message-with-ack", on_message_with_ack);
    socket.on_disconnect(on_disconnect);
}

async fn on_message(socket: SocketRef, Data(data): Data<Value>) {
    info!(?socket.id, ?data, "Received message");
    socket.emit("message-back", &data).ok();
}

async fn on_message_with_ack(socket: SocketRef, Data(data): Data<Value>, ack: AckSender) {
    info!(?socket.id, ?data, "Received message-with-ack");
    ack.send(&data).ok();
}

async fn on_disconnect(socket: SocketRef, reason: DisconnectReason) {
    info!(?socket.id, ?reason, "Socket disconnected");
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
