namespace Microsoft.Extensions.Hosting.Dataflow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Base class for implementing <see cref="IHostedService"/> using dataflow components.
    /// </summary>
    public abstract class DataflowService : IHostedService, IDisposable
    {
        private readonly CancellationTokenSource cancellationSource = new();
        private IDataflowBlock? head;

        /// <summary>
        /// Gets the collection of dataflow blocks involved in processing.
        /// </summary>
        /// <remarks>
        /// Added blocks have their faults propagated and are allowed to complete during shutdown.
        /// </remarks>
        protected ICollection<IDataflowBlock> Processors { get; } = new HashSet<IDataflowBlock>();

        /// <summary>
        /// Gets a token triggered when the service is shutting down ungracefully.
        /// </summary>
        /// <remarks>
        /// Should be passed to <see cref="DataflowBlockOptions.CancellationToken"/>.
        /// </remarks>
        protected CancellationToken CancellationToken => this.cancellationSource.Token;

        /// <inheritdoc/>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            this.head = this.StartPipeline();
            this.Processors.Add(this.head);

            foreach (var block in this.Processors.Where(x => x != this.head))
            {
                // Propagate faults upstream.
                block.Completion.ContinueWith(
                    (task, obj) => ((IDataflowBlock)obj!).Fault(task.Exception!),
                    this.head,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            if (this.head == null)
            {
                throw new InvalidOperationException("Not started");
            }

            this.head.Complete();
            cancellationToken.Register(obj => ((CancellationTokenSource)obj!).Cancel(), this.cancellationSource);

            return Task.WhenAll(this.Processors.Select(x => x.Completion));
        }

        /// <summary>
        /// Initializes dataflow components and begins processing.
        /// </summary>
        /// <remarks>
        /// <para>One block should function as the pipeline "head," receiving and propagating shutdown via <see cref="IDataflowBlock.Complete"/> or <see cref="IDataflowBlock.Fault"/>.</para>
        /// <para>Additional blocks should be added to <see cref="Processors"/>.</para>
        /// </remarks>
        /// <returns>The pipeline head.</returns>
        protected abstract IDataflowBlock StartPipeline();

        /// <summary>
        /// Disposes and/or releases resources used by the service.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cancellationSource.Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
