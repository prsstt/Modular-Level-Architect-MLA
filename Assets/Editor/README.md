# Procedural Level Generator for Unity

A robust, grid-based procedural level generation tool for Unity. This system allows developers and level designers to rapidly prototype and generate randomized dungeon or building layouts using a modular prefab system.

## Overview

The Procedural Level Generator operates on a grid coordinate system. It utilizes a random walk algorithm to build the base layout, identifies dead ends for special rooms (like Evacuation and Loot), and dynamically manages wall visibility between adjacent rooms to create seamless corridors and open spaces. The architecture is divided into a standalone logic core (`DungeonGeneratorCore.cs`) and an Editor interface (`LevelGeneratorWindow.cs`), allowing for both edit-time baking and runtime generation.

## Features

* **Grid-Based Generation:** Ensures perfect alignment of modular room prefabs without complex collision detection or overlapping issues.
* **Smart Wall Swapping (Swap System):** Automatically toggles between closed and open wall variations when two rooms connect, creating open doorways seamlessly.
* **Algorithmic Room Placement:**
    * *Base Layout:* Random walk pathfinding.
    * *Evacuation Rooms:* Placed in dead ends using a Maximin distance algorithm to ensure they are far from the start and from each other.
    * *Loot Rooms:* Populates remaining dead ends.
    * *Corridors:* Automatically converts standard rooms into corridors based on horizontal or vertical matching logic.
* **Deterministic Seeds:** Supports seed strings for repeatable and predictable level generation.
* **Prefab Validator & Auto-Fixer:** The Editor Window audits assigned prefabs to ensure they have the correct Swap System structure and offers an "Auto-Fix" button to automatically generate missing child objects.
* **Builder Helper Tab:** Provides Scene View shape guides (Full20x20, Small10x10, LShape) and labels to assist artists in constructing perfectly aligned room prefabs.
* **Dynamic Prop System:** Randomizes the visibility of interior props (clutter, furniture) to make repeated rooms feel unique.

## Installation

1. Create a folder named `Editor` in your Unity project's `Assets` directory (if one does not already exist).
2. Place the `LevelGeneratorWindow.cs` script inside the `Editor` folder. Because this script inherits from `EditorWindow`, it must be placed in an Editor folder to prevent build errors.
3. Place `DungeonGeneratorCore.cs` anywhere outside of the `Editor` folder so it can be compiled and referenced in your runtime builds.

## Usage

1. In the Unity Editor top menu, navigate to **Tools > Procedural Level Generator**.
2. A new dockable window will appear with two tabs: **Generator** and **Builder Helper**.
3. In the **Generator** tab, configure the **Room Count** and **Room Spacing**.
4. Optionally, enter a **Generation Seed**. If left empty, a random seed will be generated.
5. Assign your prepared prefabs to the respective fields:
    * **Start Room**
    * **Standard**
    * **Evacuation**
    * **Loot**
    * **Corridor**
6. If any assigned prefab is missing required wall objects, the UI will display a warning. Click **Auto-Fix** to instantly resolve it.
7. Click **Generate Level**. The script will construct the level under a new root GameObject named `GeneratedProceduralLevel` in your active scene.

## Prefab Setup Requirements

For the generator to successfully connect rooms and create passages, your modular prefabs **must** adhere to the following structural rules:

### 1. Dimensions and Pivot
* The **Pivot** of the parent GameObject must be exactly in the center of the floor `(0, 0, 0)`.
* The outer footprint of the room should match the **Room Spacing** value set in the generator.

### 2. The "Swap" Wall System
You must create a single room base with two distinct wall objects (Closed and Open) for each of the 4 directions. The script will automatically swap between these variations depending on whether a neighboring room is generated in that direction.

Your prefab **must** contain eight child GameObjects named exactly as follows:
* `WallTop_Closed` and `WallTop_Open` (The walls facing the +Z axis / North)
* `WallBottom_Closed` and `WallBottom_Open` (The walls facing the -Z axis / South)
* `WallLeft_Closed` and `WallLeft_Open` (The walls facing the -X axis / West)
* `WallRight_Closed` and `WallRight_Open` (The walls facing the +X axis / East)

### 3. Dynamic Props (Optional)
To add variety to identical room prefabs, you can use the built-in prop randomizer.
* Create an empty child GameObject named exactly `RandomProps` inside your room prefab.
* Place any clutter, trash, or decorative elements as children of `RandomProps`.
* During generation, the script will iterate through the children of `RandomProps` and give each object a 50% chance to be disabled.

## Builder Helper

If you are building your prefabs from scratch, switch to the **Builder Helper** tab in the tool window. Select a **Shape Guide** (Full20x20, Small10x10, or LShape). The tool will draw wireframes and directional labels in the Scene View to guide your 3D modeling and prefab construction.

## Runtime Generation

The core generation logic is completely decoupled from the Unity Editor, making runtime generation straightforward. 

To generate a level at runtime, create a standard `MonoBehaviour` script, instantiate the `DungeonGeneratorCore` class, set your parameters and prefabs, and call `Generate()`. 

You can also subscribe to the `OnGenerationComplete` action to trigger events (like hiding loading screens or spawning the player) once the level is finished.