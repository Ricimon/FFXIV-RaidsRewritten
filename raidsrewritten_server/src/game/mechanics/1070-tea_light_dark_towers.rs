use crate::{
    game::{
        components::*,
        condition::{self, apply_condition},
        mechanics::create_generic_mechanic,
        utils::*,
    },
    webserver::{message::*, network_mechanic::NetworkMechanicCommand},
};
use flecs_ecs::prelude::*;
use nalgebra::Vector2;
use tracing::{info, warn};
use uuid::Uuid;

#[derive(Component)]
struct SpawnLightDarkTowers;

// Shared by both towers so the party is only punished once
#[derive(Component)]
struct PartyPunished {
    value: bool,
}

#[derive(Component, Debug)]
struct LightDarkTower {
    spawner: Entity,
    // Content ID of the only player who can take this tower, 0 if nobody has the beacon
    soaker: u64,
    soaker_entity: Option<Entity>,
    // The soaker shows this tilt until they take the tower, which flips it to the other one
    soaker_tilt: condition::Condition,
    tilt_after_soak: condition::Condition,
    // Held back until the beacon locks the holder in place, so it can't help them plan
    tilt_delay: f32,
    tilt_applied: bool,
    tilt_duration: f32,
    time_to_snapshot: f32,
    attack_delay: f32,
    failure_effect_delay: f32,
    radius: f32,
    tower_vfx: String,
    pop_vfx: String,
    failure_vfx: String,
    pop_sfx: String,
    failure_sfx: String,
    hysteria_duration: f32,
    hysteria_redirection_interval: f32,
    damage_down_duration: f32,
    succeeded: bool,
    // Anyone in the tower who isn't its beacon holder
    intruders: Vec<Entity>,
    spawn_sound_vfx_id: u128,
    phase: Phase,
}

#[derive(Debug)]
enum Phase {
    Omen,
    Attack,
    Failure,
}

// The P12S Engravement of Souls towers
const LIGHT_TOWER_VFX: &str = "vfx/monster/gimmick4/eff/n5rc_b_g06_c0v.avfx";
const DARK_TOWER_VFX: &str = "vfx/monster/gimmick4/eff/n5rc_b_g07_c0v.avfx";
const LIGHT_TOWER_POP_VFX: &str = "vfx/monster/m0196/eff/m0196_hide_sp05_c0v.avfx";
const DARK_TOWER_POP_VFX: &str = "vfx/monster/m0196/eff/m0196_hide_sp06_c0v.avfx";
const LIGHT_TOWER_FAILURE_VFX: &str = "vfx/monster/gimmick4/eff/n5rc_b_g08_c0v.avfx";
const DARK_TOWER_FAILURE_VFX: &str = "vfx/monster/gimmick4/eff/n5rc_b_g09_c0v.avfx";
// Athena's tower omen, only used for its spawn sound
const SPAWN_SOUND_TOWER_VFX: &str = "vfx/omen/eff/m0196_trap_mei01v.avfx";

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(SpawnLightDarkTowers)
}

fn create_tower(
    spawner: Entity,
    soaker: u64,
    soaker_tilt: condition::Condition,
    tilt_after_soak: condition::Condition,
    tower_vfx: &str,
    pop_vfx: &str,
    failure_vfx: &str,
) -> LightDarkTower {
    LightDarkTower {
        spawner,
        soaker,
        soaker_entity: None,
        soaker_tilt,
        tilt_after_soak,
        tilt_delay: 0.5,
        tilt_applied: false,
        tilt_duration: 5.0,
        // The P12S tower effect lasts about 3s, the pop starts while it fades out
        time_to_snapshot: 2.3,
        attack_delay: 0.2,
        // Lets the pop play before a failed tower explodes
        failure_effect_delay: 1.0,
        radius: 3.0,
        tower_vfx: tower_vfx.to_string(),
        pop_vfx: pop_vfx.to_string(),
        failure_vfx: failure_vfx.to_string(),
        pop_sfx: "sound/vfx/monster8/SE_Vfx_Monster_m0196_hide_sp06.scd".to_string(),
        failure_sfx: "sound/vfx/monster8/SE_Vfx_Monster_m0196_gim09.scd".to_string(),
        hysteria_duration: 15.0,
        hysteria_redirection_interval: 5.0,
        damage_down_duration: 60.0,
        succeeded: false,
        intruders: Vec::new(),
        spawn_sound_vfx_id: 0,
        phase: Phase::Omen,
    }
}

