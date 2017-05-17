using System;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    sealed class ProgressState
    {
        public static ProgressState Empty = new ProgressState();

        private readonly IProgress<double> progress;
        private readonly object @lock = new object();
        private readonly int count;
        private int index;
        private int completed;

        private ProgressState()
        {
        }

        public ProgressState(int count, IProgress<double> progress)
        {
            this.count = count;
            this.progress = progress;
        }

        public void Update()
        {
            Interlocked.Increment(ref index);
            Report();
        }

        public void Reset()
        {
            Interlocked.Exchange(ref index, 0);
            Report();
        }

        public void SetCompleted()
        {
            Interlocked.Increment(ref completed);
        }

        public bool IsCompleted => completed > 0;

        private void Report()
        {
            if (progress != null)
                progress.Report((double)index / count);
        }
    }
}
