using System.Windows;
using MailDirector.ViewModels;
using Wpf.Ui.Controls;

namespace MailDirector.Views;

public partial class SettingsDialog : FluentWindow
{
    public SettingsDialog(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (s, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void OnLayoutRadioClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string layout && DataContext is SettingsViewModel vm)
        {
            vm.ReadingPaneLayout = layout;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
