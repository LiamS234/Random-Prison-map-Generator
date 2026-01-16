# Architecture Overview

**Project:** Random Prison Map Generator

## High-level design
- The game uses a Component-driven structure.
- Core systems:
  1. **PlayerController** — handles input, physics interactions, jump logic, animation triggers.
  2. **PlatformSpawner** — creates platforms procedurally ahead of the player, uses pooling for performance.
  3. **DynamicCamera** — switches camera angles based on zone triggers and smooth transitions.

## Key files and responsibilities
- `PlayerController.cs`: input handling, applying forces to Rigidbody, ground-check logic, grace-time for jumps.
- `PlatformSpawner.cs`: algorithm for procedural positions, difficulty scaling, reuse with object pooling.
- `DynamicCamera.cs`: lerp / damped transitions, look-at targets for cut scenes.

## Notes on testing & performance
- Uses object pooling to avoid GC spikes (see PlatformSpawner).
- Designed for 60fps target; profiler used to identify GC allocations.
- WebGL build uses simplified colliders and baked lighting for compatibility.

## Tips for reviewers
- Start with `PlayerController.cs` to see gameplay logic.
- `PlatformSpawner.cs` demonstrates algorithmic approach and separation of concerns.
- Files include inline comments explaining non-obvious decisions.
