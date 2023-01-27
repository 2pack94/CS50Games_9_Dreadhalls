using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TMPro;
using MazeData;

// Generates a random maze at game start.
public class LevelGenerator : MonoBehaviour
{
    [Tooltip("Prefab to use as floor blocks.")]
    public GameObject floorPrefab;
    [Tooltip("Prefab to use as wall blocks.")]
    public GameObject wallPrefab;
    [Tooltip("Prefab to use as ceiling blocks.")]
    public GameObject ceilingPrefab;
    [Tooltip("Player GameObject to place in the maze.")]
    public GameObject player;
    [Tooltip("Monster GameObject to place in the maze. Can chase and attack the player.")]
    public GameObject monster;
    [Tooltip("Pickup GameObject to place in the maze. Goal of the maze.")]
    public GameObject pickup;
    [Tooltip("NavMeshSurface to build after level generation.")]
    public NavMeshSurface navMeshSurface;
    [Tooltip("Reference to the GameManager ScriptableObject to increment the level number.")]
    public GameManager gameManager;
    [Tooltip("GUI Text that shows the level number.")]
    public TextMeshProUGUI textMeshLevel;
    [Tooltip("Dimensions of the maze in blocks. The maze is quadratic.")]
    public uint mazeBlockSize = 30;
    [Tooltip("Number of rooms to generate in the maze.")]
    public uint roomsToGenerate = 3;
    [Tooltip("Minimum side length of the rooms.")]
    public uint roomSizeMin = 2;
    [Tooltip("Maximum side length of the rooms.")]
    public uint roomSizeMax = 6;
    [Tooltip("Maximum path length. Primarily controls how many loops are generated.")]
    public uint pathLengthMax = 30;
    [Tooltip("Specifies how straight the maze paths are. Values higher than 1 make the paths more straight.")]
    [Min(0f)]
    public float pathStraightness = 1f;
    [Tooltip("Removes small clusters in the maze. Reduces loops slightly.")]
    public uint clusterSizeMin = 3;
    [Tooltip("Shorten dead ends by this amount of blocks. If this value is very high, " +
        "dead ends will be removed completely.")]
    public uint reduceDeadEndsBy = 0;
    [Tooltip("Probability to generate holes in the floor at suitable places.")]
    [Range(0f, 1f)]
    public float holeDensity = 0;
    [Tooltip("Desired scale of the floor-, wall- and ceiling prefabs. " +
        "Should be at least 1.5 so the player and monster can walk in the maze.")]
    [Min(0f)]
    public float blockScale = 1f;
    [Tooltip("Height of the maze walls in blocks.")]
    public uint mazeBlockHeight = 3;
    [Tooltip("Generate ceiling if true.")]
    public bool generateCeiling = true;
    // Reference to the generated maze data
    private Maze maze;

    void Awake () {
        // Increment the level number for every (re-)load of the scene.
        if (gameManager)
            gameManager.levelNr++;
        if (textMeshLevel)
            textMeshLevel.text += gameManager.levelNr;

        // Disabling the NavMeshAgent before building the NavMesh fixes a warning.
        // But there will still be a warning "Failed to create agent because there is no valid NavMesh" on scene load.
        if (monster)
            monster.GetComponent<NavMeshAgent>().enabled = false;

        GenerateMaze();
        navMeshSurface.BuildNavMesh();

        if (monster)
            monster.GetComponent<NavMeshAgent>().enabled = true;
    }

    // Input: MazeTile from the maze map.
    // Returns the position of the point in the center of the top surface of the corresponding floor block.
    Vector3 MazeTileToFloorPosition(MazeTile tile)
    {
        return new Vector3(tile.x * blockScale, blockScale / 2, tile.z * blockScale);
    }

    // Instantiate a prefab as the child of a parent GameObject. A GameObject can also be made a child like this:
    // childObject.transform.parent = parentObject.transform;
    // (x, z) Position are taken from the MazeTile. tileY is the y coordinate in blocks.
    GameObject CreateChildAtTile(GameObject prefab, GameObject parent, MazeTile tile, int tileY, float scale = 1f)
    {
        GameObject obj = Instantiate(prefab, new Vector3(tile.x * blockScale, tileY * blockScale, tile.z * blockScale),
            Quaternion.identity, parent.transform);
        obj.transform.localScale = new Vector3(scale, scale, scale);
        return obj;
    }

