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
    landing_time: f32,
    turn_delay: f32,
    turn_time: f32,
    snapshot_time: f32,
    teleport_time: f32,
    apocalyptic_ray_time: f32,
    leave_delay: f32,
    super_jump_radius: f32,
    stun_duration: f32,
    knockback_duration: f32,
    wormhole_radius: f32,
    hysteria_duration: f32,
    hysteria_redirection_interval: f32,
    wormhole_position: Vector2<f32>,
    exit_wormhole_position: Vector2<f32>,
    bj_position: Vector2<f32>,
    bj_rotation: f32,
    jump_direction: Vector2<f32>,
    target: Option<u64>,
    used_wormhole: bool,
    // Counts up from the jump, the turn and the snapshot can happen in either order
    elapsed: f32,
    landed: bool,
    turned: bool,
    teleport_start: Option<f32>,
    snapshotted: bool,
    apocalyptic_ray_start: Option<f32>,
    leave_start: Option<f32>,
    stage: Stage,
}

#[derive(Debug, Clone, Copy, PartialEq)]
enum Stage {
    Spawn,
    Cast,
    Resolve,
}

// Keep in sync with WormholeSuperJump.Phase on the client
#[derive(Debug, Clone, Copy)]
enum Phase {
    Spawn = 0,
    SuperJump = 1,
    Turn = 2,
    Snapshot = 3,
    ApocalypticRay = 4,
    Leave = 5,
    Teleport = 6,
}

