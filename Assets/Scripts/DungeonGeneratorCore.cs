using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#region Data Structures
// Logical structure storing data for a single room
public class RoomData
{
    public Vector2Int Position;
    public RoomType Type;
    
    // Connection system (logical blocking / solid wall)
    public bool HasTop;
    public bool HasBottom;
    public bool HasLeft;
    public bool HasRight;
}

public enum RoomType
{
    Start,
    Standard,
    Evacuation,
    Loot,
    Corridor
}

public enum RoomShapeGuide
{
    None,
    Full,
    Small,
    LShape
}
#endregion

#region Core Generation Logic
/// <summary>
/// Standalone procedural generation logic. 
/// Designed to be decoupled from UnityEditor so it can be called at runtime.
/// </summary>
public class DungeonGeneratorCore
{
    public int roomCount = 15;
    public float roomSpacing = 10f;
    public string seed = "";

    public GameObject startPrefab;
    public GameObject standardPrefab;
    public GameObject evacuationPrefab;
    public GameObject lootPrefab;
    public GameObject corridorPrefab;

    public Action OnGenerationComplete;
    
    // Delegate to allow the Editor to override instantiation with PrefabUtility
    public Func<GameObject, GameObject> InstantiateMethod;

    private Dictionary<Vector2Int, RoomData> rooms = new Dictionary<Vector2Int, RoomData>();
    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private readonly Vector2Int startPosition = new Vector2Int(10, -1);

    public void Generate(bool isDebugMode = false)
    {
        // Seed initialization for determinism
        if (string.IsNullOrEmpty(seed))
        {
            seed = UnityEngine.Random.Range(100000, 999999).ToString();
        }
        int seedHash = seed.GetHashCode();
        UnityEngine.Random.InitState(seedHash);

        rooms.Clear();
        
        GenerateBaseLayout();
        
        List<Vector2Int> deadEnds = FindDeadEnds();
        int maxDeadEndsToUse = Mathf.CeilToInt(roomCount / 3.0f);
        
        int evacLimit = GetEvacuationLimit();
        int evacPointsToPlace = Mathf.Min(evacLimit, deadEnds.Count, maxDeadEndsToUse);
        List<Vector2Int> placedEvacuations = PlaceEvacuationPoints(deadEnds, evacPointsToPlace);
        
        int remainingDeadEnds = maxDeadEndsToUse - placedEvacuations.Count;
        PlaceLootRooms(deadEnds, remainingDeadEnds);
        
        ApplyCorridorLogic();
        UpdateConnections();
        VisualizeLevel(isDebugMode);

        OnGenerationComplete?.Invoke();
    }

    public void ClearLevel()
    {
        GameObject existingRoot = GameObject.Find("GeneratedProceduralLevel");
        if (existingRoot != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(existingRoot);
            else
                UnityEngine.Object.DestroyImmediate(existingRoot);
        }
    }

    private void GenerateBaseLayout()
    {
        rooms.Add(startPosition, new RoomData { Position = startPosition, Type = RoomType.Start });

        Vector2Int currentPos = startPosition;
        int generatedCount = 0;
        int safetyNet = 0; 
        
        while (generatedCount < roomCount && safetyNet < 10000)
        {
            safetyNet++;
            Vector2Int dir = directions[UnityEngine.Random.Range(0, directions.Length)];
            Vector2Int nextPos = currentPos + dir;

            if (nextPos.x >= 0 && nextPos.x < 20 && nextPos.y >= 0 && nextPos.y < 20)
            {
                currentPos = nextPos;
                if (!rooms.ContainsKey(currentPos))
                {
                    rooms.Add(currentPos, new RoomData { Position = currentPos, Type = RoomType.Standard });
                    generatedCount++;
                }
            }
        }
    }

