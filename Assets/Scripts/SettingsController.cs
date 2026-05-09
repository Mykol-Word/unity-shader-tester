using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class SettingsController : MonoBehaviour
{
    private const int WindowedWidth = 1280;
    private const int WindowedHeight = 720;

    [SerializeField] private UIDocument document;
    [SerializeField] private Camera target_camera;

    private VisualElement modal_layer;
    private VisualElement options_modal;
    private Toggle fullscreen_toggle;
    private TextField background_color_field;
    private Label settings_error;
    private bool callbacks_registered;
    private bool defaults_applied;

    // caches component references
    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    // initializes settings ui
    private void OnEnable()
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        CacheElements();
        RegisterCallbacks();
        ApplyDefaults();
    }

    // retries setup if the ui document was not ready during onenable
    private void Update()
    {
        if (callbacks_registered)
        {
            return;
        }

        CacheElements();
        RegisterCallbacks();
        ApplyDefaults();
    }

    // finds settings ui elements
    private void CacheElements()
    {
        if (document == null || document.rootVisualElement == null)
        {
            return;
        }

        VisualElement root = document.rootVisualElement;
        modal_layer = root.Q<VisualElement>("modal_layer");
        options_modal = root.Q<VisualElement>("options_modal");
        fullscreen_toggle = root.Q<Toggle>("fullscreen_toggle");
        background_color_field = root.Q<TextField>("background_color_field");
        settings_error = root.Q<Label>("settings_error");
    }

    // connects settings callbacks
    private void RegisterCallbacks()
    {
        if (callbacks_registered || document == null || document.rootVisualElement == null)
        {
            return;
        }

        Button options_button = document.rootVisualElement.Q<Button>("options_button");
        Button close_options_button = document.rootVisualElement.Q<Button>("close_options_button");

        if (options_button == null || close_options_button == null || fullscreen_toggle == null || background_color_field == null)
        {
            return;
        }

        options_button.RegisterCallback<ClickEvent>(evt => OpenOptions());
        close_options_button.RegisterCallback<ClickEvent>(evt => CloseOptions());
        fullscreen_toggle.RegisterValueChangedCallback(evt => SetFullscreen(evt.newValue));
        background_color_field.RegisterValueChangedCallback(evt => SetBackgroundColor(evt.newValue));
        callbacks_registered = true;
    }

    // applies default settings
    private void ApplyDefaults()
    {
        if (defaults_applied || fullscreen_toggle == null || background_color_field == null)
        {
            return;
        }

        fullscreen_toggle.SetValueWithoutNotify(false);
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(WindowedWidth, WindowedHeight, false);

        if (target_camera != null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(target_camera.backgroundColor);
            background_color_field.SetValueWithoutNotify($"#{hex}");
        }
        else
        {
            background_color_field.SetValueWithoutNotify("#FFFFFF");
        }

        defaults_applied = true;
    }

    // opens the options modal
    private void OpenOptions()
    {
        if (modal_layer == null || options_modal == null)
        {
            return;
        }

        HideSiblingModals();
        settings_error.text = string.Empty;
        modal_layer.RemoveFromClassList("hidden");
        options_modal.RemoveFromClassList("hidden");
    }

    // closes the options modal
    private void CloseOptions()
    {
        if (modal_layer == null || options_modal == null)
        {
            return;
        }

        options_modal.AddToClassList("hidden");
        modal_layer.AddToClassList("hidden");
    }

    // sets fullscreen or windowed mode
    private void SetFullscreen(bool is_fullscreen)
    {
        if (is_fullscreen)
        {
            Resolution res = Screen.currentResolution;
            Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(WindowedWidth, WindowedHeight, false);
        }
    }

    // applies a hex camera background color
    private void SetBackgroundColor(string value)
    {
        if (target_camera == null)
        {
            settings_error.text = "No camera assigned.";
            return;
        }

        string hex = value.Trim();

        if (!Regex.IsMatch(hex, "^#([0-9a-fA-F]{6})$"))
        {
            settings_error.text = "Use #FFFFFF hex format.";
            return;
        }

        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            settings_error.text = "Invalid color.";
            return;
        }

        target_camera.backgroundColor = color;
        settings_error.text = string.Empty;
    }

    // hides other modals sharing the modal layer
    private void HideSiblingModals()
    {
        modal_layer.Q<VisualElement>("import_sprite_modal")?.AddToClassList("hidden");
        modal_layer.Q<VisualElement>("import_shader_modal")?.AddToClassList("hidden");
    }
}
