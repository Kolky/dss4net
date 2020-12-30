using System.Collections.Concurrent;

namespace dss4net
{
    public class FixedSizeQueue<T> : ConcurrentQueue<T>
    {
        private readonly int limit;

        public FixedSizeQueue(int limit)
        {
            this.limit = limit;
        }

        public new void Enqueue(T element)
        {
            base.Enqueue(element);
            if (this.Count > limit)
            {
                this.TryDequeue(out T _);
            }
        }
    }
}
