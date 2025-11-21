mod ecs_container;
mod game;
mod system_messages;
mod webserver;

use crate::system_messages::MessageToEcs;
use std::sync::mpsc;
use tracing_subscriber::FmtSubscriber;

// The server operates on two primary systems:
// - socketioxide for the webserver logic
// - flecs_ecs for the game server logic
// The ECS system runs an update loop on its own thread, with the ownership of the ECS world kept on that thread.
// Therefore, for the webserver system to affect the ECS system, it needs to send information through a Channel.
// The ECS system can then pick up these messages every game tick to process and affect the ECS world.
// Conversely, the socketioxide SocketIo object can be cheaply cloned, so it's directly cloned and used in the ECS system.

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing::subscriber::set_global_default(FmtSubscriber::default())?;

    let (tx_to_ecs, rx_from_ws) = mpsc::channel::<MessageToEcs>();

    let (layer, io) = webserver::create_layer();

    ecs_container::run_ecs_container(rx_from_ws, &io);

    // The webserver occupies the main thread
    webserver::run_webserver(layer, io, tx_to_ecs)
        .await
        .unwrap();

    Ok(())
}
