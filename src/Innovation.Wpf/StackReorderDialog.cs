using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Innovation.Core;

namespace Innovation.Wpf;

/// <summary>
/// Modal dialog for Publications-style stack reorder. Lists the stack
/// top-to-bottom; the user clicks a row to select it, then "Move Up" /
/// "Move Down" to shuffle. OK commits the new order; Cancel returns the
/// stack unchanged. Compact (no drag-and-drop) but functional.
/// </summary>
public sealed class StackReorderDialog : Window
{
    private readonly ObservableCollection<RowItem> _rows = new();
    private readonly ListBox _list;

    /// <summary>The accepted new order (top-first), or null if cancelled.</summary>
    public IReadOnlyList<int>? Result { get; private set; }

    public StackReorderDialog(string title, IReadOnlyList<Card> cardsTopFirst, Splay splay)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(new TextBlock
        {
            Text = $"{title}  —  {cardsTopFirst.Count} card(s), splay: {splay}",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6),
        });
        root.Children.Add(new TextBlock
        {
            Text = "Top of stack",
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 0, 0, 4),
        });

        foreach (var card in cardsTopFirst)
            _rows.Add(new RowItem(card));

        _list = new ListBox
        {
            ItemsSource = _rows,
            ItemTemplate = BuildItemTemplate(),
            Width = 320,
            MaxHeight = 480,
            SelectedIndex = _rows.Count > 0 ? 0 : -1,
        };
        root.Children.Add(_list);

        root.Children.Add(new TextBlock
        {
            Text = "Bottom of stack",
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 4, 0, 8),
        });

        // Move buttons
        var moveRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
        };
        var up = new Button
        {
            Content = "▲ Move up",
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 0),
        };
        up.Click += (_, _) => Move(-1);
        var down = new Button
        {
            Content = "▼ Move down",
            Padding = new Thickness(10, 4, 10, 4),
        };
        down.Click += (_, _) => Move(+1);
        moveRow.Children.Add(up);
        moveRow.Children.Add(down);
        root.Children.Add(moveRow);

        // OK / Cancel
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var ok = new Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 6, 0),
            IsDefault = true,
        };
        ok.Click += (_, _) =>
        {
            Result = _rows.Select(r => r.Card.Id).ToList();
            Close();
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true,
        };
        cancel.Click += (_, _) => Close();
        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);
        root.Children.Add(btnRow);

        Content = root;
    }

    private void Move(int delta)
    {
        int i = _list.SelectedIndex;
        int j = i + delta;
        if (i < 0 || j < 0 || j >= _rows.Count) return;
        var tmp = _rows[i];
        _rows[i] = _rows[j];
        _rows[j] = tmp;
        _list.SelectedIndex = j;
    }

    private static DataTemplate BuildItemTemplate()
    {
        // Render each row as "[<age>] <title> (<color>)" — same shape as
        // CardSummaryView but inline so the ListBox handles selection
        // styling for free.
        var dt = new DataTemplate(typeof(RowItem));
        var fef = new FrameworkElementFactory(typeof(TextBlock));
        fef.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Display"));
        fef.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
        dt.VisualTree = fef;
        return dt;
    }

    private sealed record RowItem(Card Card)
    {
        public string Display => $"[{Card.Age}] {Card.Title} ({Card.Color})";
    }
}
