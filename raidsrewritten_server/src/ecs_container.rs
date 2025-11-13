use crate::MessageToEcs;
use flecs_ecs::prelude::*;
use socketioxide::SocketIo;
use std::sync::mpsc::Receiver;
use std::time::Duration;
use tokio::time;
use tracing::info;

#[derive(Component)]
pub struct TestComponent;

pub fn run_ecs_container(rx_from_ws: Receiver<MessageToEcs>, io: &SocketIo) {
    let world = World::new();

    // let _ = world.system::<()>().each(|_| {
    //     info!("ECS system tick");
    // });

    let io1 = io.clone();
    system!(world, TestComponent).each_iter(move |it, index, _test| {
        info!("ECS tick on TestComponent");
        let e = it.entity(index);
        e.destruct();

        let io1 = io1.clone();
        tokio::spawn(async move {
            io1.emit("message", "This is from ECS").await.unwrap();
        });
    });

    // world.entity().add(TestComponent);

    let _ = tokio::spawn(async move {
        let mut interval = time::interval(Duration::from_micros(1_000_000 / 64));

        loop {
            interval.tick().await;

            for message in rx_from_ws.try_iter() {
                match message {
                    MessageToEcs::Test => {
                        world.entity().add(TestComponent);
                    }
                }
            }

            world.progress();
        }
    });
}
