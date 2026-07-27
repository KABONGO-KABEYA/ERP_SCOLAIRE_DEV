using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SchoolManagement.Desktop.Printing.CardLayout;

namespace SchoolManagement.Desktop.Views;

/// <summary>Choix du support d'impression (A4 planche vs cartes unitaires).</summary>
public sealed class CardPrintOptionsWindow : Window
{
    private readonly RadioButton _a4Five;
    private readonly RadioButton _a4Four;
    private readonly RadioButton _individual;

    public CardPrintLayoutKind SelectedLayout { get; private set; } = CardPrintLayoutKind.A4Sheet;
    public int A4Rows { get; private set; } = 5;
    public bool Confirmed { get; private set; }

    public CardPrintOptionsWindow(int cardCount)
    {
        Title = "Impression des cartes";
        Width = 460;
        Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(249, 250, 251));

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = $"Imprimer {cardCount} carte(s)",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        root.Children.Add(new TextBlock
        {
            Text = "Un seul job d'impression sera envoyé à l'imprimante (recto puis verso).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        _a4Five = new RadioButton
        {
            Content = "Planche A4 — 2 colonnes × 5 rangées (10 cartes/page) + verso",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 10),
            GroupName = "layout"
        };
        _a4Four = new RadioButton
        {
            Content = "Planche A4 — 2 colonnes × 4 rangées (8 cartes/page) + verso",
            Margin = new Thickness(0, 0, 0, 10),
            GroupName = "layout"
        };
        _individual = new RadioButton
        {
            Content = "Cartes unitaires — 1 face = 1 page (PVC / découpe)",
            Margin = new Thickness(0, 0, 0, 16),
            GroupName = "layout"
        };

        root.Children.Add(_a4Five);
        root.Children.Add(_a4Four);
        root.Children.Add(_individual);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Annuler",
            Width = 100,
            Margin = new Thickness(0, 0, 8, 0),
            Style = TryFindResource("ErpSecondaryButton") as Style
        };
        cancel.Click += (_, _) => { Confirmed = false; DialogResult = false; Close(); };

        var ok = new Button
        {
            Content = "Imprimer",
            Width = 110,
            Style = TryFindResource("ErpPrimaryButton") as Style
        };
        ok.Click += (_, _) =>
        {
            if (_individual.IsChecked == true)
            {
                SelectedLayout = CardPrintLayoutKind.Individual;
                A4Rows = 5;
            }
            else
            {
                SelectedLayout = CardPrintLayoutKind.A4Sheet;
                A4Rows = _a4Four.IsChecked == true ? 4 : 5;
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);
        Content = root;
    }
}
