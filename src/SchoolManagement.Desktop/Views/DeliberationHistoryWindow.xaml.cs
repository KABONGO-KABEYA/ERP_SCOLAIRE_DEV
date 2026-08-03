using System.Windows;

namespace SchoolManagement.Desktop.Views;

public partial class DeliberationHistoryWindow : Window
{
    public DeliberationHistoryWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
