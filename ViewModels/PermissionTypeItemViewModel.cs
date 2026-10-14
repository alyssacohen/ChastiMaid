using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class PermissionTypeItemViewModel : ViewModelBase
{
    public PermissionTypeItemViewModel(PermissionKind kind, Action<PermissionKind> onRequest, ILocalizer l)
    {
        Kind = kind;
        Name = GetName(kind, l);
        RequestCommand = ReactiveCommand.Create(() => onRequest(kind));
    }

    public PermissionKind Kind { get; }
    public string Name { get; }
    public ICommand RequestCommand { get; }

    public static string GetName(PermissionKind kind, ILocalizer l)
    {
        return kind switch
        {
            PermissionKind.Pee => l.T("Use the toilet (n.1)"),
            PermissionKind.Poop => l.T("Use the toilet (n.2)"),
            PermissionKind.SitDown => l.T("Sit down"),
            PermissionKind.Smoke => l.T("Cigarette break"),
            PermissionKind.TeaCoffee => l.T("Tea or coffee break"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
