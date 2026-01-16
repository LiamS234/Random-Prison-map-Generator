using System.Collections.Generic;
using UnityEngine;

public class CreateRoomWalls : MonoBehaviour
{
    [SerializeField] private RoomConfiguration roomConfiguration;

    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = true;   
    [SerializeField] private WallConfig wallConfig;

    private float wallThickness;
    private float WallOverlapBuffer;

    ColorApplier colorApplier;

    void Awake()
    {
        wallThickness = wallConfig.wallThickness;
        WallOverlapBuffer = wallConfig.wallOverlapBuffer;
        colorApplier = FindObjectOfType<ColorApplier>();
    }

    public GameObject CreateRoom(GameObject building, GameObject buildingInterior, Room room, Vector3Int buildingPos, int floorNumber, int floorHeight, int topFloorNumber)
    {
        // Use a dedicated RoomFactory to build and position the room.
        GameObject roomObj = RoomFactory.CreateRoomObject(buildingInterior, building, room, buildingPos, floorNumber, floorHeight);

        if (debugMode)
        {
            roomConfiguration.CreateRoomVisuals(room, roomObj);
            roomConfiguration.AssignRoomTypeColors();
        }

        // Build walls using a dedicated WallBuilder.
        WallBuilder.CreateWalls(building, roomObj, room, wallThickness, WallOverlapBuffer, colorApplier);

        if (floorNumber != topFloorNumber)
        {
            // Build ramps using a factory that returns the correct ramp strategy.
            IRampBuilder rampBuilder = RampBuilderFactory.GetRampBuilder(room, RoomSpawner.gridStep, colorApplier);
            rampBuilder?.CreateRamp(roomObj, room, floorHeight);
        }
        //Debug.Log("Coords: WorldBuildingPos: " + roomObj.transform.position + "   WorldbuildingSize: " + roomObj.transform.lossyScale + "   LocalbuildingPos: " + roomObj.transform.localPosition + "   LocalbuildingSize: " + roomObj.transform.localScale + "    buildingName: " + building.name + "   roomName: " + room.roomType + "    floor number: " + floorNumber);
        return roomObj;
    }
}
