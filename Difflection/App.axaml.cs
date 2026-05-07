using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Difflection.Infrastructure;
using Difflection.Storage;
using Difflection.ViewModels;
using Difflection.Views;
using JetBrains.Annotations;

namespace Difflection;

[UsedImplicitly]
public class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += Dispatcher_OnUnhandledException;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
            {
                var projectStorage = CreateDesktopProjectStorage();
                projectStorage.ProjectLoadIssue += ProjectStorage_OnProjectLoadIssue;
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(projectStorage)
                };
                break;
            }
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView
                {
                    DataContext = new MainWindowViewModel()
                };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static LocalFileProjectStorage CreateDesktopProjectStorage()
    {
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Difflection");

        return new LocalFileProjectStorage(rootPath);
    }

    private static void ProjectStorage_OnProjectLoadIssue(object? sender, ProjectStorageLoadIssueEventArgs e)
    {
        var message = e.RecoveredFromBackup
            ? "Difflection had trouble reading a project file and recovered it from the previous saved copy."
            : "Difflection could not read a saved project file. The damaged project was skipped so the app can continue.";

        ApplicationErrorReporter.Report(
            e.Exception,
            $"{message}{Environment.NewLine}{e.ProjectFilePath}");
    }

    private static void Dispatcher_OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ApplicationErrorReporter.Report(e.Exception, "Difflection encountered an unexpected error.");
        e.Handled = true;
    }

    private static void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ApplicationErrorReporter.Report(exception, "Difflection encountered an unexpected fatal error.", showDialog: false);
        }
    }

    private static void TaskScheduler_OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ApplicationErrorReporter.WriteLog(e.Exception, "Difflection observed an unhandled background task error.");
        e.SetObserved();
    }
}
