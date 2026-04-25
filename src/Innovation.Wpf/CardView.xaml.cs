using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;
using CoreCard = Innovation.Core.Card;

namespace Innovation.Wpf;

/// <summary>
/// The <b>detailed</b> card view — mirrors the physical Innovation
/// card face: one icon slot in the upper-left corner of the header
/// and three along the bottom. Whichever of the four slots matches
/// <see cref="CoreCard.HexagonSlot"/> renders empty (that position
/// holds the thematic image on the print card, which has no
/// gameplay effect, so in digital form it's just blank card color).
///
/// <para>Companions: <see cref="CardTileView"/> (top-of-pile, same
/// face without wrapped dogma text) and
/// <see cref="CardSummaryView"/> (one-line strip).</para>
/// </summary>
public partial class CardView : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(
            nameof(Card),
            typeof(CoreCard),
            typeof(CardView),
            new PropertyMetadata(null, OnCardChanged));

    /// <summary>The card to display. Null shows a placeholder.</summary>
    public CoreCard? Card
    {
        get => (CoreCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CardView)d).RefreshCard();

    public CardView()
    {
        InitializeComponent();
        RefreshCard();
    }

    private void RefreshCard()
    {
        if (Card is null)
        {
            HeaderGrid.Background = Brushes.Gainsboro;
            FooterGrid.Background = Brushes.Gainsboro;
            TitleText.Text = "";
            DogmaIconHost.Children.Clear();
            EffectsPanel.Children.Clear();
            ClearSlots();
            return;
        }

        var cardBrush = CardVisuals.BrushForCardColor(Card.Color);
        HeaderGrid.Background = cardBrush;
        FooterGrid.Background = cardBrush;

        // VB6 idiom: title + age read as one phrase ("Oars - Age 1",
        // "Agriculture - Age 1"), not a separate age badge.
        TitleText.Text = $"{Card.Title} - Age {Card.Age}";

        DogmaIconHost.Children.Clear();
        DogmaIconHost.Children.Add(CardVisuals.BuildIconTile(Card.DogmaIcon, 20));

        EffectsPanel.Children.Clear();
        for (int i = 0; i < Card.DogmaEffects.Count; i++)
        {
            EffectsPanel.Children.Add(new TextBlock
            {
                Text = Card.DogmaEffects[i],
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = CardVisuals.DarkText,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 15,
                Margin = new Thickness(0, i == 0 ? 0 : 5, 0, 0),
            });
        }

        // Four slots, one of which is always the hex position and
        // therefore empty. Top is in the header; Left/Middle/Right
        // are in the footer.
        ClearSlots();
        PlaceIcon(TopSlot,    Card.Top,    IconSlot.Top,    34);
        PlaceIcon(LeftSlot,   Card.Left,   IconSlot.Left,   34);
        PlaceIcon(MiddleSlot, Card.Middle, IconSlot.Middle, 34);
        PlaceIcon(RightSlot,  Card.Right,  IconSlot.Right,  34);
    }

    /// <summary>Populate a slot host with an icon tile — unless the
    /// slot is the card's hex position, in which case we leave it
    /// empty so the card color shows through.</summary>
    private void PlaceIcon(Grid host, Icon icon, IconSlot slot, double size)
    {
        if (Card!.HexagonSlot == slot) return;

        var tile = CardVisuals.BuildIconTile(icon, size);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        tile.VerticalAlignment = VerticalAlignment.Center;
        host.Children.Add(tile);
    }

    private void ClearSlots()
    {
        TopSlot.Children.Clear();
        LeftSlot.Children.Clear();
        MiddleSlot.Children.Clear();
        RightSlot.Children.Clear();
    }
}
