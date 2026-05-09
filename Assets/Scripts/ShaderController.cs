using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class ShaderController : MonoBehaviour
{
    private static ShaderController active_controller;
    private static Shader active_shader_global;

    private Material active_material;
    private Shader active_shader;

    // applies a shader to the active camera controller
    public static void ApplyShader(Shader shader)
    {
        ShaderController controller = GetActiveController();

        if (controller == null)
        {
            return;
        }

        controller.SetShader(shader);
    }

    // returns the currently active shader
    public static Shader GetActiveShader()
    {
        return active_shader_global;
    }

    // clears the active shader if it matches the supplied shader
    public static void ClearActiveShader(Shader shader)
    {
        if (active_controller == null || active_controller.active_shader != shader)
        {
            return;
        }

        active_controller.ClearShader();
    }

    // returns the currently active material
    public static Material GetActiveMaterial()
    {
        return active_controller != null ? active_controller.active_material : null;
    }

    // returns the material used by the urp render feature
    public static Material GetActiveMaterial(Camera camera)
    {
        if (active_controller == null || active_controller.active_material == null)
        {
            return null;
        }

        if (camera != active_controller.GetComponent<Camera>())
        {
            return null;
        }

        return active_controller.active_material;
    }

    // registers this camera as the active shader target
    private void Awake()
    {
        active_controller = this;
    }

    // clears generated materials
    private void OnDestroy()
    {
        ClearShader();

        if (active_controller == this)
        {
            active_controller = null;
        }
    }

    // enables a new shader and disables the previous one
    private void SetShader(Shader shader)
    {
        if (shader == null || shader == active_shader)
        {
            return;
        }

        ClearShader(false);
        active_shader = shader;
        active_shader_global = shader;
        active_material = new Material(shader);
        active_material.hideFlags = HideFlags.HideAndDontSave;
        UiBrowser.RefreshShaderSelection();
        UiBrowser.RefreshShaderProperties();
    }

    // disables the current shader
    private void ClearShader(bool refresh_ui = true)
    {
        active_shader = null;
        active_shader_global = null;

        if (active_material == null)
        {
            if (refresh_ui)
            {
                UiBrowser.RefreshShaderSelection();
                UiBrowser.RefreshShaderProperties();
            }

            return;
        }

        if (Application.isPlaying)
        {
            Destroy(active_material);
        }
        else
        {
            DestroyImmediate(active_material);
        }

        active_material = null;
        if (refresh_ui)
        {
            UiBrowser.RefreshShaderSelection();
            UiBrowser.RefreshShaderProperties();
        }
    }

    // finds the active camera shader controller
    private static ShaderController GetActiveController()
    {
        if (active_controller != null)
        {
            return active_controller;
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            return null;
        }

        active_controller = camera.GetComponent<ShaderController>();
        return active_controller;
    }
}
