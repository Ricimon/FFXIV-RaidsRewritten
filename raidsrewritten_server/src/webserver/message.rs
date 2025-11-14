use crate::game::role::Role;
use serde::{Deserialize, Serialize};

// https://stackoverflow.com/a/57578431
macro_rules! back_to_enum {
    ($(#[$meta:meta])* $vis:vis enum $name:ident {
        $($(#[$vmeta:meta])* $vname:ident $(= $val:expr)?,)*
    }) => {
        $(#[$meta])*
        $vis enum $name {
            $($(#[$vmeta])* $vname $(= $val)?,)*
        }

        impl std::convert::TryFrom<u32> for $name {
            type Error = ();

            fn try_from(v: u32) -> Result<Self, Self::Error> {
                match v {
                    $(x if x == $name::$vname as u32 => Ok($name::$vname),)*
                    _ => Err(()),
                }
            }
        }
    }
}

back_to_enum! {
    #[repr(u32)]
    pub enum Action {
        None = 0,

        // To server
        UpdatePlayer = 1,
        UpdateStatus = 2,
        StartMechanic = 3,

        // To client
        PlayVfx = 51,
        ApplyCondition = 52,
    }
}

#[derive(Serialize, Deserialize, Debug)]
pub struct Message {
    #[serde(rename = "a")]
    action: i32,

    update_player: Option<UpdatePlayerPayload>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct UpdatePlayerPayload {
    id: u64,
    name: String,
    role: Role,
    party: String,
}
