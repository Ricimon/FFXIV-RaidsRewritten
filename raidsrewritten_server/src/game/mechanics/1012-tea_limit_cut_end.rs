use crate::game::{components::*, condition, utils::*};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component)]
struct LimitCutEnd;

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.add(LimitCutEnd)
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &Party)>()
        .with(LimitCutEnd)
        .each_iter(|it, index, (mechanic, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            if let Some(pc) = find_party_container(world, &party.id) {
                let mut condition_removed = false;
                pc.each_child(|c1| {
                    c1.try_get::<&Player>(|_| {
                        c1.each_child(|c2| {
                            c2.try_get::<&Condition>(|condition| {
                                if condition.condition == condition::Condition::FireResistanceDown {
                                    c2.destruct();
                                    condition_removed = true;
                                }
                            });
                        });
                    });
                });

                if condition_removed {
                    pc.add(BroadcastConditions);
                }
            }

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(LimitCutEnd);
        });
}
