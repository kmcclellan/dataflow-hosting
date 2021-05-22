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
        private static readonly DataflowLinkOptions LinkOptions = new() { PropagateCompletion = true };

        private readonly BatchHostingOptions options;
        private Timer? intervalTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchingService{T}"/> class.
        /// </summary>
        /// <param name="options">Options to configure hosting.</param>
        protected BatchingService(BatchHostingOptions? options = null)
        {
            this.options = options ?? new BatchHostingOptions();
        }

        /// <summary>
        /// Gets the source of data to be processed by the service.
        /// </summary>
        protected abstract ISourceBlock<T> Source { get; }

        /// <summary>
        /// Gets the downstream target for batch sizes (after processing), if any.
        /// </summary>
        protected virtual ITargetBlock<int>? Target => null;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.intervalTimer?.Dispose();
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
            var batcher = new BatchBlock<T>(
                this.options.BatchSize ?? int.MaxValue,
                new GroupingDataflowBlockOptions
                {
                    BoundedCapacity = this.options.BatchSize ?? DataflowBlockOptions.Unbounded,
                    CancellationToken = this.CancellationToken,
                });

            ITargetBlock<T[]> processor;
            var processorOptions = new ExecutionDataflowBlockOptions
            {
                SingleProducerConstrained = true,
                MaxDegreeOfParallelism = this.options.Parallelism ?? DataflowBlockOptions.Unbounded,
                BoundedCapacity = this.options.Parallelism ?? DataflowBlockOptions.Unbounded,
                CancellationToken = this.CancellationToken,
            };

            if (this.Target == null)
            {
                processor = new ActionBlock<T[]>(
                    GetDelegate(batcher, this.ProcessBatch),
                    processorOptions);
            }
            else
            {
                var transformer = new TransformBlock<T[], int>(
                    GetDelegate(
                        batcher,
                        (batch, ct) =>
                        {
                            this.ProcessBatch(batch, ct);
                            return batch.Count;
                        }),
                    processorOptions);

                processor = transformer;

                transformer.LinkTo(this.Target, LinkOptions);
                this.Processors.Add(this.Target);
            }

            batcher.LinkTo(processor, LinkOptions);
            this.Source.LinkTo(batcher, LinkOptions);

            this.Processors.Add(processor);
            this.Processors.Add(batcher);
            return this.Source;
        }

        private Func<IReadOnlyList<T>, TTask> GetDelegate<TTask>(
            BatchBlock<T> batcher,
            Func<IReadOnlyList<T>, CancellationToken, TTask> process)
        {
            if (this.options.BatchInterval != null)
            {
                this.intervalTimer = new Timer(
                    obj => ((BatchBlock<T>)obj!).TriggerBatch(),
                    batcher,
                    this.options.BatchInterval.Value,
                    this.options.BatchInterval.Value);

                return batch =>
                {
                    this.intervalTimer.Change(
                        this.options.BatchInterval.Value,
                        this.options.BatchInterval.Value);

                    return process(batch, this.CancellationToken);
                };
            }

            return batch => process(batch, this.CancellationToken);
        }
    }
}
