use crate::{
    game::{
        components::*,
        condition::{self, apply_condition},
        utils::*,
    },
    webserver::{message::RunMechanicCommandPayload, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::info;

#[derive(Component, Debug)]
pub struct WormholeSuperJump {
    cast_time: f32,
    jump_time: f32,
    teleport_time: f32,
    turn_time: f32,
    apocalyptic_ray_time: f32,
    wormhole_radius: f32,
    hysteria_duration: f32,
    hysteria_redirection_interval: f32,
    wormhole_position: Vector2<f32>,
    exit_wormhole_position: Vector2<f32>,
    bj_position: Vector2<f32>,
    bj_rotation: f32,
    used_wormhole: bool,
    phase: Phase,
}

// Keep in sync with WormholeSuperJump.Phase on the client
#[derive(Debug, Clone, Copy)]
enum Phase {
    Spawn = 0,
    SuperJump = 1,
    Teleport = 2,
    ApocalypticRay = 3,
    Leave = 4,
}

const ARENA_CENTER: Vector2<f32> = Vector2::new(100.0, 100.0);
const BJ_SPAWN_DISTANCE: f32 = 23.0;
const WORMHOLE_DISTANCE: f32 = 17.0;
// Outside the wall so nobody can walk into it, it's only there to show where BJ comes out
const EXIT_WORMHOLE_DISTANCE: f32 = 23.0;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    // Wormhole goes either SE or SW
    let wormhole_direction = if rand::random_bool(0.5) {
        Vector2::new(1.0, 1.0).normalize()
    } else {
        Vector2::new(-1.0, 1.0).normalize()
    };

    entity.set(WormholeSuperJump {
        cast_time: 5.0,
        jump_time: 1.5,
        teleport_time: 2.5,
        turn_time: 1.0,
        apocalyptic_ray_time: 5.0,
        wormhole_radius: 5.0,
        hysteria_duration: 15.0,
        hysteria_redirection_interval: 5.0,
        wormhole_position: ARENA_CENTER + WORMHOLE_DISTANCE * wormhole_direction,
        exit_wormhole_position: ARENA_CENTER - EXIT_WORMHOLE_DISTANCE * wormhole_direction,
        // North, facing the center
        bj_position: ARENA_CENTER + Vector2::new(0.0, -BJ_SPAWN_DISTANCE),
        bj_rotation: 0.0,
        used_wormhole: false,
        phase: Phase::Spawn,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut WormholeSuperJump, &Party)>()
        .each_iter(|it, index, (mechanic, wormhole, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            match wormhole.phase {
                Phase::Spawn => {
                    send_update(world, &party.id, wormhole, Phase::Spawn);
                    wormhole.phase = Phase::SuperJump;
                }

                Phase::SuperJump => {
                    wormhole.cast_time -= it.delta_time();
                    if wormhole.cast_time > 0.0 {
                        return;
                    }

                    // Jump on the farthest player from Brute Justice
                    let mut target: Option<(Vector2<f32>, f32)> = None;
                    if let Some(pc) = find_party_container(world, &party.id) {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &Position, &State)>(|(_, p, s)| {
                                if !s.is_alive {
                                    return;
                                }

                                let player_position = Vector2::new(p.x, p.z);
                                let distance =
                                    wormhole.bj_position.metric_distance(&player_position);

                                if target.is_none_or(|(_, d)| distance > d) {
                                    target = Some((player_position, distance));
                                }
                            });
                        });
                    }

                    // Lands right on top of them, so they can bait it into the wormhole
                    let Some((landing_position, _)) = target else {
                        finish_mechanic(entity, mechanic, party);
                        return;
                    };

                    let to_landing = landing_position - wormhole.bj_position;
                    wormhole.bj_rotation = vector_to_rotation(to_landing.x, to_landing.y);
                    wormhole.bj_position = landing_position;
                    wormhole.used_wormhole = landing_position
                        .metric_distance(&wormhole.wormhole_position)
                        <= wormhole.wormhole_radius;

                    send_update(world, &party.id, wormhole, Phase::SuperJump);
                    wormhole.phase = Phase::Teleport;
                }

                Phase::Teleport => {
                    wormhole.jump_time -= it.delta_time();
                    if wormhole.jump_time > 0.0 {
                        return;
                    }

                    if wormhole.used_wormhole {
                        // He comes out of the exit wormhole, facing away from the arena
                        let from_center = wormhole.exit_wormhole_position - ARENA_CENTER;
                        wormhole.bj_position = wormhole.exit_wormhole_position;
                        wormhole.bj_rotation = vector_to_rotation(from_center.x, from_center.y);
                        send_update(world, &party.id, wormhole, Phase::Teleport);
                    } else {
                        // No teleport, he just turns to the center before firing
                        wormhole.teleport_time = wormhole.turn_time;
                        let to_center = ARENA_CENTER - wormhole.bj_position;
                        if to_center.norm() > 0.0 {
                            wormhole.bj_rotation = vector_to_rotation(to_center.x, to_center.y);
                        }
                        send_update(world, &party.id, wormhole, Phase::Teleport);
                    }

                    wormhole.phase = Phase::ApocalypticRay;
                }

                Phase::ApocalypticRay => {
                    wormhole.teleport_time -= it.delta_time();
                    if wormhole.teleport_time > 0.0 {
                        return;
                    }

                    send_update(world, &party.id, wormhole, Phase::ApocalypticRay);
                    wormhole.phase = Phase::Leave;
                }

                Phase::Leave => {
                    wormhole.apocalyptic_ray_time -= it.delta_time();
                    if wormhole.apocalyptic_ray_time > 0.0 {
                        return;
                    }

                    send_update(world, &party.id, wormhole, Phase::Leave);

                    if !wormhole.used_wormhole
                        && let Some(pc) = find_party_container(world, &party.id)
                    {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &State)>(|(_, s)| {
                                if !s.is_alive {
                                    return;
                                }
                                let player = c.entity_view(world);
                                apply_condition(
                                    &player,
                                    condition::Condition::Hysteria as u128,
                                    condition::Condition::Hysteria,
                                    wormhole.hysteria_duration,
                                    false,
                                )
                                .entity_view(world)
                                .set(conditions::Hysteria {
                                    redirection_interval: wormhole.hysteria_redirection_interval,
                                });
                            });
                        });
                    }

                    finish_mechanic(entity, mechanic, party);
                }
            }
        });
}

