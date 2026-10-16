using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ParkingGarageAtmosphereLightingSetup
{
    private const string PendingMarker = "Assets/Editor/ParkingGarageAtmosphereLighting.pending";
    private const string SourcePrefab = "Assets/Parkgarage/Parkgarage_prefab.prefab";
    private const string BackupPrefab = "Assets/Parkgarage/Parkgarage_prefab_PreWebOptimization.prefab";
    private const string MaterialPath = "Assets/Parkgarage/Materials/Web_Red_LightMesh.mat";
    private const string ScenePath = "Assets/Scenes/Parking.unity";
    private const string LightingSettingsPath = "Assets/Settings/ParkingAtmosphereLightingSettings.lighting";
    private const string ReportPath = "Temp/ParkingGarageAtmosphereLightingReport.txt";
    private const float CeilingLightIntensityScale = 0.58f;

    private static bool queued;
    private static bool bakeStarted;

    [InitializeOnLoadMethod]
    private static void QueuePendingSetup()
    {
        if (!File.Exists(PendingMarker)) return;
        QueueUpdate();
    }

    [MenuItem("Tools/Parking Garage/Apply Atmosphere Lighting And Bake")]
    public static void ApplyFromMenu()
    {
        File.WriteAllText(PendingMarker, "pending");
        QueueUpdate();
    }

    private static void QueueUpdate()
    {
        if (queued) return;
        queued = true;
        EditorApplication.update += TryApplyWhenReady;
    }

    private static void TryApplyWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= TryApplyWhenReady;
        queued = false;

        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                WriteReport($"WAITING: Open {ScenePath} and run Tools/Parking Garage/Apply Atmosphere Lighting And Bake.");
                return;
            }

            int restoredLights = RestoreBakedLights();
            ConfigureFluorescentMaterial();
            ConfigureLightingSettings();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            File.Delete(PendingMarker);
            Lightmapping.bakeCompleted -= HandleBakeCompleted;
            Lightmapping.bakeCompleted += HandleBakeCompleted;
            bakeStarted = Lightmapping.BakeAsync();
            WriteReport(
                $"APPLIED\nRestoredBakedLights={restoredLights}\n" +
                $"CeilingIntensityScale={CeilingLightIntensityScale:0.00}\nBakeStarted={bakeStarted}");

            if (!bakeStarted)
                Lightmapping.bakeCompleted -= HandleBakeCompleted;
        }
        catch (Exception exception)
        {
            WriteReport("FAILED\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static int RestoreBakedLights()
    {
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(BackupPrefab);
        GameObject targetRoot = PrefabUtility.LoadPrefabContents(SourcePrefab);
        int restored = 0;
        try
        {
            foreach (Light sourceLight in sourceRoot.GetComponentsInChildren<Light>(true))
            {
                Transform targetTransform = FindMatchingTransform(
                    sourceRoot.transform, targetRoot.transform, sourceLight.transform);
                if (targetTransform == null)
                {
                    Debug.LogWarning($"[Atmosphere Lighting] Matching transform not found: {sourceLight.name}");
                    continue;
                }

                Light targetLight = targetTransform.GetComponent<Light>();
                if (targetLight == null) targetLight = targetTransform.gameObject.AddComponent<Light>();
                EditorUtility.CopySerialized(sourceLight, targetLight);
                targetLight.lightmapBakeType = LightmapBakeType.Baked;
                if (IsCeilingFixture(sourceLight.transform))
                    targetLight.intensity = sourceLight.intensity * CeilingLightIntensityScale;
                EditorUtility.SetDirty(targetLight);
                restored++;
            }

            PrefabUtility.SaveAsPrefabAsset(targetRoot, SourcePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            PrefabUtility.UnloadPrefabContents(targetRoot);
        }

        return restored;
    }

    private static Transform FindMatchingTransform(Transform sourceRoot, Transform targetRoot, Transform source)
    {
        var siblingIndices = new Stack<int>();
        for (Transform current = source; current != null && current != sourceRoot; current = current.parent)
            siblingIndices.Push(current.GetSiblingIndex());

        Transform target = targetRoot;
        while (siblingIndices.Count > 0)
        {
            int index = siblingIndices.Pop();
            if (index < 0 || index >= target.childCount) return null;
            target = target.GetChild(index);
        }
        return target;
    }

    private static bool IsCeilingFixture(Transform start)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("lamps_parkinggaragefloor_")) return true;
        }
        return false;
    }

    private static void ConfigureFluorescentMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) throw new InvalidOperationException($"Missing material: {MaterialPath}");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP/Lit shader was not found.");
        material.shader = shader;

        Color baseColor = new Color(0.32f, 0.43f, 0.3f, 1f);
        Color emission = new Color(0.38f, 0.58f, 0.34f, 1f) * 2.2f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureLightingSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Parking Atmosphere Lighting Settings" };
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 6f;
        settings.lightmapMaxSize = 2048;
        settings.indirectResolution = 1f;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 128;
        settings.environmentSampleCount = 32;
        settings.maxBounces = 2;
        LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
        Lightmapping.lightingSettings = settings;
        EditorUtility.SetDirty(settings);
    }

    private static void HandleBakeCompleted()
    {
        Lightmapping.bakeCompleted -= HandleBakeCompleted;
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        WriteReport("COMPLETED\nBaked lighting data saved for Parking.unity.");
        Debug.Log("[Atmosphere Lighting] Parking garage light bake completed.");
    }

    private static void WriteReport(string text)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, text);
        Debug.Log("[Atmosphere Lighting] " + text.Replace('\n', ' '));
    }
}
