using System;
using System.Collections.Concurrent;

namespace OctaneEngine
{

        public sealed class EventfulConcurrentQueue<T>: ConcurrentQueue<T>
    {
            public ConcurrentQueue<T> Queue;

            public EventfulConcurrentQueue()
            {
                Queue = new ConcurrentQueue<T>();
            }

            public void Enqueue(T item)
            {
                Queue.Enqueue(item);
                OnItemEnqueued();
            }
            
            public int Count => Queue.Count;

            
            public bool TryDequeue(out T result)
            {
                var success = Queue.TryDequeue(out result);

                if (success)
                {
                    OnItemDequeued(result);
                }
                return success;
            }

            public event EventHandler ItemEnqueued;
            public event EventHandler<ItemDequeuedEventArgs<T>> ItemDequeued;

            void OnItemEnqueued()
            {
                ItemEnqueued?.Invoke(this, EventArgs.Empty);
            }

            void OnItemDequeued(T item)
            {
                ItemDequeued?.Invoke(this, new ItemDequeuedEventArgs<T> { Item = item });
            }
        }

    public sealed class ItemDequeuedEventArgs<T>: EventArgs
    {
        public T Item { get; set; }
    }
}