fn send_update(world: &WorldRef, party_id: &String, wormhole: &WormholeSuperJump, phase: Phase) {
    if let Some(pc) = find_party_container(world, party_id) {
        let io = get_socket_io(world);
        pc.each_child(|c| {
            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                send_run_mechanic_command(
                    io.clone(),
                    s.id,
                    RunMechanicCommandPayload {
                        mechanic_command_id: NetworkMechanicCommand::TeaUpdateWormholeSuperJump
                            as i32,
                        world_position_x: Some(wormhole.bj_position.x),
                        world_position_y: Some(0.0),
                        world_position_z: Some(wormhole.bj_position.y),
                        rotation: Some(wormhole.bj_rotation),
                        extra_data: Some(format!(
                            "{},{},{},{},{},{},{}",
                            phase as i32,
                            wormhole.wormhole_position.x,
                            wormhole.wormhole_position.y,
                            wormhole.exit_wormhole_position.x,
                            wormhole.exit_wormhole_position.y,
                            wormhole.used_wormhole as i32,
                            wormhole.apocalyptic_ray_time,
                        )),
                    },
                );
            });
        });
    }
}

fn finish_mechanic(entity: EntityView<'_>, mechanic: &Mechanic, party: &Party) {
    info!(
        mechanic.request_id,
        mechanic.mechanic_id, party.id, "Completing Mechanic"
    );
    entity.remove(WormholeSuperJump::id());
}
