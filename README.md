# SPT-InfiniteMagazineReload

A client-side BepInEx mod for SPT 4.1.x that automatically refills tagged magazines from a same-named magazine preset while in raid.

## Features

- When a tagged magazine enters the backpack / rig / pockets, it is refilled from the Magazine Preset with the exact same name (strict match). Empty and partially filled magazines are both cleared first and then reloaded from the preset (mixed ammo order preserved).
- Raid only; can be enabled / disabled in the BepInEx ConfigurationManager (enabled by default).

## Usage

1. Create a magazine preset in-game and give it a name (e.g. `M855A1`).
2. Apply a Tag with the **exact same name** to a magazine.
3. Enter a raid, empty / partially empty the magazine and reload — it is refilled automatically once it returns to the backpack / rig / pockets.

## Installation

Place `InfiniteMagazineReload.dll` into `BepInEx/plugins/InfiniteMagazineReload/`.

## Requirements

- SPT 4.1.x
- BepInEx
