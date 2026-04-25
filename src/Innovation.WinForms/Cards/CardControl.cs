using System.ComponentModel;
using Innovation.Core;

namespace Innovation.WinForms.Cards;

/// <summary>
/// A WinForms control that dynamically paints an Innovation card via
/// <see cref="CardRenderer"/>. Set <see cref="Card"/> to the card to
/// display; the control invalidates automatically. Click events
/// bubble up like any other control, so callers can use this as a
/// drop-in replacement for the old card-tile buttons.
///
/// <para>Defaults: double-buffered (no flicker on resize), redraw on
/// resize (the renderer is layout-aware), hand cursor (signals the
/// tile is clickable to jump the card viewer).</para>
/// </summary>
public sealed class CardControl : Control
{
    private Card? _card;

    // The Designer complains (WFO1000) if a non-serializable property
    // is left browsable; this control is runtime-only and never lives
    // in a .Designer.cs file, so opt out of serialization entirely.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Card? Card
    {
        get => _card;
        set
        {
            if (ReferenceEquals(_card, value)) return;
            _card = value;
            Invalidate();
        }
    }

    public CardControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        // Don't set BackColor = Transparent: the base Control class
        // throws ArgumentException ("Control does not support
        // transparent background colors.") unless you also flip the
        // SupportsTransparentBackColor style. We don't need it anyway
        // because CardRenderer fills the whole rectangle with the
        // white card face before any other drawing — BackColor is
        // never actually visible.
        Cursor = Cursors.Hand;
        // A little minimum size so the renderer has something to draw
        // even if the parent layout hasn't settled yet.
        MinimumSize = new Size(60, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_card is null) return;
        // Pass the full client rectangle. CardRenderer insets its own
        // border by half the pen width so the stroke stays inside
        // bounds — previously we were passing (W-1, H-1) which made
        // the bottom/right edges get clipped.
        CardRenderer.DrawCard(e.Graphics, ClientRectangle, _card);
    }
}
