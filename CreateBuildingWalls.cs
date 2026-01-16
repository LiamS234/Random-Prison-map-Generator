using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CreateBuildingWalls : MonoBehaviour
{
    // Named constants for common values.
    private const float DEFAULT_WALL_THICKNESS = 1f;
    private const float GAP_SCALE_FACTOR = 0.5f;          // Used in gap scaling calculations.
    private const float GAP_POSITION_OFFSET = 0.25f;      // Used in gap positioning.
    private const float CEILING_OFFSET = 0.5f;            // Added to ceiling position.
    private const float FLOOR_OFFSET = -0.45f;            // Floor position offset.

    [Header("Dependencies (Assign in Inspector)")]
    [SerializeField] private RoomSpawner roomSpawner;
    [SerializeField] private CreateRoomWalls createRoomWalls;
    [SerializeField] private FloorGenerator floorGenerator;
    [SerializeField] private InternalDoorManager doorManager;
    [SerializeField] private ExteriorDoorAndWindowGapGenerator exteriorDoorAndWindowGapGenerator;

    [Header("Floor Settings")]
    public int floorNumber = 0;

    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;

    //WebGL fix
    private ColorApplier colorApplier;

    void Awake()
    {
        colorApplier = FindObjectOfType<ColorApplier>();
    }

    public GameObject CreateBuilding(string tagName, Color randomColor, Vector3Int buildingSize, Vector3Int buildingPos, int floorHeight)
    {
        // Create the building and its containers.
        GameObject buildingEmpty, buildingEmptyExterior, buildingEmptyInterior;
        CreateBuildingContrainers(out buildingEmpty, out buildingEmptyExterior, out buildingEmptyInterior);
        float wallThickness = DEFAULT_WALL_THICKNESS;

        GameObject frontWall, backWall, leftWall, rightWall, floor;
        CreateBaseStructure(tagName, ref buildingSize, ref buildingPos, buildingEmpty, buildingEmptyExterior, buildingEmptyInterior, wallThickness,out frontWall, out backWall, out leftWall, out rightWall, out floor);

        
        // Assign floor color.
        Renderer floorRenderer = floor.GetComponent<Renderer>();
        if (floorRenderer != null)
        {
            //floorRenderer.material.color = Color.white;
            colorApplier.ApplyColor(floorRenderer, Color.white);
        }       

        GameObject[] wallArray = new GameObject[4] { frontWall, backWall, leftWall, rightWall };

        Vector3 buildingOriginPos = new Vector3(
            buildingEmpty.transform.position.x - buildingEmpty.transform.localScale.x / 2f,
            buildingEmpty.transform.position.y - buildingEmpty.transform.localScale.y / 2f,
            buildingEmpty.transform.position.z - buildingEmpty.transform.localScale.z / 2f
        );

        float floorPropOfBuilding;
        int numOfFloors;
        Dictionary<int, Dictionary<WallSide, List<FloatQuad>>> floorWindowGaps;
        buildingSize = ProcessFloors(tagName, buildingSize, buildingPos, floorHeight, buildingEmpty, buildingEmptyExterior, buildingEmptyInterior,out floorPropOfBuilding, out numOfFloors, out floorWindowGaps);
        CreateExteriorWallSegments(randomColor, buildingSize, floorHeight, buildingEmptyExterior, buildingEmptyInterior,wallThickness, wallArray, buildingOriginPos, floorPropOfBuilding, numOfFloors, floorWindowGaps);

        return buildingEmpty;
    }

    // Helper to create a colored cube, set its name and parent.
    private GameObject CreateColoredCube(string cubeName, Color color, Transform parent)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Renderer renderer = cube.GetComponent<Renderer>();
        //renderer.material.color = color;
        colorApplier.ApplyColor(renderer, color);
        cube.name = cubeName;
        cube.transform.SetParent(parent);
        return cube;
    }

    // Delegates exterior wall segment creation to two helpers.
    private void CreateExteriorWallSegments(Color randomColor, Vector3Int buildingSize, int floorHeight, GameObject buildingEmptyExterior,
                                            GameObject buildingEmptyInterior, float wallThickness, GameObject[] wallArray, Vector3 buildingOriginPos,
                                            float floorPropOfBuilding, int numOfFloors, Dictionary<int, Dictionary<WallSide, List<FloatQuad>>> floorWindowGaps)
    {
        CreateFrontBackWallSegments(randomColor, buildingSize, floorHeight, buildingEmptyExterior, wallThickness, wallArray, buildingOriginPos,floorPropOfBuilding, numOfFloors, floorWindowGaps);
        CreateSideWallSegments(randomColor, buildingSize, floorHeight, buildingEmptyExterior, wallThickness, wallArray, buildingOriginPos,floorPropOfBuilding, numOfFloors, floorWindowGaps);

        int wallLayer = LayerMask.NameToLayer("Wall");
        buildingEmptyExterior.SetLayerRecursively(wallLayer);
        buildingEmptyInterior.SetLayerRecursively(wallLayer);
    }

    // Helper for front/back wall segments.
    private void CreateFrontBackWallSegments(Color randomColor, Vector3Int buildingSize, int floorHeight, GameObject buildingEmptyExterior,float wallThickness, GameObject[] wallArray, Vector3 buildingOriginPos, float floorPropOfBuilding,int numOfFloors, Dictionary<int, Dictionary<WallSide, List<FloatQuad>>> floorWindowGaps)
    {
        float propOfWindowForScaling;
        float propOfWindowForPositioning;
        for (int wallIndex = 0; wallIndex < 2; wallIndex++)
        {
            WallSide wallKey = (wallIndex == 0) ? WallSide.Front : WallSide.Back;
            for (int floorIndex = 0; floorIndex < numOfFloors; floorIndex++)
            {
                List<FloatQuad> currentGaps = InsertBoundaryGaps(floorWindowGaps[floorIndex][wallKey]);
                for (int j = 1; j < currentGaps.Count; j++)
                {
                    CalculateGapScalingAndPositioning(currentGaps, j, buildingSize.x, out propOfWindowForScaling, out propOfWindowForPositioning);
                    float scaleX = currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord - propOfWindowForScaling;
                    GameObject wall = null;
                    //IF statement ensures no walls are created with 0 scale.
                    if (scaleX > 0)
                    {
                        wall = CreateColoredCube("horizontalWall" + wallKey + j + "_Floor" + floorIndex, randomColor, buildingEmptyExterior.transform);
                        wall.transform.localScale = new Vector3(
                            scaleX,
                            floorPropOfBuilding,
                            1.0f / buildingSize.z);

                        if (wallIndex == 0)
                        {
                            wall.transform.position = new Vector3(
                                buildingOriginPos.x + (currentGaps[j].wallXCoord - (currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord) / 2f) * buildingSize.x + propOfWindowForPositioning,
                                floorIndex * floorHeight + floorHeight / 2f,
                                wallArray[wallIndex].transform.position.z - wallThickness / buildingSize.z / 2f);
                        }
                        else
                        {
                            wall.transform.position = new Vector3(
                                buildingOriginPos.x + (currentGaps[j].wallXCoord - (currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord) / 2f) * buildingSize.x + propOfWindowForPositioning,
                                floorIndex * floorHeight + floorHeight / 2f,
                                wallArray[wallIndex].transform.position.z + wallThickness / buildingSize.z / 2f);
                        }

                        if (j < currentGaps.Count - 1)
                        {
                            for (int k = 1; k < 3; k++)
                            {
                                CreateGapSegment(true, wallIndex, floorIndex, k, currentGaps[j], randomColor, buildingEmptyExterior,buildingOriginPos, buildingSize, floorHeight, numOfFloors, wallArray, wallThickness);
                            }
                        }
                    }
                }
            }
        }
    }

    // Helper for left/right wall segments.
    private void CreateSideWallSegments(Color randomColor, Vector3Int buildingSize, int floorHeight, GameObject buildingEmptyExterior,float wallThickness, GameObject[] wallArray, Vector3 buildingOriginPos, float floorPropOfBuilding,int numOfFloors, Dictionary<int, Dictionary<WallSide, List<FloatQuad>>> floorWindowGaps)
    {
        float propOfWindowForScaling;
        float propOfWindowForPositioning;
        for (int wallIndex = 2; wallIndex < 4; wallIndex++)
        {
            WallSide wallKey = (wallIndex == 2) ? WallSide.Left : WallSide.Right;
            for (int floorIndex = 0; floorIndex < numOfFloors; floorIndex++)
            {
                List<FloatQuad> currentGaps = InsertBoundaryGaps(floorWindowGaps[floorIndex][wallKey]);
                for (int j = 1; j < currentGaps.Count; j++)
                {
                    CalculateGapScalingAndPositioning(currentGaps, j, buildingSize.z, out propOfWindowForScaling, out propOfWindowForPositioning);
                    float scaleZ = currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord - propOfWindowForScaling;
                    GameObject wall = null;

                    //IF statement ensures no walls are created with 0 scale.
                    if (scaleZ > 0)
                    {
                        wall = CreateColoredCube("horizontalWall" + wallKey + j + "_Floor" + floorIndex, randomColor, buildingEmptyExterior.transform);
                        wall.transform.localScale = new Vector3(
                            1.0f / buildingSize.x,
                            floorPropOfBuilding,
                            scaleZ);

                        if (wallIndex == 2)
                        {
                            wall.transform.position = new Vector3(
                                wallArray[wallIndex].transform.position.x - DEFAULT_WALL_THICKNESS / buildingSize.x / 2f,
                                floorIndex * floorHeight + floorHeight / 2f,
                                buildingOriginPos.z + (currentGaps[j].wallXCoord - (currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord) / 2f) * buildingSize.z + propOfWindowForPositioning);
                        }
                        else
                        {
                            wall.transform.position = new Vector3(
                                wallArray[wallIndex].transform.position.x + DEFAULT_WALL_THICKNESS / buildingSize.x / 2f,
                                floorIndex * floorHeight + floorHeight / 2f,
                                buildingOriginPos.z + (currentGaps[j].wallXCoord - (currentGaps[j].wallXCoord - currentGaps[j - 1].wallXCoord) / 2f) * buildingSize.z + propOfWindowForPositioning);
                        }

                        if (j < currentGaps.Count - 1)
                        {
                            for (int k = 1; k < 3; k++)
                            {
                                CreateGapSegment(false, wallIndex, floorIndex, k, currentGaps[j], randomColor, buildingEmptyExterior,
                                                 buildingOriginPos, buildingSize, floorHeight, numOfFloors, wallArray, DEFAULT_WALL_THICKNESS);
                            }
                        }
                    }
                }
            }
        }
    }

    // Helper to create a single gap segment.
    private void CreateGapSegment(bool isFrontBack,int wallIndex,int floorIndex,int gapSegmentIndex,FloatQuad gap,Color randomColor,GameObject buildingEmptyExterior,Vector3 buildingOriginPos,Vector3Int buildingSize,int floorHeight,int numOfFloors,GameObject[] wallArray,float wallThickness)
    {
        float propScaling = GAP_SCALE_FACTOR;
        float propPositioning = (gapSegmentIndex == 1) ? -GAP_POSITION_OFFSET : GAP_POSITION_OFFSET;
        float wallAmountPos = (gapSegmentIndex == 1) ? gap.wallYCoord : (1f - gap.wallYCoord);
        float wallAmountScale = (gapSegmentIndex == 1) ? gap.wallYCoord / 2f : 1f - ((1f - gap.wallYCoord) / 2f);
        float scaleY = (Mathf.Max(wallAmountPos - (propScaling * gap.winHeight / floorHeight), 0)) / numOfFloors;
        float scaleX; float scaleZ;
        float posX; float posY; float posZ;
        GameObject gapSegment = gameObject;

        //IF statement ensures no walls are created with 0 scale.
        if (scaleY != 0) {
        gapSegment = CreateColoredCube("verticalSegment_" + (isFrontBack ? ((wallIndex == 0) ? "FrontWall_" : "BackWall_") : ((wallIndex == 2) ? "LeftWall_" : "RightWall_")) + gapSegmentIndex + "_Floor" + floorIndex, randomColor, buildingEmptyExterior.transform);
            }
       
        if (isFrontBack)
        {
            scaleX = gap.winWidth / (float)buildingSize.x;
            scaleZ = 1.0f / buildingSize.z;
            
            posX = buildingOriginPos.x + gap.wallXCoord * buildingSize.x;
            posY = floorIndex * floorHeight + (wallAmountScale * buildingSize.y) / numOfFloors + gap.winHeight * propPositioning;
            posZ = (wallIndex == 0)
                ? wallArray[wallIndex].transform.position.z - wallThickness / buildingSize.z / 2f
                : wallArray[wallIndex].transform.position.z + wallThickness / buildingSize.z / 2f;            
        }
        else
        {
            scaleX = 1.0f / buildingSize.x;
            scaleZ = gap.winWidth / (float)buildingSize.z;

            posX = (wallIndex == 2)
                ? wallArray[wallIndex].transform.position.x - wallThickness / buildingSize.x / 2f
                : wallArray[wallIndex].transform.position.x + wallThickness / buildingSize.x / 2f;
            posY = floorIndex * floorHeight + (wallAmountScale * buildingSize.y) / numOfFloors + gap.winHeight * propPositioning;
            posZ = buildingOriginPos.z + gap.wallXCoord * buildingSize.z;
        }
        gapSegment.transform.position = new Vector3(posX, posY, posZ);
        gapSegment.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
    }

    // Helper method to insert boundary gap points.
    private List<FloatQuad> InsertBoundaryGaps(List<FloatQuad> gaps)
    {
        gaps.Insert(0, new FloatQuad(0f, 0f, 0f, 0f));
        gaps.Add(new FloatQuad(1f, 1f, 0f, 0f));
        return gaps;
    }

    // Helper to calculate gap scaling and positioning.
    private void CalculateGapScalingAndPositioning(List<FloatQuad> gaps, int j, float buildingDimension, out float scaling, out float positioning)
    {
        if (j == 1)
        {
            scaling = (gaps[j].winWidth / 2f) / buildingDimension;
            positioning = -GAP_POSITION_OFFSET * gaps[j].winWidth;
        }
        else if (j == (gaps.Count - 1))
        {
            scaling = (gaps[j - 1].winWidth / 2f) / buildingDimension;
            positioning = GAP_POSITION_OFFSET * gaps[j - 1].winWidth;
        }
        else
        {
            scaling = ((gaps[j].winWidth + gaps[j - 1].winWidth) / 2f) / buildingDimension;
            positioning = -GAP_POSITION_OFFSET * (gaps[j].winWidth - gaps[j - 1].winWidth);
        }
    }

    private static void CreateBuildingContrainers(out GameObject buildingEmpty, out GameObject buildingEmptyExterior, out GameObject buildingEmptyInterior)
    {
        buildingEmpty = new GameObject();
        buildingEmptyExterior = new GameObject();
        buildingEmptyInterior = new GameObject();
        buildingEmptyExterior.transform.SetParent(buildingEmpty.transform);
        buildingEmptyInterior.transform.SetParent(buildingEmpty.transform);
    }

    private static void CreateBaseStructure(string tagName, ref Vector3Int buildingSize, ref Vector3Int buildingPos, GameObject buildingEmpty, GameObject buildingEmptyExterior, GameObject buildingEmptyInterior, float wallThickness, out GameObject frontWall, out GameObject backWall, out GameObject leftWall, out GameObject rightWall, out GameObject floor)
    {
        frontWall = new GameObject();
        backWall = new GameObject();
        leftWall = new GameObject();
        rightWall = new GameObject();
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);

        frontWall.transform.SetParent(buildingEmptyExterior.transform);
        backWall.transform.SetParent(buildingEmptyExterior.transform);
        leftWall.transform.SetParent(buildingEmptyExterior.transform);
        rightWall.transform.SetParent(buildingEmptyExterior.transform);
        floor.transform.SetParent(buildingEmptyExterior.transform);

        buildingEmpty.tag = tagName;
        buildingEmpty.name = tagName;

        buildingEmpty.transform.position = new Vector3(buildingPos.x + buildingSize.x / 2f, buildingSize.y / 2f, buildingPos.z + buildingSize.z / 2f);
        buildingEmpty.transform.localScale = new Vector3(buildingSize.x, buildingSize.y, buildingSize.z);
        
        buildingEmptyExterior.name = tagName + "Exterior";buildingEmptyInterior.name = tagName + "Interior";
        frontWall.transform.position = new Vector3(buildingEmptyExterior.transform.position.x, buildingEmptyExterior.transform.position.y, buildingEmptyExterior.transform.position.z - buildingSize.z / 2f - wallThickness / 2f);
        backWall.transform.position = new Vector3(buildingEmptyExterior.transform.position.x, buildingEmptyExterior.transform.position.y, buildingEmptyExterior.transform.position.z + buildingSize.z / 2f + wallThickness / 2f);
        leftWall.transform.position = new Vector3(buildingEmptyExterior.transform.position.x - buildingSize.x / 2f - wallThickness / 2f, buildingEmptyExterior.transform.position.y, buildingEmptyExterior.transform.position.z);
        rightWall.transform.position = new Vector3(buildingEmptyExterior.transform.position.x + buildingSize.x / 2f + wallThickness / 2f, buildingEmptyExterior.transform.position.y, buildingEmptyExterior.transform.position.z);
        floor.transform.position = new Vector3(buildingEmptyExterior.transform.position.x, buildingEmptyExterior.transform.position.y - buildingSize.y / 2f, buildingEmptyExterior.transform.position.z);

        frontWall.transform.localScale = new Vector3(1, 1, 1.0f / buildingSize.z);
        backWall.transform.localScale = new Vector3(1, 1, 1.0f / buildingSize.z);
        leftWall.transform.localScale = new Vector3(1.0f / buildingSize.x, 1, 1);
        rightWall.transform.localScale = new Vector3(1.0f / buildingSize.x, 1, 1);
        floor.transform.localScale = new Vector3(1, 1.0f / buildingSize.y, 1 + frontWall.transform.localScale.z);

        floor.transform.position = new Vector3(floor.transform.position.x, FLOOR_OFFSET, floor.transform.position.z);
        frontWall.name = "frontWall";backWall.name = "backWall";leftWall.name = "leftWall";rightWall.name = "rightWall";floor.name = "floor";
    }

    private Vector3Int ProcessFloors(string tagName, Vector3Int buildingSize, Vector3Int buildingPos, int floorHeight, GameObject buildingEmpty, GameObject buildingEmptyExterior, GameObject buildingEmptyInterior, out float floorPropOfBuilding, out int numOfFloors, out Dictionary<int, Dictionary<WallSide, List<FloatQuad>>> floorWindowGaps)
    {
        floorPropOfBuilding = floorHeight / (float)buildingSize.y;
        numOfFloors = buildingSize.y / floorHeight;
        floorWindowGaps = new Dictionary<int, Dictionary<WallSide, List<FloatQuad>>>();
        int topFloorNumber = numOfFloors - 1;

        for (int i = 0; i < numOfFloors; i++)
        {
            floorNumber = i;
            Dictionary<WallSide, List<FloatQuad>> currentFloorGaps = ProcessSingleFloor(tagName, buildingSize, buildingPos, floorHeight, i, buildingEmpty, buildingEmptyExterior, buildingEmptyInterior, topFloorNumber);
            floorWindowGaps[i] = currentFloorGaps;
        }
        
        floorGenerator.CreateCeiling(buildingEmpty, buildingPos, buildingSize, floorHeight, topFloorNumber);

        roomSpawner.fixedRoomPositions.Clear();
        roomSpawner.fixedRoomSizes.Clear();

        if (debugMode)
        {
            //This shows the free cells and cells taken within a building
            GridManager.PrintGridWithCoordinates(roomSpawner.grid, tagName, floorNumber, RoomSpawner.gridStep);
        }
        return buildingSize;
    }

    // Process a single floor and return its gap data.
    // Updated: Process a single floor and return its gap data.
    private Dictionary<WallSide, List<FloatQuad>> ProcessSingleFloor(
        string tagName,
        Vector3Int buildingSize,
        Vector3Int buildingPos,
        int floorHeight,
        int floorIndex,
        GameObject buildingEmpty,
        GameObject buildingEmptyExterior,
        GameObject buildingEmptyInterior,
        int topFloorNumber)
    {
        // Ensure this HashSet is declared outside any room loop so it's not reinitialized per room.
        HashSet<Vector3> usedHeaderPositions = new HashSet<Vector3>();
        List<Room> rooms = roomSpawner.GenerateRoomsForFloor(buildingSize.x, buildingSize.z, tagName, floorHeight);
        int roomIndex = 0; // Counter for rooms

        GameObject floorGO = floorGenerator.CreateFloorExcludingFixed(buildingEmpty, rooms, buildingPos, buildingSize, floorIndex, RoomSpawner.gridStep, floorHeight);
        floorGO.transform.SetParent(buildingEmptyInterior.transform);
        floorGO.name = "floor_" + floorIndex;

        foreach (Room room in rooms)
        {
            roomIndex++; // Increment room count
                         // Create the room (which returns or sets up a room GameObject)
            GameObject roomGO = createRoomWalls.CreateRoom(buildingEmpty, buildingEmptyInterior, room, buildingPos, floorIndex, floorHeight, topFloorNumber);
            roomGO.transform.SetParent(floorGO.transform);
        }

        bool isGroundFloor = (floorIndex == 0);
        Dictionary<WallSide, List<FloatQuad>> currentFloorGaps =
            exteriorDoorAndWindowGapGenerator.GenerateExteriorGaps(rooms, buildingPos, buildingSize, floorHeight, isGroundFloor, tagName, buildingEmptyExterior, floorIndex);

        // Get doorHeight from DoorGapCreator
        float doorHeight = doorManager.DoorGapCreator(tagName, floorHeight, usedHeaderPositions);

        foreach (Room room in rooms.ToList())
        {
            // Retrieve the room GameObject from your hierarchy.
            Transform roomTransform = floorGO.transform.Find(room.roomType);
            if (roomTransform != null)
            {
                // Retrieve furniture data (or later, item data) for this room.
                List<FurnitureItem> roomFurniture = RoomFurniture.GetFurnitureForRoom(room);
                List<RoomItem> items = RoomItems.GetItemsForRoom(room);

                // Combine them (order can be adjusted if needed)
                List<PlaceableObject> objectsToPlace = new List<PlaceableObject>();
                objectsToPlace.AddRange(roomFurniture);
                objectsToPlace.AddRange(items);


                InternalWallSegCreator wallSegCreator = FindObjectOfType<InternalWallSegCreator>();
                // (This code may still be used for door-gap related processing if needed.)
                //var roomDoorGapCubes = wallSegCreator.GetRoomDoorGapCubes();

                // Get fixed rectangles for floor tiling (these are in room–local coordinates).
                List<Rect> fixedRects = new List<Rect>();
                if (roomSpawner.GetRoomFixed(room.roomType))
                {
                    fixedRects.Add(BuildingCalculator.GetFixedRoomRectangleForRoom(room, Vector3Int.zero, roomSpawner));
                }
                float margin = 2f;
                List<Rect> doorGapZones = NoFurnitureZonesFinder.GetNoFurnitureZonesForRoom(
                    tagName, room, margin, wallSegCreator.GetRoomDoorGapCubes(), buildingPos, floorHeight, floorIndex);

                // Combine the two lists into one master list:
                List<Rect> noFurnitureZones = new List<Rect>(fixedRects);
                noFurnitureZones.AddRange(doorGapZones);

                // Convert Rect zones into 3D Bounds for the placement algorithm.
                List<Bounds> doorGapBounds = NoFurnitureZonesFinder.ConvertDoorGapZonesToBounds(doorGapZones, 0f, doorHeight, floorHeight, floorIndex);
                List<Bounds> fixedRectBounds = NoFurnitureZonesFinder.ConvertFixedRectsToBounds(fixedRects, floorHeight, floorIndex);

                List<Bounds> noFurnitureZoneBounds = new List<Bounds>();
                noFurnitureZoneBounds.AddRange(doorGapBounds);
                noFurnitureZoneBounds.AddRange(fixedRectBounds);

                NoFurnitureZonesFinder.DebugDrawNoFurnitureZoneBounds(buildingPos, noFurnitureZoneBounds, Color.green, 500f);

                // NEW: Use the generic item placer (which works the same as your furniture placer)
                ItemPlacer3D itemPlacer = new ItemPlacer3D();
                itemPlacer.PlaceItemsInRoom(room, roomTransform, objectsToPlace, buildingPos, floorIndex, floorHeight, noFurnitureZoneBounds, colorApplier);

                // Existing handling for cellBlockRooms remains the same.
                if (room.roomType == RoomsList.cellBlockRoom)
                {
                    //Debug.Log("RoomPos: " + room.position + "   RoomSize:" + room.size + "   floorNumber" + floorIndex);
                    int startingZCoord = room.position.z;
                    int zLengthOfRoom = room.size.z;
                    //Debug.Log("startingZCoord: " + startingZCoord + "   zLength:" + zLengthOfRoom + "   floorNumber" + floorIndex);

                    const int cellWidth = 20; // fixed x dimension

                    // Generate cell rooms for left and right walls.
                    GenerateCellRoomsForWall(room, true, noFurnitureZones, cellWidth, floorHeight, buildingEmpty, buildingEmptyInterior, buildingPos, topFloorNumber, floorGO);
                    GenerateCellRoomsForWall(room, false, noFurnitureZones, cellWidth, floorHeight, buildingEmpty, buildingEmptyInterior, buildingPos, topFloorNumber, floorGO);
                }
            }
            else
            {
                Debug.LogWarning($"Room GameObject for {room.roomType} not found under {buildingEmptyInterior.name}");
            }
        }

        rooms.Clear();
        return currentFloorGaps;
    }


    public struct CellCandidate
    {
        public Vector3Int position; // relative to the cellBlockRoom
        public Vector3Int size;     // size of the cell room
    }

    // availableZStart is the starting z coordinate (relative to the cellBlockRoom) 
    // and availableZLength is the full length available along z.
    List<CellCandidate> CalculateCellCandidates(int availableZStart, int availableZLength, int cellWidth, int floorHeight)
    {
        List<CellCandidate> candidates = new List<CellCandidate>();

        //
        const int numCellsPerSide = 10;

        // Compute cell length as an integer multiple of gridStep.
        int cellLength = (availableZLength / numCellsPerSide / RoomSpawner.gridStep) * RoomSpawner.gridStep;
        // Calculate any leftover gap at the end (if any)
        int leftover = availableZLength - cellLength * numCellsPerSide;

        // For each candidate cell, compute its starting z offset.
        for (int i = 0; i < numCellsPerSide; i++)
        {
            // You can distribute leftover gap on one end (or split it evenly at the start and end).
            int gapOffset = leftover; // Here, simply put at the beginning for simplicity.
            int candidateZStart = availableZStart + gapOffset + i * cellLength;

            // The candidate cell’s position inside the cellBlockRoom:
            // For left wall: x will be 0 (flush with the left edge); for right wall, x will be cellBlockRoom.width - cellWidth.
            // We pass x later when calling our method for left vs. right.
            Vector3Int pos = new Vector3Int(0, 0, candidateZStart);
            Vector3Int size = new Vector3Int(cellWidth, floorHeight, cellLength);
            candidates.Add(new CellCandidate { position = pos, size = size });
            //Debug.Log(" CellRoomPos: " + pos + "   cellRoomSize: " + size);
        }
        return candidates;
    }

    bool CandidateOverlapsNoFurnitureZone(CellCandidate candidate, List<Rect> noFurnitureZones, Vector3Int cellBlockWorldPos, bool isLeftWall, Room cellBlockRoom, int cellWidth)
    {
        // Convert candidate candidate rectangle to world coordinates.
        // Here, candidate.position is relative to cellBlockRoom.
        if (!isLeftWall) {
        candidate.position.x = candidate.position.x + cellBlockRoom.size.x - cellWidth;
        }

        float worldX = cellBlockWorldPos.x + candidate.position.x;
        float worldZ = cellBlockWorldPos.z + candidate.position.z;

        //Debug.Log("WorldX: " + cellBlockWorldPos.x + "    worldZ: " + cellBlockWorldPos.z + "   candX: " + candidate.position.x + "   candZ: " + candidate.position.z);

        Rect candidateRect = new Rect(worldX, worldZ, candidate.size.x, candidate.size.z);

        foreach (Rect nfZone in noFurnitureZones)
        {
            if (candidateRect.Overlaps(nfZone))
            {
                return true;
            }
        }
        return false;
    }

    void GenerateCellRoomsForWall(Room cellBlockRoom, bool isLeftWall, List<Rect> noFurnitureZones, int cellWidth, int floorHeight, GameObject buildingGO, GameObject buildingInteriorGO, Vector3Int buildingPos, int topFloorNumber, GameObject floorGO)
    {
        // Determine wall-specific x position:
        int wallX;
        if (isLeftWall)
        {
            wallX = cellBlockRoom.position.x; // left edge of cellBlockRoom
        }
        else
        {
            wallX = cellBlockRoom.position.x + cellBlockRoom.size.x - cellWidth; // aligned so right edge of the cell room matches cellBlockRoom's right wall
        }

        // Available z range within cellBlockRoom (assuming full height in z):
        int availableZStart = 0; // relative to cellBlockRoom.position.z
        int availableZLength = cellBlockRoom.size.z;
        List<CellCandidate> candidates = CalculateCellCandidates(availableZStart, availableZLength, cellWidth, floorHeight);
        int roomIndex = 1;

        // For each candidate, check for overlap and create a cell room.
        foreach (CellCandidate candidate in candidates)
        {
            
            if (CandidateOverlapsNoFurnitureZone(candidate, noFurnitureZones, cellBlockRoom.position, isLeftWall, cellBlockRoom, cellWidth))
            {
                // Skip this cell candidate as it overlaps a noFurnitureZone.
                continue;
            }

            // Compute world position by combining candidate position with cellBlockRoom offset.
            Vector3Int cellRoomPos = new Vector3Int(wallX, cellBlockRoom.position.y, cellBlockRoom.position.z + candidate.position.z);

            // Assign room type based on wall side
            string roomType = isLeftWall ? "Room_Cell_LeftSide_" + roomIndex : "Room_Cell_RightSide_" + roomIndex;

            // The size is candidate.size, with y inherited from cellBlockRoom.
            Room cellRoom = new Room(cellRoomPos, candidate.size, roomType);

            // Optionally flag which wall gets the door.
            // For left cells, door on right wall; for right cells, door on left wall.
            // You could add a property in Room, e.g.: cellRoom.doorSide = isLeftWall ? "rightWall" : "leftWall";

            // Add cellRoom to your RoomSpawner's list, for instance:
            roomSpawner.AddCellRoom(cellRoom); // Depends on your current system.

            // Create the cell room’s visuals and walls using your existing CreateRoomWalls logic.
            // If desired, call something like:
            GameObject cellBlockGO = createRoomWalls.CreateRoom(buildingGO, buildingInteriorGO, cellRoom, buildingPos, floorNumber, floorHeight, topFloorNumber);
            cellBlockGO.transform.SetParent(floorGO.transform);
            roomIndex++;
        }
    }
}
