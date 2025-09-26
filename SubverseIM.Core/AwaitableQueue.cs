namespace SubverseIM.Core
{
    public class AwaitableQueue<T>
    {
        private readonly Dictionary<Task, TaskCompletionSource<T>> _items;

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
                    itemTasks = _items.Values.Select(x => x.Task).ToArray();
                }

                cancellationToken.ThrowIfCancellationRequested();
                itemTask = await Task.WhenAny(itemTasks).WaitAsync(cancellationToken);
            }
            catch (ArgumentException)
            {
                TaskCompletionSource<T> item = new();
                lock (_items)
                {
                    _items.Add(item.Task, item);
                }

                itemTask = item.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            T resultItem = await itemTask.WaitAsync(cancellationToken);

            lock (_items)
            {
                _items.Remove(itemTask);
            }

            return resultItem;
        }

        public void Enqueue(T value)
        {
            lock (_items)
            {
                bool shouldAddFlag = true;
                if (_items.Count > 0)
                {
                    foreach (TaskCompletionSource<T> item in _items.Values)
                    {
                        if (item.TrySetResult(value))
                        {
                            shouldAddFlag = false;
                            break;
                        }
                    }
                }

                if (shouldAddFlag)
                {
                    TaskCompletionSource<T> item = new();
                    item.SetResult(value);

                    _items.Add(item.Task, item);
                }
            }
        }
    }
}
