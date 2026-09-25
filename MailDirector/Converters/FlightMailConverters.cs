using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MailDirector.Models;
using Wpf.Ui.Controls;

namespace MailDirector.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Inverse) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVis = value is Visibility v && v == Visibility.Visible;
        return Inverse ? !isVis : isVis;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Inverse) isNullOrEmpty = !isNullOrEmpty;
        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToBoolConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNotNull = value != null;
        return Inverse ? !isNotNull : isNotNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FriendlyDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto)
        {
            if (value is DateTime dt) dto = new DateTimeOffset(dt);
            else return string.Empty;
        }

        var local = dto.ToLocalTime();
        var now = DateTimeOffset.Now;
        var diff = now - local;

        if (local.Date == now.Date)
        {
            return local.ToString("t", culture); // 3:45 PM
        }
        if (local.Date == now.Date.AddDays(-1))
        {
            return "Yesterday";
        }
        if (diff.TotalDays < 7)
        {
            return local.ToString("ddd", culture); // Mon, Tue...
        }
        if (local.Year == now.Year)
        {
            return local.ToString("MMM d", culture); // Sep 24
        }

        return local.ToString("d", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { }
        }

        return new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringTruncateConverter : IValueConverter
{
    public int MaxLength { get; set; } = 80;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s)) return string.Empty;

        int len = MaxLength;
        if (parameter is int pLen) len = pLen;
        else if (parameter is string pStr && int.TryParse(pStr, out var parsed)) len = parsed;

        if (s.Length <= len) return s;
        return s.Substring(0, len) + "...";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name)) return "EM";

        var parts = name.Trim().Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpperInvariant() : parts[0].ToUpperInvariant();
        }

        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[parts.Length - 1][0])}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FolderLevelToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int level = value is int l ? l : 0;
        double indent = level * 16.0;
        return new Thickness(indent, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FolderSymbolIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MailFolderItem folder)
        {
            if (!string.IsNullOrEmpty(folder.CustomIcon))
            {
                if (Enum.TryParse<SymbolRegular>(folder.CustomIcon, true, out var customSym))
                    return customSym;
            }

            var name = folder.DisplayName.ToLowerInvariant().Replace(" ", "");
            return name switch
            {
                "inbox" => SymbolRegular.MailInbox24,
                "archive" => SymbolRegular.Archive24,
                "sentitems" or "sent" => SymbolRegular.Send24,
                "drafts" => SymbolRegular.Drafts24,
                "deleteditems" or "deleted" or "trash" => SymbolRegular.Delete24,
                "junkemail" or "junk" or "spam" => SymbolRegular.ShieldDismiss24,
                "projects" => SymbolRegular.Briefcase24,
                "notifications" => SymbolRegular.Alert24,
                "personal" => SymbolRegular.Person24,
                _ => SymbolRegular.Folder24
            };
        }

        return SymbolRegular.Folder24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ActionTypeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ActionType type)
        {
            return type switch
            {
                ActionType.MarkAsRead => "Mark as read",
                ActionType.Star => "Flag / Star",
                ActionType.ClearFlag => "Clear Flag",
                ActionType.Archive => "Move to Archive",
                ActionType.Move => "Move to Folder",
                ActionType.AddCategory => "Add Category",
                _ => type.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
