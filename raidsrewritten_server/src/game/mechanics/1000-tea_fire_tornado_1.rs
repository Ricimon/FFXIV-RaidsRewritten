use crate::{
    game::{
        components::*,
        condition::{self, Condition::Stun},
        utils::*,
    },
    webserver::message::{PlayActorVfxOnPositionPayload, PlayActorVfxOnTargetPayload},
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use std::collections::HashSet;
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
    // hit_count: u32,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(TeaFireTornado1 {
        cone_vfx: "vfx/monster/gimmick3/eff/n4g6_b_g10cok1.avfx".to_string(),
        stack_vfx: "vfx/monster/gimmick4/eff/n5r4_b0_g02c0c.avfx".to_string(),
    })
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
                            // hit_count: 0,
                        });
                    });
                });

                targets.sort_unstable_by(|a, b| a.distance.total_cmp(&b.distance));

                let mut cone_targets: Vec<Target> = Vec::new();
                for t in &targets {
                    if cone_targets.len() < 2 {
                        cone_targets.push(*t);
                    }
                }

                let mut stack_targets: Vec<Target> = Vec::new();
                for t in targets.iter().rev() {
                    if stack_targets.len() < 2 {
                        stack_targets.push(*t);
                    }
                }

                let stack_target_ids: Vec<u64> =
                    stack_targets.iter().map(|t| t.content_id).collect();

                let mut stacks: Vec<Vec<Target>> = Vec::new();

                for stack_target in &stack_targets {
                    let mut stack: Vec<Target> = Vec::new();

                    for player in &targets {
                        let p1 = [stack_target.position.x, stack_target.position.z];
                        let p2 = [player.position.x, player.position.z];
                        let distance: f64 = euclidean_sq(&p1, &p2);
                        if distance.sqrt() > 6.0 {
                            continue;
                        }
                        stack.push(*player);
                    }
                    stacks.push(stack);
                }

                let mut intersects: Vec<Target> = Vec::new();

                if stacks.len() > 1 {
                    intersects = stacks[0]
                        .iter()
                        .filter(|player| {
                            stacks[1].iter().any(|p| p.content_id == player.content_id)
                        })
                        .copied()
                        .collect();
                }

                let io = get_socket_io(&it.world());
                pc.each_child(|c| {
                    c.try_get::<&Socket>(|s| {
                        for t in &cone_targets {
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
                                content_id_targets: stack_target_ids.clone(),
                                ..Default::default()
                            },
                        );
                    });
                });

                let mut failed_stacks: Vec<Target> = Vec::new();
                for stack in stacks {
                    if stack.len() < 3 {
                        failed_stacks.extend(stack);
                    }
                }

                let half_cone = (90.0f32 / 2.0).to_radians();

                let mut cone_hits: Vec<Target> = Vec::new();
                for cone_target in &cone_targets {
                    let rotation = vector_to_rotation(
                        cone_target.position.x - position.x,
                        cone_target.position.z - position.z,
                    );
                    let rotation_angle = [position.x + rotation.sin(), position.z + rotation.cos()];

                    for player in &targets {
                        if player.content_id == cone_target.content_id {
                            continue;
                        }
                        let angle = get_angle_between_lines(
                            [position.x, position.z],
                            [player.position.x, player.position.z],
                            [position.x, position.z],
                            rotation_angle,
                        );
                        if angle <= half_cone || angle.is_nan() {
                            cone_hits.push(*player);
                        }
                    }
                }

                let mut punished_ids: HashSet<u64> = HashSet::new();
                for t in failed_stacks.into_iter().chain(intersects).chain(cone_hits) {
                    if punished_ids.insert(t.content_id) {
                        world
                            .entity()
                            .set(Condition {
                                id: condition::Condition::Stun as u128,
                                condition: Stun,
                                time_remaining: 15f32,
                            })
                            .child_of(t.entity.entity_view(world));
                    }
                }

                if !punished_ids.is_empty() {
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
