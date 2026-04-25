using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Innovation.Wpf;

/// <summary>
/// Startup dialog: pick a random seed (blank = time-based), the total number
/// of players (2–4), and each seat's controller kind. Constructs the Window
/// procedurally so we don't add another xaml page to the project.
/// </summary>
public enum SeatKind { Human, Greedy, Random }

public sealed class NewGameOptions
{
    public int Seed { get; init; }
    public SeatKind[] Seats { get; init; } = Array.Empty<SeatKind>();
}

public sealed class NewGameDialog : Window
{
    private const int MinPlayers = 2;
    private const int MaxPlayers = 4;

    private readonly TextBox _seedBox;
    // One seat row per supported player count; rows beyond the chosen count
    // collapse so the user only sees seats that will actually play.
    private readonly TextBlock[] _seatLabels = new TextBlock[MaxPlayers];
    private readonly ComboBox[] _seatCombos = new ComboBox[MaxPlayers];
    private readonly Dictionary<int, RadioButton> _playerCountButtons = new();

    private int _playerCount = 2;

    public NewGameOptions? Result { get; private set; }

    public NewGameDialog()
    {
        Title = "New Game";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xEF, 0xD3));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var root = new StackPanel { Margin = new Thickness(16) };

        // ---- Welcome banner ----
        root.Children.Add(new TextBlock
        {
            Text = "Welcome to Innovation. How many players?",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6),
        });

        // ---- Player-count picker (2/3/4) ----
        var countRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
        };
        for (int n = MinPlayers; n <= MaxPlayers; n++)
        {
            int captured = n;
            var rb = new RadioButton
            {
                Content = $"{n} Players",
                GroupName = "PlayerCount",
                Margin = new Thickness(0, 0, 12, 0),
                IsChecked = (n == _playerCount),
                Padding = new Thickness(4, 0, 0, 0),
            };
            rb.Checked += (_, _) => SetPlayerCount(captured);
            _playerCountButtons[n] = rb;
            countRow.Children.Add(rb);
        }
        root.Children.Add(countRow);

        // ---- Seed + seats grid ----
        var grid = new Grid();
        // 1 seed row + MaxPlayers seat rows.
        for (int i = 0; i < 1 + MaxPlayers; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        AddLabel(grid, "Seed:", 0);
        _seedBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "Leave blank for a time-based random seed.",
        };
        Grid.SetRow(_seedBox, 0); Grid.SetColumn(_seedBox, 1);
        grid.Children.Add(_seedBox);

        for (int i = 0; i < MaxPlayers; i++)
        {
            int row = 1 + i;
            var label = new TextBlock
            {
                Text = $"Player {i + 1}:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 8),
            };
            Grid.SetRow(label, row); Grid.SetColumn(label, 0);
            grid.Children.Add(label);
            _seatLabels[i] = label;

            // Default: P1 = Human (the local player), all others = Greedy.
            var initial = (i == 0) ? SeatKind.Human : SeatKind.Greedy;
            var cb = BuildSeatCombo(initial);
            Grid.SetRow(cb, row); Grid.SetColumn(cb, 1);
            grid.Children.Add(cb);
            _seatCombos[i] = cb;
        }
        root.Children.Add(grid);

        // ---- OK / Cancel ----
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var ok = new Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };
        ok.Click += OnOkClick;
        var cancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = root;
        SetPlayerCount(_playerCount);
    }

    private void SetPlayerCount(int n)
    {
        _playerCount = n;
        for (int i = 0; i < MaxPlayers; i++)
        {
            var v = (i < n) ? Visibility.Visible : Visibility.Collapsed;
            _seatLabels[i].Visibility = v;
            _seatCombos[i].Visibility = v;
        }
    }

    private static void AddLabel(Grid g, string text, int row)
    {
        var t = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 8),
        };
        Grid.SetRow(t, row); Grid.SetColumn(t, 0);
        g.Children.Add(t);
    }

    private static ComboBox BuildSeatCombo(SeatKind initial)
    {
        var cb = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        foreach (SeatKind k in Enum.GetValues(typeof(SeatKind)))
            cb.Items.Add(k);
        cb.SelectedItem = initial;
        return cb;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        int seed;
        var s = _seedBox.Text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            seed = Environment.TickCount;
        }
        else if (!int.TryParse(s, out seed))
        {
            MessageBox.Show(this, "Seed must be an integer (or blank).", "New Game",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var seats = new SeatKind[_playerCount];
        for (int i = 0; i < _playerCount; i++)
            seats[i] = (SeatKind)_seatCombos[i].SelectedItem;

        Result = new NewGameOptions
        {
            Seed = seed,
            Seats = seats,
        };
        DialogResult = true;
    }
}
