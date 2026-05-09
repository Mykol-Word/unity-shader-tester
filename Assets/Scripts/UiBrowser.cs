using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(UIDocument))]
[ExecuteAlways]
public sealed class UiBrowser : MonoBehaviour
{
    private const string SpriteFolder = "Assets/Sprites";
    private const string ShaderFolder = "Assets/Shaders";
    private const string ShaderPreviewFolder = "Assets/Shaders/Preview";
    private const string ShaderDefaultPreviewPath = "Assets/Shaders/Default/default.png";

    private static readonly List<UiBrowser> ActiveBrowsers = new List<UiBrowser>();

    [SerializeField] private UIDocument document;

    private ScrollView shader_list;
    private ScrollView sprite_list;
    private VisualElement modal_layer;
    private VisualElement sprite_modal;
    private VisualElement shader_modal;
    private VisualElement context_menu;
    private Button delete_asset_button;
    private TextField sprite_name_field;
    private TextField shader_name_field;
    private Label sprite_file_label;
    private Label shader_file_label;
    private Label shader_preview_label;
    private Label sprite_import_error;
    private Label shader_import_error;
    private VisualElement properties_panel;
    private ScrollView properties_list;
    private readonly Dictionary<Shader, VisualElement> shader_items = new Dictionary<Shader, VisualElement>();
    private string selected_sprite_path;
    private string selected_shader_path;
    private string selected_shader_preview_path;
    private string context_asset_path;
    private string context_shader_preview_name;
    private bool context_is_shader;
    private bool callbacks_registered;

    // caches component references
    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    // registers the browser and fills the lists
    private void OnEnable()
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        if (!ActiveBrowsers.Contains(this))
        {
            ActiveBrowsers.Add(this);
        }

