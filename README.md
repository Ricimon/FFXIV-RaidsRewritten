# RaidsRewritten

Plugin for [XIVLauncher/Dalamud](https://goatcorp.github.io/)

This plugin augments existing fights with custom mechanics just for fun.

<img src="images/icon.png" height=100px />

## What's happening?

RaidsRewritten is (currently) entirely executed client-side. It hooks fight events to spawn fake attack VFX objects, then runs custom hit-detection checks to determine if the local player was hit by a fake attack. If so, this plugin hijacks client controls to simulate the effects of a stun, knockback, etc. If multiple players are running the plugin, mechanic variations between all players are synced via using a fixed RNG seed.

## Available fights

- UCOB Rewritten (WIP)

_Development is ongoing... designing and building custom mechanics is hard..._
