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

        private ProgressState()
        {
        }

        public ProgressState(int count, IProgress<double> progress)
        {
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
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

        private void Report()
        {
            if (progress != null)
                progress.Report((double)index / count);
        }
    }
}
