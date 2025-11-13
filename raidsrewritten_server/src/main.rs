mod ecs_container;
mod webserver;

use std::sync::mpsc;
use tracing_subscriber::FmtSubscriber;

enum MessageToEcs {
    Test,
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing::subscriber::set_global_default(FmtSubscriber::default())?;

    let (tx_to_ecs, rx_from_ws) = mpsc::channel::<MessageToEcs>();

    let (layer, io) = webserver::create_layer();

    ecs_container::run_ecs_container(rx_from_ws, &io);

    webserver::run_webserver(layer, io, tx_to_ecs)
        .await
        .unwrap();

    Ok(())
}
