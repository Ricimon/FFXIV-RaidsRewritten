use crate::{
    game::{components::*, mechanics::m1020_tea_spawn_shanoa::TeaShanoa, utils::*},
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::info;

#[derive(Component, Debug)]
struct FireTornadoAttackShanoa {
    omen_duration: f32,
    distance_threshold: f32,
    attack_sent: bool,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(FireTornadoAttackShanoa {
        omen_duration: 10.0,
        distance_threshold: 10.0,
        attack_sent: false,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut FireTornadoAttackShanoa, &Position, &Party)>()
        .each_iter(|it, index, (mechanic, attack, position, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            if !attack.attack_sent {
                attack.attack_sent = true;

                if let Some(pc) = find_party_container(world, &party.id) {
                    let io = get_socket_io(world);
                    pc.each_child(|c| {
                        c.try_get::<(&Socket, &Player)>(|(s, _)| {
                            send_run_mechanic_command(
                                io.clone(),
                                s.id,
                                RunMechanicCommandPayload {
                                    mechanic_command_id:
                                        NetworkMechanicCommand::TeaFireTornadoAttackShanoa as i32,
                                    extra_data: Some(format!(
                                        "{},{}",
                                        attack.omen_duration, attack.distance_threshold
                                    )),
                                    ..Default::default()
                                },
                            );
                        });
                    });
                }
            }

            attack.omen_duration -= it.delta_time();

            if attack.omen_duration > 0.0 {
                return;
            }

            // Attack Shanoa
            world
                .query::<(&TeaShanoa, &Position, &Party)>()
                .build()
                .each_entity(|e, (shanoa, shanoa_position, p)| {
                    if p.id != party.id {
                        return;
                    }
                    let shanoa_position = Vector2::new(shanoa_position.x, shanoa_position.z);
                    let fire_tornado_position = Vector2::new(position.x, position.z);
                    if shanoa_position.metric_distance(&fire_tornado_position)
                        <= attack.distance_threshold
                    {
                        e.destruct();

                        if let Some(pc) = find_party_container(world, &party.id) {
                            let io = get_socket_io(world);
                            pc.each_child(|c| {
                                c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                    send_run_mechanic_command(
                                        io.clone(),
                                        s.id,
                                        RunMechanicCommandPayload {
                                            mechanic_command_id:
                                                NetworkMechanicCommand::TeaShanoaRunsAway as i32,
                                            extra_data: Some(format!(
                                                "{},{}",
                                                shanoa.movement_speed, shanoa.rotation_speed
                                            )),
                                            ..Default::default()
                                        },
                                    );
                                });
                            });
                        }
                    }
                });

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(FireTornadoAttackShanoa::id());
        });
}
