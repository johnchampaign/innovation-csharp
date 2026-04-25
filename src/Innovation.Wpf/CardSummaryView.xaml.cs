using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;
using CoreCard = Innovation.Core.Card;

namespace Innovation.Wpf;

/// <summary>
/// One-line "<i>age</i>-<i>title</i>" summary of a card. VB6 idiom:
/// the whole strip is the card's color, the text is a single dark
/// phrase like "1-Agriculture", and a small bare icon sits on the
/// right. Used in the player's hand, score pile, opponent stacks,
/// and covered-card lists.
/// </summary>
public partial class CardSummaryView : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(
            nameof(Card),
            typeof(CoreCard),
            typeof(CardSummaryView),
            new PropertyMetadata(null, OnCardChanged));

    /// <summary>The card to summarize. Null shows a blank placeholder.</summary>
    public CoreCard? Card
    {
        get => (CoreCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CardSummaryView)d).RefreshCard();

    public CardSummaryView()
    {
        InitializeComponent();
        RefreshCard();
    }

    private void RefreshCard()
    {
        if (Card is null)
        {
            Root.Background = Brushes.Gainsboro;
            LabelText.Text = "";
            IconHost.Children.Clear();
            return;
        }

        Root.Background = CardVisuals.BrushForCardColor(Card.Color);
        LabelText.Text = $"{Card.Age}-{Card.Title}";

        IconHost.Children.Clear();
        IconHost.Children.Add(CardVisuals.BuildBareIcon(Card.DogmaIcon, 16));
    }
}
