using System;
using System.Collections.Generic;
using System.Linq;
using HelperClasses;

namespace MazeData
{

// Maze Generation:
// The maze is represented by a maze map, which is a Matrix of MazeTile objects.
// During maze generation a certain number of tiles gets converted from wall tiles to floor tiles.
// Each floor area separated by walls constitutes a cluster.
// First a specified number of rooms is created. Then paths are generated until the maze area is filled.
// Each path has a maximum length. During path generation no loops will be created.
// All clusters (unconnected rooms and paths) will get connected to one big cluster by removing separating wall tiles.
// Additional features:
//  Creating holes in the floor using the hole tile type.
//  Reducing the length (and therefore also the number) of dead ends.
//  Modifying the straightness of the paths.

// Notes:
// The number of loops can be primarily controlled with the maximum path length. Rooms and small paths
// can also lead to loops when they are connected.
// Even if the path length is greater than the tiles in the maze there can be still room to generate small paths
// with a few tiles. When these paths get connected, it can lead to loops, even if its only 1 connection.
// Removing small clusters before connecting will reduce loops slightly.
// Rooms will always connect to each other when they are 1 tile apart.

// Used to encode the direction of maze tiles relative to each other.
public enum Direction
{
    Left,
    Right,
    Up,
    Down,
    None
}

// Specifies the type of a MazeTile object
// "Wall" is the default. Tiles that should be walkable will have the type "Floor".
// "Hole" is a tile where the player can fall through.
public enum TileType
{
    Wall,
    Floor,
    Hole
}

// A cluster contains a list of multiple connected MazeTile objects with type "Floor" or "Hole".
// It is used to indicate which areas of the maze are connected.
// Two unconnected areas (separated by walls) will have their own cluster reference.
// If two areas get connected, they must agree to reference the same cluster.
// After connection, the old cluster object may be discarded.
public class Cluster
{
    // List that contains all tiles in the Cluster
    public List<MazeTile> tiles;
    // If this cluster gets converted to another cluster due to a cluster connection,
    // the reference to the new cluster gets set here.
    public Cluster convertedTo;

    public Cluster()
    {
        tiles = new();
    }

    // Convert this cluster to another cluster.
    // Every MazeTile from this cluster gets added to the other cluster. There will be no tiles left in this cluster.
    public void Convert(Cluster otherCluster)
    {
        if (otherCluster == this)
            return;
        foreach (var tile in tiles)
        {
            tile.cluster = otherCluster;
            otherCluster.tiles.Add(tile);
        }
        // The tile list should not be referenced any more.
        tiles = null;
        convertedTo = otherCluster;
    }

    // Reset each MazeTile in this cluster.
    public void Delete()
    {
        foreach (var tile in tiles)
        {
            tile.cluster = null;
            tile.type = TileType.Wall;
        }
        // The tile list should not be referenced any more.
        tiles = null;
    }
}

// Helper Class used when making Cluster connections.
// A cluster connection initially consists of 2 Cluster objects and the wall tiles
// that are the border between the two.
public class ClusterConnection
{
    // The clusterSet initially contains 2 clusters that shall be connected.
    // Its possible for both clusters to get converted to other clusters from another ClusterConnection object.
    // Its possible for both clusters to get connected indirectly through other ClusterConnection objects.
    // In this case the clusterSet will only contain 1 cluster after updating the Set.
    public HashSet<Cluster> clusterSet;
    // Wall tiles that can potentially connect both clusters.
    public List<MazeTile> connectingTiles;

    public ClusterConnection()
    {
        clusterSet = new();
        connectingTiles = new();
    }

