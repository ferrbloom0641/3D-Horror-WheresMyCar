using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Configure the native startup sequence before Windows builds; no runtime objects.
public sealed class FerrbloomBuildSplash : IPreprocessBuildWithReport
{
    private const string LogoPath = "Assets/FERRBLOOM_Logo.png";
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows
            && report.summary.platform != BuildTarget.StandaloneWindows64) return;
        Configure();
    }

    [MenuItem("Tools/FERRBLOOM/Apply Windows Startup Logos")]
    public static void Configure()
    {
        ConfigureGameIcon();
        AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
        if (importer == null) throw new BuildFailedException("FERRBLOOM startup logo is missing.");
        if (importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.mipmapEnabled || importer.isReadable || importer.maxTextureSize != 1024)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        if (logo == null) throw new BuildFailedException("FERRBLOOM logo could not be imported as a Sprite.");
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
        PlayerSettings.SplashScreen.backgroundColor = Color.black;
        PlayerSettings.SplashScreen.overlayOpacity = 0f;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.logos = new[]
        {
            PlayerSettings.SplashScreenLogo.CreateWithUnityLogo(2f),
            PlayerSettings.SplashScreenLogo.Create(2f, logo)
        };
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/FERRBLOOM/Apply Parking Game Icon")]
    public static void ConfigureGameIcon()
    {
        const string iconPath = "Assets/ParkingGameIcon.png";
        AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer == null) throw new BuildFailedException("Parking game icon is missing.");
        if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.maxTextureSize != 1024)
        {
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (icon == null) throw new BuildFailedException("Parking game icon could not be loaded.");
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
        Texture2D[] icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = icon;
        PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
        AssetDatabase.SaveAssets();
    }
}

