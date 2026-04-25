using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;

namespace Innovation.Wpf;

/// <summary>
/// Modal dialog listing every card in a single color stack, top-first.
/// Invoked from the board's "View Stack" button or the "Size: N" label.
/// Each row is a <see cref="CardSummaryView"/> so the user can read ages,
/// colors, and icons without having to mentally decode the splay.
/// </summary>
public sealed class StackViewDialog : Window
{
    public StackViewDialog(string title, IReadOnlyList<Card> cardsTopFirst, Splay splay, System.Action<Card>? onHover = null)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock
        {
            Text = cardsTopFirst.Count == 0
                ? $"{title}  (empty)"
                : $"{title}  —  {cardsTopFirst.Count} card(s), splay: {splay}",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Top of stack",
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 0, 0, 4),
        });

        for (int i = 0; i < cardsTopFirst.Count; i++)
        {
            var row = new CardSummaryView
            {
                Card = cardsTopFirst[i],
                Width = 260,
                Margin = new Thickness(0, 0, 0, 3),
            };
            if (onHover is not null)
            {
                var captured = cardsTopFirst[i];
                row.MouseEnter += (_, _) => onHover(captured);
            }
            panel.Children.Add(row);
        }

        if (cardsTopFirst.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Bottom of stack",
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(16, 4, 16, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 640,
            Content = panel,
        };
    }
}