    // When making cluster connections, the Cluster objects in the clusterSet might get outdated.
    // This method should be called before making a connection
    // to replace references to outdated clusters in the the clusterSet.
    // Old cluster objects that do not get referenced any more will be garbage collected.
    public void UpdateClusterSet()
    {
        // Capture every cluster that already got converted.
        // A HashSet cannot be modified while iterating over it, so a second loop has to be done.
        HashSet<Cluster> clustersChanged = new();
        foreach (var cluster in clusterSet)
        {
            if (cluster.convertedTo != null)
            {
                clustersChanged.Add(cluster);
            }
        }
        // Update clusterSet in case the clusters in it got converted.
        foreach (var cluster in clustersChanged)
        {
            Cluster clusterConvertedTo = cluster.convertedTo;
            // A cluster may get converted more than once during connecting the clusters.
            while (clusterConvertedTo.convertedTo != null)
            {
                clusterConvertedTo = clusterConvertedTo.convertedTo;
            }
            clusterSet.Remove(cluster);
            clusterSet.Add(clusterConvertedTo);
        }
    }

    // Connect the clusters in clusterSet by picking 1 wall tile and converting it to a floor tile.
    public void Connect()
    {
        UpdateClusterSet();

        // Even if clusterSet has only 1 cluster because the clusters are already connected, remove 1 wall.
        // The reason why the connection is made anyways is because the number of connections/ loops
        // in the maze should be controlled by the total number of clusters before connecting
        // and not depend on the order of making the connections.
        Cluster biggestCluster = Maze.GetBiggestCluster(clusterSet);

        // Pick random wall tile and convert it to a floor to make a connection.
        connectingTiles[UnityEngine.Random.Range(0, connectingTiles.Count)].MakeFloor(biggestCluster);

        // Convert the smaller cluster in the clusterSet to the bigger cluster.
        foreach (var cluster in clusterSet)
        {
            if (cluster != biggestCluster)
            {
                cluster.Convert(biggestCluster);
            }
        }
    }
}

// Each Element of the maze map is represented by a MazeTile object.
public class MazeTile
{
    // (x, z) coordinate of the tile in the maze map.
    // (y is not used because in a 3D game the y-axis points upwards)
    public int x;
    public int z;
    public TileType type;
    // If type is not Wall, there will be a reference to the cluster the tile belongs to.
    public Cluster cluster;

    public MazeTile(int _x, int _z)
    {
        x = _x;
        z = _z;
        type = TileType.Wall;
        cluster = null;
    }

    // Revert all MazeTile type and cluster to their defaults.
    public void Reset()
    {
        type = TileType.Wall;
        if (cluster != null)
        {
            cluster.tiles.Remove(this);
            cluster = null;
        }
    }

    // Make a floor out of this tile and assign a cluster.
    public void MakeFloor(Cluster clusterAssign)
    {
        type = TileType.Floor;
        cluster = clusterAssign;
        cluster.tiles.Add(this);
    }

    // Called when converting this object to a string (when printing)
    public override string ToString()
        => $"Tile({x}, {z})";
    
    // GetHashCode serves as a hash function when the object is used as a key in a hash table.
    // E.g. used when finding a key in Dictionary<TKey, TValue> or HashSet<T>
    public override int GetHashCode()
    {
        return HashCode.Combine(x, z);
    }

    // override Equals method. Two MazeTile objects shall be equal if they have the same (x, z) coordinates.
    // Or if their reference is the same or they are both null.
    public override bool Equals(object obj)
    {
        return Equals(obj as MazeTile);
    }

    public bool Equals(MazeTile other)
        => ReferenceEquals(this, other) || (other is not null && x == other.x && z == other.z);

    // overload comparison operators
     public static bool operator ==(MazeTile tileA, MazeTile tileB)
         => ReferenceEquals(tileA, tileB) || (tileA is not null && tileA.Equals(tileB));

    public static bool operator !=(MazeTile tileA, MazeTile tileB)
        => !(tileA == tileB);
}

// The MazeRoom class is used to generate rectangular areas of Floor Tiles in the maze map.
public class MazeRoom
{
    // (x, z) coordinates of the top-left MazeTile of the Room.
    public int x;
    public int z;
    // Number of tiles in x direction
    public int width;
    // Number of tiles in z direction
    public int length;
    public Maze maze;

    public MazeRoom(Maze _maze)
    {
        maze = _maze;
    }

