use crate::game::role::Role;
use serde::{Deserialize, Serialize};
use serde_repr::*;
use serde_with::{serde_as, formats::Flexible, BoolFromInt};

#[derive(Serialize_repr, Deserialize_repr, PartialEq, Default, Debug)]
#[repr(u32)]
pub enum Action {
    #[default]
    None = 0,

    // To server
    UpdatePlayer = 1,
    UpdateStatus = 2,
    StartMechanic = 3,

    // To client
    PlayVfx = 51,
    ApplyCondition = 52,
}

#[serde_with::skip_serializing_none]
#[derive(Serialize, Deserialize, Default, Debug)]
pub struct Message {
    #[serde(rename = "a")]
    pub action: Action,

    // To server
    #[serde(rename = "up")]
    pub update_player: Option<UpdatePlayerPayload>,
    #[serde(rename = "us")]
    pub update_status: Option<UpdateStatusPayload>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdatePlayerPayload {
    pub id: u64,
    pub name: String,
    pub role: Role,
    pub party: String,
}

#[serde_as]
#[derive(Serialize, Deserialize, Debug)]
pub struct UpdateStatusPayload {
    #[serde(rename = "x")]
    pub world_position_x: f32,
    #[serde(rename = "y")]
    pub world_position_y: f32,
    #[serde(rename = "z")]
    pub world_position_z: f32,
    #[serde(rename = "a")]
    #[serde_as(as = "BoolFromInt<Flexible>")]
    pub is_alive: bool,
}
