use crate::{game::{components::*, utils::*}, webserver::message::PlayActorVfxOnPositionPayload};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component, Debug)]
pub struct TeaFireTornado1 {
    cone_vfx: String,
    stack_vfx: String,
}

struct Target {
    player: Entity,
    distance: f32,
    hit_count: u32,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(TeaFireTornado1 {
        cone_vfx: "vfx/monster/gimmick3/eff/n4g6_b_g10cok1.avfx".to_string(),
        stack_vfx: "".to_string(),
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &TeaFireTornado1, &Position, &Party)>()
        .each_iter(|it, index, (mechanic, fire_tornado, position, party)| {
            let entity = it.entity(index);

            if let Some(pc) = find_party_container(&it.world(), &party.id) {
                let mut targets: Vec<Target> = Vec::new();
                pc.each_child(|c| {
                    c.try_get::<(&Player, &Position, &State)>(|(_, p2, s)| {
                        if !s.is_alive {
                            return;
                        }

                        let p1 = [position.x, position.z];
                        let p2 = [p2.x, p2.z];
                        let distance_sq: f32 = euclidean_sq(&p1, &p2);

                        targets.push(Target {
                            player: *c,
                            distance: distance_sq,
                            hit_count: 0,
                        });
                    });
                });

                targets.sort_unstable_by(|a, b| a.distance.total_cmp(&b.distance));

                let mut cone_targets: Vec<Entity> = Vec::new();
                for t in &targets {
                    if cone_targets.len() < 2 {
                        cone_targets.push(t.player);
                    }
                }

                let mut stack_targets: Vec<Entity> = Vec::new();
                for t in targets.iter().rev() {
                    if stack_targets.len() < 2 {
                        stack_targets.push(t.player);
                    }
                }

                let io = get_socket_io(&it.world());
                pc.each_child(|c| {
                    c.try_get::<&Socket>(|s| {
                        for t in &cone_targets {
                            let t1 = t.entity_view(it.world());
                            t1.try_get::<&Position>(|p| {
                                let r = vector_to_rotation(p.x - position.x, p.z - position.z);
                                send_play_actor_vfx_on_position(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnPositionPayload{
                                        vfx_path: fire_tornado.cone_vfx.clone(),
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: r,
                                    });
                            });
                        }
                    });
                });
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.destruct();
        });
}
