use crate::{
    ecs_container::{Player, SocketIoSingleton},
    game::mechanics::Target,
    webserver::message::{Action, ApplyConditionPayload, Message, PlayVfxPayload},
};
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};
use tracing::info;

pub fn get_target_ids(entity: &EntityView<'_>) -> Vec<u64> {
    let mut targets: Vec<u64> = Vec::new();
    entity.each_target(Target, |e| {
        e.try_get::<&Player>(|pl| {
            targets.push(pl.content_id);
        });
    });
    targets
}

pub fn get_socket_io(world: &WorldRef<'_>) -> SocketIo {
    world.get::<&SocketIoSingleton>(|sio| sio.io.clone())
}

pub fn send_play_vfx(io: SocketIo, socket_id: Sid, play_vfx_payload: PlayVfxPayload) {
    info!(
        socket_str = socket_id.as_str(),
        play_vfx_payload.vfx_path, "Sending play_vfx"
    );
    tokio::spawn(async move {
        io.to(socket_id)
            .emit(
                "message",
                &Message {
                    action: Action::PlayVfx,
                    play_vfx: Some(play_vfx_payload),
                    ..Default::default()
                },
            )
            .await
            .unwrap();
    });
}

pub fn send_apply_condition(
    io: SocketIo,
    socket_id: Sid,
    condition_payload: ApplyConditionPayload,
) {
    info!(socket_str = socket_id.as_str(), "Sending apply_condition");
    tokio::spawn(async move {
        io.to(socket_id)
            .emit(
                "message",
                &Message {
                    action: Action::ApplyCondition,
                    apply_condition: Some(condition_payload),
                    ..Default::default()
                },
            )
            .await
            .unwrap();
    });
}
