using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MailDirector.Helpers;

public static class WebBrowserHelper
{
    [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect,
        int nTopRect,
        int nRightRect,
        int nBottomRect,
        int nWidthEllipse,
        int nHeightEllipse);

    [DllImport("user32.dll", EntryPoint = "SetWindowRgn")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    public static readonly DependencyProperty HtmlProperty =
        DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(WebBrowserHelper),
            new FrameworkPropertyMetadata(null, OnHtmlOrDarkModeChanged));

    public static string? GetHtml(DependencyObject obj) => (string?)obj.GetValue(HtmlProperty);
    public static void SetHtml(DependencyObject obj, string? value) => obj.SetValue(HtmlProperty, value);

    public static readonly DependencyProperty IsDarkModeProperty =
        DependencyProperty.RegisterAttached(
            "IsDarkMode",
            typeof(bool),
            typeof(WebBrowserHelper),
            new FrameworkPropertyMetadata(false, OnHtmlOrDarkModeChanged));

    public static bool GetIsDarkMode(DependencyObject obj) => (bool)obj.GetValue(IsDarkModeProperty);
    public static void SetIsDarkMode(DependencyObject obj, bool value) => obj.SetValue(IsDarkModeProperty, value);

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(double),
            typeof(WebBrowserHelper),
            new FrameworkPropertyMetadata(0.0, OnCornerRadiusChanged));

    public static double GetCornerRadius(DependencyObject obj) => (double)obj.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject obj, double value) => obj.SetValue(CornerRadiusProperty, value);

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(WebBrowserHelper),
            new PropertyMetadata(false));

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WebBrowser browser) return;
        EnsureEventsHooked(browser);
        ApplyRoundedCorners(browser);
    }

    public static void ApplyRoundedCorners(WebBrowser browser)
    {
        var radius = GetCornerRadius(browser);
        if (radius <= 0) return;

        var width = (int)Math.Ceiling(browser.ActualWidth);
        var height = (int)Math.Ceiling(browser.ActualHeight);
        if (width <= 0 || height <= 0) return;

        try
        {
            var hwnd = browser.Handle;
            if (hwnd != IntPtr.Zero)
            {
                var ellipse = (int)Math.Round(radius * 2);
                var hRgn = CreateRoundRectRgn(0, 0, width, height, ellipse, ellipse);
                if (hRgn != IntPtr.Zero)
                {
                    SetWindowRgn(hwnd, hRgn, true);
                }
            }
        }
        catch
        {
            // Ignore Win32 exceptions
        }
    }

    private static void OnHtmlOrDarkModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WebBrowser browser) return;

        EnsureEventsHooked(browser);

        var rawContent = GetHtml(browser);
        var isDarkMode = GetIsDarkMode(browser);

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            try
            {
                var bg = isDarkMode ? "#1e1e1e" : "#ffffff";
                var scrollbarCss = GetScrollbarCss(isDarkMode);
                browser.NavigateToString($"<!DOCTYPE html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />{scrollbarCss}</head><body style=\"background-color:{bg};\"></body></html>");
            }
            catch
            {
                // Ignore navigation exceptions
            }
            return;
        }

        try
        {
            var html = PrepareHtml(rawContent, isDarkMode);
            browser.NavigateToString(html);
        }
        catch
        {
            try
            {
                browser.NavigateToString(FormatHtmlTemplate(WebUtility.HtmlEncode(rawContent), isDarkMode));
            }
            catch
            {
                // Fallback
            }
        }
    }

    private static void EnsureEventsHooked(WebBrowser browser)
    {
        if ((bool)browser.GetValue(IsHookedProperty)) return;
        browser.SetValue(IsHookedProperty, true);

        browser.Navigating += (s, e) =>
        {
            // Allow about:blank or internal string navigation
            if (e.Uri == null || e.Uri.ToString().Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Cancel navigation within the preview pane and open in system default browser
            e.Cancel = true;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if unable to launch process
            }
        };

        browser.SizeChanged += (s, e) => ApplyRoundedCorners(browser);
        browser.Navigated += (s, e) =>
        {
            SetSilent(browser, true);
            ApplyRoundedCorners(browser);
        };
        browser.Loaded += (s, e) =>
        {
            SetSilent(browser, true);
            ApplyRoundedCorners(browser);
        };
    }

    private static void SetSilent(WebBrowser browser, bool silent)
    {
        try
        {
            var fi = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null)
            {
                var browserObj = fi.GetValue(browser);
                if (browserObj != null)
                {
                    browserObj.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, browserObj, new object[] { silent });
                }
            }
        }
        catch
        {
            // Ignore reflection failures
        }
    }

    public static string PrepareHtml(string content, bool isDarkMode = false)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return FormatHtmlTemplate(string.Empty, isDarkMode);
        }

        bool isHtml = content.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<p", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<table", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<br", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<span", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<h1", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<h2", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<h3", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<h4", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<ul", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("<ol", StringComparison.OrdinalIgnoreCase);

        if (!isHtml)
        {
            var encoded = WebUtility.HtmlEncode(content).Replace("\r\n", "<br/>").Replace("\n", "<br/>");
            return FormatHtmlTemplate(encoded, isDarkMode);
        }

        // If it's already a full HTML page
        if (content.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            var metaTags = "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" /><meta charset=\"utf-8\" />";
            var scrollbarStyle = GetScrollbarCss(isDarkMode);
            var darkStyle = isDarkMode ? GetDarkModeCssOverride() : string.Empty;
            var headInjection = metaTags + scrollbarStyle + darkStyle;

            if (content.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            {
                return Regex.Replace(
                    content,
                    "<head>",
                    "<head>" + headInjection,
                    RegexOptions.IgnoreCase);
            }
            if (content.Contains("<head", StringComparison.OrdinalIgnoreCase))
            {
                return Regex.Replace(
                    content,
                    "<head([^>]*)>",
                    "<head$1>" + headInjection,
                    RegexOptions.IgnoreCase);
            }

            return headInjection + content;
        }

        // HTML fragment
        return FormatHtmlTemplate(content, isDarkMode);
    }

    private static string GetScrollbarCss(bool isDarkMode)
    {
        var thumbColor = isDarkMode ? "#4b5563" : "#c1c5cc";
        var thumbHoverColor = isDarkMode ? "#6b7280" : "#9ca3af";
        var trackColor = isDarkMode ? "#1e1e1e" : "#f9fafb";
        var arrowColor = isDarkMode ? "#9ca3af" : "#6b7280";

        return $$"""
            <style>
                html, body {
                    scrollbar-face-color: {{thumbColor}};
                    scrollbar-track-color: {{trackColor}};
                    scrollbar-arrow-color: {{arrowColor}};
                    scrollbar-highlight-color: transparent;
                    scrollbar-shadow-color: transparent;
                    scrollbar-3dlight-color: transparent;
                    scrollbar-darkshadow-color: transparent;
                    scrollbar-color: {{thumbColor}} transparent;
                    scrollbar-width: thin;
                }
                * {
                    scrollbar-color: {{thumbColor}} transparent;
                    scrollbar-width: thin;
                }
                ::-webkit-scrollbar {
                    width: 8px;
                    height: 8px;
                }
                ::-webkit-scrollbar-track {
                    background: transparent;
                }
                ::-webkit-scrollbar-thumb {
                    background-color: {{thumbColor}};
                    border-radius: 4px;
                }
                ::-webkit-scrollbar-thumb:hover {
                    background-color: {{thumbHoverColor}};
                }
                ::-webkit-scrollbar-corner {
                    background: transparent;
                }
            </style>
            """;
    }

    private static string GetDarkModeCssOverride()
    {
        return """
            <style>
                * {
                    border-color: #374151 !important;
                }
                html, body, table, thead, tbody, tfoot, tr, td, th, div, section, article, header, footer, aside, nav, main, p, ul, ol, li, blockquote, pre, form, fieldset, font, center {
                    background-color: #1e1e1e !important;
                    background-image: none !important;
                    color: #e5e7eb !important;
                }
                span, b, strong, em, i, u, small, sub, sup, code {
                    background-color: transparent !important;
                    color: #e5e7eb !important;
                }
                a, a * {
                    color: #60a5fa !important;
                    background-color: transparent !important;
                }
                a:visited, a:visited * {
                    color: #a78bfa !important;
                }
                hr {
                    border-color: #374151 !important;
                    background-color: #374151 !important;
                }
                input, textarea, select, button {
                    background-color: #2d2d2d !important;
                    color: #e5e7eb !important;
                    border-color: #4b5563 !important;
                }
            </style>
            """;
    }

    private static string FormatHtmlTemplate(string bodyContent, bool isDarkMode = false)
    {
        var textColor = isDarkMode ? "#e5e7eb" : "#1f2937";
        var bgColor = isDarkMode ? "#1e1e1e" : "#ffffff";
        var linkColor = isDarkMode ? "#60a5fa" : "#2563eb";
        var hrColor = isDarkMode ? "#374151" : "#e5e7eb";
        var scrollbarCss = GetScrollbarCss(isDarkMode);
        var darkCss = isDarkMode ? GetDarkModeCssOverride() : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                <meta charset="utf-8" />
                {{scrollbarCss}}
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                        font-size: 13px;
                        line-height: 1.5;
                        margin: 16px;
                        color: {{textColor}};
                        background-color: {{bgColor}};
                        word-wrap: break-word;
                    }
                    a { color: {{linkColor}}; }
                    img { max-width: 100%; height: auto; }
                    pre, code { font-family: Consolas, 'Courier New', monospace; font-size: 12px; }
                    table { border-collapse: collapse; max-width: 100%; }
                    hr { border: 0; border-top: 1px solid {{hrColor}}; margin: 16px 0; }
                </style>
                {{darkCss}}
            </head>
            <body>
            {{bodyContent}}
            </body>
            </html>
            """;
    }
}
