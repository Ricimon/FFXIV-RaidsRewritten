use crate::game::role::Role;
use serde::{Deserialize, Serialize};
use serde_repr::*;

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

#[derive(Serialize, Deserialize, Default, Debug)]
#[serde(rename_all = "camelCase")]
pub struct Message {
    #[serde(rename = "a")]
    pub action: Action,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub update_player: Option<UpdatePlayerPayload>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdatePlayerPayload {
    pub id: u64,
    pub name: String,
    pub role: Role,
    pub party: String,
}
