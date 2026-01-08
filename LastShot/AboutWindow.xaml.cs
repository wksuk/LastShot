using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace LastShot;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadBrandImage();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void LoadBrandImage()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/256.ico", UriKind.Absolute);
            var resource = Application.GetResourceStream(uri);
            if (resource?.Stream is null)
            {
                return;
            }

            using var stream = resource.Stream;
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
            if (frame is null)
            {
                return;
            }

            BrandImage.Source = frame;
        }
        catch
        {
            // Swallow icon load failures; UI can function without the image.
        }
    }
}
