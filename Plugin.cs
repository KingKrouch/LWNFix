using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;


namespace LWNFix;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("LittleWitchNobeta.exe")]
public class Plugin : BasePlugin
{
    private static ManualLogSource? LogSource { get; set; }

    public override void Load()
    {
        LogSource = Log;
        
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Harmony.CreateAndPatchAll(typeof(ResolutionPatches));
        Harmony.CreateAndPatchAll(typeof(UIPatches));
        Harmony.CreateAndPatchAll(typeof(GraphicsPatches));
        Harmony.CreateAndPatchAll(typeof(FOVPatches));
    }
    
    [HarmonyPatch]
    public class ResolutionPatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.UpdateResolutionSettings)), HarmonyPrefix]
        public static bool ForceCustomResolution()
        {
            Debug.Log("Resolution Value Changed");
            Screen.SetResolution(3440, 1440, FullScreenMode.FullScreenWindow);
            return false;
        }
    }

    [HarmonyPatch]
    public class GraphicsPatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.UpdateQualitySettings)), HarmonyPostfix]
        public static void LoadGraphicsSettings()
        {
            // TODO:
            // 1. Figure out why the texture filtering is not working correctly. Despite our patches, the textures are still blurry as fuck and has visible seams.
            // 2. Find a way of writing to the shadow resolution variables in the UniversalRenderPipelineAsset.
            
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Texture.SetGlobalAnisotropicFilteringLimits(16, 16);
            //Texture.masterTextureLimit      = 0; // Can raise this to force lower the texture size. Goes up to 14.
            //QualitySettings.maximumLODLevel = 0; // Can raise this to force lower the LOD settings. 3 at max if you want it to look like a blockout level prototype.
            //QualitySettings.lodBias         = 1.0f;
            
            // Let's adjust some of the Render Pipeline Settings during runtime.
            //var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

            //if (asset == null) return;
            //asset.renderScale = 0.25f / 100; // Test
            //asset.msaaSampleCount = 0; // Default is 4x MSAA
            //asset.shadowCascadeCount = 0; // Default is 4
            //QualitySettings.renderPipeline = asset;
        }
    }

    [HarmonyPatch]
    public class FOVPatches
    {
        //[HarmonyPatch(typeof(Camera), MethodType.StaticConstructor), HarmonyPostfix]
        public static void UpdateCameraFOV(Camera __instance)
        {
            var oldFOV = __instance.fieldOfView;
            __instance.sensorSize = new Vector2(16,9);
            __instance.gateFit = Camera.GateFitMode.Overscan;
            __instance.usePhysicalProperties = true;
            __instance.fieldOfView = oldFOV;
        }
    }

    [HarmonyPatch]
    public class UIPatches
    {
        [HarmonyPatch(typeof(UIMagicBar), nameof(UIMagicBar.Init)), HarmonyPostfix]
        public static void AddAspectRatioFitterToMagicBar(UIMagicBar __instance)
        {
            // We need to explicitly check if it exists first, as for some reason, the component can be added twice and cause slow movement.
            var magicBarAspectRatioFitter = __instance.gameObject.GetComponent<AspectRatioFitter>();
            if (magicBarAspectRatioFitter != null) return;
            magicBarAspectRatioFitter = __instance.gameObject.AddComponent<AspectRatioFitter>();
            Debug.Log("Adding Aspect Ratio Fitter to " + __instance.gameObject.name);
            magicBarAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            magicBarAspectRatioFitter.enabled = true;

            // Check if the display aspect ratio is less than 16:9, and if so, disable the AspectRatioFitter and use the old transforms.
            if (Screen.currentResolution.m_Width / Screen.currentResolution.m_Height >= 1920.0f / 1080.0f)
            {
                magicBarAspectRatioFitter.aspectRatio = 1920.0f / 1080.0f;
            }
            else
            {
                magicBarAspectRatioFitter.aspectRatio = Screen.currentResolution.m_Width / (float)Screen.currentResolution.m_Height;
            }
        }
    }
}
