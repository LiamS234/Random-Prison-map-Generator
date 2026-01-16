# Random Prison Map Generator — Code Samples

**Project:** Random Prison Map Generator (prototype with primitive shapes)  
**Role:** Solo developer — code, systems, procedural generation  
**Unity:** 2022.3.46f1  
**What’s here:** A small selection of key C# scripts and architecture notes demonstrating procedural building & room generation, wall segmentation, and placement algorithms.

## Play the prototype
- Play in browser (itch.io): https://liamsmith234.itch.io/random-prison-map-generator-prototype

## Included files (recommended for reviewers)
These five files are the most representative of the project architecture and engineering choices. Inspect them to quickly evaluate system design, algorithms, and code quality.

- `CreateBuildingWalls.cs`  
  **Why include:** Central coordinator that assembles buildings, floors, and exterior wall segmentation; integrates many subsystems.  
  **Overview:** Entry point for building creation. Creates building containers, base walls and floor, handles multi-floor processing, delegates interior room creation and exterior gap/wall segmentation. Shows how the systems (RoomSpawner, FloorGenerator, CreateRoomWalls, exterior gap generator) are wired together.

- `InternalWallSegCreator.cs`  
  **Why include:** Shows algorithmic depth — grouping door candidates and converting them into wall segments and door headers.  
  **Overview:** Processes grouped internal door candidates and produces scaled wall segments and door header objects. Contains single-gap and multi-gap strategies, coordinate conversions, and rendering helpers. Good for reviewers who want to judge geometric reasoning and robustness.

- `RoomSpawner.cs`  
  **Why include:** High-level placement orchestration and grid management — reveals strategy for packing rooms into floors.  
  **Overview:** Orchestrates floor initialization, room configuration, size snapping, scaling adjustments to fit a floor, and delegates placement to RoomPlacer. Manages occupancy grid and APIs for fixed room sizes/positions.

- `RoomPlacer.cs`  
  **Why include:** Core placement algorithm — candidate selection and greedy-fit logic.  
  **Overview:** Finds free grid positions (corner/edge/regular), tries candidate placements, reduces room size incrementally to fit, and marks the grid as occupied on success. Shows practical, testable placement logic.

- `CreateRoomWalls.cs`  
  **Why include:** Where room metadata becomes GameObjects and walls; demonstrates factory usage and integration with wall/ramp builders.  
  **Overview:** Uses `RoomFactory` to build room GameObjects, optionally adds debug visuals, calls `WallBuilder` to place walls, and hooks into ramp generation. Clean handoff from data to geometry.

## How to read this repo
- Start with `CreateBuildingWalls.cs` — it links the systems together (entry → floor processing → room spawning → walls).
- Then open `RoomSpawner.cs` and `RoomPlacer.cs` to see the placement pipeline.
- Inspect `InternalWallSegCreator.cs` to evaluate the segmentation and door-header algorithms.
- Finally review `CreateRoomWalls.cs` to see the conversion of `Room` data into GameObjects and walls.

## Additional files to look for
- `ARCHITECTURE.md` — high-level overview and reading tips (below).

## Notes for reviewers
- The code is intentionally focused on algorithmic clarity and modularity — look for clear single-responsibility classes and small helpers (CoordinateUtils, SegmentCalculator, Grid helpers).
- Many objects are primitive cubes for the prototype; the focus is on systems and placement accuracy.

## Contact
Liam Smith — liam.smith234@gmail.com — https://liamsmith234.itch.io/