        CacheElements();
        RegisterCallbacks();
        Refresh();
    }

    // unregisters the browser
    private void OnDisable()
    {
        ActiveBrowsers.Remove(this);
    }

    // retries setup if the ui document was not ready during onenable
    private void Update()
    {
        if (shader_list != null && sprite_list != null && callbacks_registered)
        {
            return;
        }

        CacheElements();
        RegisterCallbacks();
        Refresh();
    }

    // refreshes every active ui browser
    public static void RefreshActiveBrowsers()
    {
        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            ActiveBrowsers[i].Refresh();
        }
    }

    // returns true if a screen position is over shader tester ui
    public static bool IsScreenPositionOverUi(Vector2 screen_position)
    {
        Vector2 panel_position = new Vector2(screen_position.x, Screen.height - screen_position.y);

        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            if (ActiveBrowsers[i].ContainsPanelPosition(panel_position))
            {
                return true;
            }
        }

        return false;
    }

    // updates shader selection visuals on all browsers
    public static void RefreshShaderSelection()
    {
        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            ActiveBrowsers[i].UpdateShaderSelection();
        }
    }

    // updates shader property panels on all browsers
    public static void RefreshShaderProperties()
    {
        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            ActiveBrowsers[i].BuildPropertyPanel();
        }
    }

    // returns true when a keyboard-editing ui field has focus
    public static bool IsKeyboardInputFocused()
    {
        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            if (ActiveBrowsers[i].HasKeyboardInputFocus())
            {
                return true;
            }
        }

        return false;
    }

    // returns true if any blocking modal is visible
    public static bool IsModalOpen()
    {
        for (int i = 0; i < ActiveBrowsers.Count; i++)
        {
            if (ActiveBrowsers[i].HasOpenModal())
            {
                return true;
            }
        }

        return false;
    }

    // rebuilds both asset lists
    public void Refresh()
    {
        if (shader_list == null || sprite_list == null)
        {
            CacheElements();
        }

        if (shader_list == null || sprite_list == null)
        {
            return;
        }

        shader_list.Clear();
        sprite_list.Clear();
        shader_items.Clear();
        HideContextMenu();

        AddSprites();
        AddShaders();
        BuildPropertyPanel();
    }

    // finds the list elements in the uxml tree
    private void CacheElements()
    {
        if (document == null || document.rootVisualElement == null)
        {
            return;
        }

        VisualElement root = document.rootVisualElement;
        shader_list = root.Q<ScrollView>("shader_list");
        sprite_list = root.Q<ScrollView>("sprite_list");
        modal_layer = root.Q<VisualElement>("modal_layer");
        sprite_modal = root.Q<VisualElement>("import_sprite_modal");
        shader_modal = root.Q<VisualElement>("import_shader_modal");
        sprite_name_field = root.Q<TextField>("sprite_name_field");
        shader_name_field = root.Q<TextField>("shader_name_field");
        sprite_file_label = root.Q<Label>("sprite_file_label");
        shader_file_label = root.Q<Label>("shader_file_label");
        shader_preview_label = root.Q<Label>("shader_preview_label");
        sprite_import_error = root.Q<Label>("sprite_import_error");
        shader_import_error = root.Q<Label>("shader_import_error");
        properties_panel = root.Q<VisualElement>("properties_panel");
        properties_list = root.Q<ScrollView>("properties_list");
        context_menu = root.Q<VisualElement>("context_menu");
        delete_asset_button = root.Q<Button>("delete_asset_button");
    }

    // connects button callbacks
    private void RegisterCallbacks()
    {
        if (callbacks_registered)
        {
            return;
        }

        if (document == null || document.rootVisualElement == null)
        {
            return;
        }

        VisualElement root = document.rootVisualElement;
        Button import_sprite_button = root.Q<Button>("import_sprite_button");
        Button import_shader_button = root.Q<Button>("import_shader_button");

        if (import_sprite_button == null || import_shader_button == null)
        {
            return;
        }

        import_sprite_button.RegisterCallback<ClickEvent>(evt => OpenSpriteModal());
        import_shader_button.RegisterCallback<ClickEvent>(evt => OpenShaderModal());
        root.Q<Button>("choose_sprite_file_button")?.RegisterCallback<ClickEvent>(evt => ChooseSpriteFile());
        root.Q<Button>("choose_shader_file_button")?.RegisterCallback<ClickEvent>(evt => ChooseShaderFile());
        root.Q<Button>("choose_shader_preview_button")?.RegisterCallback<ClickEvent>(evt => ChooseShaderPreviewFile());
        root.Q<Button>("cancel_sprite_import_button")?.RegisterCallback<ClickEvent>(evt => CloseModals());
        root.Q<Button>("cancel_shader_import_button")?.RegisterCallback<ClickEvent>(evt => CloseModals());
        root.Q<Button>("confirm_sprite_import_button")?.RegisterCallback<ClickEvent>(evt => ImportSprite());
        root.Q<Button>("confirm_shader_import_button")?.RegisterCallback<ClickEvent>(evt => ImportShader());
        delete_asset_button?.RegisterCallback<ClickEvent>(evt => DeleteContextAsset());
        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0 && (context_menu == null || !context_menu.worldBound.Contains(evt.position)))
            {
                HideContextMenu();
            }
        });
        callbacks_registered = true;
    }

    // adds sprite assets from the sprite folder
    private void AddSprites()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

            if (sprite == null)
            {
                for (int j = 0; j < sprites.Length; j++)
                {
                    Sprite nested_sprite = sprites[j] as Sprite;

                    if (nested_sprite == null)
                    {
                        continue;
                    }

                    sprite_list.Add(CreateSpriteItem(nested_sprite.name, nested_sprite, true, path));
                }

                continue;
            }

            sprite_list.Add(CreateSpriteItem(sprite.name, sprite, true, path));
        }
#endif
    }

    // adds shader assets from the shader folder
    private void AddShaders()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { ShaderFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

            if (shader == null)
            {
                continue;
            }

            shader_list.Add(CreateShaderItem(Path.GetFileNameWithoutExtension(path), shader, GetShaderPreview(Path.GetFileNameWithoutExtension(path)), path));
        }
