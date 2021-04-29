# Dataflow Hosting
An extension of `Microsoft.Extensions.Hosting` for use with `System.Threading.Tasks.Dataflow`.

### Features

* Provides base implementations of `IHostedService` using dataflow components.

## Installation

Add the NuGet package to your project:

    $ dotnet add package dataflow-hosting

## Usage

Implement a custom `DataflowService`:

```c#
class MyService : DataflowService
{
    protected override IDataflowBlock StartPipeline()
    {
        // Initialize one or more connected processing blocks.
        var processor = new ActionBlock<string>(name =>
        {
            Thread.Sleep(1000);
            Console.WriteLine($"Hello, {name}.");
        });

        // Start processing data.
        foreach (var name in new[] { "Jennifer", "Greg", "Sandy" })
        {
            processor.Post(name);
        }

        // Return the "head" block that will handle shutdown.
        return processor;
    }
}
```

Alternatively, implement `BatchingService<T>`:

```c#
class MyService : BatchingService<string>
{
    public MyService()
        : base (new BatchHostingOptions { BatchSize = 3 })
    {
        // Initialize application data source.
        var buffer = new BufferBlock<string>(
            new DataflowBlockOptions { CancellationToken = this.CancellationToken });

        foreach (var name in new[] { "Bill", "Suzy", "Dana", "Walt", "Abigail", "Montgomery", "Harold" })
        {
            buffer.Post(name);
        }

        this.Source = buffer;
    }

    protected override ISourceBlock<string> Source { get; }

    protected override Task ProcessBatch(IReadOnlyList<string> batch, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Hello, {string.Join(" and ", batch)}.");
        return Task.Delay(1000, cancellationToken);
    }
}
```

Register with host:

```c#
hostBuilder.ConfigureServices(x => x.AddHostedService<MyService>());
```
