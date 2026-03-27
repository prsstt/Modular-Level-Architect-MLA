using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class LevelGeneratorWindow : EditorWindow
{
    private int roomCount = 15;
    private float roomSpacing = 10f; // Size of a single room to offset them properly
    private string seedString = "";

    private GameObject startPrefab;
    private GameObject standardPrefab;
    private GameObject evacuationPrefab;
    private GameObject lootPrefab;
    private GameObject corridorPrefab;

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
        roomCount = EditorPrefs.GetInt("ProcGen_RoomCount", 15);
        roomSpacing = EditorPrefs.GetFloat("ProcGen_RoomSpacing", 10f);
        seedString = EditorPrefs.GetString("ProcGen_SeedString", "");
        
        startPrefab = LoadPrefab("ProcGen_StartPrefab");
        standardPrefab = LoadPrefab("ProcGen_StandardPrefab");
        evacuationPrefab = LoadPrefab("ProcGen_EvacPrefab");
        lootPrefab = LoadPrefab("ProcGen_LootPrefab");
        corridorPrefab = LoadPrefab("ProcGen_CorridorPrefab");

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        EditorPrefs.SetInt("ProcGen_RoomCount", roomCount);
        EditorPrefs.SetFloat("ProcGen_RoomSpacing", roomSpacing);
        EditorPrefs.SetString("ProcGen_SeedString", seedString);
        
        SavePrefab("ProcGen_StartPrefab", startPrefab);
        SavePrefab("ProcGen_StandardPrefab", standardPrefab);
        SavePrefab("ProcGen_EvacPrefab", evacuationPrefab);
        SavePrefab("ProcGen_LootPrefab", lootPrefab);
        SavePrefab("ProcGen_CorridorPrefab", corridorPrefab);

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
            GUILayout.Label("Generator Settings", EditorStyles.boldLabel);
            
            roomCount = EditorGUILayout.IntSlider("Room Count", roomCount, 5, 100);
            roomSpacing = EditorGUILayout.FloatField("Room Spacing", roomSpacing);
            
            EditorGUILayout.Space();
        GUILayout.Label("Seed Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Leave seed empty to generate a random one.", MessageType.Info);
        seedString = EditorGUILayout.TextField("Generation Seed", seedString);

        EditorGUILayout.Space();
        GUILayout.Label("Room Prefabs & Auditor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign prefabs below. The Auditor ensures your prefabs have the correct Swap System structure.", MessageType.Info);
        
        DrawPrefabRow("Start Room", ref startPrefab);
        DrawPrefabRow("Standard", ref standardPrefab);
        DrawPrefabRow("Evacuation", ref evacuationPrefab);
        DrawPrefabRow("Loot", ref lootPrefab);
        DrawPrefabRow("Corridor", ref corridorPrefab);
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Level", GUILayout.Height(40)))
            {
            RunGeneration(false);
            }
            
            if (GUILayout.Button("Clear Level", GUILayout.Height(40)))
            {
            new DungeonGeneratorCore().ClearLevel();
            Debug.Log("<color=yellow>Level cleared from the scene.</color>");
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Debug Generate (Cubes)", GUILayout.Height(40)))
            {
            RunGeneration(true);
            }
    }

    private void DrawBuilderHelperTab()
    {
            GUILayout.Label("Room Builder Helper", EditorStyles.boldLabel);
            selectedShapeGuide = (RoomShapeGuide)EditorGUILayout.EnumPopup("Shape Guide", selectedShapeGuide);
            EditorGUILayout.HelpBox("Select a shape guide to display wireframes and labels in the Scene View to assist with prefab creation.", MessageType.Info);
    }

    private void RunGeneration(bool isDebugMode)
    {
        DungeonGeneratorCore core = new DungeonGeneratorCore
        {
            roomCount = this.roomCount,
            roomSpacing = this.roomSpacing,
            seed = this.seedString,
            startPrefab = this.startPrefab,
            standardPrefab = this.standardPrefab,
            evacuationPrefab = this.evacuationPrefab,
            lootPrefab = this.lootPrefab,
            corridorPrefab = this.corridorPrefab
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
        prefabRef = (GameObject)EditorGUILayout.ObjectField(label, prefabRef, typeof(GameObject), false);
        
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
                GUILayout.Label($"Missing {missing.Count}", GUILayout.Width(70));
                GUI.contentColor = Color.white;
                
                if (GUILayout.Button("Auto-Fix", GUILayout.Width(70)))
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

        // The Grid Bounds: Draw a faint 20x20 bounding box at Vector3.zero
        Handles.color = Color.cyan;
        Handles.DrawWireCube(Vector3.zero, new Vector3(20, 0, 20));

        // The Labels
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.cyan;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;

        Handles.Label(new Vector3(0, 0, 10), "WallTop_Closed / Open", labelStyle);
        Handles.Label(new Vector3(0, 0, -10), "WallBottom_Closed / Open", labelStyle);
        Handles.Label(new Vector3(-10, 0, 0), "WallLeft_Closed / Open", labelStyle);
        Handles.Label(new Vector3(10, 0, 0), "WallRight_Closed / Open", labelStyle);

        // The Ghost Shapes
        Handles.color = Color.yellow;

        if (selectedShapeGuide == RoomShapeGuide.Full20x20)
        {
            Handles.DrawWireCube(Vector3.zero, new Vector3(20, 0, 20));
        }
        else if (selectedShapeGuide == RoomShapeGuide.Small10x10)
        {
            Handles.DrawWireCube(Vector3.zero, new Vector3(10, 0, 10));
            
            // Connecting corridors
            Handles.DrawWireCube(new Vector3(0, 0, 7.5f), new Vector3(3, 0, 5));
            Handles.DrawWireCube(new Vector3(0, 0, -7.5f), new Vector3(3, 0, 5));
            Handles.DrawWireCube(new Vector3(-7.5f, 0, 0), new Vector3(5, 0, 3));
            Handles.DrawWireCube(new Vector3(7.5f, 0, 0), new Vector3(5, 0, 3));
        }
        else if (selectedShapeGuide == RoomShapeGuide.LShape)
        {
            // L-Shape structure using multiple wire cubes
            Handles.DrawWireCube(new Vector3(-5, 0, 5), new Vector3(10, 0, 10));
            Handles.DrawWireCube(new Vector3(-5, 0, -5), new Vector3(10, 0, 10));
            Handles.DrawWireCube(new Vector3(5, 0, -5), new Vector3(10, 0, 10));
            
            // Connecting corridors
            Handles.DrawWireCube(new Vector3(0, 0, 7.5f), new Vector3(3, 0, 5));
            Handles.DrawWireCube(new Vector3(0, 0, -7.5f), new Vector3(3, 0, 5));
            Handles.DrawWireCube(new Vector3(-7.5f, 0, 0), new Vector3(5, 0, 3));
            Handles.DrawWireCube(new Vector3(7.5f, 0, 0), new Vector3(5, 0, 3));
        }

        // Repaint the scene view to ensure the guides are drawn smoothly without needing to move the mouse
        sceneView.Repaint();
    }
#endregion
}