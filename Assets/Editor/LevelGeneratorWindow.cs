using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public class LevelGeneratorWindow : EditorWindow
{
    private DungeonGenerationRules generationRules = new DungeonGenerationRules();
    private float roomSpacing = 10f;
    private string seedString = "";

    private GameObject entrancePrefab;
    private GameObject basicPrefab;
    private GameObject objectivePrefab;
    private GameObject rewardPrefab;
    private GameObject connectorPrefab;

    private int selectedTab = 0;
    private string[] tabs = { "Generator", "Builder Helper" };
    private RoomShapeGuide selectedShapeGuide = RoomShapeGuide.None;

    [MenuItem("Tools/Procedural Level Generator")]
    public static void ShowWindow()
    {
        System.Type inspectorType = System.Type.GetType("UnityEditor.InspectorWindow,UnityEditor");
        
        LevelGeneratorWindow window;
        if (inspectorType != null)
        {
            window = GetWindow<LevelGeneratorWindow>("Level Generator", inspectorType);
        }
        else
        {
            window = GetWindow<LevelGeneratorWindow>("Level Generator");
        }
        
        window.minSize = new Vector2(350, 450); 
        window.Show();
    }

    private void OnEnable()
    {
        // Load Generation Rules
        generationRules.minMainPathLength = EditorPrefs.GetInt("ProcGen_MinMainPath", 8);
        generationRules.maxMainPathLength = EditorPrefs.GetInt("ProcGen_MaxMainPath", 12);
        generationRules.branchingChance = EditorPrefs.GetFloat("ProcGen_BranchChance", 0.3f);
        generationRules.maxBranchDepth = EditorPrefs.GetInt("ProcGen_MaxBranchDepth", 3);

        roomSpacing = EditorPrefs.GetFloat("ProcGen_RoomSpacing", 10f);
        seedString = EditorPrefs.GetString("ProcGen_SeedString", "");
        
        entrancePrefab = LoadPrefab("ProcGen_StartPrefab");
        basicPrefab = LoadPrefab("ProcGen_StandardPrefab");
        objectivePrefab = LoadPrefab("ProcGen_ObjectivePrefab");
        rewardPrefab = LoadPrefab("ProcGen_RewardPrefab");
        connectorPrefab = LoadPrefab("ProcGen_ConnectorPrefab");

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        // Save Generation Rules
        EditorPrefs.SetInt("ProcGen_MinMainPath", generationRules.minMainPathLength);
        EditorPrefs.SetInt("ProcGen_MaxMainPath", generationRules.maxMainPathLength);
        EditorPrefs.SetFloat("ProcGen_BranchChance", generationRules.branchingChance);
        EditorPrefs.SetInt("ProcGen_MaxBranchDepth", generationRules.maxBranchDepth);

        EditorPrefs.SetFloat("ProcGen_RoomSpacing", roomSpacing);
        EditorPrefs.SetString("ProcGen_SeedString", seedString);
        
        SavePrefab("ProcGen_StartPrefab", entrancePrefab);
        SavePrefab("ProcGen_StandardPrefab", basicPrefab);
        SavePrefab("ProcGen_ObjectivePrefab", objectivePrefab);
        SavePrefab("ProcGen_RewardPrefab", rewardPrefab);
        SavePrefab("ProcGen_ConnectorPrefab", connectorPrefab);

        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void SavePrefab(string key, GameObject prefab)
    {
        if (prefab == null) { EditorPrefs.DeleteKey(key); return; }
        string path = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(key, guid);
    }

    private GameObject LoadPrefab(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

#region Editor UI
    private void OnGUI()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        EditorGUILayout.Space();

        if (selectedTab == 0)
        {
            DrawGeneratorTab();
        }
        else if (selectedTab == 1)
        {
            DrawBuilderHelperTab();
        }
    }

    private void DrawGeneratorTab()
    {
        GUILayout.Label("Generation Rules", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The algorithm builds a Main Path from the Entrance to the Objective. It then branches out to create dead ends for Rewards.", MessageType.Info);
        
        generationRules.minMainPathLength = EditorGUILayout.IntSlider(new GUIContent("Min Main Path", "Guarantees a baseline length for the level."), generationRules.minMainPathLength, 3, 50);
        generationRules.maxMainPathLength = EditorGUILayout.IntSlider(new GUIContent("Max Main Path", "Sets a hard limit on the level's maximum length."), generationRules.maxMainPathLength, 3, 50);
        if (generationRules.minMainPathLength > generationRules.maxMainPathLength)
        {
            generationRules.minMainPathLength = generationRules.maxMainPathLength;
        }
        generationRules.branchingChance = EditorGUILayout.Slider(new GUIContent("Branching Chance", "Controls how often side paths appear."), generationRules.branchingChance, 0f, 1f);
        generationRules.maxBranchDepth = EditorGUILayout.IntSlider(new GUIContent("Max Branch Depth", "Limits how long dead-end paths can be."), generationRules.maxBranchDepth, 1, 10);

        EditorGUILayout.Space();
        GUILayout.Label("General Settings", EditorStyles.boldLabel);
        roomSpacing = EditorGUILayout.FloatField(new GUIContent("Room Spacing", "Distance between the centers of adjacent rooms on the grid."), roomSpacing);

        EditorGUILayout.Space();
        GUILayout.Label("Seed Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Leave seed empty to generate a random one.", MessageType.Info);
        seedString = EditorGUILayout.TextField(new GUIContent("Generation Seed", "Value used to initialize the random generator. The same seed always produces the exact same level."), seedString);

        EditorGUILayout.Space();
        GUILayout.Label("Room Prefabs & Auditor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign prefabs below. The Auditor ensures your prefabs have the correct Swap System structure.", MessageType.Info);
        
        DrawPrefabRow("Entrance (Start)", ref entrancePrefab);
        DrawPrefabRow("Basic (Standard)", ref basicPrefab);
        DrawPrefabRow("Objective (Boss/Exit)", ref objectivePrefab);
        DrawPrefabRow("Reward (Loot)", ref rewardPrefab);
        DrawPrefabRow("Connector (Corridor)", ref connectorPrefab);
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Generate Level", "Spawns the level in the scene using the assigned prefabs."), GUILayout.Height(40)))
            {
            RunGeneration(false);
            }
            
            if (GUILayout.Button(new GUIContent("Clear Level", "Removes the currently generated level from the scene."), GUILayout.Height(40)))
            {
            new DungeonGeneratorCore().ClearLevel();
            Debug.Log("<color=yellow>Level cleared from the scene.</color>");
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(new GUIContent("Debug Generate (Cubes)", "Spawns the level layout using primitive colored cubes instead of real prefabs."), GUILayout.Height(40)))
            {
            RunGeneration(true);
            }
    }

    private void DrawBuilderHelperTab()
    {
        GUILayout.Label("Room Builder Helper", EditorStyles.boldLabel);
        selectedShapeGuide = (RoomShapeGuide)EditorGUILayout.EnumPopup(new GUIContent("Shape Guide", "Displays a visual wireframe in the Scene View to help you build and size your room prefabs."), selectedShapeGuide);
        EditorGUILayout.HelpBox("Select a shape guide to display wireframes and labels in the Scene View to assist with prefab creation.", MessageType.Info);

        EditorGUILayout.Space();
        GUILayout.Label("Active Tools", EditorStyles.boldLabel);

        if (GUILayout.Button(new GUIContent("Create Template Room", "Creates an empty GameObject setup with the correct child structure for the Auto-Fixer."), GUILayout.Height(30)))
        {
            CreateTemplateRoom();
        }

        if (GUILayout.Button(new GUIContent("Snap Selected to Center", "Moves the selected GameObject to Vector3.zero. Very useful before saving a room as a Prefab."), GUILayout.Height(30)))
        {
            SnapSelectedToCenter();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Generate Auto-Floor", "Creates a Plane scaled perfectly to your room spacing."), GUILayout.Height(30)))
        {
            GenerateAutoFloor();
        }
        if (GUILayout.Button(new GUIContent("Snap Internal Geometry", "Snaps all child objects to a 0.25 micro-grid."), GUILayout.Height(30)))
        {
            SnapInternalGeometry();
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.Label("Live Door Tester", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select a room on the scene to instantly test door configurations.", MessageType.Info);

        // Disable buttons if nothing is selected
        EditorGUI.BeginDisabledGroup(Selection.activeGameObject == null);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open All", GUILayout.Height(25)))
        {
            SetDoorState("Top", true); SetDoorState("Bottom", true); 
            SetDoorState("Left", true); SetDoorState("Right", true);
        }
        if (GUILayout.Button("Close All", GUILayout.Height(25)))
        {
            SetDoorState("Top", false); SetDoorState("Bottom", false); 
            SetDoorState("Left", false); SetDoorState("Right", false);
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        float btnWidth = 110f;
        float btnHeight = 30f;
        float centerGap = btnWidth + 10f; // Gap perfectly matching the top/bottom button width + padding

        // NORTH
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Toggle Top", GUILayout.Width(btnWidth), GUILayout.Height(btnHeight))) ToggleDoorState("Top");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // WEST & EAST
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Toggle Left", GUILayout.Width(btnWidth), GUILayout.Height(btnHeight))) ToggleDoorState("Left");
        GUILayout.Space(centerGap); 
        if (GUILayout.Button("Toggle Right", GUILayout.Width(btnWidth), GUILayout.Height(btnHeight))) ToggleDoorState("Right");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // SOUTH
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Toggle Bottom", GUILayout.Width(btnWidth), GUILayout.Height(btnHeight))) ToggleDoorState("Bottom");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    private void ToggleDoorState(string direction)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        Transform openObj = selected.transform.Find($"Wall{direction}_Open");
        if (openObj != null)
        {
            bool isCurrentlyOpen = openObj.gameObject.activeSelf;
            SetDoorState(direction, !isCurrentlyOpen);
        }
    }

    private void CreateTemplateRoom()
    {
        GameObject templateRoom = new GameObject("New_Room_Template");
        templateRoom.transform.position = Vector3.zero;
        templateRoom.transform.rotation = Quaternion.identity;

        string[] childNames = new string[]
        {
            "WallTop_Closed", "WallTop_Open",
            "WallBottom_Closed", "WallBottom_Open",
            "WallLeft_Closed", "WallLeft_Open",
            "WallRight_Closed", "WallRight_Open",
            "RandomProps"
        };

        foreach (string childName in childNames)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(templateRoom.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
        }

        // Register undo so the user can Ctrl+Z the creation 
        Undo.RegisterCreatedObjectUndo(templateRoom, "Create Template Room");
        
        // Automatically select the new template to streamline workflow
        Selection.activeGameObject = templateRoom;
        
        Debug.Log("<color=green>New Room Template created successfully!</color>");
    }

    private void SnapSelectedToCenter()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Cannot snap: No GameObject is currently selected in the hierarchy.");
            return;
        }

        Undo.RecordObject(selected.transform, "Snap to Center");
        selected.transform.position = Vector3.zero;
        selected.transform.rotation = Quaternion.identity;
        
        Debug.Log($"<color=cyan>Snapped '{selected.name}' to center (Vector3.zero).</color>");
    }

    private void GenerateAutoFloor()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Cannot generate Auto-Floor: No GameObject is currently selected.");
            return;
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Auto_Floor";
        floor.transform.SetParent(selected.transform);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localRotation = Quaternion.identity;
        
        // A Unity primitive Plane is 10x10 units. Scale it to match roomSpacing.
        float scale = roomSpacing / 10f;
        floor.transform.localScale = new Vector3(scale, 1f, scale);

        Undo.RegisterCreatedObjectUndo(floor, "Generate Auto-Floor");
        Debug.Log($"<color=green>Auto-Floor generated for '{selected.name}'.</color>");
    }

    private void SnapInternalGeometry()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Cannot snap geometry: No GameObject is currently selected.");
            return;
        }

        float snapValue = 0.25f;
        Transform[] children = selected.GetComponentsInChildren<Transform>(true);
        
        Undo.RecordObjects(children, "Snap Internal Geometry");

        foreach (Transform child in children)
        {
            if (child == selected.transform) continue; // Skip the parent itself
            
            Vector3 pos = child.localPosition;
            pos.x = Mathf.Round(pos.x / snapValue) * snapValue;
            pos.y = Mathf.Round(pos.y / snapValue) * snapValue;
            pos.z = Mathf.Round(pos.z / snapValue) * snapValue;
            child.localPosition = pos;
        }

        Debug.Log($"<color=cyan>Snapped child transforms to a {snapValue} micro-grid.</color>");
    }

    private void SetDoorState(string direction, bool isOpen)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        Transform closedObj = selected.transform.Find($"Wall{direction}_Closed");
        Transform openObj = selected.transform.Find($"Wall{direction}_Open");

        if (closedObj != null && openObj != null)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { closedObj.gameObject, openObj.gameObject }, $"Test {direction} Door");
            closedObj.gameObject.SetActive(!isOpen);
            openObj.gameObject.SetActive(isOpen);
        }
    }

    private void RunGeneration(bool isDebugMode)
    {
        DungeonGeneratorCore core = new DungeonGeneratorCore
        {
            generationRules = this.generationRules,
            roomSpacing = this.roomSpacing,
            seed = this.seedString,
            entrancePrefab = this.entrancePrefab,
            basicPrefab = this.basicPrefab,
            objectivePrefab = this.objectivePrefab,
            rewardPrefab = this.rewardPrefab,
            connectorPrefab = this.connectorPrefab
        };

        core.InstantiateMethod = (prefab) => 
        {
            if (!Application.isPlaying)
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
                return Instantiate(prefab);
        };

        core.OnGenerationComplete += () => 
        {
            Debug.Log($"<color=green>Generation successful!</color> Using Seed: {core.seed}");
            this.seedString = core.seed; // Update UI if empty string was auto-generated
        };
        
        core.Generate(isDebugMode);
    }
