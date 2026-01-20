# Architecture Overview

**Project:** Random Prison Map Generator  
**Purpose:** Procedural generation of building interiors (prison-like layouts) with deterministic grid-based placement, room types, and internal/external wall segmentation.

## High-level design
- Component-driven, modular systems; each script focuses on a single responsibility.
- Generation pipeline (high-level):
  1. **CreateBuildingWalls** — creates building containers, floors, and calls floor processing.
  2. **RoomSpawner** → **RoomConfiguration** → **RoomPlacer** — compute room configs, snap/scale sizes, and place rooms onto an occupancy grid.
  3. **CreateRoomWalls** / **WallBuilder** — convert Room data into GameObjects and build walls.
  4. **InternalWallSegCreator** — take door candidates and split internal walls (door gaps & headers).
  5. **FloorGenerator / FloorTileGenerator** — generate floor tiles while excluding fixed areas.

## Core subsystems & responsibilities (quick)
- **CreateBuildingWalls.cs**  
  Entry-point for building creation and floor handling. Builds exterior containers, computes floor proportions and delegates: room spawning (interior), floor/ceiling generation, and exterior wall segmentation (windows/doors). Good place to begin reading.

- **RoomSpawner.cs**  
  Manages the 2D occupancy grid for a single floor. Sets up fixed room rules, calls `RoomConfiguration` for room types/sizes, snaps sizes to `gridStep`, scales rooms down when needed, and calls `RoomPlacer` to actually place rooms.

- **RoomPlacer.cs**  
  Placement algorithm — requests candidate free spaces from `GridManager` and attempts to place a room; if it doesn't fit, shrinks the room in `gridStep` increments and retries. Marks occupancy on success (`GridManager.OccupyArea`).

- **CreateRoomWalls.cs**  
  Converts `Room` -> GameObject via `RoomFactory`, optionally spawns debug visuals, and calls `WallBuilder.CreateWalls`. Also requests ramp creation via `RampBuilderFactory` when applicable.

- **InternalWallSegCreator.cs**  
  Converts grouped internal door candidates into wall segments and door headers. Handles both single-door and multi-door wall cases, performs coordinate normalization and precise scale/position calculation, and creates transparent "door gap" cubes to represent openings.

- **FloorGenerator / FloorTileGenerator**  
  Generate floor tiles while excluding fixed-room rectangles (used for yards, staircases, cellblocks, etc.). Uses `BuildingCalculator` helper methods to compute fixed rectangles.

## Data flow (read order suggestion)
1. `CreateBuildingWalls.CreateBuilding()` — top-level orchestration.  
2. `ProcessFloors()` (in `CreateBuildingWalls`) — per-floor processing.  
3. `RoomSpawner.GenerateRoomsForFloor()` → uses `RoomConfiguration` to produce room configs.  
4. `RoomPlacer.PlaceRoom()` → interacts with `GridManager` to find placements.  
5. `CreateRoomWalls.CreateRoom()` → `WallBuilder.CreateWalls()` → `InternalWallSegCreator` (when internal doors need segmentation).

## Notable algorithms & design choices
- **Grid-based placement:** deterministic occupancy grid (boolean[,]) with `gridStep` (5 units) for snapping and placement simplicity.
- **Fixed-room handling:** fixed rooms (stairs, cellblocks) stored separately; these influence floor tiling and furniture/no-furniture zones.
- **Segmentation strategy:** internal wall segmentation separates "wall segments" vs "gap (door) segments" and uses header cubes to cap door openings.
- **Separation of calculation vs rendering:** helper classes (SegmentCalculator, CoordinateUtils) compute positions/scales; SegmentRenderer/WallBuilder do instantiation.

## Testing & performance notes
- Prototype uses primitive shapes.
- The occupancy grid and incremental placement logic are intentionally cheap; `RoomPlacer` reduces room size rather than running complex packing.
- Avoid large per-frame allocations in generation flows — generation runs in an initialization step not during gameplay.

## Tips for reviewers (where to look)
- **Start:** `CreateBuildingWalls.cs` — shows how everything is connected.  
- **Placement logic:** `RoomSpawner.cs` then `RoomPlacer.cs`. Examine `GenerateRoomsForFloor()` and `PlaceRoom()` to see the flow.  
- **Wall segmentation:** `InternalWallSegCreator.cs` — open `ResizeMultipleDoorGaps` to review the segmentation algorithm and `SegmentCalculator` for positioning maths.  
- **Room -> GameObject transition:** `CreateRoomWalls.cs` and `RoomFactory.cs` to see how data maps to Unity objects.  
- **Floor tiling / exclusion:** `FloorGenerator.cs` and `FloorTileGenerator.cs` to see how fixed rooms are excluded from tiles.

## Extras
- 'RandomPrisonLayoutX.png' - 3 screenshots showing random layouts with building ceilings removed for samples.

## Contact
Liam Smith — liam.smith234@gmail.com — https://liamsmith234.itch.io/