fn apply_hysteria(player: &EntityView<'_>, tower: &LightDarkTower) {
    apply_condition(
        player,
        condition::Condition::Hysteria as u128,
        condition::Condition::Hysteria,
        tower.hysteria_duration,
        false,
    )
    .entity_view(player.world())
    .set(conditions::Hysteria {
        redirection_interval: tower.hysteria_redirection_interval,
    });
}

fn apply_tilt(player: &EntityView<'_>, tilt: condition::Condition, duration: f32) {
    apply_condition(player, tilt as u128, tilt, duration, true);
}

// The condition system drops it and lets the clients know on its next run
fn expire_condition(player: &EntityView<'_>, tilt: condition::Condition) {
    player
        .world()
        .query::<&mut crate::game::components::Condition>()
        .with((flecs::ChildOf, *player))
        .build()
        .each(|c| {
            if c.id == tilt as u128 {
                c.time_remaining = 0.0;
            }
        });
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &Party, Option<&ExtraMechanicData>)>()
        .with(SpawnLightDarkTowers)
        .each_iter(|it, index, (mechanic, party, extra_data)| {
            let entity = it.entity(index);
            let world = &it.world();

            // Light and dark beacon content IDs, then the light and dark tower X/Z. The client
            // picks the spots from the encounter's RNG seed so Shanoa can match them
            let arguments: Vec<&str> = extra_data
                .map(|d| d.value.split(',').collect())
                .unwrap_or_default();
            // A missing beacon holder, like one who died before the towers, leaves that tower
            // with nobody who can take it, so it still drops and fails
            let soakers: Vec<u64> = (0..2)
                .map(|i| arguments.get(i).and_then(|s| s.parse().ok()).unwrap_or(0))
                .collect();
            let coordinates: Vec<f32> = arguments
                .iter()
                .skip(2)
                .filter_map(|s| s.parse().ok())
                .collect();
            if coordinates.len() != 4 {
                warn!(
                    mechanic.request_id,
                    mechanic.mechanic_id, party.id, "Missing tower positions, completing mechanic"
                );
                entity.remove(SpawnLightDarkTowers);
                return;
            }
            let light_tower_position = Vector2::new(coordinates[0], coordinates[1]);
            let dark_tower_position = Vector2::new(coordinates[2], coordinates[3]);

            // Light beacon starts Umbral and takes the dark tower, dark beacon starts Astral and
            // takes the light tower. Taking it flips the tilt
            let towers = [
                (
                    "dark",
                    soakers[0],
                    condition::Condition::UmbralTilt,
                    condition::Condition::AstralTilt,
                    DARK_TOWER_VFX,
                    DARK_TOWER_POP_VFX,
                    DARK_TOWER_FAILURE_VFX,
                    dark_tower_position,
                ),
                (
                    "light",
                    soakers[1],
                    condition::Condition::AstralTilt,
                    condition::Condition::UmbralTilt,
                    LIGHT_TOWER_VFX,
                    LIGHT_TOWER_POP_VFX,
                    LIGHT_TOWER_FAILURE_VFX,
                    light_tower_position,
                ),
            ];
            for (
                name,
                soaker,
                soaker_tilt,
                tilt_after_soak,
                tower_vfx,
                pop_vfx,
                failure_vfx,
                position,
            ) in towers
            {
                create_generic_mechanic(
                    world,
                    format!("{}_{}", mechanic.request_id, name),
                    mechanic.mechanic_id,
                    mechanic.requester_socket_id,
                    party.id.clone(),
                )
                .set(Position {
                    x: position.x,
                    y: 0.0,
                    z: position.y,
                })
                .set(Rotation { value: 0.0 })
                .set(create_tower(
                    entity.id(),
                    soaker,
                    soaker_tilt,
                    tilt_after_soak,
                    tower_vfx,
                    pop_vfx,
                    failure_vfx,
                ));
            }
            entity.set(PartyPunished { value: false });

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(SpawnLightDarkTowers);
        });

    world
        .system::<(
            &Mechanic,
            &mut LightDarkTower,
            &Position,
            &Rotation,
            &Party,
        )>()
        .each_iter(|it, index, (mechanic, tower, position, rotation, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            match tower.phase {
                Phase::Omen => {
                    if !entity.has(Vfx::id()) {
                        let vfx_id = Uuid::new_v4().as_u128();
                        entity.set(Vfx { id: vfx_id });
                        tower.spawn_sound_vfx_id = Uuid::new_v4().as_u128();

                        // The P12S towers only show up on an actor, not as a static vfx
                        if let Some(pc) = find_party_container(world, &party.id) {
                            pc.each_child(|c| {
                                c.try_get::<&Player>(|player| {
                                    if player.content_id == tower.soaker {
                                        tower.soaker_entity = Some(c.id());
                                    }
                                });
                            });

                            let io = get_socket_io(world);
                            pc.each_child(|c| {
                                c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                    send_run_mechanic_command(
                                        io.clone(),
                                        s.id,
                                        RunMechanicCommandPayload {
                                            mechanic_command_id:
                                                NetworkMechanicCommand::TeaSpawnLightDarkTower
                                                    as i32,
                                            world_position_x: Some(position.x),
                                            world_position_y: Some(position.y),
                                            world_position_z: Some(position.z),
                                            rotation: Some(rotation.value),
                                            extra_data: Some(format!(
                                                "{},{}",
                                                vfx_id, tower.tower_vfx
                                            )),
                                        },
                                    );
                                    // Athena's tower omen, too small to see, for the spawn sound.
                                    // The client treats a scale of 0 as unset
                                    send_play_static_vfx(
                                        io.clone(),
                                        s.id,
                                        PlayStaticVfxPayload {
                                            id: tower.spawn_sound_vfx_id,
                                            vfx_path: SPAWN_SOUND_TOWER_VFX.to_string(),
                                            is_omen: true,
                                            world_position_x: position.x,
                                            world_position_y: position.y,
                                            world_position_z: position.z,
                                            rotation: rotation.value,
                                            scale_x: Some(0.001),
                                            scale_y: Some(0.001),
                                            scale_z: Some(0.001),
                                        },
                                    );
                                });
                            });
                        }
                    }

                    tower.tilt_delay -= it.delta_time();
                    if !tower.tilt_applied && tower.tilt_delay <= 0.0 {
                        tower.tilt_applied = true;
                        if let Some(soaker) = tower.soaker_entity
                            && let Some(soaker) = get_entity_view(&soaker, world)
                        {
                            apply_tilt(&soaker, tower.soaker_tilt, tower.tilt_duration);
                        }
                    }

                    tower.time_to_snapshot -= it.delta_time();
                    if tower.time_to_snapshot > 0.0 {
                        return;
                    }

                    // The beacon holder has to take the tower, anyone else in it gets punished
                    let mut soaker_inside = false;
                    if let Some(pc) = find_party_container(world, &party.id) {
                        let tower_position = Vector2::new(position.x, position.z);
                        pc.each_child(|c| {
                            c.try_get::<(&Player, &Position, &State)>(|(player, p, s)| {
                                if !s.is_alive {
                                    return;
                                }
                                let player_position = Vector2::new(p.x, p.z);
                                if player_position.metric_distance(&tower_position) > tower.radius {
                                    return;
                                }
                                if player.content_id == tower.soaker {
                                    soaker_inside = true;
                                } else {
                                    tower.intruders.push(c.id());
                                }
                            });
                        });
                    }
                    tower.succeeded = soaker_inside;
                    tower.phase = Phase::Attack;
                    return;
                }

                Phase::Attack => {
                    tower.attack_delay -= it.delta_time();
                    if tower.attack_delay > 0.0 {
                        return;
                    }

                    // The tower fades out on its own, stopping it here cuts it off abruptly
                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<(&Socket, &Player)>(|(s, _)| {
                                send_stop_vfx(
                                    io.clone(),
                                    s.id,
                                    StopVfxPayload {
                                        id: tower.spawn_sound_vfx_id,
                                    },
                                );
                            });
                        });
                    }

                    // The tower always pops, a failed one explodes a moment later
                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<&Socket>(|s| {
                                // The client plays the pop on a hidden Athena model
                                send_run_mechanic_command(
                                    io.clone(),
                                    s.id,
                                    RunMechanicCommandPayload {
                                        mechanic_command_id:
                                            NetworkMechanicCommand::TeaPopLightDarkTower as i32,
                                        world_position_x: Some(position.x),
                                        world_position_y: Some(position.y),
                                        world_position_z: Some(position.z),
                                        rotation: Some(rotation.value),
                                        extra_data: Some(tower.pop_vfx.clone()),
                                    },
                                );
                                send_play_sfx(
                                    io.clone(),
                                    s.id,
                                    PlaySfxPayload {
                                        sfx_path: tower.pop_sfx.clone(),
                                        sfx_index: 0,
                                    },
                                );
                            });
                        });
                    }

                    for intruder in &tower.intruders {
                        if let Some(intruder) = get_entity_view(intruder, world) {
                            apply_hysteria(&intruder, tower);
                        }
                    }

                    if tower.succeeded
                        && let Some(soaker) = tower.soaker_entity
                        && let Some(soaker) = get_entity_view(&soaker, world)
                    {
                        expire_condition(&soaker, tower.soaker_tilt);
                        apply_tilt(&soaker, tower.tilt_after_soak, tower.tilt_duration);
                    }

                    if !tower.succeeded {
                        tower.phase = Phase::Failure;
                        return;
                    }
                }

                Phase::Failure => {
                    tower.failure_effect_delay -= it.delta_time();
                    if tower.failure_effect_delay > 0.0 {
                        return;
                    }

                    if let Some(pc) = find_party_container(world, &party.id) {
                        let io = get_socket_io(world);
                        pc.each_child(|c| {
                            c.try_get::<&Socket>(|s| {
                                send_play_actor_vfx_on_position(
                                    io.clone(),
                                    s.id,
                                    PlayActorVfxOnPositionPayload {
                                        vfx_path: tower.failure_vfx.clone(),
                                        world_position_x: position.x,
                                        world_position_y: position.y,
                                        world_position_z: position.z,
                                        rotation: rotation.value,
                                    },
                                );
                                send_play_sfx(
                                    io.clone(),
                                    s.id,
                                    PlaySfxPayload {
                                        sfx_path: tower.failure_sfx.clone(),
                                        sfx_index: 0,
                                    },
                                );
                            });
                        });

                        // Both towers can fail on the same tick, and a condition the other tower
                        // just applied isn't visible yet, so only the first one punishes the party
                        let mut already_punished = true;
                        if let Some(spawner) = get_entity_view(&tower.spawner, world) {
                            spawner.try_get::<&mut PartyPunished>(|punished| {
                                already_punished = punished.value;
                                punished.value = true;
                            });
                        }
                        if !already_punished {
                            pc.each_child(|c| {
                                c.try_get::<(&Player, &State)>(|(_, s)| {
                                    if !s.is_alive {
                                        return;
                                    }
                                    let player = c.entity_view(world);
                                    apply_hysteria(&player, tower);
                                    apply_condition(
                                        &player,
                                        condition::Condition::DamageDown as u128,
                                        condition::Condition::DamageDown,
                                        tower.damage_down_duration,
                                        false,
                                    );
                                });
                            });
                        }
                    }
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(LightDarkTower::id());
        });
}
