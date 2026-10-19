using System;

namespace DvMod.HeadsUpDisplay
{
    public readonly struct WindowBounds
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public WindowBounds(double x, double y, double width, double height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    /// <summary>Bounds restored and resized windows, including resolution changes and bad saved values.</summary>
    public static class WindowGeometry
    {
        private static double Finite(double value, double fallback) =>
            double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;

        public static WindowBounds Clamp(double x, double y, double width, double height,
            double screenWidth, double screenHeight, double minimumWidth = 320, double minimumHeight = 120)
        {
            screenWidth = Math.Max(1, Finite(screenWidth, 1));
            screenHeight = Math.Max(1, Finite(screenHeight, 1));
            minimumWidth = Math.Min(screenWidth, Math.Max(1, Finite(minimumWidth, 320)));
            minimumHeight = Math.Min(screenHeight, Math.Max(1, Finite(minimumHeight, 120)));
            width = Math.Max(minimumWidth, Math.Min(screenWidth, Finite(width, minimumWidth)));
            height = Math.Max(minimumHeight, Math.Min(screenHeight, Finite(height, minimumHeight)));
            return new WindowBounds(Math.Max(0, Math.Min(screenWidth - width, Finite(x, 0))),
                Math.Max(0, Math.Min(screenHeight - height, Finite(y, 0))), width, height);
        }

        public static WindowBounds TopRight(double width, double height, double screenWidth, double screenHeight,
            double minimumWidth = 320)
        {
            var fitted = Clamp(0, 0, width, height, screenWidth, screenHeight, minimumWidth);
            return Clamp(screenWidth - fitted.Width - 10, 10, fitted.Width, fitted.Height,
                screenWidth, screenHeight, minimumWidth);
        }

        public static WindowBounds Resize(WindowBounds start, double dx, double dy, double screenWidth,
            double screenHeight, double minimumWidth = 320)
        {
            var fitted = Clamp(start.X, start.Y, start.Width, start.Height, screenWidth, screenHeight, minimumWidth);
            return Clamp(fitted.X, fitted.Y, Math.Min(screenWidth - fitted.X, fitted.Width + Finite(dx, 0)),
                Math.Min(screenHeight - fitted.Y, fitted.Height + Finite(dy, 0)), screenWidth, screenHeight, minimumWidth);
        }
    }
}
