using System;
using System.Collections.Concurrent;

namespace OctaneEngine
{
    public class ObjectPool<T>: IDisposable
    {
        private ConcurrentBag<T>? _objects;
        private readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public T Get()
        {
            return _objects != null && _objects.TryTake(out var item) ? item : _objectGenerator();
        }

        public void Return(T item)
        {
            _objects!.Add(item);
        }

        public void Empty()
        {
#if NET461 || NET472
            T? ignored;
            while (_objects.TryTake(out ignored));
#else
            _objects.Clear();
#endif

            _objects = null;
        }

        public void Dispose()
        {
            Empty();
        }
    }
}