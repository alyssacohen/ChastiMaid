using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public partial class StartSessionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private Outfit _outfit;
    private double _currentBonus = 1.0;
    private int _regenCount;

    [Reactive] private string _proposedOutfit = "";

    public string DebtDisplay { get; }

    [Reactive] private bool _isLocked;
    [Reactive] private ConfirmDialogViewModel? _activeDialog;

    public ICommand AcceptCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand ModifyCommand { get; }
    public ICommand QuitCommand { get; }

    public StartSessionViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit? outfit = null)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();

        // Daily interest: accrues only if safe is open (combination known)
        if (state is not null && !state.IsSafeLocked && state.CurrentDebt > 0)
        {
            var days = (int)(UtcNow() - state.LastSessionAt).TotalDays;
            if (days > 0)
            {
                var rate = (decimal)state.DailyInterestRate;
                for (var i = 0; i < days; i++)
                    state.CurrentDebt += Math.Round(state.CurrentDebt * rate, 0);
                saveRepo.SaveGameState(state);
            }
        }

        DebtDisplay = $"Debt: ${state?.CurrentDebt:F2}";

        if (state?.OutfitLockedDate?.Date == UtcNow().Date)
        {
            _outfit = new Outfit();
            _proposedOutfit = state.CurrentOutfit ?? "";
            _currentBonus = state.OutfitBonusPercent;
            _isLocked = true;
        }
        else
        {
            var builder = new OutfitBuilder(saveRepo.GetWardrobe(), saveRepo.GetOutfitRules());
            _outfit = outfit ?? builder.Build();
            _proposedOutfit = _outfit.ToString();
        }

        AcceptCommand = ReactiveCommand.Create(Accept);
        RegenerateCommand = ReactiveCommand.CreateFromTask(ConfirmRegenerate);
        ModifyCommand = ReactiveCommand.CreateFromTask(ConfirmModify);
        QuitCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
    }

    private void Accept()
    {
        _saveRepo.AcceptItems(_outfit.Items);
        LockAndProceed();
    }

    private async Task ConfirmRegenerate()
    {
        var penalty = _regenCount == 0 ? 0.5 : 0.5;
        var resultBonus = _currentBonus * penalty;
        var dialog = new ConfirmDialogViewModel(
            title: "Regenerate Outfit",
            message: $"Regenerating will reduce your bonus to {resultBonus * 100:F0}% of the base multiplier. Proceed?",
            confirmLabel: "Regenerate",
            cancelLabel: "Cancel");
        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

        if (!confirmed) return;

        _saveRepo.RejectItems(_outfit.Items);
        _currentBonus = resultBonus;
        _regenCount++;

        var builder = new OutfitBuilder(_saveRepo.GetWardrobe(), _saveRepo.GetOutfitRules());
        _outfit = builder.Build();
        ProposedOutfit = _outfit.ToString();

        // After 2 regenerations, lock automatically
        if (_regenCount >= 2)
            LockAndProceed();
    }

    private async Task ConfirmModify()
    {
        var resultBonus = _currentBonus * 0.2;
        var dialog = new ConfirmDialogViewModel(
            title: "Modify Outfit",
            message: $"Manually choosing clothes will reduce your bonus to {resultBonus * 100:F0}% of the base multiplier and lock the outfit. Proceed?",
            confirmLabel: "Modify",
            cancelLabel: "Cancel");
        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

        if (!confirmed) return;

        _currentBonus = resultBonus;
        _mainVm.NavigateTo(new ChangeOutfitViewModel(_mainVm, _saveRepo, _outfit, _currentBonus));
    }

    private void LockAndProceed()
    {
        var state = _saveRepo.GetGameState();
        if (state is not null && !_isLocked)
        {
            state.CurrentOutfit = _outfit.ToString();
            state.OutfitLockedDate = UtcNow();
            state.OutfitBonusPercent = _currentBonus;
            state.OutfitMultiplier = _outfit.TotalMultiplier;
            _saveRepo.SaveGameState(state);
        }

        IsLocked = true;
        _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, _currentBonus));
    }
}
