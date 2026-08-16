use flecs_ecs::core::World;
use flecs_ecs::prelude::*;
use serde_repr::*;
use strum_macros::IntoStaticStr;
use tracing::info;

use crate::{
    game::{
        components::{self, *},
        utils::*,
    },
    webserver::message::{
        Action, Message, UpdateConditionsConditionDetails, UpdateConditionsPayload,
        UpdateConditionsPlayer,
    },
};

#[derive(
    Serialize_repr, Deserialize_repr, PartialEq, IntoStaticStr, Default, Copy, Clone, Debug,
)]
#[repr(u32)]
pub enum Condition {
    #[default]
    None = 0,
    Stun = 1,
    Paralysis = 2,
    Bind = 3,
    Heavy = 4,
    Hysteria = 5,
    Pacify = 6,
    Sleep = 7,
    Knockback = 8,
    FireResistanceDown = 9,
    Flattened = 10,
}

pub fn create_systems(world: &World) {
    world
        .system::<(&mut components::Condition, &State)>()
        .term_at(1)
        .up()
        .with(PartyContainer)
        .up()
        .each_iter(|it, i, (condition, state)| {
            condition.time_remaining = f32::max(condition.time_remaining - it.delta_time(), 0.0);

            if condition.time_remaining <= 0.0 || !state.is_alive {
                let e = it.entity(i);
                // Traverse up 2 parents to get to the PartyContainer
                if let Some(p) = e.parent()
                    && let Some(p) = p.parent()
                {
                    p.add(BroadcastConditions);
                }
                e.destruct();
            }
        });

    world
        .system::<&components::Condition>()
        .without(BroadcastedCondition)
        .with(PartyContainer)
        .up()
        .each_iter(|it, i, _| {
            let e = it.entity(i);
            // Traverse up 2 parents to get to the PartyContainer
            if let Some(p) = e.parent()
                && let Some(p) = p.parent()
            {
                p.add(BroadcastConditions);
            }
        });

    world
        .system::<&Party>()
        .with(BroadcastConditions)
        .with(PartyContainer)
        .each_iter(|it, i, _| {
            let pc = it.entity(i);
            let io = get_socket_io(&it.world());
            let mut players: Vec<UpdateConditionsPlayer> = Vec::new();

            pc.each_child(|c1| {
                c1.try_get::<&Player>(|p| {
                    let mut condition_details: Vec<UpdateConditionsConditionDetails> = Vec::new();
                    c1.each_child(|c2| {
                        c2.try_get::<&components::Condition>(|c| {
                            condition_details.push(build_condition_details(c2, c));
                            c2.add(BroadcastedCondition);
                        });
                    });
                    players.push(UpdateConditionsPlayer {
                        content_id: p.content_id,
                        conditions: condition_details,
                    });
                });
            });

            pc.each_child(|c| {
                c.try_get::<(&Socket, &Player)>(|(s, _)| {
                    info!(socket_str = s.id.as_str(), "Sending update_conditions");
                    send_message(
                        io.clone(),
                        s.id,
                        Message {
                            action: Action::UpdateConditions,
                            update_conditions: Some(UpdateConditionsPayload {
                                players: players.clone(),
                            }),
                            ..Default::default()
                        },
                    );
                });
            });

            pc.remove(BroadcastConditions);
        });
}

pub fn apply_condition(
    target: &EntityView<'_>,
    id: u128,
    condition: Condition,
    duration: f32,
    override_existing_duration: bool,
) -> Entity {
    let world = target.world();
    let mut existing_condition: Entity = Entity::null();
    let q = world
        .query::<&mut components::Condition>()
        .with((flecs::ChildOf, *target))
        .build();
    q.each_entity(|entity, condition| {
        if condition.id != id {
            return;
        }
        existing_condition = *entity;
        if duration > condition.time_remaining || override_existing_duration {
            condition.time_remaining = duration;
            entity.remove(BroadcastedCondition);
        }
    });

    if existing_condition.is_valid() {
        existing_condition
    } else {
        *world
            .entity()
            .set(components::Condition {
                id,
                condition,
                time_remaining: duration,
            })
            .child_of(target.entity_view(world))
    }
}

fn build_condition_details(
    entity: EntityView<'_>,
    condition: &components::Condition,
) -> UpdateConditionsConditionDetails {
    let mut uccd = UpdateConditionsConditionDetails {
        id: condition.id,
        condition: condition.condition,
        time_remaining: condition.time_remaining,
        newly_applied: !entity.has(BroadcastedCondition),
        is_client_controlled: entity.has(ClientCondition),
        ..Default::default()
    };

    entity.try_get::<&components::conditions::Knockback>(|kb| {
        uccd.knockback_direction_x = Some(kb.knockback_direction_x);
        uccd.knockback_direction_z = Some(kb.knockback_direction_z);
    });

    entity.try_get::<&components::conditions::Paralysis>(|p| {
        uccd.paralysis_stun_interval = Some(p.stun_interval);
        uccd.paralysis_stun_duration = Some(p.stun_duration);
    });

    entity.try_get::<&components::conditions::Hysteria>(|h| {
        uccd.hysteria_redirection_interval = Some(h.redirection_interval);
    });

    uccd
}
