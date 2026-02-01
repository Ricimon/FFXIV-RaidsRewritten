use crate::game::role::Role;
use socketioxide::socket::Sid;

pub enum MessageToEcs {
    UpdatePlayer {
        socket_id: Sid,
        content_id: u64,
        name: String,
        role: Role,
        party: String,
    },
    UpdateStatus {
        socket_id: Sid,
        world_position_x: f32,
        world_position_y: f32,
        world_position_z: f32,
        is_alive: bool,
    },
    RemovePlayer {
        socket_id: Sid,
    },
    StartMechanic {
        socket_id: Sid,
        request_id: String,
        mechanic_id: u32,
        world_position_x: Option<f32>,
        world_position_y: Option<f32>,
        world_position_z: Option<f32>,
    },
    ClearMechanics {
        socket_id: Sid,
    },
}
