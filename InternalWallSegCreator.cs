using System.Collections.Generic;
using UnityEngine;

public class InternalWallSegCreator : MonoBehaviour
{
    private Dictionary<(string, Room), List<GameObject>> roomDoorGapCubes = new Dictionary<(string, Room), List<GameObject>>(); // Dictionary to store room and building associations with their door gap cubes.
    private const float SegmentThreshold = 0.05f;

    private ColorApplier colorApplier;

    #region Public API

    /// <summary>
    /// Main integration method. It receives grouped door candidates (by Room and wall type)
    /// and applies the proper resizing strategy: a single door gap or multiple gaps.
    /// </summary>
    public void ResizeWallsForGroupedDoorCandidates(
        Dictionary<(Room, string), List<DoorCandidate>> groupedCandidates,
        float doorWidth, float doorHeight, string buildingName, HashSet<Vector3> usedHeaderPositions)
    {
        foreach (var kvp in groupedCandidates)
        {
            Room room = kvp.Key.Item1;
            string wallType = kvp.Key.Item2;
            List<DoorCandidate> candidates = kvp.Value;
            bool isXAxis = (wallType == "frontWall" || wallType == "backWall");

            if (candidates.Count == 1)
            {
                GameObject wallGO = GetWallGameObject(room, wallType);
                if (wallGO != null)
                {
                    RoomDoorSelection selection = new RoomDoorSelection(room, candidates[0], wallGO);
                    ResizeSingleDoorGap(selection, doorWidth, doorHeight, room.roomType, isXAxis, buildingName, room, usedHeaderPositions);
                }
            }
            else if (candidates.Count > 1)
            {
                ResizeMultipleDoorGaps(room, wallType, candidates, doorWidth, doorHeight, room.roomType, isXAxis, buildingName, usedHeaderPositions);
            }
        }

        LogRoomDoorGapCubes();
    }

    #endregion

    #region Single Door Gap

    void Awake()
    {
        colorApplier = FindObjectOfType<ColorApplier>();
    }

