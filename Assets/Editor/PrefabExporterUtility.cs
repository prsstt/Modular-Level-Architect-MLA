using UnityEngine;
using UnityEditor;
using System.IO;

public class PrefabExporterUtility
{
    /// <summary>
    /// Exports a GameObject from the scene to a clean, base Prefab at the specified path.
    /// Safely handles overwrites and prevents prefab corruption or forced unique naming.
    /// </summary>
    /// <param name="roomObject">The GameObject in the scene to export.</param>
    /// <param name="targetPath">The destination path (e.g., "Assets/Prefabs/Rooms/Room_01.prefab").</param>
    public static bool ExportRoomToPrefab(GameObject roomObject, string targetPath)
    {
        if (roomObject == null)
        {
            Debug.LogError("Export Failed: The provided room object is null.");
            return false;
        }

        if (string.IsNullOrEmpty(targetPath) || !targetPath.StartsWith("Assets/"))
        {
            Debug.LogError("Export Failed: The target path must be a valid project path starting with 'Assets/'.");
            return false;
        }

        // 1. Check for naming collisions BEFORE trying to save
        if (File.Exists(targetPath))
        {
            // 2. Lack of User Feedback Fix: Prompt the user to overwrite
            bool shouldOverwrite = EditorUtility.DisplayDialog(
                "Prefab Already Exists",
                $"A prefab with this name already exists at:\n{targetPath}\n\nDo you want to overwrite it?",
                "Overwrite",
                "Cancel"
            );

            // If the user clicks "Cancel", abort the export process entirely
            if (!shouldOverwrite)
            {
                Debug.Log("Export canceled by the user.");
                return false;
            }
            
            // Note: We intentionally do NOT use AssetDatabase.GenerateUniqueAssetPath here,
            // because the user explicitly opted to overwrite the existing file.
        }
        else
        {
            // Ensure the target directory exists if this is a brand new file
            string directory = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // 3. CRITICAL FIX for Corruption & Variant Prevention:
        // PrefabUtility.SaveAsPrefabAsset explicitly creates a clean, base Prefab.
        // It completely ignores the fact that 'roomObject' might currently be a prefab instance 
        // (unlike SaveAsPrefabVariant), ensuring no broken inheritance or corrupted duplicates.
        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(roomObject, targetPath, out success);

        if (success && savedPrefab != null)
        {
            Debug.Log($"Successfully exported '{roomObject.name}' to a clean base prefab at: {targetPath}", savedPrefab);
            
            // Optional but recommended: Ping the newly created/overwritten asset in the Project window
            EditorGUIUtility.PingObject(savedPrefab);
        }
        else if (!success)
        {
            Debug.LogError($"Failed to save the prefab at: {targetPath}. Check the console for internal Unity errors.");
        }

        return success;
    }
}