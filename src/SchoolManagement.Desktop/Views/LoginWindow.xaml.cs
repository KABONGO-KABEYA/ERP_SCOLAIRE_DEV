using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FontAwesome.Sharp;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginSucceeded += () =>
        {
            DialogResult = true;
            Close();
        };

        viewModel.ChangeSchoolRequested += OnChangeSchoolRequested;
        viewModel.ForgotPasswordRequested += OnForgotPasswordRequested;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateRightPanelClip();
        LoginRootGrid.SizeChanged += (_, _) => UpdateRightPanelClip();
        RightPanelHost.SizeChanged += (_, _) => UpdateRightPanelClip();

        try
        {
            LeftPanelHost.BeginStoryboard((Storyboard)FindResource("LoginLeftFadeInStoryboard"), HandoffBehavior.SnapshotAndReplace);
            RightPanelHost.BeginStoryboard((Storyboard)FindResource("LoginRightSlideInStoryboard"), HandoffBehavior.SnapshotAndReplace);
        }
        catch
        {
            // Préserve le design visible si l'animation échoue.
            LeftPanelHost.Opacity = 1;
            RightPanelHost.Opacity = 1;
            if (RightPanelHost.RenderTransform is TranslateTransform slide)
            {
                slide.X = 0;
            }
        }
    }

    private void UpdateRightPanelClip()
    {
        if (RightPanelHost.ActualWidth <= 0 || RightPanelHost.ActualHeight <= 0)
        {
            return;
        }

        const double designHeight = 900;

        var baseGeometry = (PathGeometry)FindResource("LoginRightPanelClipGeometry");
        var clip = baseGeometry.CloneCurrentValue();
        var scale = RightPanelHost.ActualHeight / designHeight;
        clip.Transform = new ScaleTransform(scale, scale);

        RightPanelHost.Clip = clip;
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIcon.Icon = IconChar.Square;
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIcon.Icon = IconChar.WindowRestore;
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnChangeSchoolRequested()
    {
        var bootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory);
        var viewModel = new DatabaseServerConfigViewModel(bootstrap);
        var window = new DatabaseServerConfigWindow(viewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _viewModel.RefreshServerInfo();
        }
    }

    private void OnForgotPasswordRequested()
    {
        MessageBox.Show(
            "Contactez l'administrateur système de votre établissement pour réinitialiser votre mot de passe.",
            "Mot de passe oublié",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
