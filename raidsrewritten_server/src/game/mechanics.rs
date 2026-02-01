#[path = "mechanics/0001-spread.rs"]
pub mod m0001_spread;
#[path = "mechanics/0010-enumeration.rs"]
pub mod m0010_enumeration;

use crate::game::components::{Party, Position};
use flecs_ecs::prelude::*;
use tracing::info;

#[derive(Component, Debug)]
pub struct Mechanic {
    pub request_id: String,
    pub mechanic_id: u32,
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
    world_position_x: Option<f32>,
    world_position_y: Option<f32>,
    world_position_z: Option<f32>,
) -> Option<EntityView<'_>> {
    let mechanic_fn: Option<fn(EntityView<'_>) -> EntityView<'_>> = match mechanic_id {
        1 => Some(m0001_spread::create_mechanic),
        10 => Some(m0010_enumeration::create_mechanic),
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

        if let Some(x) = world_position_x
            && let Some(y) = world_position_y
            && let Some(z) = world_position_z
        {
            e.set(Position { x, y, z });
        }

        Some(f(e))
    } else {
        info!(mechanic_id, "Unsupported mechanic_id");
        None
    }
}

pub fn create_systems(world: &World) {
    m0001_spread::create_systems(world);
    m0010_enumeration::create_systems(world);
}
