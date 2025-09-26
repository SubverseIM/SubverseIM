namespace SubverseIM.Core
{
    public class AwaitableQueue<T>
    {
        private readonly HashSet<TaskCompletionSource<T>> _items;

        public AwaitableQueue()
        {
            _items = new();
        }

        public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            Task<T> itemTask;
            try
            {
                Task<T>[] itemTasks;
                lock (_items)
                {
                    itemTasks = _items.Select(x => x.Task).ToArray();
                }

                cancellationToken.ThrowIfCancellationRequested();
                itemTask = await Task.WhenAny(itemTasks).WaitAsync(cancellationToken);
            }
            catch (ArgumentException)
            {
                TaskCompletionSource<T> tcs;
                lock (_items)
                {
                    _items.Add(tcs = new());
                }

                itemTask = tcs.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            T resultItem = await itemTask.WaitAsync(cancellationToken);

            lock (_items)
            {
                _items.RemoveWhere(x => x.Task == itemTask);
            }

            return resultItem;
        }

        public void Enqueue(T item)
        {
            lock (_items)
            {
                bool shouldAddFlag = true;
                if (_items.Count > 0)
                {
                    foreach (TaskCompletionSource<T> tcs in _items)
                    {
                        if (tcs.TrySetResult(item))
                        {
                            shouldAddFlag = false;
                            break;
                        }
                    }
                }

                if (shouldAddFlag)
                {
                    TaskCompletionSource<T> tcs = new();
                    tcs.SetResult(item);

                    _items.Add(tcs);
                }
            }
        }
    }
}
