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
use std::collections::HashSet;
use std::collections::HashMap;
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

struct MechanicResults {
    origins: Vec<Target>,
    players_affected: Vec<Vec<Target>>,
}

struct CombinedMechanicResults {
    stack_origins: Vec<u64>,
    cone_origins: Vec<Target>,
    to_punish: Vec<Target>
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(TeaFireTornado1 {
        cone_vfx: "vfx/monster/gimmick3/eff/n4g6_b_g10cok1.avfx".to_string(),
        stack_vfx: "vfx/monster/gimmick4/eff/n5r4_b0_g02c0c.avfx".to_string(),
    })
}

fn handle_stacks(targets: &Vec<Target>) -> MechanicResults{
    let mut origins: Vec<Target> = Vec::new();
    for t in targets.iter().rev() {
        if origins.len() < 2 {
            origins.push(*t);
        }
    }

    // find people within stack range
    let mut stacks: Vec<Vec<Target>> = Vec::new();
    for stack_origin in &origins {
        let mut stack: Vec<Target> = Vec::new();

        for player in targets {
            let p1 = [stack_origin.position.x, stack_origin.position.z];
            let p2 = [player.position.x, player.position.z];
            let distance: f64 = euclidean_sq(&p1, &p2);
            if distance.sqrt() > 6.0 {
                continue;
            }
            stack.push(*player);
        }
        stacks.push(stack);
    }

    MechanicResults {origins, players_affected: stacks}
}

fn handle_cones(targets: &Vec<Target>, position: &Position) -> MechanicResults{
    let mut origins: Vec<Target> = Vec::new();
    for t in targets {
        if origins.len() < 2 {
            origins.push(*t);
        }
    }
    
    let half_cone = (90.0f32 / 2.0).to_radians();

    // find people who are hit by cones
    let mut cone_hits: Vec<Vec<Target>> = Vec::new();
    for cone_origin in &origins {
        let mut cone: Vec<Target> = Vec::new();
        let rotation = vector_to_rotation(
            cone_origin.position.x - position.x,
            cone_origin.position.z - position.z,
        );
        let rotation_angle = [position.x + rotation.sin(), position.z + rotation.cos()];

        for player in targets {
            let angle = get_angle_between_lines(
                [position.x, position.z],
                [player.position.x, player.position.z],
                [position.x, position.z],
                rotation_angle,
            );
            if angle <= half_cone || angle.is_nan() {
                cone.push(*player)
            }
        }
        cone_hits.push(cone);
    }

    MechanicResults {origins, players_affected: cone_hits}
}

fn handle_mechanics(targets: &Vec<Target>, position: &Position) -> CombinedMechanicResults {
    let stack_result = handle_stacks(&targets);
    let cone_result  = handle_cones(&targets, position);

    let mut seen: HashMap<u64, bool> = HashMap::new();  // true = already been added to punish vector
    let mut to_punish: Vec<Target> = Vec::new();

    if stack_result.players_affected.len() < 2 || cone_result.players_affected.len() < 2 {
        return CombinedMechanicResults {
            cone_origins: Vec::new(),
            stack_origins: Vec::new(),
            to_punish: Vec::new()
        };
    }

    let affected_players = stack_result.players_affected[0].iter()
        .chain(stack_result.players_affected[1].iter())
        .chain(cone_result.players_affected[0].iter())
        .chain(cone_result.players_affected[1].iter());

    // punish failed stacks
    for stack in &stack_result.players_affected {
        if stack.len() < 3 {
            for player in stack {
                if !seen.contains_key(&player.content_id) {
                    seen.insert(player.content_id, true);
                    to_punish.push(*player)
                }
            }
        }
    }

    // punish players in more than 1 mechanic
    for player in affected_players {
        if !seen.contains_key(&player.content_id) {
            seen.insert(player.content_id, false);
        } else {
            if !seen[&player.content_id] {
                to_punish.push(*player);
                seen.insert(player.content_id, true);
            }
        }
    }

    CombinedMechanicResults {
        stack_origins: stack_result.origins.iter().map(|t| t.content_id).collect(),
        cone_origins: cone_result.origins,
        to_punish
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
                            // hit_count: 0,
                        });
                    });
                });

                targets.sort_unstable_by(|a, b| a.distance.total_cmp(&b.distance));

                let mechanic_results = handle_mechanics(&targets, &position);

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


                for t in &mechanic_results.to_punish {
                    let player = t.entity.entity_view(world);
                    apply_condition(
                        &player,
                        condition::Condition::Stun as u128,
                        condition::Condition::Stun,
                        15.0,
                        false,
                    );
                }

                if !mechanic_results.to_punish.is_empty() {
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
