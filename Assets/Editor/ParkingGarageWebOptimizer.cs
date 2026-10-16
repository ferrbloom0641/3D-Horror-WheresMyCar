using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ParkingGarageWebOptimizer
{
    private const string SourcePrefab = "Assets/Parkgarage/Parkgarage_prefab.prefab";
    private const string BackupPrefab = "Assets/Parkgarage/Parkgarage_prefab_PreWebOptimization.prefab";
    private const string MaterialPath = "Assets/Parkgarage/Materials/Web_Red_LightMesh.mat";
    private const string BumpMaterialPath = "Assets/Parkgarage/Prefabs/lamps/Materials/bump.mat";
    private const string GlassMaterialPath = "Assets/Parkgarage/Prefabs/lamps/Materials/Glass.mat";
    private const string MeshFolder = "Assets/Parkgarage/WebOptimizedMeshes";
    private const string PendingMarker = "Assets/Editor/ParkingGarageWebOptimization.pending";
    private const string ReportPath = "Temp/ParkingGarageWebOptimizationReport.txt";

    [InitializeOnLoadMethod]
    private static void QueuePendingOptimization()
    {
        if (!File.Exists(PendingMarker)) return;
        EditorApplication.delayCall += OptimizeForWeb;
    }

    [MenuItem("Tools/Parking Garage/Optimize Lights For Web")]
    public static void OptimizeForWeb()
    {
        if (!File.Exists(PendingMarker) && File.Exists(ReportPath)) return;
        Directory.CreateDirectory("Temp");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BackupPrefab) == null &&
            !AssetDatabase.CopyAsset(SourcePrefab, BackupPrefab))
        {
            File.WriteAllText(ReportPath, "FAILED: Could not create prefab backup.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BackupPrefab) != null)
        {
            File.Copy(Path.GetFullPath(BackupPrefab), Path.GetFullPath(SourcePrefab), true);
            AssetDatabase.ImportAsset(SourcePrefab, ImportAssetOptions.ForceSynchronousImport |
                                                    ImportAssetOptions.ForceUpdate);
        }

        EnsureAssetFolder("Assets/Parkgarage/Materials");
        EnsureAssetFolder(MeshFolder);
        foreach (string meshGuid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(meshGuid));
        Material greenMaterial = CreateOrLoadGreenMaterial();
        Material bumpMaterial = AssetDatabase.LoadAssetAtPath<Material>(BumpMaterialPath);
        Material glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (bumpMaterial == null || glassMaterial == null)
        {
            File.WriteAllText(ReportPath, "FAILED: Could not load bump.mat or Glass.mat.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefab);
        var createdMeshPaths = new List<string>();
        try
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            var rendererFloors = new Dictionary<MeshRenderer, string>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (IsParkingCeilingFixture(renderer.transform) &&
                    HasTargetMaterial(renderer, bumpMaterial, glassMaterial))
                    rendererFloors.Add(renderer, FindFloorName(renderer.transform));
            AssignUnresolvedFloorsByHeight(rendererFloors);

            int combinedRendererCount = 0;
            foreach (IGrouping<string, KeyValuePair<MeshRenderer, string>> floorGroup in
                     rendererFloors.GroupBy(pair => pair.Value).OrderBy(group => group.Key))
            {
                List<MeshRenderer> renderers = floorGroup.Select(pair => pair.Key)
                    .Where(renderer => renderer != null).Distinct().ToList();
                if (renderers.Count == 0) continue;
                var combines = new List<CombineInstance>();
                foreach (MeshRenderer renderer in renderers)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Material[] materials = renderer.sharedMaterials;
                    int slotCount = Mathf.Min(filter.sharedMesh.subMeshCount, materials.Length);
                    for (int subMesh = 0; subMesh < slotCount; subMesh++)
                    {
                        if (!IsTargetMaterial(materials[subMesh], bumpMaterial, glassMaterial)) continue;
                        combines.Add(new CombineInstance
                        {
                            mesh = filter.sharedMesh,
                            subMeshIndex = subMesh,
                            transform = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                        });
                    }
                }

                if (combines.Count == 0) continue;
                var combinedMesh = new Mesh
                {
                    name = $"Web_GreenFluorescentTubes_{floorGroup.Key}",
                    indexFormat = IndexFormat.UInt32
                };
                combinedMesh.CombineMeshes(combines.ToArray(), true, true, false);
                combinedMesh.RecalculateBounds();
                string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{MeshFolder}/{combinedMesh.name}.asset");
                AssetDatabase.CreateAsset(combinedMesh, meshPath);
                createdMeshPaths.Add(meshPath);

                var combinedObject = new GameObject(combinedMesh.name);
                combinedObject.transform.SetParent(root.transform, false);
                combinedObject.isStatic = true;
                combinedObject.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
                MeshRenderer combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
                combinedRenderer.sharedMaterial = greenMaterial;
                combinedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                combinedRenderer.receiveShadows = false;
                combinedRenderer.lightProbeUsage = LightProbeUsage.Off;
                combinedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                combinedRendererCount++;

                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    StripTargetSubMeshes(renderer, filter, bumpMaterial, glassMaterial, createdMeshPaths);
                }
            }

            foreach (Light light in lights)
            {
                if (light == null) continue;
                light.lightmapBakeType = LightmapBakeType.Baked;
                EditorUtility.SetDirty(light);
            }

            PrefabUtility.SaveAsPrefabAsset(root, SourcePrefab);
            string report =
                $"SUCCESS\nBakedLights={lights.Length}\nFluorescentRenderers={rendererFloors.Count}\n" +
                $"CombinedFloorRenderers={combinedRendererCount}\nMeshes={string.Join(",", createdMeshPaths)}\n" +
                $"Backup={BackupPrefab}";
            File.WriteAllText(ReportPath, report);
            File.Delete(PendingMarker);
            AssetDatabase.Refresh();
            Debug.Log(report);
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, "FAILED\n" + exception);
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool HasTargetMaterial(MeshRenderer renderer, Material bumpMaterial, Material glassMaterial)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null &&
               renderer.sharedMaterials.Any(material => IsTargetMaterial(material, bumpMaterial, glassMaterial));
    }

    private static bool IsParkingCeilingFixture(Transform start)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("lamps_parkinggaragefloor_01") ||
                lower.Contains("lamps_parkinggaragefloor_02") ||
                lower.Contains("lamps_parkinggaragefloor_03") ||
                lower.Contains("lamps_parkinggaragefloor_04"))
                return true;
        }

        return false;
    }

    private static bool IsTargetMaterial(Material material, Material bumpMaterial, Material glassMaterial)
    {
        return material == bumpMaterial || material == glassMaterial;
    }

    private static void StripTargetSubMeshes(
        MeshRenderer renderer,
        MeshFilter filter,
        Material bumpMaterial,
        Material glassMaterial,
        List<string> createdMeshPaths)
    {
        if (filter == null || filter.sharedMesh == null) return;
        Mesh sourceMesh = filter.sharedMesh;
        Material[] materials = renderer.sharedMaterials;
        int slotCount = Mathf.Min(sourceMesh.subMeshCount, materials.Length);
        var retainedCombines = new List<CombineInstance>();
        var retainedMaterials = new List<Material>();
        for (int subMesh = 0; subMesh < slotCount; subMesh++)
        {
            if (IsTargetMaterial(materials[subMesh], bumpMaterial, glassMaterial)) continue;
            retainedCombines.Add(new CombineInstance
            {
                mesh = sourceMesh,
                subMeshIndex = subMesh,
                transform = Matrix4x4.identity
            });
            retainedMaterials.Add(materials[subMesh]);
        }

        if (retainedCombines.Count == 0)
        {
            UnityEngine.Object.DestroyImmediate(renderer, true);
            UnityEngine.Object.DestroyImmediate(filter, true);
            return;
        }

        var retainedMesh = new Mesh
        {
            name = $"{sourceMesh.name}_WithoutFluorescentTube",
            indexFormat = sourceMesh.indexFormat
        };
        retainedMesh.CombineMeshes(retainedCombines.ToArray(), false, false, false);
        retainedMesh.RecalculateBounds();
        string retainedPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{MeshFolder}/{retainedMesh.name}.asset");
        AssetDatabase.CreateAsset(retainedMesh, retainedPath);
        createdMeshPaths.Add(retainedPath);
        filter.sharedMesh = retainedMesh;
        renderer.sharedMaterials = retainedMaterials.ToArray();
    }

    private static string FindFloorName(Transform start)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("floor_01") || lower.Contains("floor01")) return "Floor1F";
            if (lower.Contains("floor_02") || lower.Contains("floor02")) return "Floor2F";
            if (lower.Contains("floor_03") || lower.Contains("floor03")) return "Floor3F";
            if (lower.Contains("floor_04") || lower.Contains("floor04")) return "Floor4F";
        }
        return "Unresolved";
    }

    private static void AssignUnresolvedFloorsByHeight(Dictionary<MeshRenderer, string> rendererFloors)
    {
        List<MeshRenderer> unresolved = rendererFloors
            .Where(pair => pair.Value == "Unresolved" && pair.Key != null)
            .Select(pair => pair.Key).Distinct().OrderBy(renderer => renderer.bounds.center.y).ToList();
        if (unresolved.Count == 0) return;

        int clusterCount = Mathf.Min(4, unresolved.Count);
        float[] centers = new float[clusterCount];
        for (int i = 0; i < clusterCount; i++)
        {
            int index = Mathf.RoundToInt(i * (unresolved.Count - 1f) / Mathf.Max(1, clusterCount - 1));
            centers[i] = unresolved[index].bounds.center.y;
        }

        for (int iteration = 0; iteration < 12; iteration++)
        {
            float[] sums = new float[clusterCount];
            int[] counts = new int[clusterCount];
            foreach (MeshRenderer renderer in unresolved)
            {
                int nearest = FindNearestCenter(renderer.bounds.center.y, centers);
                sums[nearest] += renderer.bounds.center.y;
                counts[nearest]++;
            }
            for (int i = 0; i < clusterCount; i++)
                if (counts[i] > 0) centers[i] = sums[i] / counts[i];
        }

        float[] orderedCenters = centers.OrderBy(value => value).ToArray();
        foreach (MeshRenderer renderer in unresolved)
        {
            int nearest = FindNearestCenter(renderer.bounds.center.y, orderedCenters);
            rendererFloors[renderer] = $"Floor{nearest + 1}F";
        }
    }

    private static int FindNearestCenter(float value, float[] centers)
    {
        int nearest = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < centers.Length; i++)
        {
            float distance = Mathf.Abs(value - centers[i]);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = i;
        }
        return nearest;
    }

    private static Material CreateOrLoadGreenMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.name = "Web Green Fluorescent Tube";
        Color green = new Color(0.48f, 0.6f, 0.42f, 1f);
        material.color = green;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", green);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", green * 2.2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
