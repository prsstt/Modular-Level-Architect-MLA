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
    Entrance,
    Basic,
    Objective,
    Reward,
    Connector
}

public enum RoomShapeGuide
{
    None,
    Full,
    Small,
    LShape,
    TShape,
    CrossShape,
    Corridor,
    Circle,
    Diamond,
    Hexagon
}
#endregion
 
[System.Serializable]
public class DungeonGenerationRules
{
    public int minMainPathLength = 8;
    public int maxMainPathLength = 12;
    [Range(0f, 1f)]
    public float branchingChance = 0.3f;
    public int maxBranchDepth = 3;
}
 
#region Core Generation Logic
/// <summary>
/// Standalone procedural generation logic. 
/// Designed to be decoupled from UnityEditor so it can be called at runtime.
/// </summary>
public class DungeonGeneratorCore
{
    #region Fields & Properties
    public DungeonGenerationRules generationRules = new DungeonGenerationRules();
    public float roomSpacing = 10f;
    public string seed = "";
 
    public GameObject entrancePrefab;
    public GameObject basicPrefab;
    public GameObject objectivePrefab;
    public GameObject rewardPrefab;
    public GameObject connectorPrefab;

    public Action OnGenerationComplete;
 
    // Delegate to allow the Editor to override instantiation with PrefabUtility
    public Func<GameObject, GameObject> InstantiateMethod;

    private Dictionary<Vector2Int, RoomData> rooms = new Dictionary<Vector2Int, RoomData>();
    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private readonly Vector2Int startPosition = new Vector2Int(10, -1);
    private List<Vector2Int> _mainPath;
    #endregion

    #region Public Methods
    public void Generate(bool isDebugMode = false)
    {
        ClearLevel();
        
        // Seed initialization for determinism
        if (string.IsNullOrEmpty(seed))
        {
            seed = UnityEngine.Random.Range(100000, 999999).ToString();
        }
        int seedHash = seed.GetHashCode();
        UnityEngine.Random.InitState(seedHash);
 
        rooms.Clear();
        
        _mainPath = GenerateDungeonLayout();
 
        List<Vector2Int> deadEnds = FindDeadEnds();

        // Filter candidates for objective points based on new rules
        List<Vector2Int> objectiveCandidates = new List<Vector2Int>();
        if (_mainPath.Count > 0)
        {
            objectiveCandidates.Add(_mainPath.Last());
        }
        objectiveCandidates.AddRange(deadEnds.Where(pos => Vector2.Distance(pos, startPosition) >= 5f && !objectiveCandidates.Contains(pos)));

        int objectiveLimit = GetObjectiveLimit();
        int objectivePointsToPlace = Mathf.Min(objectiveLimit, objectiveCandidates.Count);
        List<Vector2Int> placedObjectives = PlaceObjectivePoints(deadEnds, objectiveCandidates, objectivePointsToPlace);

        int rewardRoomsToPlace = Mathf.FloorToInt(deadEnds.Count * 0.5f); // Place reward rooms in 50% of remaining dead ends.
        PlaceRewardRooms(deadEnds, rewardRoomsToPlace);
        
        ApplyConnectorLogic();
        UpdateConnections();
        VisualizeLevel(isDebugMode);

        OnGenerationComplete?.Invoke();
    }

    public void ClearLevel()
    {
        GameObject existingLevel = GameObject.Find("GeneratedProceduralLevel");
        if (existingLevel != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(existingLevel);
            else
                UnityEngine.Object.DestroyImmediate(existingLevel);
        }
    }
    #endregion