    // Returns true if the tile is within the room or if the tile is next to the room edge.
    public bool IsConnected(MazeTile tile)
    {
        if (
            x > tile.x + 1 || x + width + 1 < tile.x ||
            z > tile.z + 1 || z + length + 1 < tile.z
        )
        {
            return false;
        }
        // If the tile is diagonally adjacent to a room edge (cannot be reached by walking from this room)
        if (
            (tile.x == x - 1     && tile.z == z - 1) ||
            (tile.x == x + width && tile.z == z - 1) ||
            (tile.x == x - 1     && tile.z == z + length) ||
            (tile.x == x + width && tile.z == z + length)
        )
        {
            return false;
        }
        return true;
    }

    // Get all clusters that have tiles that are connected to this room.
    public HashSet<Cluster> GetConnectedClusters()
    {
        HashSet<Cluster> clusterSet = new();

        for (int mazeZ = z - 1; mazeZ < z + length + 1; mazeZ++)
        {
            for (int mazeX = x - 1; mazeX < x + width + 1; mazeX++)
            {
                if (maze.mazeMap[mazeZ, mazeX].cluster != null && IsConnected(maze.mazeMap[mazeZ, mazeX]))
                    clusterSet.Add(maze.mazeMap[mazeZ, mazeX].cluster);
            }
        }
        return clusterSet;
    }

    // Modify tiles in the maze map represent a room.
    public void Generate()
    {
        // (re-)initialize Room dimensions and coordinates according to the Maze parameters.
        // The room gets placed into the Maze regardless of overlapping floor tiles.
        width = UnityEngine.Random.Range((int)maze.roomSizeMin, (int)maze.roomSizeMax + 1);
        length = UnityEngine.Random.Range((int)maze.roomSizeMin, (int)maze.roomSizeMax + 1);
        width = Math.Min(width, maze.mazeMap.GetLength(1) - 2);
        length = Math.Min(length, maze.mazeMap.GetLength(0) - 2);
        x = UnityEngine.Random.Range(1, maze.mazeMap.GetLength(1) - width);
        z = UnityEngine.Random.Range(1, maze.mazeMap.GetLength(0) - length);

        // Convert all connected clusters to the biggest cluster or create a new one if no cluster was connected.
        HashSet<Cluster> connectedClusters = GetConnectedClusters();
        Cluster biggestCluster = Maze.GetBiggestCluster(connectedClusters);
        if (biggestCluster == null)
        {
            biggestCluster = new();
        }
        foreach (var connectedCluster in connectedClusters)
        {
            if (connectedCluster != biggestCluster)
            {
                connectedCluster.Convert(biggestCluster);
            }
        }
        
        // Modify wall tiles in the mazeMap
        for (int mazeZ = z; mazeZ < z + length; mazeZ++)
        {
            for (int mazeX = x; mazeX < x + width; mazeX++)
            {
                if (maze.mazeMap[mazeZ, mazeX].cluster == null)
                {
                    maze.mazeMap[mazeZ, mazeX].MakeFloor(biggestCluster);
                }
            }
        }
    }
}

// The MazePath class is used to generate a path of floor tiles in the maze map.
public class MazePath
{
    // Tiles that got added to the path during generation.
    public List<MazeTile> tiles;
    public Maze maze;

    public MazePath(Maze _maze)
    {
        maze = _maze;
    }