#endif
    }

    // creates one sprite list entry
    private VisualElement CreateSpriteItem(string item_name, Sprite sprite, bool can_drag, string asset_path)
    {
        VisualElement item = CreateBaseItem(item_name);
        VisualElement preview = new VisualElement();
        preview.AddToClassList("item-preview");

        if (sprite != null)
        {
            preview.style.backgroundImage = new StyleBackground(sprite);
        }

        if (can_drag)
        {
            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    ShowContextMenu(evt.position, asset_path, false, string.Empty);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0 || sprite == null || !Application.isPlaying)
                {
                    return;
                }

                SpriteController.BeginSpriteDrag(sprite);
                evt.StopPropagation();
            });
        }
        else
        {
            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                ShowContextMenu(evt.position, asset_path, true, item_name);
                evt.StopPropagation();
            });
        }

        item.Insert(0, preview);
        return item;
    }

    // creates one shader list entry
    private VisualElement CreateShaderItem(string item_name, Shader shader, Sprite preview_sprite, string asset_path)
    {
        VisualElement item = CreateSpriteItem(item_name, preview_sprite, false, asset_path);
        shader_items[shader] = item;
        item.RegisterCallback<ClickEvent>(evt =>
        {
            if (shader == null || !Application.isPlaying)
            {
                return;
            }

            ShaderController.ApplyShader(shader);
            evt.StopPropagation();
        });
        UpdateShaderSelection();
        return item;
    }

    // opens the sprite import modal
    private void OpenSpriteModal()
    {
        selected_sprite_path = string.Empty;
        sprite_name_field.value = string.Empty;
        sprite_file_label.text = "No file selected";
        sprite_import_error.text = string.Empty;
        ShowModal(sprite_modal);
    }

    // opens the shader import modal
    private void OpenShaderModal()
    {
        selected_shader_path = string.Empty;
        selected_shader_preview_path = string.Empty;
        shader_name_field.value = string.Empty;
        shader_file_label.text = "No file selected";
        shader_preview_label.text = "Optional";
        shader_import_error.text = string.Empty;
        ShowModal(shader_modal);
    }

    // shows one modal panel
    private void ShowModal(VisualElement modal)
    {
        if (modal_layer == null || sprite_modal == null || shader_modal == null || modal == null)
        {
            return;
        }

        modal_layer.RemoveFromClassList("hidden");
        sprite_modal.AddToClassList("hidden");
        shader_modal.AddToClassList("hidden");
        modal.RemoveFromClassList("hidden");
    }

    // hides modal panels
    private void CloseModals()
    {
        if (modal_layer == null || sprite_modal == null || shader_modal == null)
        {
            return;
        }

        modal_layer.AddToClassList("hidden");
        sprite_modal.AddToClassList("hidden");
        shader_modal.AddToClassList("hidden");
    }

    // selects a source sprite file
    private void ChooseSpriteFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanelWithFilters("Import Sprite", string.Empty, new[] { "Image Files", "png,jpg,jpeg,psd,tga", "All Files", "*" });

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        selected_sprite_path = path;
        sprite_file_label.text = Path.GetFileName(path);
        sprite_name_field.value = Path.GetFileNameWithoutExtension(path);
        sprite_import_error.text = string.Empty;
#endif
    }

    // selects a source shader file
    private void ChooseShaderFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanelWithFilters("Import Shader", string.Empty, new[] { "Shader Files", "shader", "All Files", "*" });

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        selected_shader_path = path;
        shader_file_label.text = Path.GetFileName(path);
        shader_name_field.value = Path.GetFileNameWithoutExtension(path);
        shader_import_error.text = string.Empty;
#endif
    }

    // selects an optional shader preview sprite file
    private void ChooseShaderPreviewFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanelWithFilters("Import Shader Preview", string.Empty, new[] { "Image Files", "png,jpg,jpeg,psd,tga", "All Files", "*" });

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        selected_shader_preview_path = path;
        shader_preview_label.text = Path.GetFileName(path);
        shader_import_error.text = string.Empty;
