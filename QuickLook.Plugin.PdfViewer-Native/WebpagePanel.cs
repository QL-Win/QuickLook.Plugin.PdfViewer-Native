// Copyright © 2017-2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;

namespace QuickLook.Plugin.PDFViewerNative;

public class WebpagePanel : UserControl
{
    private Uri _currentUri;
    private WebView2 _webView;

    public WebpagePanel()
    {
        if (!Helper.IsWebView2Available())
        {
            Content = CreateDownloadButton();
        }
        else
        {
            _webView = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\\"),
                },
                DefaultBackgroundColor = OSThemeHelper.AppsUseDarkTheme() ? DrawingColor.FromArgb(255, 32, 32, 32) : DrawingColor.White, // Prevent white flash in dark mode
            };
            _webView.NavigationStarting += NavigationStarting_CancelNavigation;
            _webView.NavigationCompleted += WebView_NavigationCompleted;
            Content = _webView;

            // Handle DPI changes to prevent display glitches when switching monitors
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DpiChanged += OnDpiChanged;
            // Set initial rasterization scale
            UpdateRasterizationScale(VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DpiChanged -= OnDpiChanged;
        }
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // Update WebView2 rasterization scale when DPI changes
        UpdateRasterizationScale(e.NewDpi.PixelsPerDip);
    }

    private void UpdateRasterizationScale(double scale)
    {
        if (_webView != null)
        {
            // Force WebView2 to refresh its rendering after DPI change
            // This helps prevent display glitches when switching monitors
            Dispatcher.InvokeAsync(() =>
            {
                if (_webView?.CoreWebView2 != null)
                {
                    // Trigger a repaint by toggling visibility momentarily
                    var currentUri = _currentUri;
                    if (currentUri != null)
                    {
                        _webView.InvalidateVisual();
                        _webView.UpdateLayout();
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    public void NavigateToFile(string path)
    {
        var uri = Path.IsPathRooted(path) ? Helper.FilePathToFileUrl(path) : new Uri(path);

        NavigateToUri(uri);
    }

    public void NavigateToUri(Uri uri)
    {
        if (_webView == null)
            return;

        _webView.Source = uri;
        _currentUri = _webView.Source;
    }

    public void NavigateToHtml(string html)
    {
        _webView?.EnsureCoreWebView2Async()
            .ContinueWith(_ => Dispatcher.Invoke(() => _webView?.NavigateToString(html)));
    }

    private void NavigationStarting_CancelNavigation(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:")) // when using NavigateToString
            return;

        var newUri = new Uri(e.Uri);
        if (newUri == _currentUri) return;
        e.Cancel = true;
    }

    private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _webView.DefaultBackgroundColor = DrawingColor.White; // Reset to white after page load to match expected default behavior
    }

    public void Dispose()
    {
        if (_webView != null)
        {
            _webView.NavigationStarting -= NavigationStarting_CancelNavigation;
            _webView.NavigationCompleted -= WebView_NavigationCompleted;
            _webView.Dispose();
            _webView = null;
        }

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DpiChanged -= OnDpiChanged;
        }
    }

    private object CreateDownloadButton()
    {
        string translationFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations.config");

        var button = new Button
        {
            Content = TranslationHelper.Get("WEBVIEW2_NOT_AVAILABLE", translationFile),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(20, 6, 20, 6)
        };
        button.Click += (sender, e) => Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");

        return button;
    }
}
