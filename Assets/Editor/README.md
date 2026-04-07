# Modular Level Architect (MLA)

An integrated Unity Editor suite for the procedural generation of grid-based levels and the streamlined construction of modular room prefabs. The system combines a rule-based generation core with a comprehensive set of in-editor builder utilities to accelerate development workflows.

## Core Generator Features

The generator constructs level layouts based on a set of configurable, high-level rules rather than simple iteration counts.

### Generation Rules
The dungeon flow is controlled by a rule-based algorithm that provides explicit design control over the level's structure.
- **Main Path**: The generator first establishes a primary path of a configurable minimum and maximum length, guaranteeing a core progression route from the entrance to an objective.
- **Branching Logic**: It then iterates through the main path, creating stochastic side-corridors based on a defined `branchingChance`.
- **Path Depth**: The maximum length of these side branches is constrained by `maxBranchDepth`, allowing designers to control the complexity and size of dead-end routes where rewards are typically placed.

### Deterministic Seed System
All generation logic is derived from a single seed value. This ensures that any generated layout is 100% reproducible, which is critical for debugging, sharing, and testing specific level configurations. If no seed is provided, one is automatically generated.

### Auditor & Auto-Fixer
To enforce the structural integrity of room prefabs, the editor includes an integrated auditor.
- **Validation**: It inspects each assigned prefab to ensure it conforms to the 8-part "Swap System" hierarchy required for dynamic wall generation.
- **Auto-Fix**: If any required child GameObjects are missing, a one-click "Auto-Fix" button programmatically creates the necessary objects within the prefab asset, resolving structural errors instantly.

## Builder Helper Suite

A dedicated tab provides a suite of tools designed to accelerate the creation and validation of room prefabs directly within the Unity Editor.

### Shape Guides
Renders dynamic, non-intrusive wireframe guides in the Scene View to assist with asset placement and scale. These guides represent various room topologies and are scaled based on the `roomSpacing` variable. Supported shapes include:
- Full
- Small
- L-Shape
- T-Shape
- Cross-Shape
- Corridor
- Circle
- Diamond
- Hexagon

### Live Door Tester & Layer Controls
Provides real-time validation of a room's door/wall configurations. When a room prefab instance is selected in the scene, a compass-aligned UI allows the designer to toggle the active state of each of the four directional wall sets (e.g., `WallTop_Open`/`WallTop_Closed`). This allows for immediate visual feedback on the Swap System setup without needing to run a full level generation. All state changes are registered with the Undo system.
Additionally, a "Reset / Enable All Layers" utility allows designers to instantly re-enable all structural child objects to their default active state after testing.

### Procedural Floor Generator
A one-click tool that generates a floor mesh perfectly sized to the `roomSpacing` dimensions.
- **Optimized Mesh**: Creates a single, optimized procedural mesh for the floor instead of using scaled primitives. The bottom face is automatically culled for top-down rendering optimization, and mesh data is aggressively cached to prevent memory allocation spikes.
- **Automatic UV Tiling & Scaling**: UV coordinates are programmatically generated to maintain a strict 1:1 tiling ratio, preventing texture stretching. It supports advanced Normal/Bump maps (tangent recalculation) and includes a `uvScale` parameter for texture density control.
- **Correct Alignment**: The generated floor has a thickness of 0.5 units and is positioned so its top surface aligns perfectly at Y=0 within the room's local space.

### Micro-Grid Snapper
A utility for enforcing geometric precision within a room prefab. It iterates through all child transforms of the selected object and quantizes their `localPosition` to the nearest 0.25-unit increment, ensuring perfect alignment on a micro-grid.

## Export Workflow

### 1-Click Prefab Exporter
Streamlines the process of saving a configured scene object as a production-ready, clean base prefab. The "Save Room as Prefab" button executes the following automated pipeline:
1.  **Validation**: Verifies the selected GameObject has the correct Swap System hierarchy. The export is aborted if the structure is invalid.
2.  **Layer Reset**: Automatically forces all structural geometry layers (walls, props) to be active, preventing the accidental export of disabled layers.
3.  **Centering**: Automatically sets the object's position to `Vector3.zero` and its rotation to `Quaternion.identity`.
4.  **Folder Creation**: Ensures the `Assets/Prefabs/Rooms` directory structure exists, creating it if necessary.
5.  **Asset Creation & Overwrite Protection**: Saves the object as a fresh base prefab. If a prefab with the same name already exists, it displays a safety dialog prompting the user to overwrite, preventing the creation of numbered, corrupted duplicates or broken variants.
6.  **Scene Cleanup**: Safely clears editor references and destroys the original GameObject from the scene upon successful export.

## Technical Requirements

For the dynamic wall generation (Swap System) to function correctly, all room prefabs must adhere to a strict child naming convention. Each prefab must contain the following eight empty `GameObject` children. The generator will toggle their `activeSelf` state based on neighbor connectivity.

```csharp
WallTop_Closed
WallTop_Open
WallBottom_Closed
WallBottom_Open
WallLeft_Closed
WallLeft_Open
WallRight_Closed
WallRight_Open
```

An optional child named `RandomProps` can also be included. Any children of this object will have their visibility randomized during level generation to add visual variety.