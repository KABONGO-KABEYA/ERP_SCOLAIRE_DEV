using System.Windows;

namespace SchoolManagement.Desktop.Views;

public partial class CloudSyncSyncElementsDetailWindow : Window
{
    public CloudSyncSyncElementsDetailWindow(DateTime startedAtLocal, IReadOnlyList<string> detailLines)
    {
        InitializeComponent();
        SubtitleText.Text = startedAtLocal.ToString("dd/MM/yyyy HH:mm:ss");
        DetailItems.ItemsSource = detailLines;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
