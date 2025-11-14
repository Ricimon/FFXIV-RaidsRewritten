use serde::{Deserialize, Serialize};
use strum_macros::IntoStaticStr;

#[derive(Serialize, Deserialize, Debug, IntoStaticStr)]
pub enum Role {
    None = 0,
    Tank = 1,
    Healer = 2,
    Dps = 3,
}