const ARENA_CENTER: Vector2<f32> = Vector2::new(100.0, 100.0);
const BJ_SPAWN_DISTANCE: f32 = 23.0;
const WORMHOLE_DISTANCE: f32 = 17.0;
// The wall is 20y from the center and the exit wormhole has a 2.5y radius on the client, so all of
// it sits past the wall. Nobody can walk into it, it only shows where BJ comes out
const EXIT_WORMHOLE_DISTANCE: f32 = 24.0;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    // Wormhole goes either SE or SW
    let wormhole_direction = if rand::random_bool(0.5) {
        Vector2::new(1.0, 1.0).normalize()
    } else {
        Vector2::new(-1.0, 1.0).normalize()
    };

    entity.set(WormholeSuperJump {
        // Warp in, idle, then Super Jump's 3.9s cast. Long enough that he lands right after
        // the first Mega Holy
        cast_time: 7.5,
        // Windup and flight on the client
        landing_time: 1.0,
        // Lets his landing animation finish before he turns
        turn_delay: 1.0,
        turn_time: 1.0,
        // Dynamo and wormhole, counted from the landing
        snapshot_time: 3.0,
        teleport_time: 2.5,
        apocalyptic_ray_time: 5.0,
        // Stands around for a bit after the ray before warping out
        leave_delay: 2.0,
        // Same as the real Super Jump
        super_jump_radius: 10.0,
        stun_duration: 10.0,
        knockback_duration: 10.0,
        // Second stage wormhole, keep in sync with WormholeScale on the client
        wormhole_radius: 3.75,
        hysteria_duration: 15.0,
        hysteria_redirection_interval: 5.0,
        wormhole_position: ARENA_CENTER + WORMHOLE_DISTANCE * wormhole_direction,
        // Straight across, so a ray fired at the center from the wormhole points out of the arena
        exit_wormhole_position: ARENA_CENTER - EXIT_WORMHOLE_DISTANCE * wormhole_direction,
        // North, facing the center
        bj_position: ARENA_CENTER + Vector2::new(0.0, -BJ_SPAWN_DISTANCE),
        bj_rotation: 0.0,
        jump_direction: Vector2::new(0.0, 1.0),
        target: None,
        used_wormhole: false,
        elapsed: 0.0,
        landed: false,
        turned: false,
        teleport_start: None,
        snapshotted: false,
        apocalyptic_ray_start: None,
        leave_start: None,
        stage: Stage::Spawn,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut WormholeSuperJump, &Party)>()
        .each_iter(|it, index, (mechanic, wormhole, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            match wormhole.stage {
                Stage::Spawn => {
                    send_update(world, &party.id, wormhole, Phase::Spawn);
                    wormhole.stage = Stage::Cast;
                }

                Stage::Cast => {
                    wormhole.cast_time -= it.delta_time();
                    if wormhole.cast_time > 0.0 {
                        return;
                    }

                    // Jump on the farthest player from Brute Justice
                    let mut target: Option<(u64, Vector2<f32>, f32)> = None;
                    if let Some(pc) = find_party_container(world, &party.id) {
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &Position, &State)>(|(pl, p, s)| {
                                if !s.is_alive {
                                    return;
                                }

                                let player_position = Vector2::new(p.x, p.z);
                                let distance =
                                    wormhole.bj_position.metric_distance(&player_position);

                                if target.is_none_or(|(_, _, d)| distance > d) {
                                    target = Some((pl.content_id, player_position, distance));
                                }
                            });
                        });
                    }

                    // Lands right on top of them, so they can bait it into the wormhole
                    let Some((target_id, landing_position, _)) = target else {
                        send_update(world, &party.id, wormhole, Phase::Leave);
                        finish_mechanic(entity, mechanic, party);
                        return;
                    };

                    let to_landing = landing_position - wormhole.bj_position;
                    wormhole.bj_rotation = vector_to_rotation(to_landing.x, to_landing.y);
                    wormhole.jump_direction = to_landing.normalize();
                    wormhole.target = Some(target_id);
                    wormhole.bj_position = landing_position;
                    wormhole.used_wormhole = landing_position
                        .metric_distance(&wormhole.wormhole_position)
                        <= wormhole.wormhole_radius;

                    send_update(world, &party.id, wormhole, Phase::SuperJump);
                    wormhole.stage = Stage::Resolve;
                }

                Stage::Resolve => {
                    wormhole.elapsed += it.delta_time();
                    let elapsed = wormhole.elapsed;

                    if !wormhole.landed && elapsed >= wormhole.landing_time {
                        hit_super_jump(world, &party.id, wormhole);
                        wormhole.landed = true;
                    }

                    if !wormhole.turned && elapsed >= wormhole.landing_time + wormhole.turn_delay {
                        let to_center = ARENA_CENTER - wormhole.bj_position;
                        if to_center.norm() > 0.0 {
                            wormhole.bj_rotation = vector_to_rotation(to_center.x, to_center.y);
                        }
                        send_update(world, &party.id, wormhole, Phase::Turn);
                        wormhole.turned = true;
                        if wormhole.used_wormhole {
                            wormhole.teleport_start = Some(elapsed + wormhole.turn_time);
                        } else {
                            wormhole.apocalyptic_ray_start = Some(elapsed + wormhole.turn_time);
                        }
                    }

                    // He keeps facing the way he turned, which is out of the arena from the exit
                    if wormhole
                        .teleport_start
                        .is_some_and(|start| elapsed >= start)
                    {
                        wormhole.bj_position = wormhole.exit_wormhole_position;
                        send_update(world, &party.id, wormhole, Phase::Teleport);
                        wormhole.teleport_start = None;
                        wormhole.apocalyptic_ray_start = Some(elapsed + wormhole.teleport_time);
                    }

                    if !wormhole.snapshotted
                        && elapsed >= wormhole.landing_time + wormhole.snapshot_time
                    {
                        send_update(world, &party.id, wormhole, Phase::Snapshot);
                        if !wormhole.used_wormhole {
                            apply_hysteria(world, &party.id, wormhole);
                        }
                        wormhole.snapshotted = true;
                    }

                    if wormhole.leave_start.is_none()
                        && wormhole
                            .apocalyptic_ray_start
                            .is_some_and(|start| elapsed >= start)
                    {
                        send_update(world, &party.id, wormhole, Phase::ApocalypticRay);
                        wormhole.leave_start =
                            Some(elapsed + wormhole.apocalyptic_ray_time + wormhole.leave_delay);
                    }

                    if wormhole.snapshotted
                        && wormhole.leave_start.is_some_and(|start| elapsed >= start)
                    {
                        send_update(world, &party.id, wormhole, Phase::Leave);
                        finish_mechanic(entity, mechanic, party);
                    }
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

// Only the target is meant to take the jump, everyone else near them gets stunned and knocked away
fn hit_super_jump(world: &WorldRef, party_id: &String, wormhole: &WormholeSuperJump) {
    if let Some(pc) = find_party_container(world, party_id) {
        pc.each_child(|c| {
            c.try_get::<(&Player, &Position, &State)>(|(pl, p, s)| {
                if !s.is_alive || wormhole.target == Some(pl.content_id) {
                    return;
                }

                let from_landing = Vector2::new(p.x, p.z) - wormhole.bj_position;
                if from_landing.norm() > wormhole.super_jump_radius {
                    return;
                }

                // Standing right where he landed, push them the way he was jumping
                let knockback_direction = if from_landing.norm() > 0.0 {
                    from_landing.normalize()
                } else {
                    wormhole.jump_direction
                };

                let player = c.entity_view(world);
                apply_condition(
                    &player,
                    condition::Condition::Stun as u128,
                    condition::Condition::Stun,
                    wormhole.stun_duration,
                    false,
                );
                apply_condition(
                    &player,
                    condition::Condition::Knockback as u128,
                    condition::Condition::Knockback,
                    wormhole.knockback_duration,
                    true,
                )
                .entity_view(world)
                .set(conditions::Knockback {
                    knockback_direction_x: knockback_direction.x,
                    knockback_direction_z: knockback_direction.y,
                });
            });
        });
    }
}

// The wormhole blows up when nobody puts BJ in it
fn apply_hysteria(world: &WorldRef, party_id: &String, wormhole: &WormholeSuperJump) {
    if let Some(pc) = find_party_container(world, party_id) {
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
}

fn finish_mechanic(entity: EntityView<'_>, mechanic: &Mechanic, party: &Party) {
    info!(
        mechanic.request_id,
        mechanic.mechanic_id, party.id, "Completing Mechanic"
    );
    entity.remove(WormholeSuperJump::id());
}
