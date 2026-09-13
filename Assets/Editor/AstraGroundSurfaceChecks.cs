using System;
using AstraExpress;
using UnityEditor;
using UnityEngine;

public static class AstraGroundSurfaceChecks
{
    [MenuItem("Astra Express/Validate continuous ground in Play Mode")]
    public static void Validate()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode before validating the generated ground.");
        var game = UnityEngine.Object.FindFirstObjectByType<AstraGame>();
        var surface = UnityEngine.Object.FindFirstObjectByType<AstraGroundSurface>();
        if (game == null || surface == null) throw new InvalidOperationException("The colony ground has not been generated.");
        var simulation = game.Simulation;
        var collider = surface.GetComponent<MeshCollider>();
        var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var uv = mesh.uv;
        int checkedCells = 0;
        for (int row = 0; row < ColonySimulation.Height; row++)
            for (int column = 0; column < ColonySimulation.Width; column++)
            {
                var cell = new Cell(column, row);
                var ray = new Ray(new Vector3(column * TerrainGrid.CellSize, 20, row * TerrainGrid.CellSize), Vector3.down);
                bool found = collider.Raycast(ray, out var hit, 30);
                if (simulation.Terrain.Kind(cell) != TerrainKind.Flat)
                {
                    Require(!found, "Ground must leave cliff/ramp cells to their existing meshes: " + cell);
                    continue;
                }
                Require(found && surface.TryGetCell(hit, out var picked) && picked.Equals(cell), "Collider must select the correct grid cell: " + cell);
                Require(Mathf.Abs(hit.point.y - (simulation.Terrain.Elevation(cell) * TerrainGrid.LevelHeight - 0.02f)) < 0.001f, "Ground height must match elevation: " + cell);
                checkedCells++;
            }
        for (int index = 0; index < vertices.Length; index += 4)
        {
            if (Mathf.Abs(vertices[index].y - vertices[index + 2].y) > 0.001f) continue;
            for (int corner = 0; corner < 4; corner++)
                Require(Vector2.Distance(uv[index + corner], new Vector2(vertices[index + corner].x, vertices[index + corner].z) / Mathf.Max(0.1f, game.TerrainTextureRepeat)) < 0.0001f, "Top UVs must use continuous world coordinates.");
        }
        for (int submesh = 0; submesh < 2; submesh++)
        {
            var triangles = mesh.GetTriangles(submesh);
            for (int triangle = 0; triangle < triangles.Length; triangle += 6)
            {
                int start = triangles[triangle];
                var center = (vertices[start] + vertices[start + 1] + vertices[start + 2] + vertices[start + 3]) * 0.25f;
                var cell = new Cell(Mathf.Clamp(Mathf.FloorToInt(center.x / TerrainGrid.CellSize + 0.5f), 0, ColonySimulation.Width - 1),
                    Mathf.Clamp(Mathf.FloorToInt(center.z / TerrainGrid.CellSize + 0.5f), 0, ColonySimulation.Height - 1));
                Require(simulation.IsRevealed(cell) == (submesh == 0), "Unrevealed ground must use the opaque fog material: " + cell);
            }
        }
        Require(collider.sharedMesh != mesh, "Fog updates must not rebuild the picking collider.");
        var material = surface.GetComponent<MeshRenderer>().sharedMaterials[0];
        Require(material != null && material.shader != null && !ShaderUtil.ShaderHasError(material.shader), "The ground shader must compile without errors.");
        if (material.HasProperty("Texture2D_CDF09C33"))
        {
            Require(material.GetTexture("Texture2D_CDF09C33") != null && material.GetTexture("_Normal_Map") != null, "Desert Shader Graph textures must be assigned.");
            Require(Mathf.Abs(material.GetFloat("_Dune_Height")) < 0.0001f, "Visual dune displacement must not diverge from grid picking and rover heights.");
        }
        else
            Require(material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null && material.IsKeywordEnabled("_NORMALMAP"), "Textured ground and normal mapping must be enabled.");
        Debug.Log($"[Astra Express] Ground verified: {checkedCells} flat cells, both elevations, cliff/ramp openings, continuous UVs, grid picking, and fog isolation.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
