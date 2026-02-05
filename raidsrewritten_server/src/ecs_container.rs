use crate::game::components::*;
use crate::game::{mechanics, utils::*};
use crate::system_messages::MessageToEcs;
use crate::webserver::message::{Action, Message, UpdatePartyStatusPayload};
use flecs_ecs::prelude::*;
use socketioxide::socket::Sid;
use socketioxide::{SocketIo, socket};
use std::sync::mpsc::Receiver;
use std::time::Duration;
use tokio::time;
use tracing::info;

struct CommonQueries<'a> {
    query_socket: Query<&'a Socket>,
    query_mechanic: Query<(&'a Mechanic, &'a Party)>,
}

pub fn create_world() -> World {
    World::new()
}

#[allow(clippy::let_underscore_future)]
pub fn run_world(world: World, rx_from_ws: Receiver<MessageToEcs>, io: &SocketIo) {
    world.set(SocketIoSingleton { io: io.clone() });

    let common_queries = CommonQueries {
        query_socket: world.query::<&Socket>().set_cached().build(),
        query_mechanic: world.query::<(&Mechanic, &Party)>().set_cached().build(),
    };

    create_systems(&world);
    create_observers(&world);

    let _ = tokio::spawn(async move {
        let mut interval = time::interval(Duration::from_micros(1_000_000 / 64));

        loop {
            interval.tick().await;
            process_messages(&world, &common_queries, &rx_from_ws);
            world.progress();
        }
    });
}

fn process_messages(world: &World, queries: &CommonQueries, rx_from_ws: &Receiver<MessageToEcs>) {
    // Receive messages from the webserver system per game tick
    for message in rx_from_ws.try_iter() {
        match message {
            MessageToEcs::UpdatePlayer {
                socket_id,
                content_id,
                name,
                role,
                party,
            } => {
                let player_entity;
                if let Some(e) = find_socket(&queries.query_socket, socket_id) {
                    info!(
                        socket_str = socket_id.as_str(),
                        content_id,
                        name,
                        role_str = Into::<&str>::into(&role),
                        party,
                        "Updating Player"
                    );
                    player_entity = e;
                    player_entity.remove((flecs::ChildOf, flecs::Wildcard::ID));
                } else {
                    info!(
                        socket_str = socket_id.as_str(),
                        content_id,
                        name,
                        role_str = Into::<&str>::into(&role),
                        party,
                        "Adding Player"
                    );
                    player_entity = world.entity();
                }

                player_entity
                    .set(Socket { id: socket_id })
                    .set(Player { content_id, name })
                    .set(Role { role })
                    .set(Party { id: party.clone() });

                let party_container;
                if let Some(pc) = find_party_container(world, &party) {
                    party_container = pc;
                } else {
                    party_container = world.entity().set(Party { id: party }).add(PartyContainer);
                };
                player_entity.child_of(party_container);
            }

            MessageToEcs::UpdateStatus {
                socket_id,
                world_position_x,
                world_position_y,
                world_position_z,
                is_alive,
            } => {
                if let Some(e) = find_socket(&queries.query_socket, socket_id) {
                    info!(
                        socket_str = socket_id.as_str(),
                        world_position_x,
                        world_position_y,
                        world_position_z,
                        is_alive,
                        "Updating PlayerStatus"
                    );
                    e.set(Position {
                        x: world_position_x,
                        y: world_position_y,
                        z: world_position_z,
                    })
                    .set(State { is_alive });
                }
            }

            MessageToEcs::RemovePlayer { socket_id } => {
                world.defer(|| {
                    queries.query_socket.each_entity(|e, socket| {
                        if socket.id == socket_id {
                            e.get::<(Option<&Player>, Option<&Role>)>(|(player, role)| {
                                info!(
                                    socket_str = socket_id.as_str(),
                                    "Removing Player {:?} {:?}", player, role
                                );
                            });
                            e.destruct();
                        }
                    });
                });
            }

            MessageToEcs::StartMechanic {
                socket_id,
                request_id,
                mechanic_id,
                world_position_x,
                world_position_y,
                world_position_z,
                rotation,
            } => {
                let Some(e) = find_socket(&queries.query_socket, socket_id) else {
                    return;
                };
                e.try_get::<&Party>(|party| {
                    if queries
                        .query_mechanic
                        .find(|(m, p)| m.request_id == request_id && p.id == party.id)
                        .is_none()
                    {
                        info!(
                            socket_str = socket_id.as_str(),
                            party.id, request_id, mechanic_id, "Adding Mechanic"
                        );
                        let transform = convert_to_transform(
                            world_position_x,
                            world_position_y,
                            world_position_z,
                            rotation,
                        );
                        mechanics::create_mechanic(
                            world,
                            request_id,
                            mechanic_id,
                            party.id.clone(),
                            transform,
                        );
                    }
                });
            }

            MessageToEcs::ClearMechanics { socket_id } => {
                let Some(e) = find_socket(&queries.query_socket, socket_id) else {
                    return;
                };
                e.try_get::<&Party>(|party| {
                    queries.query_mechanic.each_entity(|e, (_, p)| {
                        if p.id == party.id {
                            info!(
                                socket_str = socket_id.as_str(),
                                party.id, "Clearing Mechanics"
                            );
                            e.destruct();
                        }
                    });
                });
            }
        }
    }
}

