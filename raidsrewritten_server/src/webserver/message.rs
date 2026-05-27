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
    ClearMechanics = 4,
    SyncConditionsOnSelf = 5,

    // To client
    // Deprecated: 51, 55
    ApplyCondition = 52, // to deprecate
    UpdatePartyStatus = 53,
    PlayStaticVfx = 54,
    PlayActorVfxOnTarget = 56,
    PlayActorVfxOnPosition = 57,
    StopVfx = 58,
    UpdateConditions = 59,
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
    #[serde(rename = "scos")]
    pub sync_conditions_on_self: Option<SyncConditionsOnSelfPayload>,

    // To client
    #[serde(rename = "ac")]
    pub apply_condition: Option<ApplyConditionPayload>,
    #[serde(rename = "ups")]
    pub update_party_status: Option<UpdatePartyStatusPayload>,
    #[serde(rename = "psv")]
    pub play_static_vfx: Option<PlayStaticVfxPayload>,
    #[serde(rename = "pavt")]
    pub play_actor_vfx_on_target: Option<PlayActorVfxOnTargetPayload>,
    #[serde(rename = "pavp")]
    pub play_actor_vfx_on_position: Option<PlayActorVfxOnPositionPayload>,
    #[serde(rename = "sv")]
    pub stop_vfx: Option<StopVfxPayload>,
    #[serde(rename = "uc")]
    pub update_conditions: Option<UpdateConditionsPayload>,
}

// To server ===============

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdatePlayerPayload {
    #[serde(rename = "contentId")]
    pub content_id: u64,
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

#[derive(Serialize, Deserialize, Default, Debug)]
pub struct StartMechanicPayload {
    #[serde(rename = "ri")]
    pub request_id: String,
    #[serde(rename = "mi")]
    pub mechanic_id: u32,
    #[serde(rename = "x")]
    pub world_position_x: Option<f32>,
    #[serde(rename = "y")]
    pub world_position_y: Option<f32>,
    #[serde(rename = "z")]
    pub world_position_z: Option<f32>,
    #[serde(rename = "r")]
    pub rotation: Option<f32>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct SyncConditionsOnSelfPayload {
    #[serde(rename = "c")]
    pub conditions: Vec<SyncConditionsOnSelfConditionDetails>,
}
#[derive(Serialize, Deserialize, Debug)]
pub struct SyncConditionsOnSelfConditionDetails {
    pub id: u128,
    #[serde(rename = "c")]
    pub condition: Condition,
    #[serde(rename = "t")]
    pub time_remaining: f32,
    #[serde(rename = "n")]
    pub newly_applied: bool,
}

// To client ===============

#[derive(Serialize, Deserialize, Default, Debug)]
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

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdatePartyStatusPayload {
    #[serde(rename = "c")]
    pub connected_players_in_party: u8,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct PlayStaticVfxPayload {
    pub id: u128,
    #[serde(rename = "v")]
    pub vfx_path: String,
    #[serde(rename = "o")]
    pub is_omen: bool,
    #[serde(rename = "x")]
    pub world_position_x: f32,
    #[serde(rename = "y")]
    pub world_position_y: f32,
    #[serde(rename = "z")]
    pub world_position_z: f32,
    #[serde(rename = "r")]
    pub rotation: f32,
}

#[derive(Serialize, Deserialize, Default, Debug)]
pub struct PlayActorVfxOnTargetPayload {
    #[serde(rename = "v")]
    pub vfx_path: String,
    #[serde(rename = "ct")]
    pub content_id_targets: Vec<u64>,
    #[serde(rename = "it")]
    pub custom_id_targets: Vec<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct PlayActorVfxOnPositionPayload {
    #[serde(rename = "v")]
    pub vfx_path: String,
    #[serde(rename = "x")]
    pub world_position_x: f32,
    #[serde(rename = "y")]
    pub world_position_y: f32,
    #[serde(rename = "z")]
    pub world_position_z: f32,
    #[serde(rename = "r")]
    pub rotation: f32,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct StopVfxPayload {
    pub id: u128,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdateConditionsPayload {
    #[serde(rename = "p")]
    pub players: Vec<UpdateConditionsPlayer>,
}
#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct UpdateConditionsPlayer {
    #[serde(rename = "i")]
    pub content_id: u64,
    #[serde(rename = "c")]
    pub conditions: Vec<UpdateConditionsConditionDetails>,
}
#[derive(Serialize, Deserialize, Clone, Default, Debug)]
pub struct UpdateConditionsConditionDetails {
    pub id: u128,
    #[serde(rename = "c")]
    pub condition: Condition,
    #[serde(rename = "t")]
    pub time_remaining: f32,
    #[serde(rename = "n")]
    pub newly_applied: bool,
    #[serde(rename = "cc")]
    pub is_client_controlled: bool,

    #[serde(rename = "kbx")]
    pub knockback_direction_x: Option<f32>,
    #[serde(rename = "kbz")]
    pub knockback_direction_z: Option<f32>,

    #[serde(rename = "psi")]
    pub paralysis_stun_interval: Option<f32>,
    #[serde(rename = "psd")]
    pub paralysis_stun_duration: Option<f32>,

    #[serde(rename = "hri")]
    pub hysteria_redirection_interval: Option<f32>,
}
