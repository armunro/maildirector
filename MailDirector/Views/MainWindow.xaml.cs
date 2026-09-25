using System;
using System.Windows;
using MailDirector.Models;
using MailDirector.Services;
using MailDirector.ViewModels;
using Wpf.Ui.Controls;

namespace MailDirector.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IRuleService _ruleService;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public MainWindow(
        MainViewModel viewModel,
        IRuleService ruleService,
        ISettingsService settingsService,
        IStorageService storageService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _ruleService = ruleService;
        _settingsService = settingsService;
        _storageService = storageService;

        DataContext = viewModel;

        viewModel.RequestOpenRulesManager += OnRequestOpenRulesManager;
        viewModel.RequestOpenCreateRule += OnRequestOpenCreateRule;
        viewModel.RequestOpenSettings += OnRequestOpenSettings;
    }

    private void OnRequestOpenRulesManager(object? sender, EventArgs e)
    {
        var vm = new RulesManagerViewModel(_ruleService);
        var dialog = new RulesManagerDialog(vm)
        {
            Owner = this
        };
        dialog.ShowDialog();
        _viewModel.LoadRules();
    }

    private void OnRequestOpenCreateRule(object? sender, (EmailItem Email, bool AddToExisting) args)
    {
        var vm = new CreateRuleViewModel(_ruleService, args.Email, args.AddToExisting);
        var dialog = new CreateRuleDialog(vm)
        {
            Owner = this
        };
        var res = dialog.ShowDialog();
        if (res == true)
        {
            _viewModel.LoadRules();
        }
    }

    private void OnRequestOpenSettings(object? sender, EventArgs e)
    {
        var vm = new SettingsViewModel(_settingsService, _storageService);
        var dialog = new SettingsDialog(vm)
        {
            Owner = this
        };
        var res = dialog.ShowDialog();
        if (res == true)
        {
            _ = _viewModel.RefreshAllAsync();
        }
    }

    private void OnSetFolderColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button && button.Tag is string colorHex && button.DataContext is MailFolderItem folder)
        {
            _viewModel.SetFolderColor(folder, colorHex);
        }
    }
}
