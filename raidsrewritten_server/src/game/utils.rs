use crate::{
    game::{components::*, mechanics::{Target, Transform}},
    webserver::message::*,
};
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};
use tracing::info;

pub fn convert_to_transform(
    x: Option<f32>,
    y: Option<f32>,
    z: Option<f32>,
    rotation: Option<f32>,
) -> Option<Transform> {
    if let Some(x) = x
        && let Some(y) = y
        && let Some(z) = z
        && let Some(r) = rotation
    {
        Some(Transform {
            x,
            y,
            z,
            rotation: r,
        })
    } else {
        None
    }
}

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

pub fn find_party_container<'a>(world: &World, party: &String) -> Option<EntityView<'a>> {
    world
        .query::<&Party>()
        .with(&PartyContainer)
        .build()
        .find(|p| p.id == *party)
}

pub fn send_apply_condition(
    io: SocketIo,
    socket_id: Sid,
    condition_payload: ApplyConditionPayload,
) {
    info!(socket_str = socket_id.as_str(), "Sending apply_condition");
    send_message(
        io,
        socket_id,
        Message {
            action: Action::ApplyCondition,
            apply_condition: Some(condition_payload),
            ..Default::default()
        },
    );
}

pub fn send_play_static_vfx(io: SocketIo, socket_id: Sid, payload: PlayStaticVfxPayload) {
    info!(
        socket_str = socket_id.as_str(),
        payload.id, payload.vfx_path, "Sending play_static_vfx"
    );
    send_message(
        io,
        socket_id,
        Message {
            action: Action::PlayStaticVfx,
            play_static_vfx: Some(payload),
            ..Default::default()
        },
    );
}

pub fn send_play_actor_vfx_on_target(
    io: SocketIo,
    socket_id: Sid,
    payload: PlayActorVfxOnTargetPayload,
) {
    info!(
        socket_str = socket_id.as_str(),
        payload.vfx_path, "Sending play_actor_vfx_on_target"
    );
    send_message(
        io,
        socket_id,
        Message {
            action: Action::PlayActorVfxOnTarget,
            play_actor_vfx_on_target: Some(payload),
            ..Default::default()
        },
    );
}

pub fn send_play_actor_vfx_on_position(
    io: SocketIo,
    socket_id: Sid,
    payload: PlayActorVfxOnPositionPayload,
) {
    info!(
        socket_str = socket_id.as_str(),
        payload.vfx_path, "Sending play_actor_vfx_on_position"
    );
    send_message(
        io,
        socket_id,
        Message {
            action: Action::PlayActorVfxOnPosition,
            play_actor_vfx_on_position: Some(payload),
            ..Default::default()
        },
    );
}

pub fn send_stop_vfx(io: SocketIo, socket_id: Sid, payload: StopVfxPayload) {
    info!(
        socket_str = socket_id.as_str(),
        payload.id, "Sending stop_vfx"
    );
    send_message(
        io,
        socket_id,
        Message {
            action: Action::StopVfx,
            stop_vfx: Some(payload),
            ..Default::default()
        },
    );
}

fn send_message(io: SocketIo, socket_id: Sid, message: Message) {
    tokio::spawn(async move {
        io.to(socket_id).emit("message", &message).await.unwrap();
    });
}
