# What's the server for?

RaidsRewritten initially launched without a server, using only client-side mechanics. This means all custom mechanics could only use information the client has, such as boss cast timings, player action events, etc. However, not all client information is accurate, especially that of player position. What this means is that it's very possible to run into desyncs if a fake stack or spread were to be done with only client side information. For example, given some fake spreads, another player could have spread away in time on their screen, but due to inherit player movement latency, their player model as it appears on your screen is still too close to you, causing you to be punished, but not them.

In order to resolve position-based mechanics consistently, a custom server is required to act as the source of truth for player positions.

*UCOB Rewritten does have some other-player position-based mechanics, but they have large acceptance leeways to reduce the risk of desync.*

## Status

The server is live for development and playtesting. I (Ricimon) am self-hosting the server on cloud machines that I have access to.

The server's only purpose is to enable fake mechanics that need a single source of truth, such as player-position mechanics. The client will launch unconnected and still be completely functional without a server connection. RaidsRewritten versions 2.0 and above will have the option to connect to the server to enable fake mechanics that require a server and see fake statuses that other connected players receive. At no point does the plugin client perform any direct P2P connection with another player.

Communication between the plugin client and the RaidsRewritten server is completely separate from the game client and official Square Enix servers. Nothing sent between the game client and official Square Enix servers is modified from normal operation in any way.

RaidsRewritten client versions below 2.0 do not have server connection capabilities.

# Don't trust my servers with your data? Host your own!

There's 2 main options to host the RaidsRewritten server: through Docker or bare rust compilation.

### Docker

Here you can either use the GitHub CI-built Docker image or build it yourself.

To use the GitHub CI-built image, I recommend writing a Docker Compose file:

```yml
services:
  server:
    image: ghcr.io/ricimon/ffxiv-raidsrewritten-server:latest
    restart: unless-stopped
    ports:
      - "3000:3000"
```

Use `docker compose up -d` to pull the image and run it. Use `docker compose pull` to pull the latest image when updates happen.

To instead build the image yourself, simply run `docker compose up -d` in a checked-out repo under the `raidsrewritten_server` folder.

### Rust compilation

Without Docker, the server is buildable with the rust toolchain. Inside of the `raidsrewritten_server` folder, running `cargo run` will build and run the server.

## Connecting with the client

Once up, the server is reachable on port 3000. The packaged plugin shipped in this repo contains an untracked `config.json` file that contain the server URLs for my servers. In your installed plugin files, you can edit this file to add your own server option. On Windows, the plugin is most likely installed to `%APPDATA%/XIVLauncher/installedPlugins/RaidsRewritten`. The `config.json` file is located inside the `Resources` folder. Change it to something like this.

```json
{
  "serverUrls": [
    {
      "url": "123.123.123.123:3000",
      "label": "Custom"
    }
  ]
}
```

The IP address in `url` will be the IP address of your server. If you're hosting locally, you can also put `localhost:3000`.

Now disable and re-enable the plugin, and the server selection options should be replaced with your custom options. Note that you'll have to repeat this whenever the plugin installs an update, as it'll overwrite your `config.json` changes. To avoid this, I suggest you build the plugin yourself with your own `config.json` file.

## Security

The client and server communicate over websocket, which by-default is unencrypted HTTP and vulnerable to man-in-the-middle attacks. To encrypt this data, I suggest placing your server behind a reverse proxy capable of supporting SSL and HTTPS. I recommend using [Caddy](https://caddyserver.com/), as it handles auto-renewing SSL certificates. An exact tutorial of this goes beyond the scope of these instructions, but you'll need your own domain to add an A record pointing to your server IP. Once setup though, you can change your `config.json` `url` record to something like `https://yourserver.com`.

Another encryption option is to place the server behind an [ngrok](https://ngrok.com/) tunnel, but I haven't tried this myself.
