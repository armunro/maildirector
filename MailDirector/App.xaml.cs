using System.Windows;
using MailDirector.Services;
using MailDirector.ViewModels;
using MailDirector.Views;

namespace MailDirector;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var storageService = new StorageService();
        var settingsService = new SettingsService(storageService);
        var folderPreferenceService = new FolderPreferenceService(settingsService);

        IEmailService emailService;
        if (settingsService.Config.Debug.DemoMode || !settingsService.Config.MicrosoftGraph.IsConfigured)
        {
            emailService = new MockEmailService();
        }
        else
        {
            emailService = new MicrosoftGraphEmailService(settingsService.Config.MicrosoftGraph, storageService);
        }

        var ruleService = new RuleService(emailService, storageService);

        var mainViewModel = new MainViewModel(
            emailService,
            ruleService,
            folderPreferenceService,
            settingsService);

        await mainViewModel.InitializeAsync();

        var mainWindow = new MainWindow(
            mainViewModel,
            ruleService,
            settingsService,
            storageService);

        mainWindow.Show();
    }
}
