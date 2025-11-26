use crate::game::{condition::Condition, role::Role};
use serde::{Deserialize, Serialize};
use serde_repr::*;
use serde_with::{BoolFromInt, formats::Flexible, serde_as};

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
    #[serde(rename = "sm")]
    pub start_mechanic: Option<StartMechanicPayload>,

    // To client
    #[serde(rename = "pv")]
    pub play_vfx: Option<PlayVfxPayload>,
    #[serde(rename = "ac")]
    pub apply_condition: Option<ApplyConditionPayload>,
}

// To server ===============

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

#[derive(Serialize, Deserialize, Debug)]
pub struct StartMechanicPayload {
    #[serde(rename = "ri")]
    pub request_id: String,
    #[serde(rename = "mi")]
    pub mechanic_id: u32,
}

// To client ===============

#[derive(Serialize, Deserialize, Debug)]
pub struct PlayVfxPayload {
    #[serde(rename = "v")]
    pub vfx_path: String,
    #[serde(rename = "t")]
    pub targets: Vec<u64>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct ApplyConditionPayload {
    #[serde(rename = "c")]
    pub condition: Condition,
    #[serde(rename = "d")]
    pub duration: f32,
    #[serde(rename = "kbx")]
    pub knockback_direction_x: Option<f32>,
    #[serde(rename = "kbz")]
    pub knockback_direction_z: Option<f32>,
}
