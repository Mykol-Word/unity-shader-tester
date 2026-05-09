using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class ShaderRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private RenderPassEvent render_pass_event = RenderPassEvent.AfterRenderingPostProcessing;

    private ShaderPass shader_pass;

    // creates the render pass
    public override void Create()
    {
        shader_pass = new ShaderPass();
        shader_pass.renderPassEvent = render_pass_event;
    }

    // queues the shader pass for cameras with shader controllers
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData rendering_data)
    {
        Camera camera = rendering_data.cameraData.camera;
        Material material = ShaderController.GetActiveMaterial(camera);

        if (material == null)
        {
            return;
        }

        shader_pass.renderPassEvent = render_pass_event;
        shader_pass.Setup(material);
        renderer.EnqueuePass(shader_pass);
    }

    private sealed class ShaderPass : ScriptableRenderPass
    {
        private const string PassName = "Shader Tester Camera Shader";

        private Material material;

        // stores the selected shader material
        public void Setup(Material active_material)
        {
            material = active_material;
            requiresIntermediateTexture = true;
        }

        // applies the selected shader with a render graph blit
        public override void RecordRenderGraph(RenderGraph render_graph, ContextContainer frame_data)
        {
            if (material == null)
            {
                return;
            }

            UniversalResourceData resource_data = frame_data.Get<UniversalResourceData>();

            if (resource_data.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resource_data.activeColorTexture;
            TextureDesc destination_desc = render_graph.GetTextureDesc(source);
            destination_desc.name = "ShaderTesterCameraShader";
            destination_desc.clearBuffer = false;
            TextureHandle destination = render_graph.CreateTexture(destination_desc);
            RenderGraphUtils.BlitMaterialParameters parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            render_graph.AddBlitPass(parameters, PassName);
            resource_data.cameraColor = destination;
        }
    }
}
