# What's the server for?

RaidsRewritten initially launched without a server, using only client-side mechanics. This means all custom mechanics could only use information the client has, such as boss cast timings, player action events, etc. However, not all client information is accurate, especially that of player position. What this means is that it's very possible to run into desyncs if a fake stack or spread were to be done with only client side information. For example, given some fake spreads, another player could have spread away in time on their screen, but due to inherit player movement latency, their player model as it appears on your screen is still too close to you, causing you to be punished, but not them.

In order to resolve position-based mechanics consistently, a custom server is required to act as the source of truth for player positions.

*UCOB Rewritten does have some other-player position-based mechanics, but they have large acceptance leeways to reduce the risk of desync.*

## Status

The server is live for development and playtesting. I (Ricimon) am self-hosting the server on cloud machines that I have access to.

The server's only purpose is to enable fake mechanics that need a single source of truth, such as player-position mechanics. The client will launch unconnected and still be completely functional without a server connection. RaidsRewritten versions 2.0 and above will have the option to connect to the server to enable fake mechanics that require a server and see fake statuses that other connected players receive. At no point does the plugin client perform any direct P2P connection with another player.

Communication between the plugin client and the RaidsRewritten server is completely separate from the game client and official Square Enix servers. Nothing sent between the game client and official Square Enix servers is modified from normal operation in any way.

RaidsRewritten client versions below 2.0 do not have server connection capabilities.
