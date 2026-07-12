using Windows.Foundation;

namespace ChastiMaid.Services;

/// <summary>
///     Converts WinRT <see cref="IAsyncOperation{TResult}" /> to a <see cref="Task{TResult}" /> by
///     hand via the <c>Completed</c> event. WinRT's own <c>.AsTask()</c> extension becomes ambiguous
///     to the compiler once both <c>Microsoft.Windows.SDK.Contracts</c> and the WinRT runtime are
///     referenced in the same project, so this avoids that overload entirely rather than working
///     around it with reflection.
/// </summary>
internal static class WinRtAsyncExtensions
{
    public static Task<TResult> ToTask<TResult>(IAsyncOperation<TResult> operation)
    {
        var tcs = new TaskCompletionSource<TResult>();
        operation.Completed = (op, status) =>
        {
            try
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult(op.GetResults());
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetCanceled();
                        break;
                    default:
                        tcs.TrySetException(op.ErrorCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };
        return tcs.Task;
    }

    public static Task ToTask(IAsyncAction action)
    {
        var tcs = new TaskCompletionSource();
        action.Completed = (op, status) =>
        {
            try
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult();
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetCanceled();
                        break;
                    default:
                        tcs.TrySetException(op.ErrorCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };
        return tcs.Task;
    }
}
