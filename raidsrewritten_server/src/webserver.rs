pub mod message;
pub mod metrics;

use crate::system_messages::MessageToEcs;
use crate::{game::components::*, webserver::metrics::*};
use axum::{Router, middleware};
use axum::{response::Html, routing::get};
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
    CONNECTED_CLIENTS.inc();
    CONNECTED_CLIENTS_TOTAL.inc();
    info!(ns = socket.ns(), ?socket.id, "Socket.IO connected");

    socket.join(socket.id);

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
    // info!(?socket.id, "Received message\n{:#?}", message);
    // socket.emit("message-back", &message).ok();

    match message.action {
        message::Action::UpdatePlayer => {
            if let Some(update_player) = message.update_player {
                tx.send(MessageToEcs::UpdatePlayer {
                    socket_id: socket.id,
                    content_id: update_player.content_id,
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
        message::Action::StartMechanic => {
            if let Some(start_mechanic) = message.start_mechanic {
                tx.send(MessageToEcs::StartMechanic {
                    socket_id: socket.id,
                    request_id: start_mechanic.request_id.clone(),
                    mechanic_id: start_mechanic.mechanic_id,
                    world_position_x: start_mechanic.world_position_x,
                    world_position_y: start_mechanic.world_position_y,
                    world_position_z: start_mechanic.world_position_z,
                    rotation: start_mechanic.rotation,
                })
                .unwrap();
            }
        }
        message::Action::ClearMechanics => {
            tx.send(MessageToEcs::ClearMechanics {
                socket_id: socket.id,
            })
            .unwrap();
        }
        _ => {}
    }
}

async fn on_message_with_ack(socket: SocketRef, Data(data): Data<Value>, ack: AckSender) {
    info!(?socket.id, ?data, "Received message-with-ack");
    ack.send(&data).ok();
}

async fn on_disconnect_impl(socket: SocketRef, reason: DisconnectReason, tx: Sender<MessageToEcs>) {
    CONNECTED_CLIENTS.dec();
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
    socket_layer: SocketIoLayer,
    io: SocketIo,
    tx_to_ecs: Sender<MessageToEcs>,
    world: World,
) -> Result<(), Box<dyn std::error::Error>> {
    metrics::init_metrics();

    // https://doc.rust-lang.org/book/ch16-03-shared-state.html#atomic-reference-counting-with-arct
    let world = Arc::new(Mutex::new(world));

    let on_connect = async |socket: SocketRef, Data::<Value>(data)| {
        on_connect_impl(socket, Data(data), tx_to_ecs).await;
    };

    io.ns("/", on_connect);

    let app = Router::new()
        .route("/", get(get_root))
        .route("/status", get(|| async move { get_status(&world) }))
        .layer(middleware::from_fn(metrics::metrics_middleware))
        .layer(socket_layer);

    let metrics_app = Router::new().route("/metrics", get(metrics::get_metrics));

    let listener = tokio::net::TcpListener::bind("0.0.0.0:3000").await.unwrap();
    let t_app = tokio::task::spawn(async move { axum::serve(listener, app).await.unwrap() });

    let listener_metrics = tokio::net::TcpListener::bind("0.0.0.0:3001").await.unwrap();
    let t_metrics = tokio::task::spawn(async move { axum::serve(listener_metrics, metrics_app).await.unwrap() });

    let _ = tokio::join!(t_app, t_metrics);

    Ok(())
}

async fn get_root() -> Html<String> {
    let mut builder = string_builder::Builder::default();
    builder.append("<h1>Welcome to the RaidsRewritten server.</h1>");
    builder.append("<p><a href=\"https://github.com/Ricimon/FFXIV-RaidsRewritten\">https://github.com/Ricimon/FFXIV-RaidsRewritten</a></p>");
    Html(builder.string().unwrap())
}

fn get_status(world: &Arc<Mutex<World>>) -> String {
    let mut builder = string_builder::Builder::default();
    let mut players = 0;
    let world = world.lock().unwrap();
    world.query::<(&Player, &Party)>().build().each(|(pl, pa)| {
        builder.append(format!("{} - {}\n", pa.id, pl.name));
        players += 1;
    });
    format!(
        "Players connected: {}\n\n{}",
        players,
        builder.string().unwrap()
    )
}
