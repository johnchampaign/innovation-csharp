using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Innovation.Core;
using CoreIcon = Innovation.Core.Icon;

namespace Innovation.Wpf;

/// <summary>
/// Shared rendering primitives for every card-shaped control:
/// <see cref="CardView"/> (detailed), <see cref="CardTileView"/>
/// (top-of-pile), <see cref="CardSummaryView"/> (one-line bar).
///
/// <para>Icons are loaded from the bundled VB6 art under
/// <c>Resources\</c> — six gameplay icons (castle, crown, leaf,
/// lightbulb, factory, clock), each in a full size and a <c>_small</c>
/// variant. Every JPG is already pre-composited as a colored tile with
/// a white glyph, so displaying an icon is just an
/// <see cref="Image"/> pointing at the right pack URI. <see cref="BitmapImage"/>
/// instances are cached in a static dictionary so the app doesn't
/// re-decode the same file for every card.</para>
///
/// <para>Card-face colors and the dark body/label text brush are kept
/// as brushes because they're used for flat fills (backgrounds,
/// typography) rather than bitmaps.</para>
/// </summary>
internal static class CardVisuals
{
    // ---------- Palette ----------

    /// <summary>
    /// Medium-saturation card colors tuned to match the VB6 screenshot.
    /// Dark text reads cleanly on every one; that's the whole reason
    /// the saturations are pulled back from the Phase-6.10 set.
    /// </summary>
    public static SolidColorBrush BrushForCardColor(CardColor c) => c switch
    {
        CardColor.Red    => new SolidColorBrush(Color.FromRgb(0xE4, 0x82, 0x82)),
        CardColor.Yellow => new SolidColorBrush(Color.FromRgb(0xF1, 0xDB, 0x6F)),
        CardColor.Blue   => new SolidColorBrush(Color.FromRgb(0x8C, 0xB4, 0xDB)),
        CardColor.Green  => new SolidColorBrush(Color.FromRgb(0x8E, 0xC0, 0x85)),
        CardColor.Purple => new SolidColorBrush(Color.FromRgb(0xB6, 0x94, 0xC9)),
        _                => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
    };

    /// <summary>Standard dark text color used everywhere card content
    /// appears over a colored background or a white body.</summary>
    public static SolidColorBrush DarkText { get; } =
        new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));

    // ---------- Icon images ----------

    /// <summary>
    /// Bitmap-backed icon tile — an <see cref="Image"/> showing the
    /// pre-composited JPG for this icon. Below roughly 20 logical
    /// pixels the <c>_small</c> variant is used so small tiles stay
    /// crisp; above it the full-size art is used and WPF scales it
    /// down with its default high-quality filter.
    /// <para><see cref="CoreIcon.None"/> renders as an empty muted
    /// square so absent-slot hosts still visually take up space
    /// without dropping a tile on them.</para>
    /// </summary>
    public static FrameworkElement BuildIconTile(CoreIcon icon, double size)
    {
        if (icon == CoreIcon.None)
        {
            return new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                RadiusX = 2,
                RadiusY = 2,
            };
        }

        return new Image
        {
            Source = LoadIconBitmap(icon, useSmall: size <= 20),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            // Crisper on small tiles: we're not rotating and don't want
            // half-pixel blur from sub-pixel layout rounding.
            SnapsToDevicePixels = true,
        };
    }

    /// <summary>
    /// Solid black rectangle — left in the toolkit even though current
    /// card layouts simply leave the hex slot empty. Kept for parity
    /// with earlier phases in case a later view wants an explicit
    /// placeholder.
    /// </summary>
    public static FrameworkElement BuildHexTile(double size) => new Rectangle
    {
        Width = size,
        Height = size,
        Fill = Brushes.Black,
        RadiusX = 2,
        RadiusY = 2,
    };

    /// <summary>
    /// A small icon rendered in the footer of <see cref="CardSummaryView"/>
    /// (the one-line hand/score strip). Uses the <c>_small</c> JPG so
    /// the pre-composited colored tile reads cleanly next to the card
    /// color stripe, matching the VB6 reference UI.
    /// </summary>
    public static FrameworkElement BuildBareIcon(CoreIcon icon, double size)
    {
        if (icon == CoreIcon.None)
        {
            return new Rectangle
            {
                Width = size, Height = size,
                Fill = Brushes.Transparent,
            };
        }

        return new Image
        {
            Source = LoadIconBitmap(icon, useSmall: true),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
        };
    }

    // ---------- Bitmap cache ----------

    private static readonly Dictionary<(CoreIcon, bool), BitmapImage> s_cache = new();

    /// <summary>Resolves and caches the pack URI for an icon variant.</summary>
    private static BitmapImage LoadIconBitmap(CoreIcon icon, bool useSmall)
    {
        var key = (icon, useSmall);
        if (s_cache.TryGetValue(key, out var cached)) return cached;

        var file = IconFileName(icon) + (useSmall ? "_small" : "") + ".jpg";
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new System.Uri(
            "pack://application:,,,/Innovation.Wpf;component/Resources/" + file,
            System.UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();

        s_cache[key] = bmp;
        return bmp;
    }

    private static string IconFileName(CoreIcon icon) => icon switch
    {
        CoreIcon.Castle    => "castle",
        CoreIcon.Crown     => "crown",
        CoreIcon.Leaf      => "leaf",
        CoreIcon.Lightbulb => "lightbulb",
        CoreIcon.Factory   => "factory",
        CoreIcon.Clock     => "clock",
        _ => throw new System.ArgumentOutOfRangeException(
            nameof(icon), icon, "No bitmap art for this icon."),
    };

    // ---------- Special achievement art ----------

    /// <summary>
    /// Loads the crest bitmap for one of the five special achievements
    /// (<c>Monument / Empire / Wonder / World / Universe</c>). Used by
    /// the "Achievements Remaining" sidebar to show which specials are
    /// still available.
    /// </summary>
    public static BitmapImage LoadSpecialAchievementImage(string name)
    {
        var file = name.ToLowerInvariant() + "achievement.jpg";
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new System.Uri(
            "pack://application:,,,/Innovation.Wpf;component/Resources/" + file,
            System.UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
