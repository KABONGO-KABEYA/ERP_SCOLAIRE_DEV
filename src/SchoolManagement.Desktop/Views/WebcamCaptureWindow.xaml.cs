using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace SchoolManagement.Desktop.Views;

public partial class WebcamCaptureWindow : System.Windows.Window
{
    private VideoCapture? _capture;
    private Mat? _latestFrame;
    private DispatcherTimer? _timer;
    private readonly object _frameLock = new();

    public string? CapturedFilePath { get; private set; }

    public WebcamCaptureWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = new VideoCapture(0);
            }

            if (_capture is null || !_capture.IsOpened())
            {
                StatusText.Text = "Webcam introuvable. Vérifiez qu'une caméra est connectée et autorisée.";
                return;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += (_, _) => RefreshPreview();
            _timer.Start();
            StatusText.Text = "Webcam active. Cliquez sur Capturer.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Impossible d'ouvrir la webcam : {ex.Message}";
        }
    }

    private void RefreshPreview()
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return;
        }

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            return;
        }

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = frame.Clone();
            PreviewImage.Source = BitmapSourceConverter.ToBitmapSource(_latestFrame);
        }
    }

    private void Capture_OnClick(object sender, RoutedEventArgs e)
    {
        Mat? snapshot;
        lock (_frameLock)
        {
            if (_latestFrame is null || _latestFrame.Empty())
            {
                StatusText.Text = "Aucune image disponible. Attendez l'aperçu vidéo.";
                return;
            }

            snapshot = _latestFrame.Clone();
        }

        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"erp_photo_{Guid.NewGuid():N}.jpg");
            Cv2.ImWrite(filePath, snapshot);
            CapturedFilePath = filePath;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la capture : {ex.Message}";
        }
        finally
        {
            snapshot.Dispose();
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }
    }
}
