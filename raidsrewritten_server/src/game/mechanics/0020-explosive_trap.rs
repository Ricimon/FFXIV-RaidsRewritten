use crate::{
    game::{components::*, condition::Condition, utils::*},
    webserver::message::*,
};
use distances::vectors::euclidean_sq;
use flecs_ecs::prelude::*;
use std::time::{Duration, Instant};
use tracing::info;
use uuid::Uuid;

#[derive(Component, Debug)]
pub struct Trap {
    // Settings
    activation_check_interval: f32,
    activation_radius: f32,
    effect_radius: f32,
    omen_vfx_path: String,
    attack_vfx_path: String,
    // Runtime
    expiration_time: Instant,
    activation_delay: f32,
    time_to_next_activation_check: f32,
    effect_delay: f32,
    activated: bool,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(Trap {
        // Settings
        activation_check_interval: 0.2,
        activation_radius: 3.0,
        effect_radius: 5.0,
        omen_vfx_path: "bg/ex3/01_nvt_n4/common/vfx/eff/b2155trp01_o.avfx".to_string(),
        attack_vfx_path: "vfx/monster/gimmick/eff/kappa_hard_bakudan_c0h.avfx".to_string(),
        // Runtime
        expiration_time: Instant::now()
            .checked_add(Duration::from_mins(60))
            .unwrap_or_else(Instant::now),
        activation_delay: 1.0,
        time_to_next_activation_check: 0.0,
        effect_delay: 0.2,
        activated: false,
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &mut Trap, &Position, &Rotation, &Party)>()
        .each_iter(|it, index, (mechanic, trap, position, rotation, party)| {
            let entity = it.entity(index);

            // Expiration timeout
            if Instant::now() >= trap.expiration_time {
                info!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Mechanic timed out"
                );
                entity.destruct();
                return;
            }

            if !trap.activated {
                // Send all players the trap vfx
                if !entity.has(Vfx::id()) {
                    let vfx_id = Uuid::new_v4().as_u128();
                    entity.set(Vfx { id: vfx_id });

                    if let Some(pc) = find_party_container(&it.world(), &party.id) {
                        let io = get_socket_io(&it.world());
                        pc.each_child(|c| {
                            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                send_play_static_vfx(
                                    io.clone(),
                                    s.id,
                                    PlayStaticVfxPayload {
                                        id: vfx_id,
                                        vfx_path: trap.omen_vfx_path.clone(),
                                        is_omen: true,
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: rotation.value,
                                    },
                                );
                            });
                        });
                    }
                }

                // Activation check procedure
                if trap.activation_delay > 0.0 {
                    trap.activation_delay = f32::max(trap.activation_delay - it.delta_time(), 0.0);
                    return;
                }

                trap.time_to_next_activation_check -= it.delta_time();
                if trap.time_to_next_activation_check > 0.0 {
                    return;
                }
                trap.time_to_next_activation_check += trap.activation_check_interval;

                // Actual activation check
                let pos1 = [position.x, position.z];
                if let Some(pc) = find_party_container(&it.world(), &party.id) {
                    pc.each_child(|c| {
                        if trap.activated {
                            return;
                        }
                        c.try_get::<(&Player, &Position)>(|(_, pos)| {
                            let pos2 = [pos.x, pos.z];
                            let distance_sq: f32 = euclidean_sq(&pos1, &pos2);

                            if distance_sq <= f32::powi(trap.activation_radius, 2) {
                                trap.activated = true;
                            }
                        });
                    });

                    // Get affected
                    if trap.activated {
                        let io = get_socket_io(&it.world());
                        pc.each_child(|c| {
                            c.try_get::<(&Socket, &Player, &Position)>(|(s, _, pos)| {
                                let pos2 = [pos.x, pos.z];
                                let distance_sq: f32 = euclidean_sq(&pos1, &pos2);

                                if distance_sq <= f32::powi(trap.effect_radius, 2) {
                                    entity.add((Affect, c));
                                }

                                // Stop trap vfx
                                entity.remove(Vfx::id());

                                // Play explosion vfx
                                send_play_actor_vfx_on_position(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnPositionPayload {
                                        vfx_path: trap.attack_vfx_path.clone(),
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: rotation.value,
                                    },
                                );
                            });
                        });
                    }
                }
                return;
            }

            trap.effect_delay -= it.delta_time();
            if trap.effect_delay > 0.0 {
                return;
            }

            // Send effects
            let io = get_socket_io(&it.world());
            entity.each_target(Affect, |e| {
                e.try_get::<&Socket>(|s| {
                    send_apply_condition(
                        io.clone(),
                        s.id,
                        ApplyConditionPayload {
                            condition: Condition::Stun,
                            duration: 10.0,
                            ..Default::default()
                        },
                    );
                });
            });

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.destruct();
        });
}
