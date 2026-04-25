using System.Drawing.Drawing2D;
using Innovation.Core;
using CardIcon = Innovation.Core.Icon;

namespace Innovation.WinForms.Cards;

/// <summary>
/// Dynamic, asset-free card renderer. Mirrors the isotropic web
/// renderer and the physical Innovation cards: a colored header
/// strip with the age badge and title on top, a white dogma body
/// in the middle, and a colored icon strip at the <b>bottom</b>
/// carrying the four icon slots.
///
/// <para>The bottom-edge icon placement is deliberate: it matches
/// the splay mechanic — when a pile is splayed "up", only the
/// bottom strip of covered cards is visible, which is exactly
/// where the icons need to be so they still count for totals.</para>
///
/// <para>Design notes:</para>
/// <list type="bullet">
/// <item>The hex slot is drawn as a <b>solid black hexagon</b> to
/// match the original art — it reads as "heavy" / special, which
/// is fitting since that slot doesn't count toward icon totals.</item>
/// <item>The age badge is a small black square with a white digit,
/// pinned to the top-left of the colored header strip.</item>
/// <item>Region sizes scale with bounds.Height so the same
/// renderer serves 300px hero cards <i>and</i> 78px pile-peek
/// tiles. Below ~70px tall the dogma body collapses and only the
/// header + icon strip show; below ~40px only the header.</item>
/// <item>The card face fills <i>all</i> of bounds, and the colored
/// border stroke is inset by half the pen width so it lands fully
/// inside bounds — GDI+ otherwise centers strokes on the path,
/// which clips the outer half when the path hugs the clip rect.</item>
/// </list>
/// Icon glyphs (castle / crown / leaf / lightbulb / factory / clock)
/// are simple vector shapes — if we later rasterize the publisher
/// EPS set, <see cref="DrawIconGlyph"/> is the single seam to swap.
/// </summary>
public static class CardRenderer
{
    private const int BorderWidth = 2;

    public static void DrawCard(Graphics g, Rectangle bounds, Card card)
    {
        if (bounds.Width <= 4 || bounds.Height <= 4) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var color = ColorForCard(card.Color);

        // Fill the full rect with white — this is the card face
        // underneath the colored header / body / icon strip.
        using (var face = new SolidBrush(Color.White))
            g.FillRectangle(face, bounds);

        // Border: inset by half the pen width so the 2px stroke is
        // fully contained in bounds (otherwise the outer 1px gets
        // clipped against the control's bounding rect, which reads
        // as "edge is cut off").
        float halfPen = BorderWidth / 2f;
        var borderRect = new RectangleF(
            bounds.X + halfPen,
            bounds.Y + halfPen,
            bounds.Width - BorderWidth,
            bounds.Height - BorderWidth);
        using (var border = new Pen(color, BorderWidth))
            g.DrawRectangle(border, borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height);

        // Interior = everything inside the border.
        var inner = new Rectangle(
            bounds.X + BorderWidth,
            bounds.Y + BorderWidth,
            bounds.Width - 2 * BorderWidth,
            bounds.Height - 2 * BorderWidth);
        if (inner.Width <= 4 || inner.Height <= 4) return;

        // Compute region heights.
        //   Header (top, colored):   ~26% of inner, clamp 22..48
        //   Icon strip (bottom, colored): ~24% of inner, clamp 20..40
        //   Dogma body (middle, white):   remainder
        // If the card is too short for a meaningful dogma body, the
        // body collapses and only the two colored strips show.
        int headerH = Math.Max(22, Math.Min(48, (int)Math.Round(inner.Height * 0.26)));
        int iconsH  = Math.Max(20, Math.Min(40, (int)Math.Round(inner.Height * 0.24)));
        int bodyH   = inner.Height - headerH - iconsH;
        // Below ~30px of body height the dogma text becomes an
        // unreadable smear; drop it entirely and split the remaining
        // space between header and icons. This is the "VB6 mini-tile"
        // regime — pile tops and hand cards land here.
        if (bodyH < 30)
        {
            bodyH = 0;
            int remaining = inner.Height;
            headerH = remaining * 55 / 100;
            iconsH  = remaining - headerH;
        }
        if (inner.Height < 32)
        {
            // Extremely small — just a colored block. Header takes all.
            headerH = inner.Height;
            iconsH = 0;
            bodyH = 0;
        }

        var headerR = new Rectangle(inner.X, inner.Y, inner.Width, headerH);
        var bodyR   = new Rectangle(inner.X, headerR.Bottom, inner.Width, bodyH);
        var iconsR  = new Rectangle(inner.X, bodyR.Bottom, inner.Width, iconsH);

        DrawHeader(g, headerR, card, color);
        if (bodyR.Height  > 0) DrawDogmaBody(g, bodyR, card);
        if (iconsR.Height > 0) DrawIconStrip(g, iconsR, card, color);
    }

