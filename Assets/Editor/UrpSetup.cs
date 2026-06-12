using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// One-click URP setup (docs/AESTHETICS_PLAN.md, Part C): creates a
    /// Quest-tuned Universal Render Pipeline asset and assigns it to
    /// Graphics and Quality settings.
    ///
    /// After running:
    ///   1. Run Window &gt; Rendering &gt; Render Pipeline Converter to upgrade
    ///      any pre-existing Built-in materials.
    ///   2. Rebuild scenes via NSF Grant &gt; Build Discovery Hall Scene —
    ///      builder-generated materials follow the active pipeline.
    ///   3. In Project Settings &gt; XR Plug-in Management, confirm the Meta
    ///      settings still apply (re-run the Meta Project Setup Tool).
    /// </summary>
    public static class UrpSetup
    {
        [MenuItem("NSF Grant/Setup URP Pipeline")]
        public static void Setup()
        {
            System.IO.Directory.CreateDirectory("Assets/Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");

            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            // Quest budget per the aesthetics plan: MSAA 4x, no HDR,
            // modest shadows; baked GI carries the lighting.
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = 20f;
            pipeline.renderScale = 1f;
            AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP_PipelineAsset.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();

            Debug.Log("[UrpSetup] URP assigned (MSAA 4x, HDR off, 20m shadows). " +
                      "Next: run the Render Pipeline Converter for existing materials, " +
                      "rebuild scenes from the NSF Grant menu, and re-run the Meta Project Setup Tool.");
        }
    }
}
