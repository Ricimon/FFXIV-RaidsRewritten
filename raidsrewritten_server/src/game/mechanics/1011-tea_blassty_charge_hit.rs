use crate::game::{
    components::*,
    condition::{self, apply_condition},
    utils::*,
};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component)]
struct BlasstyChargeHit;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(BlasstyChargeHit)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &ExtraMechanicData, &Party)>()
        .with(BlasstyChargeHit)
        .each_iter(|it, index, (mechanic, extra_data, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            let mut targets: Vec<u64> = Vec::new();
            for t in extra_data.value.split(',') {
                if let Ok(n) = t.parse::<u64>() {
                    targets.push(n);
                }
            }

            if let Some(pc) = find_party_container(world, &party.id) {
                pc.each_child(|c1| {
                    c1.try_get::<&Player>(|p| {
                        if targets.contains(&p.content_id) {
                            let mut has_vuln = false;
                            c1.each_child(|c2| {
                                c2.try_get::<&Condition>(|condition| {
                                    if condition.condition
                                        == condition::Condition::FireResistanceDown
                                    {
                                        has_vuln = true;
                                    }
                                });
                            });

                            let player = c1;
                            if has_vuln {
                                apply_condition(
                                    &player,
                                    condition::Condition::Stun as u128,
                                    condition::Condition::Stun,
                                    15.0,
                                    false,
                                );
                                apply_condition(
                                    &player,
                                    condition::Condition::Pacify as u128,
                                    condition::Condition::Pacify,
                                    30.0,
                                    false,
                                );
                            }

                            apply_condition(
                                &player,
                                condition::Condition::FireResistanceDown as u128,
                                condition::Condition::FireResistanceDown,
                                15.0,
                                false,
                            );
                        }
                    });
                });
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(BlasstyChargeHit);
        });
}