#endregion

#region Prefab Validator & Auto-Fixer
    private void DrawPrefabRow(string label, ref GameObject prefabRef)
    {
        GUILayout.BeginHorizontal();
        prefabRef = (GameObject)EditorGUILayout.ObjectField(new GUIContent(label, $"Assign the prefab to be instantiated for the {label}."), prefabRef, typeof(GameObject), false);
        
        if (prefabRef != null)
        {
            List<string> missing = GetMissingSwapObjects(prefabRef);
            if (missing.Count == 0)
            {
                GUI.contentColor = Color.green;
                GUILayout.Label("OK", GUILayout.Width(30));
                GUI.contentColor = Color.white;
            }
            else
            {
                GUI.contentColor = Color.yellow;
                string missingNames = string.Join("\n- ", missing);
                string missingTooltip = $"Missing {missing.Count} required child objects:\n- {missingNames}\n\nClick 'Auto-Fix' to generate them.";
                GUILayout.Label(new GUIContent($"Missing {missing.Count}", missingTooltip), GUILayout.Width(70));
                GUI.contentColor = Color.white;
                
                if (GUILayout.Button(new GUIContent("Auto-Fix", "Automatically creates the missing empty child GameObjects inside this prefab and saves it."), GUILayout.Width(70)))
                {
                    AutoFixPrefab(prefabRef, missing);
                }
            }
        }
        GUILayout.EndHorizontal();
    }

    private List<string> GetMissingSwapObjects(GameObject prefab)
    {
        List<string> expected = new List<string> {
            "WallTop_Closed", "WallTop_Open",
            "WallBottom_Closed", "WallBottom_Open",
            "WallLeft_Closed", "WallLeft_Open",
            "WallRight_Closed", "WallRight_Open"
        };
        List<string> missing = new List<string>();

        foreach (string exp in expected)
        {
            if (prefab.transform.Find(exp) == null)
            {
                missing.Add(exp);
            }
        }
        return missing;
    }

    private void AutoFixPrefab(GameObject prefab, List<string> missing)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Cannot auto-fix: Make sure you assign a saved Prefab from the Project window, not a Scene object.");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        foreach (string m in missing)
        {
            GameObject newChild = new GameObject(m);
            newChild.transform.SetParent(contents.transform);
            newChild.transform.localPosition = Vector3.zero;
        }
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log($"<color=green>Auto-fixed prefab: {prefab.name}. Added {missing.Count} missing swap objects.</color>");
    }
