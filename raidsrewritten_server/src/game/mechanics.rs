pub mod spread;

use flecs_ecs::prelude::*;
use tracing::info;

use crate::ecs_container::Party;

#[derive(Component, Debug)]
pub struct Mechanic {
    pub request_id: String,
    pub mechanic_id: u32,
}

#[derive(Component, Debug)]
pub struct MechanicTimer {
    pub time_remaining: f32,
}

#[derive(Component)]
pub struct Target;

#[derive(Component)]
pub struct Affect;

pub fn create_mechanic(
    world: &World,
    request_id: String,
    mechanic_id: u32,
    party: String,
) -> Option<EntityView<'_>> {
    let mechanic_fn = match mechanic_id {
        1 => Some(spread::create_mechanic),
        _ => None,
    };
    if let Some(f) = mechanic_fn {
        let e = world
            .entity()
            .set(Mechanic {
                request_id,
                mechanic_id,
            })
            .set(Party { id: party });
        Some(f(e))
    } else {
        info!(mechanic_id, "Unsupported mechanic_id");
        None
    }
}

pub fn create_systems(world: &World) {
    spread::create_systems(world);
}