    // Modify the maze map to create a path in the maze.
    // The path will be generated as long as pathLengthMax is not exceeded or as long as
    // any tile in the path has an adjacent wall tile that can be used to extend the path.
    // The path will be non-looping and not connected to any other floor tiles in the maze.
    // Returns true if a path could be created, returns false if there is no space left in the maze to create a path.
    public bool Generate()
    {
        tiles = new();
        // Tiles in the path that do not have adjacent wall tiles usable to extend the path.
        List<MazeTile> tilesNotUsable = new();
        // Tiles in the path that can be checked if they have adjacent wall tiles usable to extend the path.
        List<MazeTile> tilesUsable = new();

        // Find a tile that is not part of or connected to a cluster (surrounded by walls).
        MazeTile currentTile = maze.FindWallAreaRandom();
        if (currentTile == null)
            return false;
        
        Cluster cluster = new();
        // Store the previously created floor tile. This is only relevant if the pathStraightness
        // maze parameter is used to make the path more/ less straight.
        MazeTile previousTile = null;

        while (true)
        {
            currentTile.MakeFloor(cluster);
            tiles.Add(currentTile);
            if (tiles.Count >= maze.pathLengthMax)
                break;
            MazeTile nextTile = null;
            while (nextTile == null)
            {
                // Choose an eligible tile adjacent to the current one.
                // If not found try to find any tile in the path that still has eligible adjacent tiles.
                nextTile = maze.ChooseAdjacentWallAreaTile(currentTile, previousTile);
                if (nextTile == null)
                {
                    // previousTile should only be considered if it is next to nextTile.
                    previousTile = null;
                    tilesNotUsable.Add(currentTile);
                    tilesUsable.Remove(currentTile);
                    // Use tilesUsable like a queue
                    currentTile = tilesUsable.FirstOrDefault();
                    if (currentTile == null)
                        return true;
                }
            }
            if (!tilesUsable.Contains(currentTile))
                tilesUsable.Add(currentTile);
            previousTile = currentTile;
            currentTile = nextTile;
        }
        return true;
    }
}

// The Maze class contains parameters and methods that define how the maze gets build.
public class Maze
{
    // Dimensions of the mazeMap. The maze will be quadratic.
    public uint size;
    // Number and size of rooms in the maze.
    public uint roomsToGenerate;
    public uint roomSizeMin;
    public uint roomSizeMax;
    // Maximum length per created path. This mainly defines how many loops the maze will have.
    public uint pathLengthMax;
    // Likelihood that the next tile in the path goes straight and that the path does not take a turn.
    // When using the default value 1 every direction of a path is chosen equally likely for every tile of the path.
    public float pathStraightness;
    // All clusters below this size get deleted before connecting all clusters.
    public uint clusterSizeMin;
    // For every dead end in the maze, remove this many tiles from it to make it shorter.
    public uint reduceDeadEndsBy;
    // Probability to create a hole on suitable places.
    public float holeDensity;
    // Matrix of MazeTile objects that stores the maze data.
    // The outer tiles in mazeMap are always wall tiles.
    public MazeTile[,] mazeMap;

    // Enumerate all tiles in mazeMap.
    public IEnumerable<MazeTile> GetAllTiles()
    {   
        for (int z = 0; z < mazeMap.GetLength(0); z++)
        {
            for (int x = 0; x < mazeMap.GetLength(1); x++)
            {
                yield return mazeMap[z, x];
            }
        }
    }

    // Enumerate all inner tiles in mazeMap.
    public IEnumerable<MazeTile> GetInnerTiles()
    {   
        for (int z = 1; z < mazeMap.GetLength(0) - 1; z++)
        {
            for (int x = 1; x < mazeMap.GetLength(1) - 1; x++)
            {
                yield return mazeMap[z, x];
            }
        }
    }

    // Enumerate all inner tiles in mazeMap that have a specific type.
    public IEnumerable<MazeTile> GetInnerTiles(TileType type)
    {
        foreach (var tile in GetInnerTiles())
        {
            if (tile.type == type)
                yield return tile;
        }
    }

    // Get the 4 tiles that are surrounding the tile in this order: Left, Right, Top, Bottom
    // Tiles diagonal from the tile are not considered as connected to it. That means that paths or rooms
    // can be placed on these diagonal tiles during maze generation without connecting to another cluster.
    public List<MazeTile> GetSurroundingTiles(MazeTile tile)
    {
        List<MazeTile> surroundingTiles = new();
        if (tile.x - 1 > 0)
            surroundingTiles.Add(mazeMap[tile.z, tile.x - 1]);
        if (tile.x + 1 < mazeMap.GetLength(1))
            surroundingTiles.Add(mazeMap[tile.z, tile.x + 1]);
        if (tile.z - 1 > 0)
            surroundingTiles.Add(mazeMap[tile.z - 1, tile.x]);
        if (tile.z + 1 < mazeMap.GetLength(0))
            surroundingTiles.Add(mazeMap[tile.z + 1, tile.x]);
        return surroundingTiles;
    }

