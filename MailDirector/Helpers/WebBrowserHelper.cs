using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MailDirector.Helpers;

public static class WebBrowserHelper
{
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

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(WebBrowserHelper),
            new PropertyMetadata(false));

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
                browser.NavigateToString($"<!DOCTYPE html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" /></head><body style=\"background-color:{bg};\"></body></html>");
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

        browser.Navigated += (s, e) => SetSilent(browser, true);
        browser.Loaded += (s, e) => SetSilent(browser, true);
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
            var darkStyle = isDarkMode ? GetDarkModeCssOverride() : string.Empty;
            var headInjection = metaTags + darkStyle;

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
        var darkCss = isDarkMode ? GetDarkModeCssOverride() : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                <meta charset="utf-8" />
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