fn create_systems(world: &World) {
    mechanics::create_systems(world);
}

fn create_observers(world: &World) {
    mechanics::create_observers(world);

    // Send UpdatePartyStatus to all party members when a player joins or leaves
    world
        .observer::<flecs::OnSet, (&Player, &Party)>()
        .add_event(flecs::OnRemove)
        .each_iter(|it, _, (pl1, pa1)| {
            let mut socket_ids: Vec<Sid> = Vec::new();
            it.world()
                .query::<(&Socket, &Player, &Party)>()
                .build()
                .each(|(s, pl2, pa2)| {
                    // During OnRemove, the entity isn't actually gone yet
                    if it.event() == flecs::OnRemove::ID && pl2.content_id == pl1.content_id {
                        return;
                    }
                    if pa2.id == pa1.id {
                        socket_ids.push(s.id);
                    }
                });

            if socket_ids.is_empty() {
                return;
            }
            // This conversion is technically able to overflow, but shouldn't under normal circumstances
            // https://stackoverflow.com/a/28280042
            let players_in_party = socket_ids.len() as u8;

            let io = get_socket_io(&it.world());
            for sid in socket_ids {
                let io = io.clone();
                tokio::spawn(async move {
                    io.to(sid)
                        .emit(
                            "message",
                            &Message {
                                action: Action::UpdatePartyStatus,
                                update_party_status: Some(UpdatePartyStatusPayload {
                                    connected_players_in_party: players_in_party,
                                }),
                                ..Default::default()
                            },
                        )
                        .await
                        .unwrap();
                });
            }
        });

    // Cleanup party entities when the last player in the party leaves
    world
        .observer::<flecs::OnRemove, (&Player, &Party)>()
        .each_iter(|it, _, (player, party)| {
            let last_player = it
                .world()
                .query::<(&Player, &Party)>()
                .build()
                .find(|(pl, pa)| pa.id == party.id && pl.content_id != player.content_id)
                .is_none();
            info!(player.name, last_player, "Player removed");
            if last_player {
                // Cleanup any party entities
                it.world().query::<&Party>().build().each_entity(|e, p| {
                    if p.id == party.id {
                        e.destruct();
                    }
                });
            }
        });
}

fn find_socket<'a>(query: &Query<&'a Socket>, socket_id: socket::Sid) -> Option<EntityView<'a>> {
    query.find(|socket| socket.id == socket_id)
}