    // Returns true if the tile is at the edge of mazeMap.
    public bool IsEdgeTile(MazeTile tile)
    {
        if (
            tile.x == 0 || tile.x == mazeMap.GetLength(1) - 1 ||
            tile.z == 0 || tile.z == mazeMap.GetLength(0) - 1
        )
            return true;
        return false;
    }

    // Get the direction when looking from fromTile to toTile.
    public Direction GetTileDirection(MazeTile fromTile, MazeTile toTile)
    {
        Direction direction;

        int horDistance = toTile.x - fromTile.x;
        int vertDistance = toTile.z - fromTile.z;

        if (Math.Abs(horDistance) > Math.Abs(vertDistance))
        {
            if (horDistance > 0)
                direction = Direction.Right;
            else
                direction = Direction.Left;
        }
        else
        {
            if (vertDistance > 0)
                direction = Direction.Down;
            else
                direction = Direction.Up;
        }

        if (horDistance == 0 && vertDistance == 0)
            direction = Direction.None;

        return direction;
    }

    // Return the tile adjacent to fromTile in the specified direction. Return null if not found.
    public MazeTile GetTileInDirection(MazeTile fromTile, Direction direction)
    {
        if (direction == Direction.Left)
        {
            if (fromTile.x > 0)
                return mazeMap[fromTile.z, fromTile.x - 1];
            else
                return null;
        }
        else if (direction == Direction.Right)
        {
            if (fromTile.x < mazeMap.GetLength(1) - 1)
                return mazeMap[fromTile.z, fromTile.x + 1];
            else
                return null;
        }
        else if (direction == Direction.Up)
        {
            if (fromTile.z > 0)
                return mazeMap[fromTile.z - 1, fromTile.x];
            else
                return null;
        }
        else if (direction == Direction.Down)
        {
            if (fromTile.z < mazeMap.GetLength(0) - 1)
                return mazeMap[fromTile.z + 1, fromTile.x];
            else
                return null;
        }
        return null;
    }

    // Returns true if the surrounding tiles and the tile itself is a wall.
    public bool IsWallArea(MazeTile tile)
    {
        if (IsEdgeTile(tile))
            return false;
        List<MazeTile> tilesToCheck = GetSurroundingTiles(tile);
        tilesToCheck.Add(tile);
        foreach (var tileToCheck in tilesToCheck)
        {
            if (tileToCheck.type != TileType.Wall)
                return false;
        }
        return true;
    }

    // Used when creating a maze path to see if checkTile can be used to extend the path.
    // Returns true if checkTile and the surrounding tiles except baseTile are walls.
    public bool IsWallAreaAhead(MazeTile baseTile, MazeTile checkTile)
    {
        if (IsEdgeTile(checkTile))
            return false;

        List<MazeTile> considerTiles = GetSurroundingTiles(checkTile);
        considerTiles.Remove(baseTile);
        considerTiles.Add(checkTile);
        foreach (var considerTile in considerTiles)
        {
            if (considerTile.type != TileType.Wall)
                return false;
        }
        return true;
    }

