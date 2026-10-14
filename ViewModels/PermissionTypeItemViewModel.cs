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
        Name = PermissionKindDisplay.NameOf(kind, l);
        RequestCommand = ReactiveCommand.Create(() => onRequest(kind));
    }

    public PermissionKind Kind { get; }
    public string Name { get; }
    public ICommand RequestCommand { get; }

}
