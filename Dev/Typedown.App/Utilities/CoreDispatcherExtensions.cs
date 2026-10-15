using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// WinUI 3 replacements for the UWP <c>CoreDispatcher</c> helpers.
    /// <see cref="DispatcherQueue"/> has no idle priority and no async overloads, so these
    /// adapter extensions keep the existing call sites (<c>RunIdleAsync</c> / <c>TryRunAsync</c>)
    /// working while routing through <c>TryEnqueue</c>.
    /// </summary>
    public static class CoreDispatcherExtensions
    {
        public static Task RunIdleAsync(this DispatcherQueue dispatcher, Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (dispatcher == null)
            {
                action();
                tcs.SetResult(true);
                return tcs.Task;
            }

            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>Runs an async lambda on the UI thread.</summary>
        public static Task RunIdleAsync(this DispatcherQueue dispatcher, Func<Task> action)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (dispatcher == null)
            {
                _ = InvokeAsync(action, tcs);
                return tcs.Task;
            }

            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, async () =>
            {
                await InvokeAsync(action, tcs);
            });
            return tcs.Task;
        }

        public static Task TryRunIdleAsync(this DispatcherQueue dispatcher, Action<object> action)
            => dispatcher.RunIdleAsync(() => action?.Invoke(null));

        public static Task TryRunAsync(this DispatcherQueue dispatcher, CoreDispatcherPriority priority, Action action)
            => dispatcher.TryRunAsync(ToQueuePriority(priority), action);

        public static Task TryRunAsync(this DispatcherQueue dispatcher, DispatcherQueuePriority priority, Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (dispatcher == null)
            {
                action();
                tcs.SetResult(true);
                return tcs.Task;
            }

            dispatcher.TryEnqueue(priority, () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        public static Task<T> RunAsync<T>(this DispatcherQueue dispatcher, Func<T> action,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var tcs = new TaskCompletionSource<T>();
            if (dispatcher == null)
            {
                tcs.SetResult(action());
                return tcs.Task;
            }

            dispatcher.TryEnqueue(ToQueuePriority(priority), () =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private static async Task InvokeAsync(Func<Task> action, TaskCompletionSource<bool> tcs)
        {
            try
            {
                await action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private static DispatcherQueuePriority ToQueuePriority(CoreDispatcherPriority priority) => priority switch
        {
            CoreDispatcherPriority.High => DispatcherQueuePriority.High,
            CoreDispatcherPriority.Low => DispatcherQueuePriority.Low,
            _ => DispatcherQueuePriority.Normal,
        };
    }

    /// <summary>Mirrors the UWP <c>CoreDispatcherPriority</c> values still referenced by call sites.</summary>
    public enum CoreDispatcherPriority
    {
        Low = -1,
        Normal = 0,
        High = 1,
    }
}
