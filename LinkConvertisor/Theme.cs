using System.Windows;
using System.Windows.Media;

namespace LinkConvertisor
{
    /// <summary>
    /// Applique le thème clair ou sombre en remplaçant les pinceaux de App.xaml.
    /// Toute l'interface utilise DynamicResource, donc le changement est instantané.
    /// </summary>
    public static class Theme
    {
        public static void Apply(bool dark)
        {
            var r = Application.Current.Resources;

            if (dark)
            {
                Set(r, "BgBrush", "#0B0B0E");
                Set(r, "SurfaceBrush", "#B316161B");
                Set(r, "Surface2Brush", "#1E1E24");
                Set(r, "Surface3Brush", "#28282F");
                Set(r, "StrokeBrush", "#2A2A31");
                Set(r, "StrokeHoverBrush", "#3A3A44");
                Set(r, "TextBrush", "#F4F4F6");
                Set(r, "MutedBrush", "#8E8E99");
                Set(r, "HoverBrush", "#1AFFFFFF");
                Set(r, "SubtleBrush", "#14FFFFFF");
                Set(r, "OutlineBrush", "#1FFFFFFF");
                Set(r, "ToastBrush", "#F51C1C22");
                Set(r, "OverlayBrush", "#B30B0B0E");
                r["AmbientOverlayBrush"] = Gradient("#800B0B0E", "#D90B0B0E", "#F70B0B0E");
            }
            else
            {
                Set(r, "BgBrush", "#F3F3F6");
                Set(r, "SurfaceBrush", "#CCFFFFFF");
                Set(r, "Surface2Brush", "#FFFFFF");
                Set(r, "Surface3Brush", "#EBEBF0");
                Set(r, "StrokeBrush", "#DEDEE5");
                Set(r, "StrokeHoverBrush", "#C9C9D2");
                Set(r, "TextBrush", "#121216");
                Set(r, "MutedBrush", "#6A6A75");
                Set(r, "HoverBrush", "#12000000");
                Set(r, "SubtleBrush", "#0D000000");
                Set(r, "OutlineBrush", "#14000000");
                Set(r, "ToastBrush", "#F7FFFFFF");
                Set(r, "OverlayBrush", "#B3F3F3F6");
                r["AmbientOverlayBrush"] = Gradient("#80F3F3F6", "#D9F3F3F6", "#F7F3F3F6");
            }
        }

        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        private static void Set(ResourceDictionary r, string key, string hex) => r[key] = new SolidColorBrush(C(hex));

        private static LinearGradientBrush Gradient(string top, string middle, string bottom)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            brush.GradientStops.Add(new GradientStop(C(top), 0));
            brush.GradientStops.Add(new GradientStop(C(middle), 0.55));
            brush.GradientStops.Add(new GradientStop(C(bottom), 1));
            return brush;
        }
    }
}