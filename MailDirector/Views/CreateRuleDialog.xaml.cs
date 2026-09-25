using System.Windows;
using MailDirector.ViewModels;
using Wpf.Ui.Controls;

namespace MailDirector.Views;

public partial class CreateRuleDialog : FluentWindow
{
    public CreateRuleDialog(CreateRuleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (s, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void OnSelectColor(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string color && DataContext is CreateRuleViewModel vm)
        {
            vm.RuleColor = color;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