#endif
    }

    // imports a sprite into the sprites folder
    private void ImportSprite()
    {
#if UNITY_EDITOR
        string asset_name = SanitizeAssetName(sprite_name_field.value);

        if (string.IsNullOrWhiteSpace(selected_sprite_path) || string.IsNullOrWhiteSpace(asset_name))
        {
            sprite_import_error.text = "Choose a sprite file and enter a name.";
            return;
        }

        string target_path = $"{SpriteFolder}/{asset_name}{Path.GetExtension(selected_sprite_path).ToLowerInvariant()}";

        if (AssetExists(target_path) || NameExists(SpriteFolder, asset_name))
        {
            sprite_import_error.text = "An asset with that name already exists.";
            return;
        }

        Directory.CreateDirectory(SpriteFolder);
        File.Copy(selected_sprite_path, target_path);
        AssetDatabase.ImportAsset(target_path);
        SetTextureAsSprite(target_path);
        AssetDatabase.Refresh();
        Refresh();
        CloseModals();
#endif
    }

    // imports a shader and optional preview sprite
    private void ImportShader()
    {
#if UNITY_EDITOR
        string asset_name = SanitizeAssetName(shader_name_field.value);

        if (string.IsNullOrWhiteSpace(selected_shader_path) || string.IsNullOrWhiteSpace(asset_name))
        {
            shader_import_error.text = "Choose a shader file and enter a name.";
            return;
        }

        string shader_target_path = $"{ShaderFolder}/{asset_name}.shader";

        if (AssetExists(shader_target_path) || NameExists(ShaderFolder, asset_name))
        {
            shader_import_error.text = "An asset with that name already exists.";
            return;
        }

        string preview_target_path = string.Empty;

        if (!string.IsNullOrWhiteSpace(selected_shader_preview_path))
        {
            preview_target_path = $"{ShaderPreviewFolder}/{asset_name}{Path.GetExtension(selected_shader_preview_path).ToLowerInvariant()}";

            if (AssetExists(preview_target_path) || NameExists(ShaderPreviewFolder, asset_name))
            {
                shader_import_error.text = "A preview with that name already exists.";
                return;
            }
        }

        Directory.CreateDirectory(ShaderFolder);
        Directory.CreateDirectory(ShaderPreviewFolder);
        File.Copy(selected_shader_path, shader_target_path);

        if (!string.IsNullOrWhiteSpace(preview_target_path))
        {
            File.Copy(selected_shader_preview_path, preview_target_path);
            AssetDatabase.ImportAsset(preview_target_path);
            SetTextureAsSprite(preview_target_path);
        }

        AssetDatabase.ImportAsset(shader_target_path);
        AssetDatabase.Refresh();
        Refresh();
        CloseModals();
#endif
    }