    private List<Vector2Int> FindDeadEnds()
    {
        List<Vector2Int> potentialDeadEnds = new List<Vector2Int>();
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (rooms.ContainsKey(cell)) continue;

                int neighborCount = directions.Count(dir => rooms.ContainsKey(cell + dir));
                if (neighborCount == 1)
                {
                    potentialDeadEnds.Add(cell);
                }
            }
        }
        return potentialDeadEnds;
    }

    private int GetEvacuationLimit()
    {
        if (roomCount >= 15) return 4;
        if (roomCount >= 10) return 3;
        if (roomCount >= 8) return 2;
        if (roomCount >= 6) return 1;
        return 0;
    }

    private List<Vector2Int> PlaceEvacuationPoints(List<Vector2Int> availableDeadEnds, int amountToPlace)
    {
        List<Vector2Int> placed = new List<Vector2Int>();
        for (int i = 0; i < amountToPlace; i++)
        {
            Vector2Int bestCandidate = Vector2Int.zero;
            float maxMinDistance = -1f;

            foreach (Vector2Int candidate in availableDeadEnds)
            {
                float minDistance = Vector2.Distance(candidate, startPosition);
                foreach (Vector2Int evac in placed)
                {
                    float dist = Vector2.Distance(candidate, evac);
                    if (dist < minDistance) minDistance = dist;
                }

                if (minDistance > maxMinDistance)
                {
                    maxMinDistance = minDistance;
                    bestCandidate = candidate;
                }
            }

            placed.Add(bestCandidate);
            availableDeadEnds.Remove(bestCandidate);
            rooms.Add(bestCandidate, new RoomData { Position = bestCandidate, Type = RoomType.Evacuation });
        }
        return placed;
    }

    private void PlaceLootRooms(List<Vector2Int> availableDeadEnds, int limit)
    {
        int lootCount = Mathf.Min(limit, availableDeadEnds.Count);
        for (int i = 0; i < lootCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableDeadEnds.Count);
            Vector2Int chosenPos = availableDeadEnds[randomIndex];
            
            availableDeadEnds.RemoveAt(randomIndex);
            rooms.Add(chosenPos, new RoomData { Position = chosenPos, Type = RoomType.Loot });
        }
    }

    private void ApplyCorridorLogic()
    {
        List<RoomData> standardRooms = rooms.Values.Where(r => r.Type == RoomType.Standard).ToList();
        foreach (RoomData room in standardRooms)
        {
            bool horizontalMatch = rooms.ContainsKey(room.Position + Vector2Int.left) && 
                                   rooms.ContainsKey(room.Position + Vector2Int.right);
            bool verticalMatch = rooms.ContainsKey(room.Position + Vector2Int.up) && 
                                 rooms.ContainsKey(room.Position + Vector2Int.down);

            if ((horizontalMatch || verticalMatch) && UnityEngine.Random.value < 0.5f)
            {
                room.Type = RoomType.Corridor;
            }
        }
    }

    private void UpdateConnections()
    {
        foreach (RoomData room in rooms.Values)
        {
            room.HasTop = rooms.ContainsKey(room.Position + Vector2Int.up);
            room.HasBottom = rooms.ContainsKey(room.Position + Vector2Int.down);
            room.HasLeft = rooms.ContainsKey(room.Position + Vector2Int.left);
            room.HasRight = rooms.ContainsKey(room.Position + Vector2Int.right);
        }
    }

    private void VisualizeLevel(bool isDebug = false)
    {
        ClearLevel();
        GameObject root = new GameObject("GeneratedProceduralLevel");

        foreach (RoomData room in rooms.Values)
        {
            GameObject prefabToInstantiate = null;
            if (!isDebug)
            {
                switch (room.Type)
                {
                    case RoomType.Start: prefabToInstantiate = startPrefab; break;
                    case RoomType.Standard: prefabToInstantiate = standardPrefab; break;
                    case RoomType.Evacuation: prefabToInstantiate = evacuationPrefab; break;
                    case RoomType.Loot: prefabToInstantiate = lootPrefab; break;
                    case RoomType.Corridor: prefabToInstantiate = corridorPrefab; break;
                }
            }
            
            GameObject roomInstance;

            if (prefabToInstantiate != null && !isDebug)
            {
                // Automatically route to PrefabUtility logic if passed from the Editor
                if (InstantiateMethod != null)
                {
                    roomInstance = InstantiateMethod(prefabToInstantiate);
                }
                else
                {
                    roomInstance = UnityEngine.Object.Instantiate(prefabToInstantiate);
                }
                
                Transform wallTopClosed = roomInstance.transform.Find("WallTop_Closed");
                Transform wallTopOpen = roomInstance.transform.Find("WallTop_Open");
                if (wallTopClosed != null) wallTopClosed.gameObject.SetActive(!room.HasTop);
                if (wallTopOpen != null) wallTopOpen.gameObject.SetActive(room.HasTop);
                
                Transform wallBottomClosed = roomInstance.transform.Find("WallBottom_Closed");
                Transform wallBottomOpen = roomInstance.transform.Find("WallBottom_Open");
                if (wallBottomClosed != null) wallBottomClosed.gameObject.SetActive(!room.HasBottom);
                if (wallBottomOpen != null) wallBottomOpen.gameObject.SetActive(room.HasBottom);
                
                Transform wallLeftClosed = roomInstance.transform.Find("WallLeft_Closed");
                Transform wallLeftOpen = roomInstance.transform.Find("WallLeft_Open");
                if (wallLeftClosed != null) wallLeftClosed.gameObject.SetActive(!room.HasLeft);
                if (wallLeftOpen != null) wallLeftOpen.gameObject.SetActive(room.HasLeft);
                
                Transform wallRightClosed = roomInstance.transform.Find("WallRight_Closed");
                Transform wallRightOpen = roomInstance.transform.Find("WallRight_Open");
                if (wallRightClosed != null) wallRightClosed.gameObject.SetActive(!room.HasRight);
                if (wallRightOpen != null) wallRightOpen.gameObject.SetActive(room.HasRight);

                Transform randomProps = roomInstance.transform.Find("RandomProps");
                if (randomProps != null)
                {
                    foreach (Transform prop in randomProps)
                    {
                        prop.gameObject.SetActive(UnityEngine.Random.value > 0.5f);
                    }
                }
            }
            else
            {
                roomInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Renderer rnd = roomInstance.GetComponent<Renderer>();
                Material mat = new Material(rnd.sharedMaterial);
                
                if (room.Type == RoomType.Start) mat.color = Color.blue;
                else if (room.Type == RoomType.Standard) mat.color = Color.green;
                else if (room.Type == RoomType.Evacuation) mat.color = Color.red;
                else if (room.Type == RoomType.Loot) mat.color = Color.yellow;
                else if (room.Type == RoomType.Corridor) mat.color = Color.gray;
                
                rnd.material = mat;
            }

            roomInstance.transform.position = new Vector3(room.Position.x * roomSpacing, 0, room.Position.y * roomSpacing);
            roomInstance.transform.SetParent(root.transform);
            roomInstance.name = $"Room ({room.Position.x}, {room.Position.y}) - {room.Type}";
        }
    }
}
#endregion