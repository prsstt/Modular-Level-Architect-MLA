using UnityEngine;

public class AutoFloorGenerator : MonoBehaviour
{
    public void GenerateAutoFloor(float width, float length, Material floorMaterial, float uvScale = 1f)
    {
        // 1. Transform Integrity
        transform.localScale = Vector3.one;

        // 2. Component Setup
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.sharedMaterial = floorMaterial;

        // 3. Geometry & Alignment
        float w = width * 0.5f;
        float l = length * 0.5f;
        
        // Top surface at Y=0, exactly 0.5 units downwards
        float yTop = 0f;
        float yBot = -0.5f;

        // Define the 8 corner points of the box, centered at (0,0,0) on X/Z
        Vector3 p0 = new Vector3(-w, yTop,  l); // Top-Left-Forward
        Vector3 p1 = new Vector3( w, yTop,  l); // Top-Right-Forward
        Vector3 p2 = new Vector3(-w, yTop, -l); // Top-Left-Back
        Vector3 p3 = new Vector3( w, yTop, -l); // Top-Right-Back
        
        Vector3 p4 = new Vector3(-w, yBot,  l); // Bottom-Left-Forward
        Vector3 p5 = new Vector3( w, yBot,  l); // Bottom-Right-Forward
        Vector3 p6 = new Vector3(-w, yBot, -l); // Bottom-Left-Back
        Vector3 p7 = new Vector3( w, yBot, -l); // Bottom-Right-Back

        // 20 vertices to allow for sharp edges and distinct UVs per face (bottom face culled)
        // Each face is structured as: TopLeft, TopRight, BottomLeft, BottomRight (from its own visual perspective)
        Vector3[] vertices = new Vector3[]
        {
            // Top Face (+Y)
            p0, p1, p2, p3,
            // Front Face (+Z)
            p0, p1, p4, p5,
            // Back Face (-Z)
            p3, p2, p7, p6,
            // Left Face (-X)
            p2, p0, p6, p4,
            // Right Face (+X)
            p1, p3, p5, p7
        };

        // 4. UV Mapping (1:1 Ratio to world units * uvScale to prevent stretching/control density)
        Vector2[] uvs = new Vector2[]
        {
            // Top
            new Vector2(0, length) * uvScale, new Vector2(width, length) * uvScale, new Vector2(0, 0) * uvScale, new Vector2(width, 0) * uvScale,
            // Front
            new Vector2(0, 0.5f) * uvScale, new Vector2(width, 0.5f) * uvScale, new Vector2(0, 0) * uvScale, new Vector2(width, 0) * uvScale,
            // Back
            new Vector2(0, 0.5f) * uvScale, new Vector2(width, 0.5f) * uvScale, new Vector2(0, 0) * uvScale, new Vector2(width, 0) * uvScale,
            // Left
            new Vector2(0, 0.5f) * uvScale, new Vector2(length, 0.5f) * uvScale, new Vector2(0, 0) * uvScale, new Vector2(length, 0) * uvScale,
            // Right
            new Vector2(0, 0.5f) * uvScale, new Vector2(length, 0.5f) * uvScale, new Vector2(0, 0) * uvScale, new Vector2(length, 0) * uvScale
        };

        // Build 30 indices for the 10 triangles (5 faces * 2 triangles)
        int[] triangles = new int[30];
        for (int i = 0; i < 5; i++)
        {
            int v = i * 4;
            int t = i * 6;
            
            // Triangle 1
            triangles[t + 0] = v + 0;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            
            // Triangle 2
            triangles[t + 3] = v + 2;
            triangles[t + 4] = v + 1;
            triangles[t + 5] = v + 3;
        }

        // Create or reuse and assign the mesh
        Mesh proceduralMesh = meshFilter.sharedMesh;
        if (proceduralMesh == null)
        {
            proceduralMesh = new Mesh();
            proceduralMesh.name = "AutoFloorMesh";
            meshFilter.sharedMesh = proceduralMesh;
        }
        else
        {
            proceduralMesh.Clear();
        }

        proceduralMesh.vertices = vertices;
        proceduralMesh.uv = uvs;
        proceduralMesh.triangles = triangles;

        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateTangents();
        proceduralMesh.RecalculateBounds();
    }
}