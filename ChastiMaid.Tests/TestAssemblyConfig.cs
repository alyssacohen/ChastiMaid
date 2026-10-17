using ReactiveUI;
using ReactiveUI.Builder;

// LiteDB's BsonMapper.Global (shared by every LiteDatabase, :memory: ones included) builds each type's
// mapping lazily without locking, so two test classes serializing the same type for the first time in
// parallel can crash with "Collection was modified". The app itself only touches the DB from one thread.
// ponytail: whole suite runs serially (still a few seconds); group the LiteDB classes into one
// [Collection] instead if the suite ever gets slow.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ChastiMaid.Tests;

internal static class ReactiveUiSetup
{
    // ReactiveUI 24 refuses WhenAnyValue & co. until initialized; the app does it in Program (UseReactiveUI).
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }
}
