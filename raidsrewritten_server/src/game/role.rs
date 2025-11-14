use serde_repr::*;
use strum_macros::IntoStaticStr;

#[derive(Serialize_repr, Deserialize_repr, PartialEq, IntoStaticStr, Default, Debug)]
#[repr(u32)]
pub enum Role {
    #[default]
    None = 0,
    Tank = 1,
    Healer = 2,
    Dps = 3,
}
