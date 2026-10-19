using System;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>A passive game overlay with explicit header/grip input, independent of Mod Toolbar.</summary>
    internal sealed class HudWindow
    {
        private readonly float minimumWidth;
        private int control;
        private bool resizing;
        private Vector2 startMouse;
        private Rect startRect;
        private Rect frameRect;
        private Vector2 scroll;
        public bool OwnsInput => control != 0 && GUIUtility.hotControl == control;

        public HudWindow(float minimumWidth) { this.minimumWidth = minimumWidth; }
        public Rect Clamp(Rect rect)
        {
            var bounds = WindowGeometry.Clamp(rect.x, rect.y, rect.width, rect.height,
                Screen.width, Screen.height, minimumWidth);
            return new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        }
        public Rect TopRight(Rect rect)
        {
            var bounds = WindowGeometry.TopRight(rect.width, rect.height, Screen.width, Screen.height, minimumWidth);
            return new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        }
        public void Release()
        {
            if (OwnsInput) GUIUtility.hotControl = 0;
            control = 0;
        }
        public void Draw(ref Rect savedRect, string title, float background, float content,
            bool locked, bool allowStart, Action drawContent)
        {
            savedRect = Clamp(savedRect);
            if (Event.current.type == EventType.Layout) frameRect = savedRect;
            var ev = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var headerHeight = Styles.noWrapBold.fontSize + 14;
            var mouseFree = Cursor.visible && Cursor.lockState != CursorLockMode.Locked;
            if (locked || !mouseFree) Release();
            else if (ev.type == EventType.MouseDown && ev.button == 0 && GUIUtility.hotControl == 0 && allowStart)
            {
                var grip = new Rect(frameRect.xMax - 20, frameRect.yMax - 20, 20, 20);
                var header = new Rect(frameRect.x, frameRect.y, frameRect.width, headerHeight);
                if (grip.Contains(ev.mousePosition) || header.Contains(ev.mousePosition))
                {
                    resizing = grip.Contains(ev.mousePosition);
                    startRect = savedRect; startMouse = ev.mousePosition;
                    control = id; GUIUtility.hotControl = id; ev.Use();
                }
            }
            else if (OwnsInput)
            {
                if (ev.type == EventType.MouseDrag)
                {
                    var delta = ev.mousePosition - startMouse;
                    if (resizing)
                    {
                        var resized = WindowGeometry.Resize(new WindowBounds(startRect.x, startRect.y, startRect.width, startRect.height),
                            delta.x, delta.y, Screen.width, Screen.height, minimumWidth);
                        savedRect = new Rect((float)resized.X, (float)resized.Y, (float)resized.Width, (float)resized.Height);
                    }
                    else savedRect = Clamp(new Rect(startRect.position + delta, startRect.size));
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp && ev.button == 0)
                { Release(); Main.RequestSettingsSave(); ev.Use(); }
            }
            var savedColor = GUI.color;
            var savedEnabled = GUI.enabled;
            var eventType = ev.type;
            var suppressInput = !mouseFree && (ev.isMouse || ev.isKey || ev.type == EventType.ScrollWheel);
            try
            {
                GUI.color = new Color(0.025f, 0.035f, 0.045f, background);
                if (ev.type == EventType.Repaint) GUI.DrawTexture(frameRect, Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, content);
                GUI.Label(new Rect(frameRect.x + 10, frameRect.y + 4, frameRect.width - 20, headerHeight - 4),
                    title + (locked ? "  [locked]" : "  [drag header]"), Styles.noWrapBold);
                if (!locked) GUI.Label(new Rect(frameRect.xMax - 19, frameRect.yMax - 21, 19, 20), "↘", Styles.noWrapBold);
                // Preserve label brightness and return driving input unchanged after this window.
                if (suppressInput) ev.type = EventType.Ignore;
                var bodyHeight = Mathf.Max(1, frameRect.height - headerHeight - 16);
                GUILayout.BeginArea(new Rect(frameRect.x + 8, frameRect.y + headerHeight, Mathf.Max(1, frameRect.width - 16), bodyHeight));
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(Mathf.Max(1, frameRect.width - 16)), GUILayout.Height(bodyHeight));
                drawContent();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                if (suppressInput) ev.type = eventType;
                GUI.color = savedColor;
                GUI.enabled = savedEnabled;
            }
        }
    }
}
