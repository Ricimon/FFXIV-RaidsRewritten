#[path = "mechanics/0001-spread.rs"]
pub mod m0001_spread;
#[path = "mechanics/0010-enumeration.rs"]
pub mod m0010_enumeration;
#[path = "mechanics/0020-explosive_trap.rs"]
pub mod m0020_explosive_trap;
#[path = "mechanics/1000-tea_fire_tornado_1.rs"]
pub mod m1000_tea_fire_tornado_1;
#[path = "mechanics/1010-tea_hawk_blaster_tower.rs"]
pub mod m1010_tea_hawk_blaster_tower;
#[path = "mechanics/1011-tea_blassty_charge_hit.rs"]
pub mod m1011_tea_blassty_charge_hit;
#[path = "mechanics/1012-tea_limit_cut_end.rs"]
pub mod m1012_tea_limit_cut_end;
#[path = "mechanics/1020-tea_spawn_shanoa.rs"]
pub mod m1020_tea_spawn_shanoa;
#[path = "mechanics/1021-tea_show_shanoa_guidance_markers.rs"]
pub mod m1021_tea_show_shanoa_guidance_markers;
#[path = "mechanics/1022-tea_move_shanoa.rs"]
pub mod m1022_tea_move_shanoa;
#[path = "mechanics/1023-tea_fire_tornado_attack_shanoa.rs"]
pub mod m1023_tea_fire_tornado_attack_shanoa;
#[path = "mechanics/1026-tea_show_shanoa.rs"]
pub mod m1026_tea_show_shanoa;
#[path = "mechanics/1027-tea_hide_shanoa.rs"]
pub mod m1027_tea_hide_shanoa;
#[path = "mechanics/1028-tea_update_nisi_status.rs"]
pub mod m1028_tea_update_nisi_status;
#[path = "mechanics/1029-tea_nisi_tower.rs"]
pub mod m1029_tea_nisi_tower;

use crate::{
    game::{components::*, utils::*},
    webserver::message::StopVfxPayload,
};
use flecs_ecs::prelude::*;
use socketioxide::socket::Sid;
use tracing::info;

pub fn create_mechanic(
    world: &World,
    request_id: String,
    mechanic_id: u32,
    requester_socket_id: Sid,
    party: String,
    transform: Option<Transform>,
    extra_data: Option<String>,
) -> Option<EntityView<'_>> {
    let mechanic_fn: Option<fn(EntityView<'_>) -> EntityView<'_>> = match mechanic_id {
        1 => Some(m0001_spread::create_mechanic),
        10 => Some(m0010_enumeration::create_mechanic),
        20 => Some(m0020_explosive_trap::create_mechanic),
        // TEA
        1000 => Some(m1000_tea_fire_tornado_1::create_mechanic),
        1010 => Some(m1010_tea_hawk_blaster_tower::create_mechanic),
        1011 => Some(m1011_tea_blassty_charge_hit::create_mechanic),
        1012 => Some(m1012_tea_limit_cut_end::create_mechanic),
        1020 => Some(m1020_tea_spawn_shanoa::create_mechanic),
        1021 => Some(m1021_tea_show_shanoa_guidance_markers::create_mechanic),
        1022 => Some(m1022_tea_move_shanoa::create_mechanic),
        1023 => Some(m1023_tea_fire_tornado_attack_shanoa::create_mechanic),
        1026 => Some(m1026_tea_show_shanoa::create_mechanic),
        1027 => Some(m1027_tea_hide_shanoa::create_mechanic),
        1028 => Some(m1028_tea_update_nisi_status::create_mechanic),
        1029 => Some(m1029_tea_nisi_tower::create_mechanic),
        _ => None,
    };
    if let Some(f) = mechanic_fn {
        let e = create_generic_mechanic(world, request_id, mechanic_id, requester_socket_id, party);

        if let Some(t) = transform {
            e.set(Position {
                x: t.x,
                y: t.y,
                z: t.z,
            })
            .set(Rotation { value: t.rotation });
        }

        if let Some(ed) = extra_data {
            e.set(ExtraMechanicData { value: ed });
        }

        Some(f(e))
    } else {
        info!(mechanic_id, "Unsupported mechanic_id");
        None
    }
}

pub fn create_generic_mechanic(
    world: &World,
    request_id: String,
    mechanic_id: u32,
    requester_socket_id: Sid,
    party: String,
) -> EntityView<'_> {
    let e = world
        .entity()
        .set(Mechanic {
            request_id,
            mechanic_id,
            requester_socket_id,
        })
        .set(Party { id: party.clone() });

    if let Some(pc) = find_party_container(world, &party) {
        e.child_of(pc);
    }

    e
}

pub fn create_systems(world: &World) {
    m0001_spread::create_systems(world);
    m0010_enumeration::create_systems(world);
    m0020_explosive_trap::create_systems(world);
    // TEA
    m1000_tea_fire_tornado_1::create_systems(world);
    m1010_tea_hawk_blaster_tower::create_systems(world);
    m1011_tea_blassty_charge_hit::create_systems(world);
    m1012_tea_limit_cut_end::create_systems(world);
    m1020_tea_spawn_shanoa::create_systems(world);
    m1021_tea_show_shanoa_guidance_markers::create_systems(world);
    m1022_tea_move_shanoa::create_systems(world);
    m1023_tea_fire_tornado_attack_shanoa::create_systems(world);
    m1026_tea_show_shanoa::create_systems(world);
    m1027_tea_hide_shanoa::create_systems(world);
    m1028_tea_update_nisi_status::create_systems(world);
    m1029_tea_nisi_tower::create_systems(world);
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
