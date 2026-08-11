use crate::game::{components::*, condition, utils::*};
use flecs_ecs::prelude::*;
use std::collections::HashSet;
use tracing::info;

#[derive(Component, Debug)]
pub struct TeaShanoa {
    available_markers: HashSet<u8>,
}

pub fn create_mechanic(entity: EntityView<'_>) -> EntityView<'_> {
    entity.set(TeaShanoa {
        available_markers: HashSet::from([1, 2, 3, 4, 5, 6, 7, 8]),
    })
}

pub fn create_systems(world: &World) {
    world
        .system::<(&Mechanic, &TeaShanoa, &Party)>()
        .each_iter(|it, index, (mechanic, shanoa, party)| {
            let entity = it.entity(index);
            let world = &it.world();

            return;

            info!(
                mechanic.request_id,
                mechanic.mechanic_id, party.id, "Completing Mechanic"
            );
            entity.remove(TeaShanoa::id());
        });
}
