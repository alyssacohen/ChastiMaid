using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public enum PermissionKind
{
    Pee,
    Poop,
    SitDown,
    Smoke,
    TeaCoffee,
}

/// <summary>Localized permission names, shared by the Dashboard, Settings and the contract.</summary>
public static class PermissionKindDisplay
{
    public static string NameOf(PermissionKind kind, ILocalizer l) => kind switch
    {
        PermissionKind.Pee => l.T("Use the toilet (n.1)"),
        PermissionKind.Poop => l.T("Use the toilet (n.2)"),
        PermissionKind.SitDown => l.T("Sit down"),
        PermissionKind.Smoke => l.T("Cigarette break"),
        PermissionKind.TeaCoffee => l.T("Tea or coffee break"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
