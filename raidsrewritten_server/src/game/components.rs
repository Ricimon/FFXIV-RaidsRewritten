use crate::game::role;
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};

#[derive(Component)]
pub struct SocketIoSingleton {
    pub io: SocketIo,
}

#[derive(Component)]
pub struct Socket {
    pub id: Sid,
}

#[derive(Component, Debug)]
pub struct Player {
    pub content_id: u64,
    pub name: String,
}

#[derive(Component, Debug)]
pub struct Role {
    pub role: role::Role,
}

#[derive(Component, Debug)]
pub struct Party {
    pub id: String,
}

#[derive(Component, Debug)]
pub struct Position {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

#[derive(Component, Debug)]
pub struct State {
    pub is_alive: bool,
}
