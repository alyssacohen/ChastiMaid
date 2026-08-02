using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class PermissionTypeItemViewModel : ViewModelBase
{
    public PermissionTypeItemViewModel(PermissionKind kind, Action<PermissionKind> onRequest)
    {
        Kind = kind;
        Name = GetName(kind);
        RequestCommand = ReactiveCommand.Create(() => onRequest(kind));
    }

    public PermissionKind Kind { get; }
    public string Name { get; }
    public ICommand RequestCommand { get; }

    public static string GetName(PermissionKind kind)
    {
        return Resources.Get(kind switch
        {
            PermissionKind.Pee => "PermissionKindPee",
            PermissionKind.Poop => "PermissionKindPoop",
            PermissionKind.SitDown => "PermissionKindSitDown",
            PermissionKind.Smoke => "PermissionKindSmoke",
            PermissionKind.TeaCoffee => "PermissionKindTeaCoffee",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });
    }
}
