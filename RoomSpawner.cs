using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomSpawner : MonoBehaviour
{
    [SerializeField] private CreateBuildingWalls createBuildingWalls; // For room features (walls, ramps, etc.)
    [SerializeField] private RoomConfiguration roomConfiguration;
    [SerializeField] private RoomPlacer roomPlacer;
    [SerializeField] private List<Building> buildings; // Assign in Unity Inspector
    [SerializeField] private BuildingSpawner buildingSpawner; // Reference to BuildingSpawner


    public int buildingFloorWidthX;
    public int buildingFloorDepthZ;
    public static int gridStep = 5; // 5-meter square units

    public List<Room> rooms;
    public bool[,] grid; // The occupancy grid

    // Fixed room management dictionaries.
    public Dictionary<string, bool> fixedRoomTypes;
    public Dictionary<string, List<Vector3Int>> fixedRoomPositions;
    public Dictionary<string, Vector3Int> fixedRoomSizes;
    public Dictionary<string, Color> roomTypeColors;

    // (Optionally, buildingName can be set externally.)
    private string buildingName;

    private void Awake()
    {
        if (fixedRoomTypes == null) fixedRoomTypes = new Dictionary<string, bool>();
        if (fixedRoomPositions == null) fixedRoomPositions = new Dictionary<string, List<Vector3Int>>();
        if (fixedRoomSizes == null) fixedRoomSizes = new Dictionary<string, Vector3Int>();
        if (roomTypeColors == null) roomTypeColors = new Dictionary<string, Color>();

        // Assume dimensions are set via the Inspector.
        grid = new bool[buildingFloorWidthX, buildingFloorDepthZ];
    }

    #region Public Room Generation
    /// <summary>
    /// Generates and places rooms for the given floor dimensions.
    /// This method returns a list of Room objects.
    /// </summary>
    public List<Room> GenerateRoomsForFloor(int floorWidth, int floorDepth, string bName, int floorHeight)
    {
        // Step 1: Initialize the floor (reset grid and set dimensions).
        InitializeFloor(floorWidth, floorDepth);

        // Step 2: Setup room configurations (fixed rooms, etc.).
        roomConfiguration.SetupFixedRooms(bName);

        // Step 3: Generate the room configurations (room type and desired size).
        List<(string roomType, Vector3Int size)> roomConfigs = roomConfiguration.GenerateRoomConfigs(floorWidth, floorDepth, floorHeight, bName);

        // Step 4: Calculate the room sizes with snapping to gridStep.
        List<Vector3Int> roomSizes = CalculateRoomSizes(roomConfigs);

        // Step 5: Scale down room sizes if total area exceeds allowed threshold.
        ScaleRoomSizes(ref roomSizes, roomConfigs, floorWidth, floorDepth);

        // Step 6: Place rooms using the RoomPlacer.
        List<Room> generatedRooms = PlaceRooms(roomConfigs, roomSizes);

        // Record and return the generated rooms.
        rooms = generatedRooms;
        return rooms;
    }

    private void InitializeFloor(int floorWidth, int floorDepth)
    {
        buildingFloorWidthX = floorWidth;
        buildingFloorDepthZ = floorDepth;
        grid = new bool[floorWidth, floorDepth]; // Reset grid for current floor
    }

    private List<Vector3Int> CalculateRoomSizes(List<(string roomType, Vector3Int size)> roomConfigs)
    {
        List<Vector3Int> roomSizes = new List<Vector3Int>();
        foreach (var config in roomConfigs)
        {
            // Snap room size to gridStep.
            Vector3Int snappedSize = new Vector3Int(
                (int)(GridSnapHelper.SnapToGrid(config.size.x, gridStep)),
                config.size.y,
                (int)(GridSnapHelper.SnapToGrid(config.size.z, gridStep))
            );
            roomSizes.Add(snappedSize);
        }
        return roomSizes;
    }

    private void ScaleRoomSizes(ref List<Vector3Int> roomSizes, List<(string roomType, Vector3Int size)> roomConfigs, int floorWidth, int floorDepth)
    {
        // Calculate total area used by the rooms.
        float totalAreaUsed = 0f;
        foreach (Vector3Int size in roomSizes)
        {
            totalAreaUsed += size.x * size.z;
        }

        float maxAllowedArea = 0.8f * floorWidth * floorDepth;

        // If total area exceeds allowed area, scale down non-fixed rooms.
        if (totalAreaUsed > maxAllowedArea)
        {
            float scaleAmount = 0.85f;
            for (int i = 0; i < roomSizes.Count; i++)
            {
                string roomType = roomConfigs[i].roomType;
                if (!GetRoomFixed(roomType))
                {
                    int newWidth = (int)(roomSizes[i].x * scaleAmount);
                    int newDepth = (int)(roomSizes[i].z * scaleAmount);
                    roomSizes[i] = new Vector3Int(
                        Mathf.CeilToInt(newWidth / (float)gridStep) * gridStep,
                        roomSizes[i].y,
                        Mathf.CeilToInt(newDepth / (float)gridStep) * gridStep
                    );
                }
            }
        }
    }

    private List<Room> PlaceRooms(List<(string roomType, Vector3Int size)> roomConfigs, List<Vector3Int> roomSizes)
    {
        List<Room> generatedRooms = new List<Room>();
        for (int i = 0; i < roomConfigs.Count; i++)
        {
            string roomType = roomConfigs[i].roomType;
            Room room = roomPlacer.PlaceRoom(roomSizes[i], roomType);
            if (room != null)
            {
                generatedRooms.Add(room);
            }
        }
        return generatedRooms;
    }
    #endregion

    #region Fixed Room Management
    public void SetRoomFixed(string roomType, bool isFixed)
    {
        if (!fixedRoomTypes.ContainsKey(roomType))
            fixedRoomTypes.Add(roomType, isFixed);
        else
            fixedRoomTypes[roomType] = isFixed;
    }

    public bool GetRoomFixed(string roomType)
    {
        return fixedRoomTypes.ContainsKey(roomType) ? fixedRoomTypes[roomType] : false;
    }

    public void SetRoomSize(string roomType, Vector3Int roomSize)
    {
        if (!fixedRoomSizes.ContainsKey(roomType))
            fixedRoomSizes.Add(roomType, roomSize);
    }

    public Vector3Int GetRoomSize(string roomType)
    {
        return fixedRoomSizes.ContainsKey(roomType) ? fixedRoomSizes[roomType] : Vector3Int.zero;
    }

    public void SetFixedRoomPosition(string roomType, Vector3Int position, Vector3Int size)
    {
        if (!fixedRoomPositions.ContainsKey(roomType))
            fixedRoomPositions.Add(roomType, new List<Vector3Int>());

        // Create a Room to get its bounds.
        Room room = new Room(position, size, roomType);
        var (min, max) = room.GetBounds();

        // Calculate the area size from the bounds.
        Vector3Int areaSize = max - min;

        // Use GridManager's method to mark the area as occupied.
        GridManager.OccupyArea(grid, min, areaSize, gridStep);

        // Record the fixed room position.
        fixedRoomPositions[roomType].Add(position);
    }

    public List<Vector3Int> GetFixedRoomPositions(string roomType)
    {
        return fixedRoomPositions.ContainsKey(roomType) ? new List<Vector3Int>(fixedRoomPositions[roomType]) : new List<Vector3Int>();
    }
    #endregion

    public void AddCellRoom(Room cellRoom)
    {
        // If the rooms list is null, initialize it. (This is a safety check.)
        if (rooms == null)
        {
            rooms = new List<Room>();
        }
        rooms.Add(cellRoom);
        //Debug.Log("Added cell room: " + cellRoom.roomType);
    }

}
