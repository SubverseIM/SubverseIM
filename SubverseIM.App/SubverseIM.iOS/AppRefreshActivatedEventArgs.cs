using Avalonia.Controls.ApplicationLifetimes;
using BackgroundTasks;

namespace SubverseIM.iOS
{
    internal class AppRefreshActivatedEventArgs : ActivatedEventArgs
    {
        public BGAppRefreshTask RefreshTask { get; }

        public AppRefreshActivatedEventArgs(BGAppRefreshTask refreshTask) : base(ActivationKind.Background)
        {
            RefreshTask = refreshTask;
        }
    }
}