    // Choose a random floor tile from the maze and return the point in the center of the top surface.
    public Vector3 GetRandomFloorPoint()
    {
        List<MazeTile> floorTiles = maze.GetInnerTiles(TileType.Floor).ToList();
        MazeTile randomTile = floorTiles[UnityEngine.Random.Range(0, floorTiles.Count)];
        return MazeTileToFloorPosition(randomTile);
    }

    // Set the GameObject Position to the tile (top surface).
    void TeleportEntityToTile(GameObject entity, MazeTile tile)
    {
        // Set the GameObject to inactive during the teleport. This is needed because of the CharacterController Component
        // of the player. It will otherwise collide and get stuck at the wall when changing it position.
        bool wasActive = entity.activeSelf;
        if (wasActive)
            entity.SetActive(false);
        entity.transform.position = MazeTileToFloorPosition(tile);
        if (wasActive)
            entity.SetActive(true);
    }

    // Generate the 2D maze map and place blocks to build the maze.
    // Place player, monster and pickup into the maze.
    void GenerateMaze()
    {
        maze = new() {
            size = mazeBlockSize,
            roomsToGenerate = roomsToGenerate,
            roomSizeMin = roomSizeMin,
            roomSizeMax = roomSizeMax,
            pathLengthMax = pathLengthMax,
            pathStraightness = pathStraightness,
            clusterSizeMin = clusterSizeMin,
            reduceDeadEndsBy = reduceDeadEndsBy,
            holeDensity = holeDensity
        };
        maze.Generate();

        // Create parent GameObjects for maze blocks
        GameObject floorParent = new("Floor");
        GameObject wallsParent = new("Walls");
        GameObject ceilingParent = new("Ceiling");

        // Instantiate maze blocks
        foreach (var tile in maze.GetAllTiles())
        {
            // create floor
            if (tile.type != TileType.Hole)
            {
                CreateChildAtTile(floorPrefab, floorParent, tile, 0, blockScale);
            }
            // create walls
            if (tile.type == TileType.Wall)
            {
                for (int tileY = 1; tileY <= mazeBlockHeight; tileY++)
                {
                    CreateChildAtTile(wallPrefab, wallsParent, tile, tileY, blockScale);
                }
            }
            // create ceiling
            if (generateCeiling)
            {
                CreateChildAtTile(ceilingPrefab, ceilingParent, tile, (int)mazeBlockHeight + 1, blockScale);
            }
        }

        // Move Player into the maze
        List<MazeTile> floorTiles = maze.GetInnerTiles(TileType.Floor).ToList();
        MazeTile playerSpawnTile = floorTiles[UnityEngine.Random.Range(0, floorTiles.Count)];
        TeleportEntityToTile(player, playerSpawnTile);

        // Sort the tiles after the distance to the playerSpawnTile. From smallest to biggest distance.
        // The pickup and monster can then be placed at a certain distance to the player.
        floorTiles.Sort((MazeTile tileA, MazeTile tileB) => {
            // Comparison Function: Compare the squared distance of both tiles to playerSpawnTile.
            if (
                (new Vector2(playerSpawnTile.x, playerSpawnTile.z) - new Vector2(tileA.x, tileA.z)).sqrMagnitude <
                (new Vector2(playerSpawnTile.x, playerSpawnTile.z) - new Vector2(tileB.x, tileB.z)).sqrMagnitude
            )
            {
                // tileA has a smaller distance. Move it to the left.
                return -1;
            }
            return 1;
        });

        // Move Monster into the maze
        if (monster)
        {
            MazeTile monsterSpawnTile = floorTiles[UnityEngine.Random.Range(floorTiles.Count / 2, floorTiles.Count)];
            TeleportEntityToTile(monster, monsterSpawnTile);
        }

        // Move Pickup into the maze
        if (pickup)
        {
            MazeTile pickupSpawnTile = floorTiles[UnityEngine.Random.Range(floorTiles.Count / 4, floorTiles.Count)];
            pickup.transform.position = MazeTileToFloorPosition(pickupSpawnTile);
            // The pickup should float in the air
            pickup.transform.Translate(new Vector3(0, 0.25f, 0), Space.World);
        }
    }
}
