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
            effect_delay: 0.0,
            omen_vfx_path: "vfx/lockon/eff/target_ae_s5f.avfx".to_string(),
            attack_vfx_path: "vfx/monster/gimmick4/eff/n5r8_b_g15_t0k1.avfx".to_string(),
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
                it.world().get::<&SocketIoSingleton>(|sio| {
                    // The mechanic entity's "Target" relationships cannot be used here as operations to the ECS system are deferred (only show up next tick).
                    for target in targets.iter() {
                        it.world()
                            .query::<(&Socket, &Player)>()
                            .build()
                            .each(|(s, pl)| {
                                if *target == pl.content_id {
                                    send_play_vfx(
                                        sio.io.clone(),
                                        s.id,
                                        PlayVfxPayload {
                                            vfx_path: spread.omen_vfx_path.clone(),
                                            targets: targets.clone(),
                                        },
                                    );
                                }
                            });
                    }
                });
            }

            timer.time_remaining -= it.delta_time();

            if !spread.snapshotted && timer.time_remaining <= spread.effect_delay {
                spread.snapshotted = true;

                // Snapshot
                entity.each_target(Target, |e| {
                    // Testing: affect every target
                    entity.add((Affect, e));
                });
            }
            // Snapshot must run before the mechanic can be completed
            else if timer.time_remaining <= 0.0 {
                it.world().get::<&SocketIoSingleton>(|sio| {
                    // Send attack vfx
                    let mut targets: Vec<u64> = Vec::new();
                    entity.each_target(Target, |e| {
                        e.try_get::<&Player>(|pl| {
                            targets.push(pl.content_id);
                        });
                    });
                    entity.each_target(Target, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_play_vfx(
                                sio.io.clone(),
                                s.id,
                                PlayVfxPayload {
                                    vfx_path: spread.attack_vfx_path.clone(),
                                    targets: targets.clone(),
                                },
                            );
                        });
                    });

                    // Send effects
                    entity.each_target(Affect, |e| {
                        e.try_get::<&Socket>(|s| {
                            send_apply_condition(sio.io.clone(), s.id);
                        });
                    });
                });

                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Completing Mechanic"
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
