use crate::{
    game::{
        components::*,
        condition::{self, apply_condition},
        utils::*,
    },
    webserver::message::{PlayActorVfxOnPositionPayload, PlayActorVfxOnTargetPayload},
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component, Debug)]
pub struct TeaFireTornado1 {
    cone_vfx: String,
    stack_vfx: String,
}

#[derive(Clone, Copy)]
struct Target {
    entity: Entity,
    content_id: u64,
    position: Position,
    distance: f32,
    hit_count: u32,
}

struct MechanicResults {
    stack_origins: Vec<u64>,
    cone_origins: Vec<Target>,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(TeaFireTornado1 {
        cone_vfx: "vfx/monster/gimmick3/eff/n4g6_b_g10cok1.avfx".to_string(),
        stack_vfx: "vfx/monster/gimmick4/eff/n5r4_b0_g02c0c.avfx".to_string(),
    })
}

fn handle_stacks(targets: &mut Vec<Target>) -> Vec<Target> {
    let mut origins: Vec<Target> = Vec::new();
    for t in targets.iter().rev() {
        if origins.len() < 2 {
            origins.push(*t);
        }
    }

    // find people within stack range
    for stack_origin in &origins {
        let mut stack: Vec<&mut Target> = Vec::new();
        for player in targets.iter_mut() {
            let p1 = [stack_origin.position.x, stack_origin.position.z];
            let p2 = [player.position.x, player.position.z];
            let distance: f64 = euclidean_sq(&p1, &p2);
            if distance.sqrt() > 6.0 {
                continue;
            }
            stack.push(player);
        }

        let to_add = if stack.len() > 2 { 1 } else { 2 };
        for player in stack {
            player.hit_count += to_add;
        }
    }

    origins
}

fn handle_cones(targets: &mut Vec<Target>, position: &Position) -> Vec<Target> {
    let mut origins: Vec<Target> = Vec::new();
    for t in targets.iter() {
        if origins.len() < 2 {
            origins.push(*t);
        }
    }

    let half_cone = (90.0f32 / 2.0).to_radians();

    // find people who are hit by cones
    for cone_origin in &origins {
        let rotation = vector_to_rotation(
            cone_origin.position.x - position.x,
            cone_origin.position.z - position.z,
        );
        let rotation_angle = [position.x + rotation.sin(), position.z + rotation.cos()];

        for player in targets.iter_mut() {
            let angle = get_angle_between_lines(
                [position.x, position.z],
                [player.position.x, player.position.z],
                [position.x, position.z],
                rotation_angle,
            );
            if angle <= half_cone || angle.is_nan() {
                player.hit_count += 1;
            }
        }
    }

    origins
}

fn handle_mechanics(targets: &mut Vec<Target>, position: &Position) -> MechanicResults {
    let stack_result = handle_stacks(targets);
    let cone_result = handle_cones(targets, position);

    MechanicResults {
        stack_origins: stack_result.iter().map(|t| t.content_id).collect(),
        cone_origins: cone_result,
    }
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &TeaFireTornado1, &Position, &Party)>()
        .each_iter(|it, index, (mechanic, fire_tornado, position, party)| {
            let entity = it.entity(index);

            let world = it.world();

            if let Some(pc) = find_party_container(&world, &party.id) {
                let mut targets: Vec<Target> = Vec::new();
                pc.each_child(|c| {
                    c.try_get::<(&Player, &Position, &State)>(|(pl, p, s)| {
                        if !s.is_alive {
                            return;
                        }

                        let p1 = [position.x, position.z];
                        let p2 = [p.x, p.z];
                        let distance_sq: f32 = euclidean_sq(&p1, &p2);

                        targets.push(Target {
                            entity: *c,
                            content_id: pl.content_id,
                            position: *p,
                            distance: distance_sq,
                            hit_count: 0,
                        });
                    });
                });

                targets.sort_unstable_by(|a, b| a.distance.total_cmp(&b.distance));

                let mechanic_results = handle_mechanics(&mut targets, &position);

                let io = get_socket_io(&it.world());
                pc.each_child(|c| {
                    c.try_get::<&Socket>(|s| {
                        for t in &mechanic_results.cone_origins {
                            let r = vector_to_rotation(
                                t.position.x - position.x,
                                t.position.z - position.z,
                            );
                            send_play_actor_vfx_on_position(
                                io.clone(),
                                s.id,
                                PlayActorVfxOnPositionPayload {
                                    vfx_path: fire_tornado.cone_vfx.clone(),
                                    world_position_x: position.x,
                                    world_position_y: position.y,
                                    world_position_z: position.z,
                                    rotation: r,
                                },
                            );
                        }

                        send_play_actor_vfx_on_target(
                            io.clone(),
                            s.id,
                            PlayActorVfxOnTargetPayload {
                                vfx_path: fire_tornado.stack_vfx.clone(),
                                content_id_targets: mechanic_results.stack_origins.clone(),
                                ..Default::default()
                            },
                        );
                    });
                });

                let mut to_punish: bool = false;
                for t in targets {
                    if t.hit_count < 2 {
                        continue;
                    }
                    to_punish = true;
                    let player = t.entity.entity_view(world);
                    apply_condition(
                        &player,
                        condition::Condition::Stun as u128,
                        condition::Condition::Stun,
                        15.0,
                        false,
                    );
                }

                if to_punish {
                    pc.add(BroadcastConditions);
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(TeaFireTornado1::id());
        });
}