#if UNITY_EDITOR
    // returns the preview sprite for a shader entry
    private Sprite GetShaderPreview(string asset_name)
    {
        string[] guids = AssetDatabase.FindAssets($"{asset_name} t:Sprite", new[] { ShaderPreviewFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (Path.GetFileNameWithoutExtension(path) != asset_name)
            {
                continue;
            }

            Sprite sprite = LoadSpriteAtPath(path);

            if (sprite != null)
            {
                return sprite;
            }
        }

        return LoadSpriteAtPath(ShaderDefaultPreviewPath);
    }

    // loads a sprite from a texture asset path
    private Sprite LoadSpriteAtPath(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite != null)
        {
            return sprite;
        }

        Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

        for (int i = 0; i < sprites.Length; i++)
        {
            sprite = sprites[i] as Sprite;

            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    // configures imported image files as sprite assets
    private void SetTextureAsSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    // returns true if an exact asset path already exists
    private bool AssetExists(string path)
    {
        return File.Exists(path);
    }

    // returns true if an asset with the same file name exists directly in a folder
    private bool NameExists(string folder, string asset_name)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        string[] files = Directory.GetFiles(folder);

        for (int i = 0; i < files.Length; i++)
        {
            if (Path.GetExtension(files[i]) == ".meta")
            {
                continue;
            }

            if (Path.GetFileNameWithoutExtension(files[i]) == asset_name)
            {
                return true;
            }
        }

        return false;
    }
#endif

    // removes path characters from imported asset names
    private string SanitizeAssetName(string asset_name)
    {
        string clean_name = asset_name.Trim();
        char[] invalid_chars = Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalid_chars.Length; i++)
        {
            clean_name = clean_name.Replace(invalid_chars[i].ToString(), string.Empty);
        }

        return clean_name;
    }

    // returns true if a panel position overlaps visible browser ui
    private bool ContainsPanelPosition(Vector2 panel_position)
    {
        if (document == null || document.rootVisualElement == null)
        {
            return false;
        }

        VisualElement left_panel = document.rootVisualElement.Q<VisualElement>("left_panel");

        if (left_panel != null && left_panel.worldBound.Contains(panel_position))
        {
            return true;
        }

        if (modal_layer != null && !modal_layer.ClassListContains("hidden") && modal_layer.worldBound.Contains(panel_position))
        {
            return true;
        }

        if (properties_panel != null && !properties_panel.ClassListContains("hidden") && properties_panel.worldBound.Contains(panel_position))
        {
            return true;
        }

        return false;
    }

    // returns true if an editable ui field is focused
    private bool HasKeyboardInputFocus()
    {
        if (document == null || document.rootVisualElement == null || document.rootVisualElement.focusController == null)
        {
            return false;
        }

        VisualElement focused_element = document.rootVisualElement.focusController.focusedElement as VisualElement;

        while (focused_element != null)
        {
            if (focused_element is TextField || focused_element is FloatField || focused_element is IntegerField)
            {
                return true;
            }

            focused_element = focused_element.parent;
        }

        return false;
    }

    // returns true if this browser has an open modal
    private bool HasOpenModal()
    {
        return modal_layer != null && !modal_layer.ClassListContains("hidden");
    }

    // applies the active shader highlight
    private void UpdateShaderSelection()
    {
        Shader active_shader = ShaderController.GetActiveShader();

        foreach (KeyValuePair<Shader, VisualElement> pair in shader_items)
        {
            if (pair.Key == active_shader)
            {
                pair.Value.AddToClassList("selected-item-card");
            }
            else
            {
                pair.Value.RemoveFromClassList("selected-item-card");
            }
        }
    }

    // builds controls for supported shader material properties
    private void BuildPropertyPanel()
    {
        if (properties_panel == null || properties_list == null)
        {
            return;
        }

        properties_list.Clear();
        Material material = ShaderController.GetActiveMaterial();

        if (material == null || material.shader == null)
        {
            properties_panel.AddToClassList("hidden");
            return;
        }

        Shader shader = material.shader;
        int property_count = shader.GetPropertyCount();

        for (int i = 0; i < property_count; i++)
        {
            ShaderPropertyType property_type = shader.GetPropertyType(i);

            if (property_type != ShaderPropertyType.Float && property_type != ShaderPropertyType.Range && property_type != ShaderPropertyType.Int)
            {
                continue;
            }

            string property_name = shader.GetPropertyName(i);

            if (IsBoolProperty(shader, i))
            {
                properties_list.Add(CreateBoolProperty(material, shader, i, property_name));
            }
            else
            {
                properties_list.Add(CreateNumberProperty(material, shader, i, property_name, property_type));
            }
        }

        if (properties_list.childCount == 0)
        {
            properties_panel.AddToClassList("hidden");
        }
        else
        {
            properties_panel.RemoveFromClassList("hidden");
        }
    }

    // creates a numeric shader property row
    private VisualElement CreateNumberProperty(Material material, Shader shader, int property_index, string property_name, ShaderPropertyType property_type)
    {
        VisualElement row = CreatePropertyRow(shader, property_index);
        VisualElement controls = new VisualElement();
        controls.AddToClassList("property-controls");

        float value = property_type == ShaderPropertyType.Int ? material.GetInt(property_name) : material.GetFloat(property_name);
        Vector2 limits = GetPropertyLimits(shader, property_index, value);
        Slider slider = new Slider(limits.x, limits.y);
        slider.AddToClassList("property-slider");
        slider.value = value;

        slider.RegisterValueChangedCallback(evt =>
        {
            float next_value = property_type == ShaderPropertyType.Int ? Mathf.Round(evt.newValue) : evt.newValue;
            SetMaterialNumber(material, property_name, property_type, next_value);
        });

        controls.Add(slider);

        if (property_type == ShaderPropertyType.Int)
        {
            IntegerField int_field = new IntegerField();
            int_field.AddToClassList("property-number");
            int_field.value = Mathf.RoundToInt(value);
            slider.RegisterValueChangedCallback(evt => int_field.SetValueWithoutNotify(Mathf.RoundToInt(evt.newValue)));
            int_field.RegisterValueChangedCallback(evt =>
            {
                int next_value = evt.newValue;
                material.SetInt(property_name, next_value);
                slider.SetValueWithoutNotify(Mathf.Clamp(next_value, limits.x, limits.y));
            });
            controls.Add(int_field);
        }
        else
        {
            FloatField float_field = new FloatField();
            float_field.AddToClassList("property-number");
            float_field.value = value;
            slider.RegisterValueChangedCallback(evt => float_field.SetValueWithoutNotify(evt.newValue));
            float_field.RegisterValueChangedCallback(evt =>
            {
                float next_value = evt.newValue;
                material.SetFloat(property_name, next_value);
                slider.SetValueWithoutNotify(Mathf.Clamp(next_value, limits.x, limits.y));
            });
            controls.Add(float_field);
        }

        row.Add(controls);
        return row;
    }

    // creates a bool shader property row
    private VisualElement CreateBoolProperty(Material material, Shader shader, int property_index, string property_name)
    {
        VisualElement row = CreatePropertyRow(shader, property_index);
        Toggle toggle = new Toggle();
        toggle.AddToClassList("property-toggle");
        toggle.value = material.GetFloat(property_name) >= 0.5f;
        toggle.RegisterValueChangedCallback(evt => material.SetFloat(property_name, evt.newValue ? 1f : 0f));
        row.Add(toggle);
        return row;
    }

    // creates the common property row and label
    private VisualElement CreatePropertyRow(Shader shader, int property_index)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("property-row");
        string label_text = shader.GetPropertyDescription(property_index);

        if (string.IsNullOrWhiteSpace(label_text))
        {
            label_text = shader.GetPropertyName(property_index);
        }

        Label label = new Label(label_text);
        label.AddToClassList("property-name");
        row.Add(label);
        return row;
    }

    // sets a material number property
    private void SetMaterialNumber(Material material, string property_name, ShaderPropertyType property_type, float value)
    {
        if (property_type == ShaderPropertyType.Int)
        {
            material.SetInt(property_name, Mathf.RoundToInt(value));
        }
        else
        {
            material.SetFloat(property_name, value);
        }
    }

    // returns numeric limits for a property
    private Vector2 GetPropertyLimits(Shader shader, int property_index, float value)
    {
        if (shader.GetPropertyType(property_index) == ShaderPropertyType.Range)
        {
            Vector2 limits = shader.GetPropertyRangeLimits(property_index);
            return limits.x < limits.y ? limits : new Vector2(0f, 1f);
        }

        float center = Mathf.Approximately(value, 0f) ? 0.5f : value;
        float extent = Mathf.Max(Mathf.Abs(center) * 2f, 1f);
        return new Vector2(center - extent, center + extent);
    }

    // returns true if a numeric property should be treated as a bool
    private bool IsBoolProperty(Shader shader, int property_index)
    {
        string[] attributes = shader.GetPropertyAttributes(property_index);

        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].Contains("Toggle"))
            {
                return true;
            }
        }

        return false;
    }

    // shows the item context menu
    private void ShowContextMenu(Vector2 panel_position, string asset_path, bool is_shader, string shader_preview_name)
    {
        if (context_menu == null || string.IsNullOrWhiteSpace(asset_path))
        {
            return;
        }

        context_asset_path = asset_path;
        context_is_shader = is_shader;
        context_shader_preview_name = shader_preview_name;
        context_menu.style.left = panel_position.x;
        context_menu.style.top = panel_position.y;
        context_menu.RemoveFromClassList("hidden");
    }

    // hides the item context menu
    private void HideContextMenu()
    {
        if (context_menu == null)
        {
            return;
        }

        context_menu.AddToClassList("hidden");
    }

    // deletes the asset selected by the context menu
    private void DeleteContextAsset()
    {
#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(context_asset_path))
        {
            return;
        }

        if (context_is_shader)
        {
            DeleteShaderAsset(context_asset_path, context_shader_preview_name);
        }
        else
        {
            AssetDatabase.DeleteAsset(context_asset_path);
        }

        context_asset_path = string.Empty;
        context_shader_preview_name = string.Empty;
        HideContextMenu();
        AssetDatabase.Refresh();
        Refresh();
#endif
    }

