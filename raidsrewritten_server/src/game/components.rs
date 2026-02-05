use crate::game::role;
use flecs_ecs::prelude::*;
use socketioxide::{SocketIo, socket::Sid};
use std::collections::HashMap;

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

#[derive(Component)]
pub struct PartyContainer;

#[derive(Component, Debug)]
pub struct Position {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

#[derive(Component, Debug)]
pub struct Rotation {
    pub value: f32,
}

#[derive(Component, Debug)]
pub struct State {
    pub is_alive: bool,
}

#[derive(Component, Debug)]
pub struct Vfx {
    pub id: u128,
}

// Mechanics ================

#[derive(Component, Debug)]
pub struct Mechanic {
    pub request_id: String,
    pub mechanic_id: u32,
}

#[derive(Component, Debug)]
pub struct Targets {
    pub player_entities: Vec<Entity>,
}

#[derive(Component, Debug)]
pub struct Affects {
    pub player_entities: HashMap<Entity, u8>,
}

#[derive(Debug)]
pub struct Transform {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub rotation: f32,
}
