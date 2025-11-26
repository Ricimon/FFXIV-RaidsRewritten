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
// The webserver handles http and websocket callback events.
// The ECS system runs an update loop on its own thread that progresses the World every tick interval.
// socketioxide SocketIo object can be cheaply cloned, so it's directly cloned and used in the ECS system.
// The ECS World can also used in the webserver through an Arc type.
// However, this is only used when retrieving data from the ECS system for an HTTP request as this does lock the World to the accessing thread.
// For most ECS operations that are triggered from a webserver event, messages are sent through a Channel.
// The ECS system can then pick up these messages every game tick to process and affect the ECS World.

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing::subscriber::set_global_default(FmtSubscriber::default())?;

    let (tx_to_ecs, rx_from_ws) = mpsc::channel::<MessageToEcs>();

    let (layer, io) = webserver::create_layer();
    let world = ecs_container::create_world();

    ecs_container::run_world(world.clone(), rx_from_ws, &io);

    // The webserver occupies the main thread
    webserver::run_webserver(layer, io, tx_to_ecs, world)
        .await
        .unwrap();

    Ok(())
}
