# RaidsRewritten

Plugin for [XIVLauncher/Dalamud](https://goatcorp.github.io/)

This plugin augments existing fights with custom mechanics just for fun.

<img src="images/icon.png" height=100px />

## What's happening?

RaidsRewritten is (currently) entirely executed client-side. It hooks fight events to spawn fake attack VFX objects, then runs custom hit-detection checks to determine if the local player was hit by a fake attack. If so, this plugin hijacks client controls to simulate the effects of a stun, knockback, etc. If multiple players are running the plugin, mechanic variations between all players are synced via using a fixed RNG seed.

## Available fights

- UCOB Rewritten
- ??? (in ~~time stasis~~ development)

## Attributions

The XIV-interfacing parts of this plugin uses implementations taken from other open source projects. They are listed here and have my greatest appreciations for their work. Thank you to all the authors of these plugins for making this plugin possible.

- [Splatoon](https://github.com/PunishXIV/Splatoon), for fight event hooks
- [VFXEditor](https://github.com/0ceal0t/Dalamud-VFXEditor), for arbitrary VFX spawning
- [Brio](https://github.com/Etheirys/Brio), for arbitrary model spawning and game asset lookup
- [Penumbra](https://github.com/xivdev/Penumbra), for custom model replacements
- [Bossmod](https://github.com/awgil/ffxiv_bossmod), for player action overrides
- [vnavmesh](https://github.com/awgil/ffxiv_navmesh), for player movement overrides
- [ECommons](https://github.com/NightmareXIV/ECommons), for various XIV utility functions
- [SimpleTweaks](https://github.com/Caraxi/SimpleTweaksPlugin), for hotbar gray-out functionality
