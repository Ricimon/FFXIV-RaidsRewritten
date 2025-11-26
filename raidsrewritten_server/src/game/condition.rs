use serde_repr::*;
use strum_macros::IntoStaticStr;

#[derive(Serialize_repr, Deserialize_repr, PartialEq, IntoStaticStr, Default, Debug)]
#[repr(u32)]
pub enum Condition {
    #[default]
    None = 0,
    Stun = 1,
    Paralysis = 2,
    Bind = 3,
    Heavy = 4,
    Hysteria = 5,
    Pacify = 6,
    Sleep = 7,
    Knockback = 8,
}