    /// <summary>
    /// Resizes a wall with a single door candidate.
    /// Splits the wall into two segments (before and after the door) and inserts a door header.
    /// </summary>
    /// 
    private void ResizeSingleDoorGap(
        RoomDoorSelection doorSelection, float doorWidth, float doorHeight,
        string roomName, bool isXAxis, string buildingName, Room room, HashSet<Vector3> usedHeaderPositions)
    {
        GameObject wall = doorSelection.wallGameObject;
        if (wall == null)
        {
            Debug.LogWarning("Missing wall object for " + roomName);
            return;
        }

        (Vector3Int roomMin, Vector3Int roomMax) = doorSelection.room.GetBounds();
        Vector3 roomSize = doorSelection.room.size;
        float candidateCoord = isXAxis ? doorSelection.doorCandidate.gridPos.x : doorSelection.doorCandidate.gridPos.y;
        float roomMinCoord = isXAxis ? roomMin.x : roomMin.z;
        float roomMaxCoord = isXAxis ? roomMax.x : roomMax.z;
        float roomDim = isXAxis ? roomSize.x : roomSize.z;

        // Calculate segment sizes.
        float firstSegSize = candidateCoord - roomMinCoord;
        float secondSegSize = roomMaxCoord - (candidateCoord + doorWidth);
        if (firstSegSize < 0f || secondSegSize < 0f)
        {
            Debug.LogWarning($"Door gap on {(isXAxis ? "x" : "z")}-axis does not fit in room {roomName}");
            return;
        }

        float firstNorm = firstSegSize / roomDim;
        float secondNorm = secondSegSize / roomDim;
        float firstCenterWorld = roomMinCoord + firstSegSize * 0.5f;
        float secondCenterWorld = candidateCoord + doorWidth + secondSegSize * 0.5f;
        float firstCenterLocal = CoordinateUtils.WorldToLocal(firstCenterWorld, roomMinCoord, roomDim);
        float secondCenterLocal = CoordinateUtils.WorldToLocal(secondCenterWorld, roomMinCoord, roomDim);

        Vector3 origScale = wall.transform.localScale;
        Vector3 origPos = wall.transform.localPosition;

        //IF statement ensures no walls are created with 0 scale. Need to destroy original wall if it is going to have zero scale. If later it's set so doorGaps have to be a minimum distance from edge of wall then won't need this IF statement.
        if (firstNorm != 0)
        {
            // Adjust first (original) wall segment.
            (Vector3 firstScale, Vector3 firstPos) = SegmentCalculator.CalculateWallSegment(
                isXAxis, firstNorm, firstCenterLocal, origScale, isXAxis ? origPos.z : origPos.x, origPos.y);
            wall.transform.localScale = firstScale;
            wall.transform.localPosition = firstPos;
            wall.name = "internal_wall";

            Renderer rend = wall.GetComponent<Renderer>();
            colorApplier.ApplyColor(rend, Color.white);
        } else
        {
            Destroy(wall);
        }

        //IF statement ensures no walls are created with 0 scale.
        if (secondNorm != 0)
        {
            // Create second wall segment.
            GameObject secondWall = Instantiate(wall, wall.transform.parent);
            (Vector3 secondScale, Vector3 secondPos) = SegmentCalculator.CalculateWallSegment(
                isXAxis, secondNorm, secondCenterLocal, origScale, isXAxis ? origPos.z : origPos.x, secondWall.transform.localPosition.y);
            secondWall.transform.localScale = secondScale;
            secondWall.transform.localPosition = secondPos;
            wall.name = "internal_wall";

            Renderer rend = wall.GetComponent<Renderer>();
            colorApplier.ApplyColor(rend, Color.white);
        }

        // Create door header for a single door gap.
        float headerCenterWorld = candidateCoord + doorWidth * 0.5f;
        float headerCenterLocal = CoordinateUtils.WorldToLocal(headerCenterWorld, roomMinCoord, roomDim);
        (Vector3 headerScale, Vector3 headerPos) = SegmentCalculator.CalculateHeaderSegment(
            isXAxis, doorWidth / roomDim, headerCenterLocal, origScale,
            isXAxis ? origPos.z : origPos.x, roomMin.y, roomSize.y, doorHeight);

        // Convert headerPos (room-relative) to building-relative.
        Vector3 buildingHeaderPos = Vector3.Scale(headerPos - new Vector3(-0.5f, -0.5f, -0.5f), room.size) + room.position;
        float tolerance = 2.0f;

        // Check for duplicate.
        if (!IsHeaderPosDuplicate(buildingHeaderPos, usedHeaderPositions, tolerance))
        {
            usedHeaderPositions.Add(buildingHeaderPos);

            // Create door header.
            SegmentRenderer.CreateDoorHeader(headerScale, headerPos, wall.transform.parent, "door_header", colorApplier);

            // Create door gap cube.
            GameObject doorGapCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = doorGapCube.GetComponent<Collider>();
            if (col != null)
            {
                Object.Destroy(col);
            }
            doorGapCube.name = "door_gap";
            doorGapCube.transform.SetParent(wall.transform.parent);
            ScaleRotatePosDoorGapCube(headerScale, headerPos, doorHeight, roomSize, room, isXAxis, doorGapCube);

            // Optionally assign material...
            Renderer cubeRenderer = doorGapCube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                Material transparentMaterial = CreateTransparentMaterial();
                cubeRenderer.material = transparentMaterial;
                cubeRenderer.material.color = GetRoomColor(doorSelection.room);
            }

            // Store the created cube in the dictionary for future reference.
            if (!roomDoorGapCubes.ContainsKey((buildingName, doorSelection.room)))
            {
                roomDoorGapCubes[(buildingName, doorSelection.room)] = new List<GameObject>();
            }
            roomDoorGapCubes[(buildingName, doorSelection.room)].Add(doorGapCube);
        }
        else
        {
            Transform parent = wall.transform.parent;
            CreateDoorNoFurnitureZone(headerScale, headerPos, doorHeight, roomSize,
                            roomName, buildingName, room, parent, doorWidth, isXAxis);
            //Debug.Log($"[SKIP] Duplicate header position (within tolerance) found at {buildingHeaderPos}. Door header and gap cube not created.");
        }
    }

    #endregion

    #region Multiple Door Gaps

    /// <summary>
    /// Resizes a wall with multiple door candidates.
    /// Splits the wall into wall segments and door header segments along the chosen axis.
    /// </summary>
    private void ResizeMultipleDoorGaps(
Room room, string wallType, List<DoorCandidate> doorCandidates,
float doorWidth, float doorHeight, string roomName, bool isXAxis, string buildingName, HashSet<Vector3> usedHeaderPositions)
    {
        GameObject originalWall = GetWallGameObject(room, wallType);
        if (originalWall == null)
        {
            Debug.LogWarning($"[WARNING] No wall found for {roomName} {wallType}");
            return;
        }

        (Vector3Int roomMin, Vector3Int roomMax) = room.GetBounds();
        Vector3 roomSize = room.size;
        float roomMinCoord = isXAxis ? roomMin.x : roomMin.z;
        float roomMaxCoord = isXAxis ? roomMax.x : roomMax.z;
        float roomDim = isXAxis ? roomSize.x : roomSize.z;
        float fixedCoord = isXAxis ? originalWall.transform.localPosition.z : originalWall.transform.localPosition.x;

        // Instantiate the wall template and log unique instance
        GameObject wallTemplate = Instantiate(originalWall);
        wallTemplate.name = $"WallTemplate_{roomName}_{wallType}";
        Transform parent = originalWall.transform.parent;
        Vector3 originalTemplateScale = originalWall.transform.localScale;
        DestroyImmediate(originalWall);

        // Sort door candidates along the primary axis
        doorCandidates.Sort((a, b) => isXAxis ? a.gridPos.x.CompareTo(b.gridPos.x) : a.gridPos.y.CompareTo(b.gridPos.y));

        foreach (DoorCandidate candidate in doorCandidates)
        {
            //Debug.Log("BuildingName: " + buildingName + "      RoomName: " + room.roomType + "  Position: " + candidate.gridPos + "   WallType: " + wallType);
        }

        List<WallSegment> segments = BuildSegments(roomMinCoord, roomMaxCoord, doorWidth, doorCandidates, isXAxis);

        int segIndex = 0;
        int usedTemplates = 0; // Track how many templates are actually used



        foreach (WallSegment seg in segments)
        {
            if (seg.Length < SegmentThreshold)
            {
                segIndex++;
                continue;
            }

            float normalizedLength = seg.Length / roomDim;
            float centerWorld = seg.Center;
            float localCenter = CoordinateUtils.WorldToLocal(centerWorld, roomMinCoord, roomDim);

            if (normalizedLength != 0)
            {
                if (!seg.IsGap)
                {
                    (Vector3 segScale, Vector3 segPos) = SegmentCalculator.CalculateWallSegment(
                        isXAxis, normalizedLength, localCenter,
                        wallTemplate.transform.localScale,
                        fixedCoord,
                        wallTemplate.transform.localPosition.y);

                    SegmentRenderer.CreateWallSegment(wallTemplate, parent, segScale, segPos,
                        $"WallSegment_{roomName}_{wallType}_{segIndex}", colorApplier);
                    usedTemplates++;
                }
                else
                {
                    (Vector3 headerScale, Vector3 headerPos) = SegmentCalculator.CalculateHeaderSegment(
                        isXAxis, normalizedLength, localCenter,
                        originalTemplateScale,
                        fixedCoord,
                        roomMin.y, roomSize.y, doorHeight);

                    // Calculate a building-relative header position. 
                    // Here we assume that headerPos is room-relative and that room.position and roomBuildingOffset are provided.
                    Vector3 buildingHeaderPos = Vector3.Scale(headerPos - new Vector3(-0.5f, -0.5f, -0.5f), room.size) + room.position;

                    //Debug.Log(" Building: " + buildingName + "    room: " + room.roomType +
                              //"   headerPos (room-relative): " + headerPos +
                              //"  buildingHeaderPos (for check): " + buildingHeaderPos);

                    // Define your tolerance value (e.g., 2.0 units)
                    float tolerance = 2.0f;

                    // Use the distance-based duplicate check.
                    if (!IsHeaderPosDuplicate(buildingHeaderPos, usedHeaderPositions, tolerance))
                    {
                        usedHeaderPositions.Add(buildingHeaderPos);
                        SegmentRenderer.CreateDoorHeader(headerScale, headerPos, parent,
                            "door_header", colorApplier);
                        CreateDoorGapCube(headerScale, headerPos, doorHeight, roomSize,
                            roomName, buildingName, room, parent, doorWidth, isXAxis);
                        CreateDoorNoFurnitureZone(headerScale, headerPos, doorHeight, roomSize,
                            roomName, buildingName, room, parent, doorWidth, isXAxis);
                    }
                    else
                    {
                        CreateDoorNoFurnitureZone(headerScale, headerPos, doorHeight, roomSize,
                            roomName, buildingName, room, parent, doorWidth, isXAxis);
                        //Debug.Log($"[SKIP] Duplicate header position found (within tolerance) at {buildingHeaderPos}");
                    }
                }
            }
            segIndex++;
        }



        //Need to destroy the template at the end otherwise left with duplicate walls in world position zero. Tried to place these as the last segment but it involves a lot more code. Destroying one per call shouldn't be too bad..
        DestroyImmediate(wallTemplate);

        if (usedTemplates == 0)
        {
            Debug.LogWarning($"[WARNING] Wall template was instantiated but never used in {roomName} ({buildingName})!");
        }
    }

    /// <summary>
    /// Creates a door gap cube with a transparent material and stores it in the room door gap dictionary.
    /// </summary>
    private void CreateDoorGapCube(Vector3 headerScale, Vector3 headerPos, float doorHeight, Vector3 roomSize, string roomName, string buildingName, Room room, Transform parent, float doorWidth, bool isXAxis)
    {
        GameObject doorGapCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorGapCube.name = "door_Gap_Cube";
        doorGapCube.transform.SetParent(parent);
        ScaleRotatePosDoorGapCube(headerScale, headerPos, doorHeight, roomSize, room, isXAxis, doorGapCube);

        Collider col = doorGapCube.GetComponent<Collider>();
        if (col != null)
        {
            Object.Destroy(col);
        }
        // Assign a transparent material.
        Renderer cubeRenderer = doorGapCube.GetComponent<Renderer>();
        if (cubeRenderer != null)
        {
            Material transparentMaterial = CreateTransparentMaterial();
            cubeRenderer.material = transparentMaterial;
            cubeRenderer.material.color = GetRoomColor(room);
        }
    }

    private static void ScaleRotatePosDoorGapCube(Vector3 headerScale, Vector3 headerPos, float doorHeight, Vector3 roomSize, Room room, bool isXAxis, GameObject doorGapCube)
    {
        // Set the cube's scale and position.
        doorGapCube.transform.localScale = new Vector3(headerScale.x, doorHeight / roomSize.y, headerScale.z);
        doorGapCube.transform.localPosition = new Vector3(headerPos.x, headerPos.y - headerScale.y / 2f - (doorHeight / roomSize.y) / 2f, headerPos.z);

        if (!isXAxis)
        {
            doorGapCube.transform.localRotation = Quaternion.Euler(0, 90, 0);
            doorGapCube.transform.localScale = new Vector3(headerScale.z, doorHeight / room.size.y, headerScale.x);
        }
    }

    private void CreateDoorNoFurnitureZone(Vector3 headerScale, Vector3 headerPos, float doorHeight, Vector3 roomSize, string roomName, string buildingName, Room room, Transform parent, float doorWidth, bool isXAxis)
    {
        GameObject doorNoFurnitureZone = new GameObject();
        doorNoFurnitureZone.name = "door_gap_NoFurnZone";
        doorNoFurnitureZone.transform.SetParent(parent);

        // Set the cube's scale and position.
        doorNoFurnitureZone.transform.localScale = new Vector3(headerScale.x, doorHeight / roomSize.y, headerScale.z);
        doorNoFurnitureZone.transform.localPosition = new Vector3(headerPos.x, headerPos.y - headerScale.y / 2f - (doorHeight / roomSize.y) / 2f, headerPos.z);

        if (!isXAxis)
        {
            doorNoFurnitureZone.transform.localRotation = Quaternion.Euler(0, 90, 0);
            doorNoFurnitureZone.transform.localScale = new Vector3(headerScale.z, doorHeight / room.size.y, headerScale.x);
        }

        // Use the correct key: (buildingName, room)
        var key = (buildingName, room);
        if (!roomDoorGapCubes.ContainsKey(key))
        {
            roomDoorGapCubes[key] = new List<GameObject>();
        }
        roomDoorGapCubes[key].Add(doorNoFurnitureZone);
    }


    /// <summary>
    /// Creates a transparent material using the Universal Render Pipeline Lit shader.
    /// </summary>
    private Material CreateTransparentMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent surface type.
        mat.SetFloat("_Blend", 1); // Enable alpha blending.
        mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    /// <summary>
    /// Builds a list of wall segments (both wall and gap types) based on door candidate positions.
    /// </summary>
    private List<WallSegment> BuildSegments(
        float roomMinCoord, float roomMaxCoord, float doorWidth,
        List<DoorCandidate> doorCandidates, bool isXAxis)
    {
        List<WallSegment> segments = new List<WallSegment>();
        float current = roomMinCoord;
        foreach (DoorCandidate candidate in doorCandidates)
        {
            float gapStart = isXAxis ? candidate.gridPos.x : candidate.gridPos.y;
            float gapEnd = gapStart + doorWidth;
            if (gapStart > current)
                segments.Add(new WallSegment(current, gapStart, false));
            segments.Add(new WallSegment(gapStart, gapEnd, true));
            current = gapEnd;
        }
        if (current < roomMaxCoord)
            segments.Add(new WallSegment(current, roomMaxCoord, false));
        return segments;
    }
    #endregion

    #region Nested Helper Classes

    /// <summary>
    /// Provides basic coordinate conversion utilities.
    /// </summary>
    private static class CoordinateUtils
    {
        public static float WorldToLocal(float worldCoord, float roomMin, float roomSize)
        {
            return ((worldCoord - roomMin) / roomSize) - 0.5f;
        }

        public static float ComputeHeaderScaleY(float roomHeight, float doorHeight)
        {
            return (roomHeight - doorHeight) / roomHeight;
        }

        public static float ComputeHeaderCenterY(float roomMinY, float roomHeight, float doorHeight)
        {
            float headerWorldY = roomMinY + doorHeight + (roomHeight - doorHeight) * 0.5f;
            return WorldToLocal(headerWorldY, roomMinY, roomHeight);
        }
    }

    /// <summary>
    /// Computes the scale and position for wall segments and door headers.
    /// </summary>
    private static class SegmentCalculator
    {
        public static (Vector3 scale, Vector3 pos) CalculateWallSegment(
            bool isXAxis, float normalizedLength, float localCenter,
            Vector3 templateScale, float fixedCoord, float originalY)
        {
            if (isXAxis)
            {
                return (new Vector3(normalizedLength, templateScale.y, templateScale.z),
                        new Vector3(localCenter, originalY, fixedCoord));
            }
            else
            {
                return (new Vector3(templateScale.x, templateScale.y, normalizedLength),
                        new Vector3(fixedCoord, originalY, localCenter));
            }
        }

        public static (Vector3 scale, Vector3 pos) CalculateHeaderSegment(
            bool isXAxis, float normalizedLength, float localCenter,
            Vector3 templateScale, float fixedCoord, float roomMinY, float roomHeight, float doorHeight)
        {
            float headerScaleY = CoordinateUtils.ComputeHeaderScaleY(roomHeight, doorHeight);
            float headerCenterY = CoordinateUtils.ComputeHeaderCenterY(roomMinY, roomHeight, doorHeight);
            if (isXAxis)
            {
                return (new Vector3(normalizedLength, headerScaleY, templateScale.z),
                        new Vector3(localCenter, headerCenterY, fixedCoord));
            }
            else
            {
                return (new Vector3(templateScale.x, headerScaleY, normalizedLength),
                        new Vector3(fixedCoord, headerCenterY, localCenter));
            }
        }
    }

    /// <summary>
    /// Handles instantiation of wall segments and door headers.
    /// </summary>
    private static class SegmentRenderer
    {
        public static void CreateWallSegment(GameObject template, Transform parent, Vector3 scale, Vector3 position, string objName, ColorApplier colorApplier)
        {
            GameObject segObj = Object.Instantiate(template, parent);
            segObj.name = objName;
            segObj.transform.localScale = scale;
            segObj.transform.localPosition = position;


            
        }

        public static void CreateDoorHeader(Vector3 headerScale, Vector3 headerLocalPos, Transform parent, string headerName, ColorApplier colorApplier)
        {
            GameObject doorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorHeader.name = headerName;
            doorHeader.transform.SetParent(parent);
            doorHeader.transform.localScale = headerScale;
            doorHeader.transform.localPosition = headerLocalPos;

            Renderer rend = doorHeader.GetComponent<Renderer>();
            colorApplier.ApplyColor(rend, Color.white);
        }
    }

    bool IsHeaderPosDuplicate(Vector3 pos, HashSet<Vector3> usedPositions, float tolerance)
    {
        foreach (var existing in usedPositions)
        {
            if ((pos - existing).magnitude <= tolerance)
            {
                return true; // Duplicate found within tolerance
            }
        }
        return false;
    }


    #endregion

    #region Utility Types

    /// <summary>
    /// A simple struct that defines a segment along a wall.
    /// </summary>
    private struct WallSegment
    {
        public float Start;
        public float End;
        public bool IsGap;

        public WallSegment(float start, float end, bool isGap)
        {
            Start = start;
            End = end;
            IsGap = isGap;
        }

        public float Length => End - Start;
        public float Center => Start + Length * 0.5f;
    }
    #endregion

    #region Wall Retrieval

    /// <summary>
    /// Returns the wall GameObject for the specified room and wall type.
    /// </summary>
    private GameObject GetWallGameObject(Room room, string wall)
    {
        switch (wall)
        {
            case "frontWall": return room.frontWall;
            case "backWall": return room.backWall;
            case "leftWall": return room.leftWall;
            case "rightWall": return room.rightWall;
            default:
                Debug.LogWarning($"Unknown wall type: {wall} for room {room.roomType}");
                return null;
        }
    }
    #endregion

    public void LogRoomDoorGapCubes()
    {
        foreach (var entry in roomDoorGapCubes)
        {
            string buildingName = entry.Key.Item1;
            Room room = entry.Key.Item2;
            List<GameObject> doorGapCubes = entry.Value;

            //Debug.Log($"Building: {buildingName}, Room: ({room.roomType})");

            foreach (GameObject cube in doorGapCubes)
            {
                //Debug.Log($"  Door Gap Cube: {cube.name} at Position: {cube.transform.position}" + "Building: " + buildingName);
            }
        }
    }

    public Dictionary<(string, Room), List<GameObject>> GetRoomDoorGapCubes()
    {
        return roomDoorGapCubes;
    }

    private Color GetRoomColor(Room room)
    {
        // Example color mapping based on room type.
        switch (room.roomType)
        {
            case RoomsList.staircaseRoom:
                return new Color(0.5f, 0.5f, 1f, 0.5f); // Light blue
            case RoomsList.cellBlockRoom:
                return new Color(0.2f, 0.8f, 0.2f, 0.5f); // Bright green
            case RoomsList.showerRoom:
                return new Color(0.2f, 0.6f, 1f, 0.5f); // Sky blue
            case RoomsList.visitationRoom:
                return new Color(1f, 0.4f, 0.4f, 0.5f); // Soft red
            case RoomsList.toiletRoom:
                return new Color(0.8f, 0.8f, 0.2f, 0.5f); // Yellow-green
            case RoomsList.laundryRoom:
                return new Color(0.7f, 0.7f, 1f, 0.5f); // Light lavender
            case RoomsList.laundryRoom1:
                return new Color(1f, 0.6f, 0.2f, 0.5f); // Orange
            case RoomsList.laundryRoom2:
                return new Color(0.6f, 0.2f, 0.6f, 0.5f); // Purple
            case RoomsList.laundryRoom3:
                return new Color(0.2f, 1f, 0.6f, 0.5f); // Teal
            default:
                return new Color(0.5f, 0.5f, 0.5f, 0.5f); // Default gray
        }
    }    
}
