using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;


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
        Harmony.CreateAndPatchAll(typeof(SteamPatches));
        Harmony.CreateAndPatchAll(typeof(CameraPatches));
    }

    [HarmonyPatch]
    public class SteamPatches
    {
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.InitOnPlayMode)), HarmonyPostfix]
        public static void SteamHook(SteamManager __instance)
        {
            
        }
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
        public static void LoadGraphicsSettings(Game __instance)
        {
            // TODO: Figure out why this FixedDeltaTime adjustment won't work.
            // Quick Fixed Delta Time Fix (for hair physics). May just find the hair component and move it's FixedUpdate to standard Update instead.
            Time.fixedDeltaTime = __instance.config.screenSettings.fps switch {
                FPSLimitation.FPS30     => 1.0f / 30,
                FPSLimitation.FPS60     => 1.0f / 60,
                FPSLimitation.FPS120    => 1.0f / 120,
                FPSLimitation.Unlimited => 1.0f / Screen.currentResolution.m_RefreshRate,
                _ => throw new ArgumentOutOfRangeException()
            };
            
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
    public class CameraPatches
    {
        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.Init), new Type[] { typeof(WizardGirlManage) }), HarmonyPostfix]
        public static void PlayerCameraTweaks(PlayerCamera __instance)
        {
            // Find the GameObject named "LookHereRotation"
            var lookHereRot = GameObject.Find("LookHereRotation");
            //var lookHereRot = __instance.g_PlayerLookHereRot.Find("LookHereRotation");
            if (lookHereRot != null) {
                lookHereRot.gameObject.transform.localPosition = new Vector3(-0.5f, 0.5f, 0.0f);
            }
            
            // Use Overscan FOV scaling. First we need to get the camera FOV before making adjustments.
            // NOTE: This probably does not take cutscenes or other cameras into account. Will need to find a hook for those.
            var oldFOV = __instance.g_CameraSet.fieldOfView;
            __instance.g_CameraSet.sensorSize = new Vector2(16,9);
            __instance.g_CameraSet.gateFit = Camera.GateFitMode.Overscan;
            __instance.g_CameraSet.usePhysicalProperties = true;
            __instance.g_CameraSet.fieldOfView = oldFOV;
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

        [HarmonyPatch(typeof(StaffManager), nameof(StaffManager.PlayVideo)), HarmonyPostfix]
        public static void FixCreditsVideoScaling(StaffManager __instance)
        {
            __instance.player.aspectRatio = VideoAspectRatio.FitInside;
        }

        [HarmonyPatch(typeof(UIVideoPlayer), nameof(UIVideoPlayer.PlayFromBeginning))]
        public static void FixVideoPlayback(UIVideoPlayer __instance)
        {
            // TODO: Figure out why this code isn't working.
            var videoPlayerComponent = __instance.gameObject.GetComponent<VideoPlayer>();
            if (videoPlayerComponent != null) {
                // By default, the game uses FitHorizontal, which while works for ultrawide, might cause problems with resolutions narrower than 16:9.
                videoPlayerComponent.aspectRatio = VideoAspectRatio.FitInside;
            }
        }

        [HarmonyPatch(typeof(UIOpeningMenu), nameof(UIOpeningMenu.Init))]
        public static void FixMainMenuVignette(UIOpeningMenu __instance)
        {
            // We need to explicitly check if it exists first, as for some reason, the component can be added twice and cause slow movement.
            var vignetteAspectRatioFitter = __instance.gameObject.GetComponent<AspectRatioFitter>();
            if (vignetteAspectRatioFitter != null) return;
            vignetteAspectRatioFitter = __instance.gameObject.AddComponent<AspectRatioFitter>();
            Debug.Log("Adding Aspect Ratio Fitter to " + __instance.gameObject.name);
            vignetteAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            vignetteAspectRatioFitter.enabled = true;

            // Check if the display aspect ratio is less than 16:9, and if so, disable the AspectRatioFitter and use the old transforms.
            if (Screen.currentResolution.m_Width / Screen.currentResolution.m_Height >= 1920.0f / 1080.0f)
            {
                vignetteAspectRatioFitter.aspectRatio = 1920.0f / 1080.0f;
            }
            else
            {
                vignetteAspectRatioFitter.aspectRatio = Screen.currentResolution.m_Width / (float)Screen.currentResolution.m_Height;
            }
        }
    }
}
