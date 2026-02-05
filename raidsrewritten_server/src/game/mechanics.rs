#[path = "mechanics/0001-spread.rs"]
pub mod m0001_spread;
#[path = "mechanics/0010-enumeration.rs"]
pub mod m0010_enumeration;
#[path = "mechanics/0020-explosive_trap.rs"]
pub mod m0020_explosive_trap;

use crate::{
    game::{components::*, utils::*},
    webserver::message::StopVfxPayload,
};
use flecs_ecs::prelude::*;
use tracing::info;

pub fn create_mechanic(
    world: &World,
    request_id: String,
    mechanic_id: u32,
    party: String,
    transform: Option<Transform>,
) -> Option<EntityView<'_>> {
    let mechanic_fn: Option<fn(EntityView<'_>) -> EntityView<'_>> = match mechanic_id {
        1 => Some(m0001_spread::create_mechanic),
        10 => Some(m0010_enumeration::create_mechanic),
        20 => Some(m0020_explosive_trap::create_mechanic),
        _ => None,
    };
    if let Some(f) = mechanic_fn {
        let e = world
            .entity()
            .set(Mechanic {
                request_id,
                mechanic_id,
            })
            .set(Party { id: party.clone() });

        if let Some(t) = transform {
            e.set(Position {
                x: t.x,
                y: t.y,
                z: t.z,
            })
            .set(Rotation { value: t.rotation });
        }

        if let Some(pc) = find_party_container(world, &party) {
            e.child_of(pc);
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
    m0020_explosive_trap::create_systems(world);
}

pub fn create_observers(world: &World) {
    // Send message to remove VFX objects with IDs
    world
        .observer::<flecs::OnRemove, (&Vfx, &Party)>()
        .each_iter(|it, _index, (vfx, party)| {
            if let Some(pc) = find_party_container(&it.world(), &party.id) {
                let io = get_socket_io(&it.world());
                pc.each_child(|c| {
                    c.try_get::<(&Socket, &Player)>(|(s, _)| {
                        send_stop_vfx(io.clone(), s.id, StopVfxPayload { id: vfx.id });
                    });
                });
            }
        });
}