    // Find a random tile in mazeMap that is a wall and is surrounded by walls.
    public MazeTile FindWallAreaRandom()
    {
        // Choose a random start point in mazeMap and search forward and then backwards from it.
        int startX = UnityEngine.Random.Range(1, mazeMap.GetLength(0));
        int startZ = UnityEngine.Random.Range(1, mazeMap.GetLength(1));
        int z = startZ;
        int x = startX;

        // search forward
        while (z < mazeMap.GetLength(0) - 1)
        {
            while (x < mazeMap.GetLength(1) - 1)
            {
                if (IsWallArea(mazeMap[z, x]))
                {
                    return mazeMap[z, x];
                }
                x++;
            }
            x = 0;
            z++;
        }

        // search backwards
        z = startZ;
        x = startX;
        while (z > 0)
        {
            while (x > 0)
            {
                if (IsWallArea(mazeMap[z, x]))
                {
                    return mazeMap[z, x];
                }
                x--;
            }
            x = mazeMap.GetLength(1) - 1;
            z--;
        }

        return null;
    }

    // Return all surrounding tiles where IsWallAreaAhead returns true.
    public List<MazeTile> GetAdjacentWallAreaTiles(MazeTile tile)
    {
        List<MazeTile> tilesFound = new();
        foreach (var surroundingTile in GetSurroundingTiles(tile))
        {
            if (IsWallAreaAhead(tile, surroundingTile))
            {
                tilesFound.Add(surroundingTile);
            }
        }
        return tilesFound;
    }

    // Choose a tile returned from GetAdjacentWallAreaTiles completely randomly or according to pathStraightness.
    // Used for maze path generation.
    public MazeTile ChooseAdjacentWallAreaTile(MazeTile tile, MazeTile previousTile = null)
    {
        List<MazeTile> tilesFound = GetAdjacentWallAreaTiles(tile);
        if (tilesFound.Count == 0)
            return null;
        
        // Choose a random tile.
        if (previousTile == null || pathStraightness == 1)
            return tilesFound[UnityEngine.Random.Range(0, tilesFound.Count)];

        // Choose a tile that considers the pathStraightness.
        Direction previousDirection = GetTileDirection(previousTile, tile);
        // Find the Tile going in the same direction as the previous tile
        MazeTile sameDirectionTile = null;
        List<MazeTile> differentDirectionTiles = new(tilesFound);
        foreach (var tileFound in tilesFound)
        {
            if (previousDirection == GetTileDirection(tile, tileFound))
            {
                sameDirectionTile = tileFound;
                break;
            }
        }
        // Choose a random tile that does not go in the same direction.
        if (sameDirectionTile == null)
            return differentDirectionTiles[UnityEngine.Random.Range(0, differentDirectionTiles.Count)];

        differentDirectionTiles.Remove(sameDirectionTile);
        // Assign a weight to every tile. The tiles inside differentDirectionTiles have the weight 1.
        // sameDirectionTile has a weight of pathStraightness.
        // Choose randomly between the tiles considering the weight. If pathStraightness > 1, sameDirectionTile
        // has a higher chance to be picked over a tile in differentDirectionTiles.
        float totalWeight = differentDirectionTiles.Count + pathStraightness;
        float weight = UnityEngine.Random.Range(0f, totalWeight);
        
        if (weight > pathStraightness)
            return differentDirectionTiles[UnityEngine.Random.Range(0, differentDirectionTiles.Count)];

        return sameDirectionTile;
    }

    // Returns true if the tile is a dead end. A dead end tile is a floor tile that is surrounded by
    // 3-4 walls and 0-1 floor/ hole. Holes are treated like Floors in this method.
    public bool IsDeadEnd(MazeTile tile)
    {
        if (tile.type == TileType.Wall)
            return false;
        int surroundingFloors = 0;
        foreach (var surroundingTile in GetSurroundingTiles(tile))
        {
            if (surroundingTile.type != TileType.Wall)
            {
                surroundingFloors++;
                if (surroundingFloors > 1)
                    return false;
            }
        }
        return true;
    }

    // Return all tiles in the maze that are a dead end.
    public List<MazeTile> GetAllDeadEnds()
    {
        List<MazeTile> deadEnds = new();
        foreach (var tile in GetInnerTiles())
        {
            if (IsDeadEnd(tile))
                deadEnds.Add(tile);
        }
        return deadEnds;
    }

