using ReactiveUI;

namespace ChastiMaid.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Inherits from ReactiveObject to provide property change notifications
/// and reactive programming capabilities.
/// </summary>
public class ViewModelBase : ReactiveObject
{
    // This class serves as the foundation for all ViewModels.
    // ReactiveObject provides:
    // - INotifyPropertyChanged implementation
    // - INotifyPropertyChanging implementation
    // - RaiseAndSetIfChanged method for property setters
    // - WhenAnyValue for creating observable streams from properties
}
