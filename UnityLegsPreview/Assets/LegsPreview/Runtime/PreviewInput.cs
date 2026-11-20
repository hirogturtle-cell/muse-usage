using UnityEngine;
#if LEGSPREVIEW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace LegsPreview
{
    /// <summary>
    /// Tiny input shim so the preview works with the legacy Input Manager, the Input System
    /// package, or "Both" (Unity 6 defaults to the Input System).
    /// </summary>
    public static class PreviewInput
    {
#if LEGSPREVIEW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        public static bool OrbitHeld => Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
        public static bool PanHeld => Mouse.current != null && Mouse.current.middleButton.isPressed;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static bool OrbitHeld => Input.GetMouseButton(0) || Input.GetMouseButton(1);
        public static bool PanHeld => Input.GetMouseButton(2);
        public static Vector2 MouseDelta => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        public static float Scroll => Input.mouseScrollDelta.y;
        public static Vector2 MousePosition => Input.mousePosition;
#else
        public static bool OrbitHeld => false;
        public static bool PanHeld => false;
        public static Vector2 MouseDelta => Vector2.zero;
        public static float Scroll => 0f;
        public static Vector2 MousePosition => Vector2.zero;
#endif
    }
}