    // ---------- header ----------

    /// <summary>
    /// Colored top strip: black age-square on the left, white bold
    /// title filling the rest.
    /// </summary>
    private static void DrawHeader(Graphics g, Rectangle r, Card card, Color cardColor)
    {
        using (var bg = new SolidBrush(cardColor))
            g.FillRectangle(bg, r);

        // Age badge — black square, pinned top-left with small inset.
        int ageD = Math.Max(14, Math.Min(r.Height - 6, 26));
        var ageR = new Rectangle(r.X + 4, r.Y + (r.Height - ageD) / 2, ageD, ageD);
        DrawAgeBadge(g, ageR, card.Age);

        // Title fills the remainder. Shrink-to-fit so long names
        // ("Software Engineering", "Evolutionary Theory") still land
        // on a single line even on narrow pile tiles.
        int titleLeft  = ageR.Right + 6;
        int titleRight = r.Right - 6;
        if (titleRight - titleLeft > 20)
        {
            var titleR = new Rectangle(titleLeft, r.Y, titleRight - titleLeft, r.Height);
            DrawTitle(g, titleR, card.Title);
        }
    }

    private static void DrawTitle(Graphics g, Rectangle rect, string title)
    {
        if (rect.Width <= 4 || rect.Height <= 4) return;
        float lo = 6f, hi = Math.Min(rect.Height * 0.6f, 15f);
        Font? best = null;
        try
        {
            while (hi - lo > 0.25f)
            {
                float mid = (lo + hi) / 2;
                using var probe = new Font("Segoe UI", mid, FontStyle.Bold);
                var sz = g.MeasureString(title, probe);
                if (sz.Width <= rect.Width && sz.Height <= rect.Height) lo = mid;
                else hi = mid;
            }
            best = new Font("Segoe UI", lo, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            g.DrawString(title, best, brush, rect, sf);
        }
        finally { best?.Dispose(); }
    }

    private static void DrawAgeBadge(Graphics g, Rectangle box, int age)
    {
        using (var blk = new SolidBrush(Color.Black))
            g.FillRectangle(blk, box);
        using var font = new Font("Segoe UI", box.Height * 0.6f, FontStyle.Bold);
        using var white = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(age.ToString(), font, white, box, sf);
    }

    // ---------- body ----------

    /// <summary>
    /// White dogma panel with one row per effect. Each row has a
    /// small dogma-icon disc on the left and the effect text wrapped
    /// to the right. The font is shrink-to-fit so dense 2–3-effect
    /// cards still render without truncation at hero size.
    /// </summary>
    private static void DrawDogmaBody(Graphics g, Rectangle r, Card card)
    {
        // The dogma body stays white; no colored background so the
        // text reads at high contrast.
        if (card.DogmaEffects.Count == 0) return;

        int lineGap  = 4;
        int iconSize = Math.Max(12, Math.Min(22, r.Height / Math.Max(2, card.DogmaEffects.Count + 1)));
        int padL     = 6;
        int padR     = 6;
        int padT     = 4;
        int padB     = 4;

        int textLeft  = r.X + padL + iconSize + 6;
        int textRight = r.Right - padR;
        int textWidth = textRight - textLeft;
        int availH    = r.Height - padT - padB;
        if (textWidth < 40 || availH < iconSize) return;

        var fmt = new StringFormat
        {
            Trimming = StringTrimming.EllipsisWord,
            FormatFlags = StringFormatFlags.LineLimit,
        };

        // Binary-search for the largest font at which every effect
        // fits, accounting for icon-height row minimum so short
        // one-line effects don't clip their icon disc.
        float lo = 7f, hi = 11f;
        while (hi - lo > 0.25f)
        {
            float mid = (lo + hi) / 2;
            using var probe = new Font("Segoe UI", mid, FontStyle.Regular);
            float total = 0;
            foreach (var effect in card.DogmaEffects)
            {
                var sz = g.MeasureString(effect, probe, textWidth, fmt);
                total += Math.Max(sz.Height, iconSize) + lineGap;
            }
            total -= lineGap;
            if (total <= availH) lo = mid;
            else hi = mid;
        }
        using var font = new Font("Segoe UI", lo, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.Black);

        float y = r.Y + padT;
        foreach (var effect in card.DogmaEffects)
        {
            var sz = g.MeasureString(effect, font, textWidth, fmt);
            float rowH = Math.Max(sz.Height, iconSize);
            int iconY = (int)(y + Math.Min(sz.Height, iconSize * 1.5f) / 2 - iconSize / 2);
            var iconBox = new Rectangle(r.X + padL, iconY, iconSize, iconSize);
            DrawIconGlyph(g, iconBox, card.DogmaIcon);

            var textR = new RectangleF(textLeft, y, textWidth, rowH + 1);
            g.DrawString(effect, font, textBrush, textR, fmt);

            y += rowH + lineGap;
            if (y > r.Bottom - padB) break;
        }
    }

    // ---------- icon strip (bottom) ----------

    /// <summary>
    /// The bottom colored strip carrying the four icon slots, in the
    /// order Top, Left, Middle, Right (which is how the data model
    /// indexes them — the names refer to positions on a splayed pile,
    /// not on-card geometry). The slot matching
    /// <see cref="Card.HexagonSlot"/> is a solid black hexagon; the
    /// other three are colored icon discs with white glyphs.
    /// </summary>
    private static void DrawIconStrip(Graphics g, Rectangle r, Card card, Color cardColor)
    {
        using (var bg = new SolidBrush(cardColor))
            g.FillRectangle(bg, r);

        int iconSize = Math.Max(10, Math.Min(r.Height - 4, 30));
        int slotW = r.Width / 4;
        // Cap icon size against slot width too — on narrow tiles we
        // don't want the icons to overlap each other.
        iconSize = Math.Min(iconSize, slotW - 4);
        if (iconSize < 8) return;

        var slots = new (CardIcon icon, IconSlot slot)[]
        {
            (card.Top,    IconSlot.Top),
            (card.Left,   IconSlot.Left),
            (card.Middle, IconSlot.Middle),
            (card.Right,  IconSlot.Right),
        };
        for (int i = 0; i < 4; i++)
        {
            int cx = r.X + slotW * i + slotW / 2;
            int cy = r.Y + r.Height / 2;
            var box = new Rectangle(cx - iconSize / 2, cy - iconSize / 2, iconSize, iconSize);
            var (icon, slot) = slots[i];
            if (slot == card.HexagonSlot) DrawHexagonSlot(g, box);
            else DrawIconGlyph(g, box, icon);
        }
    }

    // ---------- icon glyphs ----------

    /// <summary>
    /// Paint a single icon into <paramref name="box"/> as a colored
    /// disc with a white glyph on top. Public so the icon-compare
    /// row reuses the same drawing. For <see cref="CardIcon.None"/>
    /// draws a thin dashed outline.
    /// </summary>
    public static void DrawIconGlyph(Graphics g, Rectangle box, CardIcon icon)
    {
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            if (icon == CardIcon.None)
            {
                using var dash = new Pen(Color.Silver, 1f);
                g.DrawEllipse(dash, box);
                return;
            }

            var iconBg = BackgroundForIcon(icon);
            using (var bg = new SolidBrush(iconBg))
                g.FillEllipse(bg, box);
            using (var outline = new Pen(Color.White, 1.2f))
                g.DrawEllipse(outline, box);

            var inset = Rectangle.Inflate(box, -(int)(box.Width * 0.22), -(int)(box.Height * 0.22));
            using var white = new SolidBrush(Color.White);
            using var whitePen = new Pen(Color.White, Math.Max(1f, box.Width / 14f));
            switch (icon)
            {
                case CardIcon.Castle:    DrawCastleGlyph(g, inset, white); break;
                case CardIcon.Crown:     DrawCrownGlyph(g, inset, white); break;
                case CardIcon.Leaf:      DrawLeafGlyph(g, inset, white); break;
                case CardIcon.Lightbulb: DrawBulbGlyph(g, inset, white, whitePen); break;
                case CardIcon.Factory:   DrawFactoryGlyph(g, inset, white); break;
                case CardIcon.Clock:     DrawClockGlyph(g, inset, white, whitePen); break;
            }
        }
        finally { g.SmoothingMode = prev; }
    }