#if UNITY_EDITOR
    // deletes a shader and its matching preview assets
    private void DeleteShaderAsset(string shader_path, string shader_preview_name)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shader_path);
        ShaderController.ClearActiveShader(shader);
        AssetDatabase.DeleteAsset(shader_path);

        string[] guids = AssetDatabase.FindAssets(shader_preview_name, new[] { ShaderPreviewFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string preview_path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string preview_folder = Path.GetDirectoryName(preview_path)?.Replace("\\", "/");

            if (preview_folder != ShaderPreviewFolder || Path.GetFileNameWithoutExtension(preview_path) != shader_preview_name)
            {
                continue;
            }

            AssetDatabase.DeleteAsset(preview_path);
        }
    }
#endif

    // creates the common item container and label
    private VisualElement CreateBaseItem(string item_name)
    {
        VisualElement item = new VisualElement();
        item.AddToClassList("item-card");

        Label label = new Label(item_name);
        label.AddToClassList("item-name");
        item.Add(label);

        return item;
    }
}

#if UNITY_EDITOR
public sealed class UiAssetPostprocessor : AssetPostprocessor
{
    // configures dropped image assets for browser preview use
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteFolder) && !assetPath.StartsWith(ShaderPreviewFolder) && !assetPath.StartsWith(ShaderDefaultFolder))
        {
            return;
        }

        TextureImporter importer = assetImporter as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
    }

    // refreshes open ui lists when project assets change
    private static void OnPostprocessAllAssets(string[] imported_assets, string[] deleted_assets, string[] moved_assets, string[] moved_from_asset_paths)
    {
        if (!TouchesBrowserFolder(imported_assets) && !TouchesBrowserFolder(deleted_assets) && !TouchesBrowserFolder(moved_assets) && !TouchesBrowserFolder(moved_from_asset_paths))
        {
            return;
        }

        EditorApplication.delayCall += UiBrowser.RefreshActiveBrowsers;
    }

    // returns true if an asset path belongs to the sprite or shader folders
    private static bool TouchesBrowserFolder(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i].StartsWith(SpriteFolder) || paths[i].StartsWith(ShaderFolder))
            {
                return true;
            }
        }

        return false;
    }

    private const string SpriteFolder = "Assets/Sprites";
    private const string ShaderFolder = "Assets/Shaders";
    private const string ShaderPreviewFolder = "Assets/Shaders/Preview";
    private const string ShaderDefaultFolder = "Assets/Shaders/Default";
}
#endif
