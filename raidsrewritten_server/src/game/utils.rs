use crate::{game::components::*, webserver::message::*};
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};
use std::collections::HashMap;
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

pub fn get_entity_view<'a>(entity: &Entity, world: &WorldRef<'a>) -> Option<EntityView<'a>> {
    let ev = entity.entity_view(world);
    if ev.is_valid() { Some(ev) } else { None }
}

pub fn get_target_ids(entity: &EntityView<'_>) -> Vec<u64> {
    let mut targets: Vec<u64> = Vec::new();
    entity.try_get::<&Targets>(|t| {
        for e in &t.player_entities {
            if let Some(ev) = get_entity_view(e, &entity.world()) {
                ev.try_get::<&Player>(|pl| {
                    targets.push(pl.content_id);
                });
            }
        }
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

pub fn add_affect(affects: &mut HashMap<Entity, u8>, entity: &Entity, affect_count: u8) {
    if let Some(c) = affects.get_mut(entity) {
        *c += affect_count;
    } else {
        affects.insert(*entity, affect_count);
    }
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
