using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;
using CoreCard = Innovation.Core.Card;

namespace Innovation.Wpf;

/// <summary>
/// Top-of-pile tile. Same physical layout as <see cref="CardView"/>
/// (one icon in the upper-left corner, three along the bottom) but
/// no wrapped dogma text — hover reveals the full rules in the
/// detail panel. Used for the card showing on top of each color
/// pile on a player's board. `Size: N` and `View Stack` are drawn
/// by the host; this control is just the tile face.
/// </summary>
public partial class CardTileView : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(
            nameof(Card),
            typeof(CoreCard),
            typeof(CardTileView),
            new PropertyMetadata(null, OnCardChanged));

    /// <summary>The card at the top of the pile. Null shows a blank
    /// placeholder.</summary>
    public CoreCard? Card
    {
        get => (CoreCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CardTileView)d).RefreshCard();

    public CardTileView()
    {
        InitializeComponent();
        RefreshCard();
    }

    private void RefreshCard()
    {
        if (Card is null)
        {
            TileBorder.Background = Brushes.Gainsboro;
            TitleText.Text = "";
            AgeText.Text = "";
            DogmaIconHost.Children.Clear();
            ClearSlots();
            return;
        }

        TileBorder.Background = CardVisuals.BrushForCardColor(Card.Color);
        TitleText.Text = Card.Title;
        AgeText.Text = Card.Age.ToString();

        DogmaIconHost.Children.Clear();
        DogmaIconHost.Children.Add(CardVisuals.BuildIconTile(Card.DogmaIcon, 16));

        ClearSlots();
        PlaceIcon(TopSlot,    Card.Top,    IconSlot.Top,    26);
        PlaceIcon(LeftSlot,   Card.Left,   IconSlot.Left,   28);
        PlaceIcon(MiddleSlot, Card.Middle, IconSlot.Middle, 28);
        PlaceIcon(RightSlot,  Card.Right,  IconSlot.Right,  28);
    }

    /// <summary>Populate a slot host with an icon tile unless this
    /// slot is the card's hex position — the hex slot stays empty so
    /// the tile's card color shows through.</summary>
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
