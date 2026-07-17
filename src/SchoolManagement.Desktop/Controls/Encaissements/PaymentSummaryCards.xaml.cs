using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls.Encaissements;

public partial class PaymentSummaryCards
{
    public PaymentSummaryCards()
    {
        InitializeComponent();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CardsPanel.Columns = e.NewSize.Width < 700 ? 1 : 2;
        if (CardsPanel.Children.Count >= 2
            && CardsPanel.Children[0] is Border left
            && CardsPanel.Children[1] is Border right)
        {
            if (CardsPanel.Columns == 1)
            {
                left.Margin = new Thickness(0, 0, 0, 8);
                right.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                left.Margin = new Thickness(0, 0, 8, 0);
                right.Margin = new Thickness(8, 0, 0, 0);
            }
        }
    }
}
