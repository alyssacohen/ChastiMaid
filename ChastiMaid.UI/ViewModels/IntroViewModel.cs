using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Reactive;
using ChastiMaid.Core.Services;

namespace ChastiMaid.UI.ViewModels;

/// <summary>
/// View model for the intro/main menu screen.
/// Presents options to start a new session, continue existing, or exit.
/// </summary>
public partial class IntroViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVM;
    private readonly SessionManager _sessionManager;

    [Reactive] private bool _canContinue;

    public IntroViewModel(MainViewModel mainVM, SessionManager sessionManager)
    {
        _mainVM = mainVM;
        _sessionManager = sessionManager;

        // Check if there's an existing session to continue
        _canContinue = _sessionManager.HasExistingSession();

        // ContinueSessionCommand will be auto-generated with CanExecute based on CanContinue
        InitializeContinueCommand();
    }

    [ReactiveCommand]
    private void StartNewSession()
    {
        _mainVM.StartNewSessionCommand.Execute().Subscribe();
    }

    // Manual command creation for conditional execution
    // The [ReactiveCommand] attribute doesn't support WhenAnyValue directly yet
    public ReactiveCommand<Unit, Unit> ContinueSessionCommand { get; private set; }

    private void InitializeContinueCommand()
    {
        ContinueSessionCommand = ReactiveCommand.Create(
            ContinueSession,
            this.WhenAnyValue(x => x.CanContinue)
        );
    }

    private void ContinueSession()
    {
        _mainVM.ContinueSessionCommand.Execute().Subscribe();
    }

    [ReactiveCommand]
    private void ExitApp()
    {
        _mainVM.ExitAppCommand.Execute().Subscribe();
    }
}
