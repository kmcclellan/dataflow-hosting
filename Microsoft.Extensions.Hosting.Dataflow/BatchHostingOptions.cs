namespace Microsoft.Extensions.Hosting.Dataflow
{
    using System;

    /// <summary>
    /// Options for hosting using batched dataflow processing.
    /// </summary>
    public class BatchHostingOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of simultaneous batches (<see langword="null"/> for unlimited).
        /// </summary>
        /// <remarks>
        /// Default is <c>1</c> (no parallelism).
        /// </remarks>
        public int? Parallelism { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of dataflow messages to process together (<see langword="null"/> for unlimited).
        /// </summary>
        /// <remarks>
        /// Default is <c>1</c> (no batching).
        /// </remarks>
        public int? BatchSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the longest timespan of dataflow messages to process together (<see langword="null"/> for unlimited).
        /// </summary>
        /// <remarks>
        /// Default is <see langword="null"/> (unlimited).
        /// </remarks>
        public TimeSpan? BatchInterval { get; set; }
    }
}
