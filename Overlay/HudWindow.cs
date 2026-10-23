using System;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>A passive game overlay with explicit header/grip input, independent of Mod Toolbar.</summary>
    internal sealed class HudWindow
    {
        private readonly float minimumWidth;
        private readonly float minimumHeight;
        private int control;
        private bool resizing;
        private Vector2 startMouse;
        private Rect startRect;
        private Rect frameRect;
        private Vector2 scroll;
        private float bodyHeight;
        public float VisibleContentTop => scroll.y;
        public float VisibleContentBottom => scroll.y + bodyHeight;
        public bool OwnsInput => control != 0 && GUIUtility.hotControl == control;

        public HudWindow(float minimumWidth, float minimumHeight = 120) { this.minimumWidth = minimumWidth; this.minimumHeight = minimumHeight; }
        public Rect Clamp(Rect rect)
        {
            var bounds = WindowGeometry.Clamp(rect.x, rect.y, rect.width, rect.height,
                Screen.width, Screen.height, minimumWidth, minimumHeight);
            return new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        }
        public Rect TopRight(Rect rect)
        {
            var bounds = WindowGeometry.TopRight(rect.width, rect.height, Screen.width, Screen.height, minimumWidth, minimumHeight);
            return new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        }
        public void Release()
        {
            if (OwnsInput) GUIUtility.hotControl = 0;
            control = 0;
        }
        /// <summary>
        /// Draws the window. Header actions are optional: a detached window carries none, so every
        /// attach and detach control stays together on the main window.
        /// </summary>
        public void Draw(ref Rect savedRect, string title, float background, float content,
            bool locked, bool allowStart, Action drawContent, string actionLabel = "",
            Action? headerAction = null, string secondActionLabel = "", Action? secondAction = null)
        {
            savedRect = Clamp(savedRect);
            if (Event.current.type == EventType.Layout) frameRect = savedRect;
            if (string.IsNullOrEmpty(actionLabel) && !string.IsNullOrEmpty(secondActionLabel))
            {
                actionLabel = secondActionLabel; headerAction = secondAction ?? headerAction; secondActionLabel = "";
            }
            var ev = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var pad = Styles.Padding;
            var headerHeight = Styles.noWrapBold.fontSize + pad * 2 + 4;
            var secondRow = !string.IsNullOrEmpty(secondActionLabel);
            var titleHeight = headerHeight;
            if (secondRow) headerHeight += titleHeight;
            // Widths follow the font, so a small font gives a small button rather than the
            // skin's default, and the title keeps whatever room is left.
            var fontSize = Styles.noWrapBold.fontSize;
            var actionWidth = string.IsNullOrEmpty(actionLabel) ? 0 : Mathf.Min(frameRect.width - fontSize * 3,
                Mathf.Max(fontSize * 3, Styles.ButtonWidth(actionLabel)));
            var actionRect = new Rect(frameRect.xMax - actionWidth - pad, frameRect.y + pad / 2 + 1,
                actionWidth, headerHeight - pad - 2);
            var secondRect = new Rect();
            if (secondRow)
            {
                // Two actions sit on their own row, each no wider than it needs, centred together.
                var gap = Mathf.Max(4, fontSize / 2);
                var widest = Mathf.Max(Styles.ButtonWidth(actionLabel), Styles.ButtonWidth(secondActionLabel));
                actionWidth = Mathf.Min(widest, Mathf.Max(fontSize * 3, (frameRect.width - gap - pad * 2) / 2));
                var left = frameRect.x + Mathf.Max(pad, (frameRect.width - (actionWidth * 2 + gap)) / 2);
                actionRect = new Rect(left, frameRect.y + titleHeight + 1, actionWidth, titleHeight - pad - 2);
                secondRect = new Rect(actionRect.xMax + gap, actionRect.y, actionWidth, actionRect.height);
            }
            var mouseFree = Cursor.visible && Cursor.lockState != CursorLockMode.Locked;
            if (locked || !mouseFree) Release();
            else if (ev.type == EventType.MouseDown && ev.button == 0 && GUIUtility.hotControl == 0 && allowStart)
            {
                var grip = new Rect(frameRect.xMax - 20, frameRect.yMax - 20, 20, 20);
                var header = new Rect(frameRect.x, frameRect.y, frameRect.width, headerHeight);
                if (grip.Contains(ev.mousePosition) || (header.Contains(ev.mousePosition)
                    && (actionWidth == 0 || !actionRect.Contains(ev.mousePosition)) && !secondRect.Contains(ev.mousePosition)))
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
                            delta.x, delta.y, Screen.width, Screen.height, minimumWidth, minimumHeight);
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
                GUI.Label(new Rect(frameRect.x + pad + 2, frameRect.y + pad,
                        Mathf.Max(1, frameRect.width - (secondRow ? pad * 2 + 4 : actionWidth + pad * 2 + 8)), titleHeight - pad),
                    title + (locked ? "  [locked]" : "  [drag header]"), Styles.noWrapBold);
                if (!locked) GUI.Label(new Rect(frameRect.xMax - 19, frameRect.yMax - 21, 19, 20), "↘", Styles.noWrapBold);
                // The action is outside the scroll view and the drag hit area. Keep it usable when geometry is locked.
                // Ignore driving input before drawing any interactive controls, then restore it for the game.
                if (suppressInput) ev.type = EventType.Ignore;
                GUI.enabled = savedEnabled && mouseFree && (allowStart || OwnsInput);
                if (!string.IsNullOrEmpty(actionLabel) && GUI.Button(actionRect, actionLabel, Styles.button))
                    headerAction?.Invoke();
                if (secondRow && GUI.Button(secondRect, secondActionLabel, Styles.button)) secondAction?.Invoke();
                GUI.enabled = savedEnabled;
                bodyHeight = Mathf.Max(1, frameRect.height - headerHeight - pad * 2);
                var bodyWidth = Mathf.Max(1, frameRect.width - pad * 2);
                GUILayout.BeginArea(new Rect(frameRect.x + pad, frameRect.y + headerHeight, bodyWidth, bodyHeight));
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(bodyWidth), GUILayout.Height(bodyHeight));
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
