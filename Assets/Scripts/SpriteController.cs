using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SpriteController : MonoBehaviour
{
    private const float HandleScreenSize = 18f;
    private const float MinimumScale = 0.12f;
    private const int SpriteSortingOrder = 10;
    private const int PreviewSortingOrder = 100;

    private static SpriteController instance;
    private static Texture2D white_texture;

    private readonly List<SpriteRenderer> spawned_sprites = new List<SpriteRenderer>();

    private Camera scene_camera;
    private SpriteRenderer preview_renderer;
    private SpriteRenderer selected_renderer;
    private Sprite pending_sprite;
    private Vector3 drag_offset;
    private Vector3 resize_anchor;
    private Vector3 resize_start_position;
    private Vector3 resize_start_scale;
    private bool is_placing;
    private bool is_dragging;
    private bool is_resizing;

    // starts dragging a sprite from the ui browser
    public static void BeginSpriteDrag(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        GetInstance().StartPlacement(sprite);
    }

    // initializes the singleton reference
    private void Awake()
    {
        instance = this;
        scene_camera = Camera.main;
    }

    // handles placement and selected sprite controls
    private void Update()
    {
        if (scene_camera == null)
        {
            scene_camera = Camera.main;
        }

        if (scene_camera == null || Mouse.current == null)
        {
            return;
        }

        HandleKeyboardDelete();

        Vector2 screen_position = Mouse.current.position.ReadValue();
        Vector3 world_position = GetWorldPosition(screen_position);

        UpdatePlacement(screen_position, world_position);
        UpdateSceneControls(screen_position, world_position);
    }

    // draws selected sprite controls after camera shaders
    private void OnGUI()
    {
        if (selected_renderer == null || scene_camera == null || !Application.isPlaying || UiBrowser.IsModalOpen())
        {
            return;
        }

        Rect selection_rect = GetSelectionGuiRect();
        DrawRectOutline(selection_rect, new Color(0.32f, 0.68f, 1f, 1f), 2f);
        GUI.color = new Color(0.9f, 0.18f, 0.18f, 1f);
        GUI.DrawTexture(GetDeleteGuiRect(selection_rect), GetWhiteTexture());
        GUI.color = Color.white;
        GUI.Label(GetDeleteGuiRect(selection_rect), "x", GetCenteredLabelStyle());
        GUI.color = new Color(0.22f, 0.58f, 0.94f, 1f);
        GUI.DrawTexture(GetResizeGuiRect(selection_rect), GetWhiteTexture());
        GUI.color = Color.white;
    }

    // creates or finds the runtime controller
    private static SpriteController GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<SpriteController>();

        if (instance != null)
        {
            return instance;
        }

        GameObject control_object = new GameObject("Sprite Controller");
        return control_object.AddComponent<SpriteController>();
    }

    // begins a placement preview
    private void StartPlacement(Sprite sprite)
    {
        pending_sprite = sprite;
        is_placing = true;
        is_dragging = false;
        is_resizing = false;
        EnsurePreviewRenderer();
        preview_renderer.sprite = sprite;
        preview_renderer.gameObject.SetActive(true);
        preview_renderer.color = new Color(1f, 1f, 1f, 0.55f);
        Select(null);
    }

    // updates the dragged placement preview and spawns on release
    private void UpdatePlacement(Vector2 screen_position, Vector3 world_position)
    {
        if (!is_placing)
        {
            return;
        }

        preview_renderer.transform.position = world_position;

        if (!Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        bool over_ui = UiBrowser.IsScreenPositionOverUi(screen_position);
        preview_renderer.gameObject.SetActive(false);
        is_placing = false;

        if (!over_ui)
        {
            Select(SpawnSprite(pending_sprite, world_position));
        }

        pending_sprite = null;
    }

    // handles selecting, moving, resizing, and deleting spawned sprites
    private void UpdateSceneControls(Vector2 screen_position, Vector3 world_position)
    {
        if (is_placing || UiBrowser.IsModalOpen() || UiBrowser.IsScreenPositionOverUi(screen_position))
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandlePress(screen_position, world_position);
        }

        if (Mouse.current.leftButton.isPressed)
        {
            if (is_dragging && selected_renderer != null)
            {
                selected_renderer.transform.position = world_position + drag_offset;
            }

            if (is_resizing && selected_renderer != null)
            {
                ResizeSelected(world_position);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            is_dragging = false;
            is_resizing = false;
        }
    }

    // deletes the selected sprite from the keyboard
    private void HandleKeyboardDelete()
    {
        if (selected_renderer == null || Keyboard.current == null || UiBrowser.IsKeyboardInputFocused())
        {
            return;
        }

        if (Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            DeleteSelected();
        }
    }

    // handles the first frame of a pointer press
    private void HandlePress(Vector2 screen_position, Vector3 world_position)
    {
        if (selected_renderer != null && IsInsideDeleteHandle(screen_position))
        {
            DeleteSelected();
            return;
        }

        if (selected_renderer != null && IsInsideResizeHandle(screen_position))
        {
            BeginResize(world_position);
            return;
        }

        SpriteRenderer hit_renderer = FindSpriteAt(world_position);
        Select(hit_renderer);

        if (selected_renderer != null)
        {
            is_dragging = true;
            drag_offset = selected_renderer.transform.position - world_position;
        }
    }

    // spawns a sprite renderer in the scene
    private SpriteRenderer SpawnSprite(Sprite sprite, Vector3 world_position)
    {
        GameObject sprite_object = new GameObject(sprite.name);
        SpriteRenderer sprite_renderer = sprite_object.AddComponent<SpriteRenderer>();
        sprite_renderer.sprite = sprite;
        sprite_renderer.sortingOrder = SpriteSortingOrder;
        sprite_object.transform.position = world_position;
        spawned_sprites.Add(sprite_renderer);
        return sprite_renderer;
    }

    // selects a spawned sprite
    private void Select(SpriteRenderer sprite_renderer)
    {
        selected_renderer = sprite_renderer;
    }

    // deletes the selected sprite
    private void DeleteSelected()
    {
        if (selected_renderer == null)
        {
            return;
        }

        SpriteRenderer renderer_to_delete = selected_renderer;
        spawned_sprites.Remove(renderer_to_delete);
        Select(null);
        Destroy(renderer_to_delete.gameObject);
    }

    // starts resizing the selected sprite
    private void BeginResize(Vector3 world_position)
    {
        is_resizing = true;
        is_dragging = false;
        Bounds bounds = selected_renderer.bounds;
        resize_anchor = bounds.min;
        resize_start_position = world_position;
        resize_start_scale = selected_renderer.transform.localScale;
    }

    // resizes the selected sprite from its bottom-left anchor
    private void ResizeSelected(Vector3 world_position)
    {
        Vector3 start_delta = resize_start_position - resize_anchor;
        Vector3 current_delta = world_position - resize_anchor;
        float start_size = Mathf.Max(Mathf.Max(Mathf.Abs(start_delta.x), Mathf.Abs(start_delta.y)), 0.001f);
        float current_size = Mathf.Max(Mathf.Max(Mathf.Abs(current_delta.x), Mathf.Abs(current_delta.y)), 0.001f);
        float scale = Mathf.Max(current_size / start_size, MinimumScale);
        selected_renderer.transform.localScale = resize_start_scale * scale;
        selected_renderer.transform.position += resize_anchor - selected_renderer.bounds.min;
    }

    // finds the topmost spawned sprite under a world position
    private SpriteRenderer FindSpriteAt(Vector3 world_position)
    {
        SpriteRenderer best_renderer = null;
        int best_order = int.MinValue;

        for (int i = spawned_sprites.Count - 1; i >= 0; i--)
        {
            SpriteRenderer sprite_renderer = spawned_sprites[i];

            if (sprite_renderer == null)
            {
                spawned_sprites.RemoveAt(i);
                continue;
            }

            if (!sprite_renderer.bounds.Contains(world_position) || sprite_renderer.sortingOrder < best_order)
            {
                continue;
            }

            best_renderer = sprite_renderer;
            best_order = sprite_renderer.sortingOrder;
        }

        return best_renderer;
    }

    // returns true if a screen position hits the delete button
    private bool IsInsideDeleteHandle(Vector2 screen_position)
    {
        return GetDeleteGuiRect(GetSelectionGuiRect()).Contains(ToGuiPosition(screen_position));
    }

    // returns true if a screen position hits the resize button
    private bool IsInsideResizeHandle(Vector2 screen_position)
    {
        return GetResizeGuiRect(GetSelectionGuiRect()).Contains(ToGuiPosition(screen_position));
    }

    // ensures the placement preview object exists
    private void EnsurePreviewRenderer()
    {
        if (preview_renderer != null)
        {
            return;
        }

        GameObject preview_object = new GameObject("Sprite Placement Preview");
        preview_renderer = preview_object.AddComponent<SpriteRenderer>();
        preview_renderer.sortingOrder = PreviewSortingOrder;
    }

    // converts a screen position to scene world position
    private Vector3 GetWorldPosition(Vector2 screen_position)
    {
        Vector3 world_position = scene_camera.ScreenToWorldPoint(new Vector3(screen_position.x, screen_position.y, -scene_camera.transform.position.z));
        world_position.z = 0f;
        return world_position;
    }

    // converts a screen position to imgui coordinates
    private Vector2 ToGuiPosition(Vector2 screen_position)
    {
        return new Vector2(screen_position.x, Screen.height - screen_position.y);
    }

    // returns the selected sprite bounds in imgui coordinates
    private Rect GetSelectionGuiRect()
    {
        Bounds bounds = selected_renderer.bounds;
        Vector3 min = scene_camera.WorldToScreenPoint(bounds.min);
        Vector3 max = scene_camera.WorldToScreenPoint(bounds.max);
        float left = Mathf.Min(min.x, max.x);
        float right = Mathf.Max(min.x, max.x);
        float top = Screen.height - Mathf.Max(min.y, max.y);
        float bottom = Screen.height - Mathf.Min(min.y, max.y);
        return Rect.MinMaxRect(left, top, right, bottom);
    }

    // returns the delete button rect in imgui coordinates
    private Rect GetDeleteGuiRect(Rect selection_rect)
    {
        return new Rect(selection_rect.xMax - HandleScreenSize * 0.5f, selection_rect.yMin - HandleScreenSize * 0.5f, HandleScreenSize, HandleScreenSize);
    }

    // returns the resize button rect in imgui coordinates
    private Rect GetResizeGuiRect(Rect selection_rect)
    {
        return new Rect(selection_rect.xMax - HandleScreenSize * 0.5f, selection_rect.yMax - HandleScreenSize * 0.5f, HandleScreenSize, HandleScreenSize);
    }

    // draws a screen-space rectangle outline
    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), GetWhiteTexture());
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), GetWhiteTexture());
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), GetWhiteTexture());
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), GetWhiteTexture());
        GUI.color = Color.white;
    }

    // creates a shared white texture for imgui drawing
    private static Texture2D GetWhiteTexture()
    {
        if (white_texture != null)
        {
            return white_texture;
        }

        white_texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        white_texture.SetPixel(0, 0, Color.white);
        white_texture.Apply();
        return white_texture;
    }

    // returns the centered handle label style
    private static GUIStyle GetCenteredLabelStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        return style;
    }
}
