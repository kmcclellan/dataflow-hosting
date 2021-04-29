namespace Microsoft.Extensions.Hosting.Dataflow
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Base class for implementing <see cref="IHostedService"/> using batched dataflow processing.
    /// </summary>
    /// <typeparam name="T">The type of data processed by the service.</typeparam>
    public abstract class BatchingService<T> : DataflowService
    {
        private readonly BatchBlock<T> batcher;
        private readonly ActionBlock<T[]> processor;
        private readonly Timer intervalTimer;
        private readonly BatchHostingOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchingService{T}"/> class.
        /// </summary>
        /// <param name="options">Options to configure hosting.</param>
        protected BatchingService(BatchHostingOptions? options = null)
        {
            this.options = options ?? new BatchHostingOptions();

            this.batcher = new BatchBlock<T>(
                this.options.BatchSize ?? int.MaxValue,
                new GroupingDataflowBlockOptions
                {
                    BoundedCapacity = this.options.BatchSize ?? DataflowBlockOptions.Unbounded,
                    CancellationToken = this.CancellationToken,
                });

            this.intervalTimer = new Timer(
                obj => ((BatchBlock<T>)obj!).TriggerBatch(),
                this.batcher,
                Timeout.Infinite,
                Timeout.Infinite);

            this.processor = new ActionBlock<T[]>(
                async batch =>
                {
                    this.SetInterval();

                    try
                    {
                        await this.ProcessBatch(batch, this.CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (this.CancellationToken.IsCancellationRequested)
                    {
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    SingleProducerConstrained = true,
                    MaxDegreeOfParallelism = this.options.Parallelism ?? DataflowBlockOptions.Unbounded,
                    BoundedCapacity = this.options.Parallelism ?? DataflowBlockOptions.Unbounded,
                    CancellationToken = this.CancellationToken,
                });

            this.Processors.Add(this.processor);
            this.Processors.Add(this.batcher);
        }

        /// <summary>
        /// Gets the source of data to be processed by the service.
        /// </summary>
        protected abstract ISourceBlock<T> Source { get; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.intervalTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Processes a batch of dataflow messages.
        /// </summary>
        /// <remarks>
        /// Invoked according to the service's <see cref="BatchHostingOptions"/>.
        /// </remarks>
        /// <param name="batch">The source batch.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected abstract Task ProcessBatch(IReadOnlyList<T> batch, CancellationToken cancellationToken);

        /// <inheritdoc/>
        protected override IDataflowBlock StartPipeline()
        {
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            this.batcher.LinkTo(this.processor, linkOptions);
            this.Source.LinkTo(this.batcher, linkOptions);

            this.SetInterval();

            return this.Source;
        }

        private void SetInterval()
        {
            if (this.options.BatchInterval != null)
            {
                this.intervalTimer.Change(this.options.BatchInterval.Value, this.options.BatchInterval.Value);
            }
        }
    }
}
