using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomPlacer : MonoBehaviour
{
    #region Room Placement Methods
    [SerializeField] private CreateBuildingWalls createBuildingWalls; // For room features (walls, ramps, etc.)
    [SerializeField] private RoomSpawner roomSpawner; // Responsible for room spawning.

    /// <summary>
    /// Attempts to place a room using free grid spaces.
    /// If no candidate fits, reduces the room size gradually.
    /// </summary>
    public Room PlaceRoom(Vector3Int desiredSize, string roomType)
    {
        var freeSpaces = GridManager.GetFreeSpaces(roomSpawner.grid, RoomSpawner.gridStep, roomSpawner.buildingFloorWidthX, roomSpawner.buildingFloorDepthZ, desiredSize);
        System.Random rng = new System.Random();
        List<Vector3Int> candidates = new List<Vector3Int>();
        candidates.AddRange(freeSpaces.cornerSpaces.OrderBy(_ => rng.Next()));
        candidates.AddRange(freeSpaces.edgeSpaces.OrderBy(_ => rng.Next()));
        candidates.AddRange(freeSpaces.regularSpaces.OrderBy(_ => rng.Next()));

        Vector3Int minSize = new Vector3Int(RoomSpawner.gridStep, desiredSize.y, RoomSpawner.gridStep);
        Vector3Int currentSize = desiredSize;

        while (currentSize.x >= minSize.x && currentSize.z >= minSize.z)
        {
            foreach (Vector3Int candidate in candidates)
            {
                // Directly call GridManager's method instead of roomSpawner.IsAreaFree.
                if (GridManager.IsAreaAvailable(roomSpawner.grid, candidate, currentSize, RoomSpawner.gridStep, 0))
                {
                    Vector3Int chosenCandidate = candidate; // Use a temporary variable.
                    if (roomSpawner.fixedRoomTypes.ContainsKey(roomType) && roomSpawner.fixedRoomTypes[roomType])
                    {
                        if (createBuildingWalls.floorNumber == 0)
                        {
                            roomSpawner.SetFixedRoomPosition(roomType, candidate, currentSize);
                        }
                        else if (roomSpawner.fixedRoomPositions.ContainsKey(roomType) && roomSpawner.fixedRoomPositions[roomType].Count > 0)
                        {
                            chosenCandidate = roomSpawner.GetFixedRoomPositions(roomType)[0];
                        }
                    }
                    // Create the room object.
                    Room newRoom = new Room(chosenCandidate, currentSize, roomType);
                    // Calculate occupied area based on room bounds.
                    var (min, max) = newRoom.GetBounds();
                    Vector3Int roomSize = max - min;
                    // Directly call GridManager's method to mark the area as occupied.
                    GridManager.OccupyArea(roomSpawner.grid, min, roomSize, RoomSpawner.gridStep);
                    return newRoom;
                }
            }
            // Reduce room size and try again.
            currentSize = new Vector3Int(
                Mathf.Max(currentSize.x - RoomSpawner.gridStep, minSize.x),
                currentSize.y,
                Mathf.Max(currentSize.z - RoomSpawner.gridStep, minSize.z)
            );
        }
        return null;
    }
    #endregion
}