    #region Generator Algorithms
    private List<Vector2Int> GenerateDungeonLayout()
    {
        List<Vector2Int> mainPath = new List<Vector2Int>();
        int mainPathLength = UnityEngine.Random.Range(generationRules.minMainPathLength, generationRules.maxMainPathLength + 1);
        
        rooms.Add(startPosition, new RoomData { Position = startPosition, Type = RoomType.Entrance });
        mainPath.Add(startPosition);
        
        Vector2Int currentPos = startPosition;
        Vector2Int lastDir = Vector2Int.zero;

        // 1. Main Path Generation
        for (int i = 0; i < mainPathLength - 1; i++)
        {
            int safetyNet = 0;
            Vector2Int nextPos = new Vector2Int(-1, -1);
            Vector2Int dir = Vector2Int.zero;
            do
            {
                safetyNet++;
                if (safetyNet > 100) {
                    Debug.LogWarning("Main path generation got stuck. Breaking loop.");
                    goto BranchGeneration; // Exit the main path loop if stuck
                }
                dir = directions[UnityEngine.Random.Range(0, directions.Length)];
                // Try not to go back immediately, adds a bit of forward momentum
                if (dir == -lastDir && mainPath.Count > 1)
                {
                    nextPos = new Vector2Int(-1, -1); // Invalid position to ensure the loop continues
                    continue; 
                }
                nextPos = currentPos + dir;
            } while (rooms.ContainsKey(nextPos) || nextPos.x < 0 || nextPos.x >= 20 || nextPos.y < 0 || nextPos.y >= 20);

            currentPos = nextPos;
            lastDir = dir;
            rooms.Add(currentPos, new RoomData { Position = currentPos, Type = RoomType.Basic });
            mainPath.Add(currentPos);
        }

    BranchGeneration:
        // 2. Branch Generation
        List<Vector2Int> mainPathCopy = new List<Vector2Int>(mainPath); // Iterate over a copy
        foreach (Vector2Int roomPos in mainPathCopy)
        {
            // Don't branch from start or end of main path
            if (roomPos == startPosition || roomPos == mainPath.Last()) continue;

            if (UnityEngine.Random.value < generationRules.branchingChance)
            {
                Vector2Int branchCurrentPos = roomPos;
                int branchLength = UnityEngine.Random.Range(1, generationRules.maxBranchDepth + 1);
                
                for (int i = 0; i < branchLength; i++)
                {
                    // Find a valid neighbor to branch into
                    List<Vector2Int> validDirections = directions.Where(d => !rooms.ContainsKey(branchCurrentPos + d)).ToList();
                    if (validDirections.Count == 0) break; // No place to branch

                    Vector2Int dir = validDirections[UnityEngine.Random.Range(0, validDirections.Count)];
                    branchCurrentPos += dir;
                    if (branchCurrentPos.x >= 0 && branchCurrentPos.x < 20 && branchCurrentPos.y >= 0 && branchCurrentPos.y < 20)
                    {
                        rooms.Add(branchCurrentPos, new RoomData { Position = branchCurrentPos, Type = RoomType.Basic });
                    }
                    else
                    {
                        break; // Hit grid boundary
                    }
                }
            }
        }
        return mainPath;
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

    private int GetObjectiveLimit()
    {
        int totalRooms = rooms.Count;
        if (totalRooms >= 25) return 4;
        if (totalRooms >= 18) return 3;
        if (totalRooms >= 12) return 2;
        if (totalRooms >= 8) return 1;
        return 0;
    }

    private List<Vector2Int> PlaceObjectivePoints(List<Vector2Int> allDeadEnds, List<Vector2Int> candidates, int amountToPlace)
    {
        List<Vector2Int> placed = new List<Vector2Int>();
        for (int i = 0; i < amountToPlace && candidates.Count > 0; i++)
        {
            Vector2Int bestCandidate = Vector2Int.zero;
            float maxMinDistance = -1f;

            foreach (Vector2Int candidate in candidates)
            {
                float minDistance = Vector2.Distance(candidate, startPosition);
                foreach (Vector2Int obj in placed)
                {
                    float dist = Vector2.Distance(candidate, obj);
                    if (dist < minDistance) minDistance = dist;
                }

                if (minDistance > maxMinDistance)
                {
                    maxMinDistance = minDistance;
                    bestCandidate = candidate;
                }
            }

            if (rooms.ContainsKey(bestCandidate))
            {
                rooms[bestCandidate].Type = RoomType.Objective;
            }
            else
            {
                rooms.Add(bestCandidate, new RoomData { Position = bestCandidate, Type = RoomType.Objective });
            }
            placed.Add(bestCandidate);
            candidates.Remove(bestCandidate);
            allDeadEnds.Remove(bestCandidate); // Remove from main dead ends list so it's not used for reward rooms
        }
        return placed;
    }

    private void PlaceRewardRooms(List<Vector2Int> availableDeadEnds, int limit)
    {
        int rewardCount = Mathf.Min(limit, availableDeadEnds.Count);
        for (int i = 0; i < rewardCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, availableDeadEnds.Count);
            Vector2Int chosenPos = availableDeadEnds[randomIndex];
            
            availableDeadEnds.RemoveAt(randomIndex);
            rooms.Add(chosenPos, new RoomData { Position = chosenPos, Type = RoomType.Reward });
        }
    }

    private void ApplyConnectorLogic()
    {
        List<RoomData> basicRooms = rooms.Values.Where(r => r.Type == RoomType.Basic).ToList();
        foreach (RoomData room in basicRooms)
        {
            bool horizontalMatch = rooms.ContainsKey(room.Position + Vector2Int.left) && 
                                   rooms.ContainsKey(room.Position + Vector2Int.right);
            bool verticalMatch = rooms.ContainsKey(room.Position + Vector2Int.up) && 
                                 rooms.ContainsKey(room.Position + Vector2Int.down);

            if ((horizontalMatch || verticalMatch) && UnityEngine.Random.value < 0.5f)
            {
                room.Type = RoomType.Connector;
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
    #endregion

    #region Visualization
    private void VisualizeLevel(bool isDebug = false)
    {
        GameObject root = new GameObject("GeneratedProceduralLevel");

        foreach (RoomData room in rooms.Values)
        {
            GameObject prefabToInstantiate = null;
            if (!isDebug)
            {
                switch (room.Type)
                {
                    case RoomType.Entrance: prefabToInstantiate = entrancePrefab; break;
                    case RoomType.Basic: prefabToInstantiate = basicPrefab; break;
                    case RoomType.Objective: prefabToInstantiate = objectivePrefab; break;
                    case RoomType.Reward: prefabToInstantiate = rewardPrefab; break;
                    case RoomType.Connector: prefabToInstantiate = connectorPrefab; break;
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
                
                if (room.Type == RoomType.Entrance) mat.color = Color.blue;
                else if (room.Type == RoomType.Basic) mat.color = Color.green;
                else if (room.Type == RoomType.Objective) mat.color = Color.red;
                else if (room.Type == RoomType.Reward) mat.color = Color.yellow;
                else if (room.Type == RoomType.Connector) mat.color = Color.gray;
                
                rnd.material = mat;
            }

            roomInstance.transform.position = new Vector3(room.Position.x * roomSpacing, 0, room.Position.y * roomSpacing);
            roomInstance.transform.SetParent(root.transform);
            roomInstance.name = $"Room ({room.Position.x}, {room.Position.y}) - {room.Type}";
        }
    }
    #endregion
}
#endregion