using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int mapWidth;
    public int mapHeight;
    public string seed;
    public bool useRandomSeed;

    public int smoothCycles = 5;
    public int smooth1 = 4;
    public int smooth2 = 4;

    public int borderSize = 5;
    public int wallThresholdSize = 50;
    public int roomThresholdSize = 50;

    public int passageSize = 1;

    [Range(0,100)]
    public int randomWallFillPercent;

    int[,] map;
    int[,] mapCycle;
    int[,] borderedMap;

    public void Start()
    {
        GenerateNewMap();
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
            GenerateNewMap();
    }

    // Generates a new int[,] map
    private void GenerateNewMap()
    {
        map = new int[mapWidth, mapHeight];
        mapCycle = new int[mapWidth, mapHeight];
        FillMapWithRandomTiles();

        for(int i = 0; i < smoothCycles; i++)
            SmoothMap();

        FillMapRegions();
        AddMapBorder();
    }

    // Adds a border to the generated map
    private void AddMapBorder()
    {
        borderedMap = new int[mapWidth + borderSize * 2, mapHeight + borderSize * 2];

        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < mapWidth + borderSize && y >= borderSize && y < mapHeight + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }
    }

    // Gets all the regions of the given tile type using floodfill
    private List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }
        return regions;
    }

    // Fills the maps regions based on map generation paramaters
    private void FillMapRegions()
    {
        List<List<Coord>> wallRegions = GetRegions(1);
        FillRegion(wallRegions, wallThresholdSize, 0);

        List<List<Coord>> roomRegions = GetRegions(0);
        List<Room> survivingRooms = new List<Room>();
        FillRegion(roomRegions, roomThresholdSize, 1, true, survivingRooms);

        survivingRooms.Sort();
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessibleFromMainRoom = true;

        ConnectClosestRooms(survivingRooms);
    }

    // Fills the given region with the given tile
    private void FillRegion(List<List<Coord>> regions, int threshold, int newTileType, bool isRoom = false, List<Room> survivingRooms = null)
    {
        foreach (List<Coord> region in regions)
        {
            if (region.Count < threshold)
            {
                foreach (Coord tile in region)
                    map[tile.tileX, tile.tileY] = newTileType;
            } else if (isRoom)
            {
                survivingRooms.Add(new Room(region, map));
            }
        }
    }

    private void ConnectClosestRooms(List<Room> allRooms, bool forceAccessiblityFromMainRoom = false)
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (forceAccessiblityFromMainRoom)
        {
            foreach(Room room in allRooms)
            {
                if (room.isAccessibleFromMainRoom)
                    roomListB.Add(room);
                else
                    roomListA.Add(room);
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int closestDistance = 0;
        Coord closestTileA = new Coord();
        Coord closestTileB = new Coord();
        Room closestRoomA = new Room();
        Room closestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessiblityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0 )
                    continue;
            }

            foreach(Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB))
                    continue;

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < closestDistance || !possibleConnectionFound)
                        {
                            closestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            closestTileA = tileA;
                            closestTileB = tileB;
                            closestRoomA = roomA;
                            closestRoomB = roomB;
                        }
                    }
                }
            }

            if (possibleConnectionFound && !forceAccessiblityFromMainRoom)
                CreateConnection(closestRoomA, closestRoomB, closestTileA, closestTileB);
        }

        if (possibleConnectionFound && forceAccessiblityFromMainRoom)
        {
            CreateConnection(closestRoomA, closestRoomB, closestTileA, closestTileB);
            ConnectClosestRooms(allRooms, true);
        } 

        if (!forceAccessiblityFromMainRoom)
            ConnectClosestRooms(allRooms, true);
    }

    private void CreateConnection(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        //Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 3);

        List<Coord> line = GetLine(tileA, tileB);
        foreach(Coord c in line)
        {
            DrawCircle(c, passageSize);
        }
    }

    private void DrawCircle(Coord c, int r)
    {
        for(int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if(x*x + y*y <= r*r)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if(IsInMapRange(drawX, drawY))
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    private List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();
        bool inverted = false;

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        //TODO: Make this a method instead
        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for(int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));
            //Shorthand if
            if (inverted)
            {
                y += step;
            }else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if(gradientAccumulation >= longest)
            {
                // Shorthand if
                if (inverted)
                {
                    x += gradientStep;
                } else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    private Vector2 CoordToWorldPoint(Coord tile)
    {
        return new Vector2(-mapWidth / 2 + .5f + tile.tileX, -mapHeight / 2 + .5f + tile.tileY);
    }

    private List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[mapWidth, mapHeight];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for(int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    // Checks if x and y are within the map boundries 
    private bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }

    // Fills the map with random values to give the algorithm a staring point
    private void FillMapWithRandomTiles()
    {
        if (useRandomSeed)
            seed = DateTime.Now.Ticks.ToString();

        System.Random pseudoRandomNumber = new System.Random(seed.GetHashCode());

        //TODO: Function RandomFillWalls()
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                //TODO: Function IsMapBorder(x, y, tile you want returned) ???
                // Maybe have the walls set elswhere before the fill so you can choose the type ???
                if (x == 0 || x == mapWidth - 1 || y == 0 || y == mapHeight - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandomNumber.Next(0, 100) < randomWallFillPercent) ? 1 : 0;
                }
            }
        }

        //TODO: Add functionallity for other types like water
    }

    private void SmoothMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if(neighbourWallTiles > smooth1)
                {
                    mapCycle[x, y] = 1;
                } else if(neighbourWallTiles < smooth2)
                {
                    mapCycle[x, y] = 0;
                }
            }
        }

        map = mapCycle; 
    }

    //TODO: Add support for multiple tile types
    //Loop through neighbours in a 3x3 and add them to a count
    private int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for(int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                //Check if inside map -InMapBoundries(x,y)-
                if(IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    private void OnDrawGizmos()
    {
        if (borderedMap != null)
        {
            for (int x = 0; x < borderedMap.GetLength(0); x++)
            {
                for (int y = 0; y < borderedMap.GetLength(1); y++)
                {
                    Gizmos.color = (borderedMap[x, y] == 1) ? Color.black : Color.white;
                    Vector2 position = new Vector2(-borderedMap.GetLength(0) / 2 + x + .5f, -borderedMap.GetLength(1) / 2 + y + .5f);
                    Gizmos.DrawCube(position, new Vector2(1, 1));
                }
            }
        }
    }
}