    private static void DrawCastleGlyph(Graphics g, Rectangle r, Brush fill)
    {
        int battH = r.Height / 4;
        int wallH = r.Height - battH;
        g.FillRectangle(fill, r.X, r.Y + battH, r.Width, wallH);
        int bw = r.Width / 5;
        int y = r.Y;
        g.FillRectangle(fill, r.X, y, bw, battH);
        g.FillRectangle(fill, r.X + 2 * bw, y, bw, battH);
        g.FillRectangle(fill, r.X + 4 * bw, y, bw, battH);
    }

    private static void DrawCrownGlyph(Graphics g, Rectangle r, Brush fill)
    {
        int baseH = r.Height / 3;
        g.FillRectangle(fill, r.X, r.Bottom - baseH, r.Width, baseH);
        var peaks = new[]
        {
            new Point(r.X, r.Bottom - baseH),
            new Point(r.X + r.Width / 6, r.Y + r.Height / 6),
            new Point(r.X + r.Width / 3, r.Bottom - baseH),
            new Point(r.X + r.Width / 2, r.Y),
            new Point(r.X + 2 * r.Width / 3, r.Bottom - baseH),
            new Point(r.X + 5 * r.Width / 6, r.Y + r.Height / 6),
            new Point(r.Right, r.Bottom - baseH),
        };
        g.FillPolygon(fill, peaks);
    }