    // For every dead end, remove reduceDeadEndsBy tiles from it.
    // Every path that does not have loops (only junctions) can be completely removed if reduceDeadEndsBy
    // is big enough. A path that loops or that is connected to rooms on both sides does not get removed here.
    public void ReduceDeadEnds()
    {
        for (int i = 0; i < reduceDeadEndsBy; i++)
        {
            List<MazeTile> deadEnds = GetAllDeadEnds();
            if (deadEnds.Count == 0)
                return;
            foreach (var deadEnd in deadEnds)
            {
                deadEnd.Reset();
            }
        }
    }

    // Return true if the surroundings of the tile look like this (Floor at a corner):
    //   - w -     - W -     F F -     - F F
    //   w F F     F F W     F F W     W F F
    //   - F F     F F -     - W -     - W -
    // W: Wall, F: Floor, [Hyphen]: Don't care
    // Used to find a suitable place to generate a hole.
    // Holes are treated like walls in this method to not cut off paths when generating holes.
    // This can happen when 2 corner holes are generated diagonally to each other.
    public bool IsOpenCorner(MazeTile tile)
    {
        if (tile.type != TileType.Floor || IsEdgeTile(tile))
            return false;
        
        int surroundingWalls = 0;
        List<Direction> wallDirections = new();
        foreach (var surroundingTile in GetSurroundingTiles(tile))
        {
            if (surroundingTile.type == TileType.Wall)
            {
                // Opposite tile must be a floor
                Direction direction = GetTileDirection(surroundingTile, tile);
                if (GetTileInDirection(tile, direction).type != TileType.Floor)
                    return false;
                wallDirections.Add(direction);
                surroundingWalls++;
            }
        }
        if (surroundingWalls != 2)
            return false;
        
        // Diagonal tile opposite from the walls must be floor
        MazeTile diagonalTile = tile;
        foreach (var direction in wallDirections)
        {
            diagonalTile = GetTileInDirection(diagonalTile, direction);
        }
        if (diagonalTile.type != TileType.Floor)
            return false;
        
        return true;
    }

    // Return the cluster in clusters that has the most tiles.
    public static Cluster GetBiggestCluster(IEnumerable<Cluster> clusters)
    {
        Cluster biggestCluster = null;
        foreach (var cluster in clusters)
        {
            if (biggestCluster == null || cluster.tiles.Count > biggestCluster.tiles.Count)
            {
                biggestCluster = cluster;
            }
        }
        return biggestCluster;
    }

    // Return the Clusters for the 4 tiles surrounding the tile.
    public HashSet<Cluster> GetSurroundingClusters(MazeTile tile)
    {
        HashSet<Cluster> clusterSet = new();
        foreach (var surroundingTile in GetSurroundingTiles(tile))
        {
            if (surroundingTile.cluster != null)
                clusterSet.Add(surroundingTile.cluster);
        }
        return clusterSet;
    }

    // Get all clusters in the maze map
    public HashSet<Cluster> GetAllClusters()
    {
        HashSet<Cluster> clusterSet = new();

        foreach (var tile in GetInnerTiles())
        {
            if (tile.cluster != null)
                clusterSet.Add(tile.cluster);
        }
        return clusterSet;
    }

    // Remove all clusters with less tiles than clusterSizeMin
    public void RemoveSmallClusters()
    {
        if (clusterSizeMin < 2)
            return;
        foreach (var cluster in GetAllClusters())
        {
            if (cluster.tiles.Count < clusterSizeMin)
                cluster.Delete();
        }
    }

