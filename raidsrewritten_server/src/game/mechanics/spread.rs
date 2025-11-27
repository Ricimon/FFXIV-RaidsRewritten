use crate::{
    ecs_container::{Party, Player, Socket, SocketIoSingleton},
    game::{condition::Condition, mechanics::MechanicTimer},
    webserver::message::{Action, ApplyConditionPayload, Message, PlayVfxPayload},
};

use super::{Affect, Mechanic, Target};
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};
use tracing::info;

#[derive(Component, Debug)]
pub struct Spread {
    started: bool,
    snapshotted: bool,
    effect_delay: f32,
    omen_vfx_path: String,
    attack_vfx_path: String,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity
        .set(MechanicTimer {
            time_remaining: 1.0,
        })
        .set(Spread {
            started: false,
            snapshotted: false,
            effect_delay: 0.5,
            omen_vfx_path: "spread/omen/vfx/path".to_string(),
            attack_vfx_path: "spread/attack/vfx/path".to_string(),
        })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut MechanicTimer, &mut Spread, &Party)>()
        .each_iter(|it, index, (mechanic, timer, spread, party)| {
            let entity = it.entity(index);

            if !spread.started {
                spread.started = true;

                // Assign targets
                let mut targets: Vec<u64> = Vec::new();
                it.world()
                    .query::<(&Player, &Party)>()
                    .build()
                    .each_entity(|e, (pl, pa)| {
                        if party.id == pa.id {
                            entity.add((Target, e));
                            targets.push(pl.content_id);
                        }
                    });

                // Send omen vfx
                let omen_vfx_path = &spread.omen_vfx_path;
                it.world().get::<&SocketIoSingleton>(|sio| {
                    it.world()
                        .query::<(&Socket, &Party)>()
                        .build()
                        .each_entity(|_, (s, pa)| {
                            if party.id == pa.id {
                                send_play_vfx(
                                    sio.io.clone(),
                                    s.id,
                                    PlayVfxPayload {
                                        vfx_path: omen_vfx_path.clone(),
                                        targets: targets.clone(),
                                    },
                                );
                            }
                        });
                });
            }

            if !spread.snapshotted && timer.time_remaining < spread.effect_delay {
                spread.snapshotted = true;

                // Snapshot
                entity.each_target(Target, |e| {
                    // Testing: affect every target
                    entity.add((Affect, e));
                });
            }

            timer.time_remaining -= it.delta_time();

            if timer.time_remaining <= 0.0 {
                // Send effects
                it.world().get::<&SocketIoSingleton>(|sio| {
                    entity.each_target(Affect, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_apply_condition(sio.io.clone(), s.id);
                        });
                    });
                });

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Removing Mechanic"
                );
                entity.destruct();
            }
        });
}

fn send_play_vfx(io: SocketIo, socket_id: Sid, play_vfx_payload: PlayVfxPayload) {
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

fn send_apply_condition(io: SocketIo, socket_id: Sid) {
    info!(socket_str = socket_id.as_str(), "Sending apply_condition");
    tokio::spawn(async move {
        io.to(socket_id)
            .emit(
                "message",
                &Message {
                    action: Action::ApplyCondition,
                    apply_condition: Some(ApplyConditionPayload {
                        condition: Condition::Stun,
                        duration: 5.0,
                        ..Default::default()
                    }),
                    ..Default::default()
                },
            )
            .await
            .unwrap();
    });
}