#endregion

#region Scene Drawing Logic
    private void OnSceneGUI(SceneView sceneView)
    {
        if (selectedTab != 1 || selectedShapeGuide == RoomShapeGuide.None) return;

        // Use the roomSpacing variable to dynamically scale the visual guides
        float boundsSize = roomSpacing;
        float halfBounds = boundsSize / 2f;

        // The Grid Bounds: Draw a faint bounding box at Vector3.zero
        Handles.color = Color.cyan;
        Handles.DrawWireCube(Vector3.zero, new Vector3(boundsSize, 0, boundsSize));

        // The Labels
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.cyan;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;

        Handles.Label(new Vector3(0, 0, halfBounds), "WallTop_Closed / Open", labelStyle);
        Handles.Label(new Vector3(0, 0, -halfBounds), "WallBottom_Closed / Open", labelStyle);
        Handles.Label(new Vector3(-halfBounds, 0, 0), "WallLeft_Closed / Open", labelStyle);
        Handles.Label(new Vector3(halfBounds, 0, 0), "WallRight_Closed / Open", labelStyle);

        // The Ghost Shapes
        Handles.color = Color.yellow;

        if (selectedShapeGuide == RoomShapeGuide.Full)
        {
            Handles.DrawWireCube(Vector3.zero, new Vector3(boundsSize, 0, boundsSize));
        }
        else if (selectedShapeGuide == RoomShapeGuide.Small)
        {
            float smallSize = boundsSize * 0.5f;
            Handles.DrawWireCube(Vector3.zero, new Vector3(smallSize, 0, smallSize));
            
            // Connecting corridors
            float corridorLength = boundsSize * 0.25f; // Equivalent to 5 on a 20 scale
            float corridorWidth = boundsSize * 0.15f;  // Equivalent to 3 on a 20 scale
            float offset = boundsSize * 0.375f;        // Equivalent to 7.5 on a 20 scale
            
            Handles.DrawWireCube(new Vector3(0, 0, offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(0, 0, -offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(-offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
            Handles.DrawWireCube(new Vector3(offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
        }
        else if (selectedShapeGuide == RoomShapeGuide.LShape)
        {
            float quarterSize = boundsSize * 0.25f; // Equivalent to 5 on a 20 scale
            float smallSize = boundsSize * 0.5f;    // Equivalent to 10 on a 20 scale

            // L-Shape structure using multiple wire cubes
            Handles.DrawWireCube(new Vector3(-quarterSize, 0, quarterSize), new Vector3(smallSize, 0, smallSize));
            Handles.DrawWireCube(new Vector3(-quarterSize, 0, -quarterSize), new Vector3(smallSize, 0, smallSize));
            Handles.DrawWireCube(new Vector3(quarterSize, 0, -quarterSize), new Vector3(smallSize, 0, smallSize));
            
            // Connecting corridors
            float corridorLength = boundsSize * 0.25f; // Equivalent to 5 on a 20 scale
            float corridorWidth = boundsSize * 0.15f;  // Equivalent to 3 on a 20 scale
            float offset = boundsSize * 0.375f;        // Equivalent to 7.5 on a 20 scale
            
            Handles.DrawWireCube(new Vector3(0, 0, offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(0, 0, -offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(-offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
            Handles.DrawWireCube(new Vector3(offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
        }
        else if (selectedShapeGuide == RoomShapeGuide.TShape)
        {
            float quarterSize = boundsSize * 0.25f; 
            float smallSize = boundsSize * 0.5f;    

            Handles.DrawWireCube(new Vector3(0, 0, quarterSize), new Vector3(boundsSize, 0, smallSize)); // Top bar
            Handles.DrawWireCube(new Vector3(0, 0, -quarterSize), new Vector3(smallSize, 0, smallSize)); // Bottom stem
            
            // Connecting corridors
            float corridorLength = boundsSize * 0.25f;
            float corridorWidth = boundsSize * 0.15f; 
            float offset = boundsSize * 0.375f;       
            
            Handles.DrawWireCube(new Vector3(0, 0, offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(0, 0, -offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(-offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
            Handles.DrawWireCube(new Vector3(offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
        }
        else if (selectedShapeGuide == RoomShapeGuide.CrossShape)
        {
            float smallSize = boundsSize * 0.5f;
            Handles.DrawWireCube(Vector3.zero, new Vector3(boundsSize, 0, smallSize)); // Horizontal
            Handles.DrawWireCube(Vector3.zero, new Vector3(smallSize, 0, boundsSize)); // Vertical
            
            // Connecting corridors
            float corridorLength = boundsSize * 0.25f;
            float corridorWidth = boundsSize * 0.15f; 
            float offset = boundsSize * 0.375f;       
            
            Handles.DrawWireCube(new Vector3(0, 0, offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(0, 0, -offset), new Vector3(corridorWidth, 0, corridorLength));
            Handles.DrawWireCube(new Vector3(-offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
            Handles.DrawWireCube(new Vector3(offset, 0, 0), new Vector3(corridorLength, 0, corridorWidth));
        }
        else if (selectedShapeGuide == RoomShapeGuide.Corridor)
        {
            float corridorWidth = boundsSize * 0.25f;
            float corridorLength = boundsSize * 0.25f;
            float cWidth = boundsSize * 0.15f;
            float offset = boundsSize * 0.375f;

            Handles.DrawWireCube(Vector3.zero, new Vector3(corridorWidth, 0, boundsSize)); // Long along Z
            Handles.DrawWireCube(new Vector3(0, 0, offset), new Vector3(cWidth, 0, corridorLength)); // Top
            Handles.DrawWireCube(new Vector3(0, 0, -offset), new Vector3(cWidth, 0, corridorLength)); // Bottom
        }
        else if (selectedShapeGuide == RoomShapeGuide.Circle)
        {
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, halfBounds);
        }
        else if (selectedShapeGuide == RoomShapeGuide.Diamond)
        {
            Vector3[] diamondPoints = new Vector3[] {
                new Vector3(0, 0, halfBounds),
                new Vector3(halfBounds, 0, 0),
                new Vector3(0, 0, -halfBounds),
                new Vector3(-halfBounds, 0, 0),
                new Vector3(0, 0, halfBounds)
            };
            Handles.DrawPolyLine(diamondPoints);
        }
        else if (selectedShapeGuide == RoomShapeGuide.Hexagon)
        {
            Vector3[] hexPoints = new Vector3[7];
            for (int i = 0; i <= 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                hexPoints[i] = new Vector3(Mathf.Sin(angle) * halfBounds, 0, Mathf.Cos(angle) * halfBounds);
            }
            Handles.DrawPolyLine(hexPoints);
        }

        // Repaint the scene view to ensure the guides are drawn smoothly without needing to move the mouse
        sceneView.Repaint();
    }
#endregion
}