    // Iterate over all inner walls in the maze and create ClusterConnection objects.
    // Connect the clusters in the ClusterConnection objects. A connection between 2 clusters
    // can only be made if they have at least 1 adjacent wall in common that separates both.
    public void ConnectClusters()
    {
        List<ClusterConnection> clusterConnections = new();
        const int connectBetween = 2;

        foreach (var wall in GetInnerTiles(TileType.Wall))
        {
            // Only if the wall tile has at least 2 surrounding clusters a cluster connection can be made
            HashSet<Cluster> surroundingClusters = GetSurroundingClusters(wall);
            if (surroundingClusters.Count < connectBetween)
                continue;
            // Group the surrounding clusters in pairs of 2.
            IEnumerable<Cluster[]> connectedClusters = 
                Combinations.CombinationsWoRecursion(surroundingClusters.ToArray(), connectBetween);
            // Find out if the cluster pairs are already in clusterConnections
            List<Cluster[]> connectedClustersNotFound = new();
            foreach (var connectedCluster in connectedClusters)
            {
                bool isFound = false;
                foreach (var clusterConnection in clusterConnections)
                {
                    if (clusterConnection.clusterSet.SetEquals(connectedCluster))
                    {
                        clusterConnection.connectingTiles.Add(wall);
                        isFound = true;
                        break;
                    }
                }
                if (!isFound)
                {
                    connectedClustersNotFound.Add(connectedCluster);
                }
            }
            // Create a new ClusterConnection object
            foreach (var connectedClusterNotFound in connectedClustersNotFound)
            {
                ClusterConnection clusterConnection = new();
                foreach (var cluster in connectedClusterNotFound)
                {
                    clusterConnection.clusterSet.Add(cluster);
                }
                clusterConnection.connectingTiles.Add(wall);
                clusterConnections.Add(clusterConnection);
            }
        }

        // Connect all clusters
        foreach (var clusterConnection in clusterConnections)
        {
            clusterConnection.Connect();
        }
    }

    // If there are still clusters that were not connected after connecting all clusters, remove them.
    // An example situation where this can happen is if a room is completely surrounded by other rooms
    // that have exactly a distance of 2 tiles to the room (2 wall tiles in between).
    // It can also happen when small clusters get removed before connecting.
    public void RemoveUnconnectedClusters()
    {
        HashSet<Cluster> allClusters = GetAllClusters();
        Cluster biggestCluster = GetBiggestCluster(allClusters);
        foreach (var cluster in allClusters)
        {
            if (cluster != biggestCluster)
                cluster.Delete();
        }
    }

    // Find eligible tiles to change their type from Floor to Hole.
    // Holes can be generated at corners and in dead ends.
    // This ensures that all paths stay connected and don't get cut off by the holes.
    // (2 holes diagonally to each other can also cut off a path at least for the NavMesh agent)
    public void GenerateHoles()
    {
        if (holeDensity == 0)
            return;
        foreach (var tile in GetInnerTiles(TileType.Floor))
        {
            if (UnityEngine.Random.Range(0f, 1f) <= holeDensity && (IsDeadEnd(tile) || IsOpenCorner(tile)))
                tile.type = TileType.Hole;
        }
    }

    // Generate roomsToGenerate rooms.
    public List<MazeRoom> GenerateRooms()
    {
        List<MazeRoom> mazeRooms = new();
        for (int i = 0; i < roomsToGenerate; i++)
        {
            MazeRoom mazeRoom = new(this);
            mazeRoom.Generate();
            mazeRooms.Add(mazeRoom);
        }
        return mazeRooms;
    }

    // Generate paths until the maze does not have any space left.
    public List<MazePath> GeneratePaths()
    {
        List<MazePath> mazePaths = new();
        while (true)
        {
            MazePath mazePath = new(this);
            if (mazePath.Generate())
                mazePaths.Add(mazePath);
            else
                break;
        }
        return mazePaths;
    }

    // Generate the maze. Initialize and fill mazeMap.
    public void Generate() {
        mazeMap = new MazeTile[size, size];
        for (int z = 0; z < mazeMap.GetLength(0); z++)
        {
            for (int x = 0; x < mazeMap.GetLength(1); x++)
            {
                mazeMap[z, x] = new MazeTile(x, z);
            }
        }

        GenerateRooms();

        GeneratePaths();

        RemoveSmallClusters();

        ConnectClusters();

        RemoveUnconnectedClusters();

        ReduceDeadEnds();

        GenerateHoles();
    }
}

} // namespace MazeData