    private static void DrawLeafGlyph(Graphics g, Rectangle r, Brush fill)
    {
        var state = g.Save();
        g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
        g.RotateTransform(-30);
        var ell = new Rectangle(-r.Width / 2, -r.Height / 3, r.Width, (int)(r.Height * 0.66));
        g.FillEllipse(fill, ell);
        g.Restore(state);
    }

    private static void DrawBulbGlyph(Graphics g, Rectangle r, Brush fill, Pen stroke)
    {
        int d = (int)(Math.Min(r.Width, r.Height) * 0.7);
        int cx = r.X + r.Width / 2;
        int bulbY = r.Y;
        var bulb = new Rectangle(cx - d / 2, bulbY, d, d);
        g.FillEllipse(fill, bulb);
        int capW = (int)(d * 0.5);
        int capH = (int)(d * 0.25);
        g.FillRectangle(fill, cx - capW / 2, bulb.Bottom - 1, capW, capH);
    }

    private static void DrawFactoryGlyph(Graphics g, Rectangle r, Brush fill)
    {
        int stackW = r.Width / 5;
        int stackH = (int)(r.Height * 0.85);
        g.FillRectangle(fill, r.X, r.Bottom - stackH, stackW, stackH);
        int bodyX = r.X + stackW + 2;
        int bodyY = r.Y + r.Height / 2;
        int bodyW = r.Right - bodyX;
        int bodyH = r.Bottom - bodyY;
        g.FillRectangle(fill, bodyX, bodyY, bodyW, bodyH);
        int step = bodyW / 3;
        g.FillRectangle(fill, bodyX + step, bodyY - step / 2, step, step / 2);
    }

    private static void DrawClockGlyph(Graphics g, Rectangle r, Brush fill, Pen stroke)
    {
        int d = Math.Min(r.Width, r.Height);
        var face = new Rectangle(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
        g.FillEllipse(fill, face);
        int cx = face.X + face.Width / 2;
        int cy = face.Y + face.Height / 2;
        using var handPen = new Pen(ControlPaint.Dark(((SolidBrush)fill).Color, 0.7f), Math.Max(1.5f, d / 12f));
        g.DrawLine(handPen, cx, cy, cx, cy - d / 3);
        g.DrawLine(handPen, cx, cy, cx + d / 4, cy);
    }

    /// <summary>
    /// Hex / bonus slot: a solid black hexagon. Drawn against the
    /// colored icon strip, so the black stands out on every card
    /// color including the dark blue and purple.
    /// </summary>
    private static void DrawHexagonSlot(Graphics g, Rectangle box)
    {
        int cx = box.X + box.Width / 2;
        int cy = box.Y + box.Height / 2;
        double rad = Math.Min(box.Width, box.Height) / 2.0 - 1;
        var pts = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double a = Math.PI / 3 * i + Math.PI / 6;
            pts[i] = new PointF((float)(cx + rad * Math.Cos(a)), (float)(cy + rad * Math.Sin(a)));
        }
        using var fill = new SolidBrush(Color.Black);
        g.FillPolygon(fill, pts);
    }

    // ---------- palette ----------

    /// <summary>
    /// Saturated card-color palette tuned to match the isotropic
    /// renderer's vivid look.
    /// </summary>
    public static Color ColorForCard(CardColor c) => c switch
    {
        CardColor.Red    => Color.FromArgb(0xC8, 0x1E, 0x24),
        CardColor.Yellow => Color.FromArgb(0xE8, 0xB6, 0x2C),
        CardColor.Blue   => Color.FromArgb(0x15, 0x6B, 0xC6),
        CardColor.Green  => Color.FromArgb(0x1F, 0x87, 0x35),
        CardColor.Purple => Color.FromArgb(0x70, 0x27, 0x9F),
        _                => Color.DimGray,
    };

    public static Color BackgroundForIcon(CardIcon i) => i switch
    {
        CardIcon.Castle    => Color.FromArgb(0x8B, 0x4A, 0x1D),
        CardIcon.Crown     => Color.FromArgb(0xE0, 0xA3, 0x1B),
        CardIcon.Leaf      => Color.FromArgb(0x2E, 0x8B, 0x3F),
        CardIcon.Lightbulb => Color.FromArgb(0xF0, 0x7B, 0x10),
        CardIcon.Factory   => Color.FromArgb(0x55, 0x55, 0x55),
        CardIcon.Clock     => Color.FromArgb(0x31, 0x6B, 0xA2),
        _                  => Color.Silver,
    };
}
