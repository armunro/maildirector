using System.Windows;
using MailDirector.ViewModels;
using Wpf.Ui.Controls;

namespace MailDirector.Views;

public partial class RulesManagerDialog : FluentWindow
{
    public RulesManagerDialog(RulesManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (s, e) => Close();
    }

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string color && DataContext is RulesManagerViewModel vm)
        {
            vm.Color = color;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
