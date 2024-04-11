# 创建可观察序列

在前一章中，我们看到了两个基本的Rx接口，`IObservable<T>`和`IObserver<T>`。我们还看到了如何通过实现`IObserver<T>`来接收事件，以及如何使用`System.Reactive`包提供的实现。在本章中，我们将看到如何创建`IObservable<T>`源，以表示应用程序中感兴趣的源事件。

我们将从直接实现`IObservable<T>`开始。实际上，这样做相对不常见，因此我们接下来将探讨如何使用`System.Reactive`提供的实现，这些实现可以为你完成大部分工作。

## 一个非常基础的`IObservable<T>`实现

下面是一个产生一系列数字的`IObservable<int>`的实现：

```csharp
public class MySequenceOfNumbers : IObservable<int>
{
    public IDisposable Subscribe(IObserver<int> observer)
    {
        observer.OnNext(1);
        observer.OnNext(2);
        observer.OnNext(3);
        observer.OnCompleted();
        return System.Reactive.Disposables.Disposable.Empty; // 方便的无操作IDisposable
    }
}
```

我们可以通过构造它的一个实例，然后订阅它来测试这一点：

```csharp
var numbers = new MySequenceOfNumbers();
numbers.Subscribe(
    number => Console.WriteLine($"Received value: {number}"),
    () => Console.WriteLine("Sequence terminated"));
```

这将产生以下输出：

```
Received value 1
Received value 2
Received value 3
Sequence terminated
```

虽然`MySequenceOfNumbers`在技术上是`IObservable<int>`的正确实现，但它过于简单，难以派上用场。首先，我们通常在有感兴趣的事件时使用Rx，但这并不真正具备反应性——它只是立即产生一组固定的数字。此外，实现是阻塞的——它甚至在生产完所有值之后才从`Subscribe`返回。这个例子说明了源是如何向订阅者提供事件的基本原理，但如果我们只是想表示一个预定的数字序列，我们可能还是使用`IEnumerable<T>`实现，例如`List<T>`或数组更为合适。

## 在 Rx 中表示文件系统事件

让我们看一个更现实一些的例子。这是一个围绕 .NET 的 `FileSystemWatcher` 的封装，它将文件系统更改通知呈现为一个 `IObservable<FileSystemEventArgs>`。（注意：这不一定是最佳设计的 Rx `FileSystemWatcher` 封装。监视器提供了几种不同类型的更改事件，其中一个，`Renamed`，提供了作为 `RenamedEventArgs` 的详细信息。这是从 `FileSystemEventArgs` 派生的，所以把所有事件合并到一个事件流中是可行的，但对于想要访问重命名事件详细信息的应用程序而言，这会很不便。一个更严重的设计问题是，这无法报告 `FileSystemWatcher.Error` 中的多个事件。这种错误可能是短暂的并且可以恢复，这种情况下应用程序可能想要继续运行，但由于这个类选择用单一的 `IObservable<T>` 表示一切，它通过调用观察者的 `OnError` 来报告错误，这时 Rx 的规则要求我们停止。可以使用 Rx 的 `Retry` 操作符进行解决，它可以在错误后自动重新订阅，但可能更好的做法是提供一个单独的 `IObservable<ErrorEventArgs>`，这样我们可以以非终止方式报告错误。然而，增加的复杂性并非总是必要的。这种设计的简单性意味着它将适合某些应用程序。正如软件设计常有的方式，没有一种适合所有情况的方法。）

```csharp
// 将文件系统变化表示为 Rx 可观察序列。
// 注意：这是一个为了说明目的而过度简化的示例。
//       它未能高效处理多个订阅者，未使用 IScheduler，
//       并且在首个错误后会立即停止。
public class RxFsEvents : IObservable<FileSystemEventArgs>
{
    private readonly string folder;

    public RxFsEvents(string folder)
    {
        this.folder = folder;
    }

    public IDisposable Subscribe(IObserver<FileSystemEventArgs> observer)
    {







        // 如果我们获得多个订阅者，这将是低效的。
        FileSystemWatcher watcher = new(this.folder);






        // FileSystemWatcher 文档并未说明它在哪个线程上引发事件（除非你使用它的 SynchronizationObject，
        // 该对象与 Windows Forms 很好地集成，但在这里使用不便）也没有保证在我们完成处理一个事件之前不会交付下一个事件。
        // MacOS、Windows 和 Linux 的实现都有显著不同，因此依赖于文档未保证的行为是不明智的。（实际上，在 .NET 7
        // 的 Win32 实现上，它确实似乎会等到每个事件处理程序返回后再交付下一个事件，所以我们可能可以暂时忽略这个问题。
        // 仅限于 Windows。实际上 Linux 实现为此任务专门使用了一个线程，但源代码中有一条评论说这应该改变，这是只依赖
        // 文档行为的另一个理由）。因此，确保我们遵守 IObserver<T> 规则是我们的问题。首先，我们需要确保我们一次只调用
        // 观察者一次。更真实的例子会使用 Rx 的 IScheduler，但既然我们还没有解释这是什么，我们只是使用 lock 这个对象。
        object sync = new();






        // 更微妙的是，FileSystemWatcher 文档没有明确是否在报告错误后可能继续获得更多更改事件。由于没有针对线程的承诺，
        // 可能存在导致我们在 FileSystemWatcher 报告错误后尝试处理事件的竞争条件。因此，如果我们已经调用过 OnError，
        // 我们需要记住这点，以确保我们不违反 IObserver<T> 规则。
        bool onErrorAlreadyCalled = false;

        void SendToObserver(object _, FileSystemEventArgs e)
        {
            lock (sync)
            {
                if (!onErrorAlreadyCalled)
                {
                    observer.OnNext(e); 
                }
            }
        }

        watcher.Created += SendToObserver;
        watcher.Changed += SendToObserver;
        watcher.Renamed += SendToObserver;
        watcher.Deleted += SendToObserver;

        watcher.Error += (_, e) =>
        {
            lock (sync)
            {
                // FileSystemWatcher 可能会报告多个错误，但
                // 我们只能向 IObservable<T> 报告一个。
                if (!onErrorAlreadyCalled)
                {
                    observer.OnError(e.GetException());
                    onErrorAlreadyCalled = true; 
                    watcher.Dispose();
                }
            }
        };

        watcher.EnableRaisingEvents = true;

        return watcher;
    }
}
```

这很快变得复杂。这说明了 `IObservable<T>` 的实现需要遵守 `IObserver<T>` 的规则。这通常是件好事：它将并发性的复杂问题限制在一个地方。任何我订阅这个 `RxFsEvents` 的 `IObserver<FileSystemEventArgs>` 都不必担心并发性问题，因为它可以依赖 `IObserver<T>` 规则，这保证它一次只处理一件事。如果源代码没有要求我强制执行这些规则，我的 `RxFsEvents` 类可能会简单些，但是处理重叠事件的所有复杂性将蔓延到处理事件的代码中。并发性已经足够难处理了，一旦其影响扩散到多种类型，几乎就不可能合理地推理了。Rx 的 `IObserver<T>` 规则防止了这种情况的发生。

（注：这是 Rx 的一个重要特征。规则为观察者保持了简单性。随着您的事件源或事件处理的复杂性增加，这一点变得越来越重要。）

这段代码有几个问题（除了已经提到的 API 设计问题之外）。其中一个问题是，在 `IObservable<T>` 的实现产生事件时，模拟实际异步活动（例如文件系统变更）时，应用程序通常希望能够控制通知在哪个线程上到达。例如，UI 框架倾向于有线程亲和性要求。你通常需要在特定线程上才能更新用户界面。Rx 提供了机制来将通知重定向到不同的调度器上，我们可以绕过这个问题，但我们通常期望能够为这种观察者提供一个 `IScheduler`，并通过它来传递通知。我们将在后续章节中讨论调度器。

另一个问题是这段代码没有高效地处理多个订阅者。您可以多次调用 `IObservable<T>.Subscribe`，如果您这样做，每次都会创建一个新的 `FileSystemWatcher`。这可能比您想象的更容易发生。假设我们有一个这样的观察者实例，并希望以不同的方式处理不同的事件。我们可能会使用 `Where` 操作符来定义可观察源，按我们想要的方式分隔事件：

```csharp
IObservable<FileSystemEventArgs> configChanges =
    fs.Where(e => Path.GetExtension(e.Name) == ".config");
IObservable<FileSystemEventArgs> deletions =
    fs.Where(e => e.ChangeType == WatcherChangeTypes.Deleted);
```

当您在 `Where` 操作符返回的 `IObservable<T>` 上调用 `Subscribe` 时，它将调用其输入上的 `Subscribe`。因此，在这种情况下，如果我们分别调用 `configChanges` 和 `deletions` 上的 `Subscribe`，这将导致对 `fs` 的 _两_ 次调用 `Subscribe`。因此，如果 `fs` 是我们上面的 `RxFsEvents` 类型的实例，每次都会构造它自己的 `FileSystemEventWatcher`，这是低效的。

Rx 提供了几种处理这种情况的方法。它提供了专门设计的操作符，可以包装一个不容忍多个订阅者的 `IObservable<T>`，并将其包装在适配器中：

```csharp
IObservable<FileSystemEventArgs> fs =
    new RxFsEvents(@"c:\temp")
    .Publish()
    .RefCount();
```

但这是向前走了。（这些操作符在[发布操作符章节](15_PublishingOperators.md)中有描述。）如果您想构建一个本质上友好于多订阅者的类型，您实际上需要做的就是跟踪您所有的订阅者并在循环中通知他们。以下是修改版的文件系统观察者：

```csharp
public class RxFsEventsMultiSubscriber : IObservable<FileSystemEventArgs>
{
    private readonly object sync = new();
    private readonly List<Subscription> subscribers = new();
    private readonly FileSystemWatcher watcher;

    public RxFsEventsMultiSubscriber(string folder)
    {
        this.watcher = new FileSystemWatcher(folder);

        watcher.Created += SendEventToObservers;
        watcher.Changed += SendEventToObservers;
        watcher.Renamed += SendEventToObservers;
        watcher.Deleted += SendEventToObservers;

        watcher.Error += SendErrorToObservers;
    }

    public IDisposable Subscribe(IObserver<FileSystemEventArgs> observer)
    {
        Subscription sub = new(this, observer);
        lock (this.sync)
        {
            this.subscribers.Add(sub); 

            if (this.subscribers.Count == 1)
            {
                // 我们之前没有订阅者，但现在我们有了，
                // 所以我们需要启动 FileSystemWatcher。
                watcher.EnableRaisingEvents = true;
            }
        }

        return sub;
    }

    private void Unsubscribe(Subscription sub)
    {
        lock (this.sync)
        {
            this.subscribers.Remove(sub);

            if (this.subscribers.Count == 0)
            {
                watcher.EnableRaisingEvents = false;
            }
        }
    }

    void SendEventToObservers(object _, FileSystemEventArgs e)
    {
        lock (this.sync)
        {
            foreach (var subscription in this.subscribers)
            {
                subscription.Observer.OnNext(e);
            }
        }
    }

    void SendErrorToObservers(object _, ErrorEventArgs e)
    {
        Exception x = e.GetException();
        lock (this.sync)
        {
            foreach (var subscription in this.subscribers)
            {
                subscription.Observer.OnError(x);
            }

            this.subscribers.Clear();
        }
    }

    private class Subscription : IDisposable
    {
        private RxFsEventsMultiSubscriber? parent;

        public Subscription(
            RxFsEventsMultiSubscriber rxFsEventsMultiSubscriber,
            IObserver<FileSystemEventArgs> observer)
        {
            this.parent = rxFsEventsMultiSubscriber;
            this.Observer = observer;
        }
        
        public IObserver<FileSystemEventArgs> Observer { get; }

        public void Dispose()
        {
            this.parent?.Unsubscribe(this);
            this.parent = null;
        }
    }
}
```

这创建了只有一个 `FileSystemWatcher` 实例，无论 `Subscribe` 被调用多少次。注意我不得不引入一个嵌套类来提供 `Subscribe` 返回的 `IDisposable`。我在本章的最初 `IObservable<T>` 实现中不需要这样做，因为它在返回之前就已经完成了序列，所以它能够方便地返回 Rx 提供的 `Disposable.Empty` 属性。（这在你必须提供一个 `IDisposable`，但实际上不需要在处置时做任何事情的情况下很方便。）而且在我的第一个 `FileSystemWatcher` 包装器 `RxFsEvents` 中，我直接从 `Dispose` 返回 `FileSystemWatcher`。（这有效是因为 `FileSystemWatcher.Dispose` 关闭了观察器，每个订阅者都得到了自己的 `FileSystemWatcher`。）但现在一个 `FileSystemWatcher` 支持多个观察者，当观察者取消订阅时我们需要做更多的工作。

当我们从 `Subscribe` 返回的 `Subscription` 实例被处置时，它会从订阅者列表中移除自己，确保它不会再接收任何通知。如果没有更多的订阅者，它还会将 `FileSystemWatcher` 的 `EnableRaisingEvents` 设置为 false，确保如果当前没有需要通知的对象，则此源不会做不必要的工作。

这看起来比第一个例子更现实。这确实是可能在任何时刻发生的事件的来源（使其非常适合 Rx），而且现在它能智能地处理多个订阅者。然而，我们通常不会以这种方式编写东西。我们在这里自己做所有工作——这段代码甚至不需要引用 `System.Reactive` 包，因为它引用的唯一 Rx 类型是 `IObservable<T>` 和 `IObserver<T>`，这两者都内置于 .NET 运行时库中。实际上我们通常将实现 `IObservable<T>` 的工作委托给 `System.Reactive`，因为它可以为我们做很多工作。

例如，假设我们只关心 `Changed` 事件。我们可以这样写：

```csharp
FileSystemWatcher watcher = new (@"c:\temp");
IObservable<FileSystemEventArgs> changes = Observable
    .FromEventPattern<FileSystemEventArgs>(watcher, nameof(watcher.Changed))
    .Select(ep => ep.EventArgs);
watcher.EnableRaisingEvents = true;
```

这里我们使用了 `System.Reactive` 库的 `Observable` 类中的 `FromEventPattern` 辅助函数，它可以用来从符合常规模式的任何 .NET 事件（在此事件处理函数接受两个参数：一种是类型为 `object` 的发送者，然后是包含有关事件的信息的派生自 `EventArgs` 的类型）构建 `IObservable<T>`。这不像之前的示例那样灵活。它只报告其中一个事件，并且我们必须手动启动（并且如果需要的话停止）`FileSystemWatcher`。但对于某些应用程序来说这已经足够好了，而且这需要编写的代码要少得多。如果我们的目标是编写一个适用于许多不同情况的完整功能的 `FileSystemWatcher` 包装器，编写一个专门的 `IObservable<T>` 实现（如前所示）可能是值得的。（我们可以轻松扩展这最后一个示例以监视所有事件。我们只需为每个事件使用一次 `FromEventPattern`，然后使用 `Observable.Merge` 将四个结果合成一个。我们从完整自定义实现中获得的唯一真正好处是我们可以根据当前是否有观察者自动启动和停止 `FileSystemWatcher`。）但如果我们只需要将某些事件表示为 `IObservable<T>`，以便我们可以在应用程序中使用它们，我们可以使用这种更简单的方法。

实践中，我们几乎总是让 `System.Reactive` 为我们实现 `IObservable<T>`。即使我们想要控制某些方面（例如在这些示例中自动启动和关闭 `FileSystemWatcher`）我们几乎总是能找到一种操作符组合来实现这一功能。以下代码使用 `System.Reactive` 中的各种方法返回一个 `IObservable<FileSystemEventArgs>`，其功能与以上完全编写的 `RxFsEventsMultiSubscriber` 相同，但代码大为简化。

```csharp
IObservable<FileSystemEventArgs> ObserveFileSystem(string folder)
{
    return 
        // Observable.Defer 使我们能够
        // 避免在有订阅者之前做任何工作。
        Observable.Defer(() =>
            {
                FileSystemWatcher fsw = new(folder);
                fsw.EnableRaisingEvents = true;

                return Observable.Return(fsw);
            })
        // 一旦前面的部分发出 FileSystemWatcher
        //（当有人首次订阅时会发生），
        // 我们希望将所有事件包装为 IObservable<T>，
        // 为此我们使用投影。为了避免得到一个
        // IObservable<IObservable<FileSystemEventArgs>>，
        // 我们使用 SelectMany，它可以通过一层有效地将其展平。
        .SelectMany(fsw =>
            Observable.Merge(new[]
                {
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Created += h, h => fsw.Created -= h),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Changed += h, h => fsw.Changed -= h),
                    Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(
                        h => fsw.Renamed += h, h => fsw.Renamed -= h),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Deleted += h, h => fsw.Deleted -= h)
                })
            // FromEventPattern 提供发
            // 送者和事件参数。只提取后者。
            .Select(ep => ep.EventArgs)
            // Finally 这里确保一旦我们没
            // 有订阅者，观察器就会被关闭。
            .Finally(() => fsw.Dispose()))
        // 这种 Publish 和 RefCount 的组合意味着
        // 多个订阅者会共享一个 FileSystemWatcher，
        // 但是如果所有订阅者取消订阅，那么它将被关闭。
        .Publish()
        .RefCount();
}
```

这里使用了许多方法，其中大多数我之前还没有讨论过。为了使这个例子有意义，我显然需要开始描述 `System.Reactive` 包如何为您实现 `IObservable<T>`。

## 简单工厂方法

由于可用于创建可观察序列的方法数量众多，我们将它们分为几类。我们首先介绍的这一类方法用于创建最多产生单个结果的 `IObservable<T>` 序列。

### Observable.Return

最简单的工厂方法之一是 `Observable.Return<T>(T value)`，你可能已经在前一章的 `Quiescent` 示例中见过了。此方法接受一个类型为 `T` 的值，返回一个 `IObservable<T>`，它将产生这个单个值然后完成。从某种意义上来说，这将一个值 _包装_ 在一个 `IObservable<T>` 中；它在概念上类似于编写 `new T[] { value }`，就是说它是一个只包含一个元素的序列。你也可以将其视为 Rx 版本的 `Task.FromResult`，当你有一个某种类型 `T` 的值，并需要将其传递给一个需要 `Task<T>` 的东西时，你可以使用它。

```csharp
IObservable<string> singleValue = Observable.Return<string>("Value");
```

我为了清晰起见指定了类型参数，但这不是必需的，因为编译器可以根据提供的参数推断出类型：

```csharp
IObservable<string> singleValue = Observable.Return("Value");
```

`Return` 生成一个冷可观察对象：每个订阅者在订阅时立即接收该值。（[热和冷可观察对象](02_KeyTypes.md#hot-and-cold-sources)已在前一章中描述。）

### Observable.Empty

有时候拥有一个空序列可能很有用。.NET 的 `Enumerable.Empty<T>()` 就是为 `IEnumerable<T>` 实现这一功能，而 Rx 有一个直接的等价物，即 `Observable.Empty<T>()`，它返回一个空的 `IObservable<T>`。我们需要提供类型参数，因为没有值让编译器能从中推断出类型。

```csharp
IObservable<string> empty = Observable.Empty<string>();
```

实际上，一个空序列是指对任何订阅者立即调用 `OnCompleted` 的序列。

与 `IEnumerable<T>` 相比，这仅仅是 Rx 版本的空列表，但还有另一种看待它的方式。Rx 是一种强大的异步处理模型，所以你可以将其看作是一个立即完成而不产生任何结果的任务——因此它在概念上类似于 `Task.CompletedTask`。（这与 `Observable.Return` 和 `Task.FromResult` 之间的类比不太一样，因为在那种情况下，我们是在比较一个 `IObservable<T>` 和一个 `Task<T>`，而在这里，我们比较的是一个 `IObservable<T>` 和一个 `Task`——任务唯一能完成而不产生任何东西的方式是如果我们使用非泛型版本的 `Task`。）

### Observable.Never

`Observable.Never<T>()` 方法返回一个序列，这个序列和 `Empty` 一样，不产生任何值，但与 `Empty` 不同的是，它永远不会结束。实际上，这意味着它永远不会在订阅者上调用任何方法（`OnNext`、`OnCompleted` 和 `OnError` 都不会调用）。虽然 `Observable.Empty<T>()` 会立即完成，`Observable.Never<T>` 则具有无限期的持续时间。

```csharp
IObservable<string> never = Observable.Never<string>();
```

这种用法的实用性可能不是很明显。我在上一章中给出了一个可能的用途：你可以在测试中使用它来模拟一个不产生任何值的源，也许是为了让你的测试验证超时逻辑。

它也可以用在我们使用观察者模式表示基于时间的信息的地方。有时我们并不真正关心从观察者模式中出现何种值；我们可能只关心 _何时_ 发生了某事（任何事）。（我们在前一章中看到了这种“纯粹用于定时目的的观察者序列”概念的一个例子，尽管在那个特定场景中使用 `Never` 不合适。`Quiescent` 示例使用了 `Buffer` 操作符，它操作两个观察者序列：第一个包含感兴趣的项目，第二个纯粹用于确定如何将第一个序列切分成块。`Buffer` 并不处理第二个观察者生成的值：它只关注 _何时_ 有值出现，每当第二个观察者产生一个值时就完成前一个块。而且如果我们在表示时间信息时，有时候表示某个事件永远不会发生的概念，可能会有用。）

作为一个你可能想要将 `Never` 用于定时目的的例子，假设你正在使用一个基于 Rx 的库，该库提供了一个超时机制，当某个超时发生时，某个操作将被取消，而超时本身被模拟为一个观察者序列。如果由于某种原因你不想要超时，只是想无限期地等待，那么你可以指定超时为 `Observable.Never`。

### Observable.Throw

`Observable.Throw<T>(Exception)` 返回一个序列，该序列立即向任何订阅者报告一个错误。与 `Empty` 和 `Never` 一样，我们不向这个方法提供值（只提供一个异常），所以我们需要提供一个类型参数，以便它知道在返回的 `IObservable<T>` 中使用什么 `T`。（它实际上永远不会产生一个 `T`，但你不能有一个 `IObservable<T>` 实例而不为 `T` 指定一个特定的类型。）

```csharp
IObservable<string> throws = Observable.Throw<string>(new Exception()); 
```

### Observable.Create

`Create`工厂方法比其他创建方法更为强大，因为它可以用来创建任何类型的序列。你可以用`Observable.Create`实现前面提到的任何四种方法。
这个方法的签名乍一看可能比必要的更复杂，但一旦习惯了就会觉得很自然。

```csharp
// 从指定的Subscribe方法实现中创建一个可观察序列。
public static IObservable<TSource> Create<TSource>(
    Func<IObserver<TSource>, IDisposable> subscribe)
{...}
public static IObservable<TSource> Create<TSource>(
    Func<IObserver<TSource>, Action> subscribe)
{...}
```

你需要提供一个委托，每次订阅时都会执行这个委托。你的委托将接收一个`IObserver<T>`。从逻辑上讲，这代表了传递给`Subscribe`方法的观察者，尽管实际上出于各种原因，Rx 会在其周围加上一个包装层。你可以根据需要调用`OnNext`/`OnError`/`OnCompleted`方法。这是你直接使用`IObserver<T>`接口的少数几种情况之一。下面是一个简单的示例，生成三个项目：

```csharp
private IObservable<int> SomeNumbers()
{
    return Observable.Create<int>(
        (IObserver<int> observer) =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
            observer.OnNext(3);
            observer.OnCompleted();

            return Disposable.Empty;
        });
}
```

你的委托必须返回一个`IDisposable`或一个`Action`，以启用取消订阅。当订阅者处理他们的订阅以取消订阅时，Rx 将调用你返回的`IDisposable`的`Dispose()`方法，或者如果你返回了一个`Action`，它将调用该方法。

这个示例让人想起本章开始时的`MySequenceOfNumbers`示例，因为它立即产生了一些固定的值。这种情况的主要区别在于Rx添加了一些包装层，可以处理诸如重入等尴尬情况。有时Rx会自动推迟工作以防止死锁，因此，消费由该方法返回的`IObservable<int>`的代码可能会看到对`Subscribe`的调用在上面的代码回调运行之前返回，在这种情况下，他们可能会在他们的`OnNext`处理程序内部取消订阅。

下图是一个序列图，显示了这种情况如何在实际中发生。假设由`SomeNumbers`返回的`IObservable<int>`已经被Rx以某种方式包装，确保订阅发生在某个不同的执行上下文中。我们通常使用合适的[调度器](11_SchedulingAndThreading.md#schedulers)来确定上下文。（[`SubscribeOn`](11_SchedulingAndThreading.md#subscribeon-and-observeon) 操作符创建了这样的包装。）我们可能使用[`TaskPoolScheduler`](11_SchedulingAndThreading.md#taskpoolscheduler)，以确保订阅发生在某个任务池线程上。因此，当我们的应用程序代码调用`Subscribe`时，包装的`IObservable<int>`不会立即订阅底层的可观察对象。相反，它会将一个工作项排队到调度器以执行该工作，然后立即返回而不等待该工作运行。这就是我们的订阅者可以在`Observable.Create`调用我们的回调之前拥有代表订阅的`IDisposable`的方式。图中显示订阅者然后使此可供观察者使用。

![A sequence diagram with 6 participants: Subscriber, Rx IObservable Wrapper, Scheduler, Observable.Create, Rx IObserver Wrapper, and Observer. It shows the following messages. Subscriber sends "Subscribe()" to Rx IObservable Wrapper. Rx IObservable Wrapper sends "Schedule Subscribe()" to Scheduler. Rx IObservable Wrapper returns "IDisposable (subscription)" to Subscriber. Subscriber sends "Set subscription IDisposable" to Observer. Scheduler sends "Subscribe()" to Observable.Create. Observable.Create sends "OnNext(1)" to Rx IObserver Wrapper. Rx IObserver Wrapper sends "OnNext(1)" to Observer. Observable.Create sends "OnNext(2)" to Rx IObserver Wrapper. Rx IObserver Wrapper sends "OnNext(2)" to Observer. Observer sends "subscription.Dispose()" to Rx IObservable Wrapper. Observable.Create sends "OnNext(3)" to Rx IObserver Wrapper. Observable.Create sends "OnCompleted()" to Rx IObserver Wrapper.](GraphicsIntro/Ch03-Sequence-CreateWrappers.svg)

图显示调度员在此之后调用底层可观察对象上的`Subscribe`，这将意味着我们传递给`Observable.Create<int>`的回调现在将运行。我们的回调调用`OnNext`，但它传递的不是真正的观察者：而是传递了另一个Rx生成的包装。最初，该包装将调用直接转发到真正的观察者，但我们的图显示，当真正的观察者（最右边的那个）接收到它的第二个调用（`OnNext(2)`）时，它通过调用其订阅的`IDisposable`上的`Dispose`来取消订阅。这里的两个包装——`IObservable`和`IObserver`包装——是相连的，所以当我们从`IObservable`包装取消订阅时，它会告诉`IObserver`包装订阅正在被关闭。这意味着当我们的`Observable.Create<int>`回调在`IObserver`包装上调用`OnNext(3)`时，该包装_不会_将它转发给真正的观察者，因为它知道该观察者已经取消订阅了。（它也不会转发`OnCompleted`，原因相同。）

你可能想知道我们返回给`Observable.Create`的`IDisposable`如何才能真正发挥作用。这是回调的返回值，所以我们只能在回调的最后将其返回给Rx。我们不总是在返回时就已经完成了我们的工作，所以没有什么可取消的吗？不一定——我们可能在返回后启动一些继续运行的工作。接下来的这个例子就是这样做的，这意味着它返回的取消订阅操作能够发挥有用的作用：它设置了一个被循环观察的可观察输出生成的取消令牌。（这返回了一个回调而不是一个`IDisposable`——`Observable.Create`提供了允许你这样做的重载。在这种情况下，当提前终止订阅时，Rx将调用我们的回调。）

```csharp
IObservable<char> KeyPresses() =>
    Observable.Create<char>(observer =>
    {
        CancellationTokenSource cts = new();
        Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                ConsoleKeyInfo ki = Console.ReadKey();
                observer.OnNext(ki.KeyChar);
            }
        });

        return () => cts.Cancel();
    });
```

这说明取消并不一定会立即生效。`Console.ReadKey` API 不提供接受`CancellationToken`的重载，因此此可观察对象无法检测到请求取消，直到用户下次按键，导致`ReadKey`返回。

考虑到我们可能在等待`ReadKey`返回时已经请求了取消，你可能会认为我们应该在`ReadKey`返回后并在调用`OnNext`之前检查这一点。实际上，如果我们不这样做也无关紧要。Rx有一条规则，即可观察源_不得_在对该订阅的观察者的`Dispose`调用返回_之后_调用观察者。为了执行这条规则，如果你传递给`Observable.Create`的回调在请求取消订阅后继续调用其`IObserver<T>`上的方法，Rx只会忽略该调用。这就是为什么传递给你的`IObserver<T>`是一个包装的原因：它可以在调用传递到底层观察者之前拦截这些调用。然而，这种便利意味着有两个重要的事情需要注意：
1. 如果你_确实_忽视取消订阅的尝试并继续工作以产生项目，你只是在浪费时间，因为没有什么会接收到这些项目
2. 如果你调用`OnError`，可能没有任何东西在听，错误可能会被完全忽略。

设计了几种`Create`的重载以支持`async`方法。下一个方法利用此功能，能够使用异步方法`ReadLineAsync`将文件中的文本行作为可观察源呈现。

```csharp
IObservable<string> ReadFileLines(string path) =>
    Observable.Create<string>(async (observer, cancellationToken) =>
    {
        using (StreamReader reader = File.OpenText(path))
        {
            while (cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                observer.OnNext(line);
            }

            observer.OnCompleted();
        }
    });
```

从存储设备读取数据通常不会立即发生（除非它已经在文件系统缓存中），因此这个源将尽可能快地提供数据。

注意，因为这是一个`async`方法，它通常会在完成之前返回给其调用者。（第一个实际需要等待的`await`返回，并且方法的其余部分通过回调在工作完成时运行。）这意味着订阅者通常会在 possession of the `IDisposable` representing their subscription before this method finishes, so we're using a different mechanism to handle unsubscription here. This particular overload of `Create` passes its callback not just an `IObserver<T>` but also a `CancellationToken`, with which it will request cancellation when unsubscription occurs.

文件 IO 可能会遇到错误。我们要查找的文件可能不存在，或者由于安全限制我们可能无法打开它，或者因为其他应用程序正在使用它。文件可能位于远程存储服务器上，我们可能会失去网络连接。因此，我们必须预期此类代码会出现异常。这个例子没有做任何异常检测，然而，这个`ReadFileLines`方法返回的`IObservable<string>`实际上会报告任何发生的异常。这是因为 `Create` 方法会捕获从我们的回调中出现的任何异常并用 `OnError` 报告它。(如果我们的代码已经在观察者上调用了`OnComplete`，Rx不会调用`OnError`，因为这将违反规则。相反，它会静默地丢弃异常，因此最好不要在调用`OnCompleted`后尝试完成任何工作。)

这种自动异常传递是另一个例子，说明为什么 `Create` 工厂方法是实现自定义可观察序列的首选方式。这不仅仅是因为它可以为你节省一些时间。还因为Rx处理了你可能没有想到的复杂情况，如通知的线程安全和订阅的处理。

`Create` 方法包含了懒惰求值，这是 Rx 的一个非常重要的部分。它为我们将要看到的其他强大功能如调度和序列的组合打开了大门。代理只会在进行订阅时被调用。所以在`ReadFileLines`示例中，它不会尝试打开文件，直到你订阅返回的`IObservable<string>`。 如果你多次订阅，它将每次执行回调。（因此，如果文件已更改，你可以通过再次调用`Subscribe`来检索最新内容。）

作为练习，尝试使用`Create`方法自己构建`Empty`, `Return`, `Never`和`Throw`扩展方法。如果你现在可以使用Visual Studio或[LINQPad](http://www.linqpad.net/)，尽快编写代码，或者如果你使用的是Visual Studio Code，你可以创建一个新的[Polyglot Notebook](https://code.visualstudio.com/docs/languages/polyglot)。 (Polyglot Notebooks自动使Rx可用，因此你只需要写一个带有适当`using`指令的C#单元，你就可以开始了。) 如果你没有（也许你正在火车上去上班的路上），试着概念化你将如何解决这个问题。

你在移动到这一段之前完成了最后一步，对吧? 因为你现在可以将你的版本与使用`Observable.Create`重新创建的`Empty`, `Return`, `Never` and `Throw`这些示例进行比较：

```csharp
public static IObservable<T> Empty<T>()
{
    return Observable.Create<T>(o =>
    {
        o.OnCompleted();
        return Disposable.Empty;
    });
}

public static IObservable<T> Return<T>(T value)
{
    return Observable.Create<T>(o =>
    {
        o.OnNext(value);
        o.OnCompleted();
        return Disposable.Empty;
    });
}

public static IObservable<T> Never<T>()
{
    return Observable.Create<T>(o =>
    {
        return Disposable.Empty;
    });
}

public static IObservable<T> Throws<T>(Exception exception)
{
    return Observable.Create<T>(o =>
    {
        o.OnError(exception);
        return Disposable.Empty;
    });
}
```

你可以看到`Observable.Create`提供了构建我们自己的工厂方法的能力。

### Observable.Defer

`Observable.Create`的一个非常有用的方面是它提供了一个代码执行的位置，这些代码只有在订阅时才会运行。经常会有库提供`IObservable<T>`属性，这些属性不一定会被所有应用程序使用，因此在你知道真正需要这些属性时再进行相关工作是非常有用的。这种延迟初始化是`Observable.Create`工作方式的固有特征，但是如果我们的源的性质意味着`Observable.Create`不是一个好的选择，我们应该如何进行延迟初始化呢？Rx提供了`Observable.Defer`来解决这个问题。

我之前已经使用过`Defer`了。`ObserveFileSystem`方法返回一个`IObservable<FileSystemEventArgs>`，报告文件夹中的更改。它不适合使用`Observable.Create`，因为它以.NET事件的形式提供了我们想要的所有通知，因此使用Rx的事件适配功能是有意义的。但我们仍然希望推迟`FileSystemWatcher`的创建，直到订阅的那一刻，这就是为什么那个示例使用了`Observable.Defer`。

`Observable.Defer`接受一个回调，该回调返回一个`IObservable<T>`，`Defer`将此用一个`IObservable<T>`包装起来，在订阅时调用该回调。为了展示效果，我首先将展示一个不使用`Defer`的示例：

```csharp
static IObservable<int> WithoutDeferal()
{
    Console.WriteLine("正在进行一些启动工作...");
    return Observable.Range(1, 3);
}

Console.WriteLine("调用工厂方法");
IObservable<int> s = WithoutDeferal();

Console.WriteLine("第一次订阅");
s.Subscribe(Console.WriteLine);

Console.WriteLine("第二次订阅");
s.Subscribe(Console.WriteLine);
```

这将产生以下输出：

```
调用工厂方法
正在进行一些启动工作...
第一次订阅
1
2
3
第二次订阅
1
2
3
```

如你所见，当我们调用工厂方法时，`"正在进行一些启动工作..."`消息就会出现，且在我们订阅之前。因此，如果没有任何订阅者订阅返回的`IObservable<int>`，这项工作还是会被执行，浪费时间和能源。这里是`Defer`版本：

```csharp
static IObservable<int> WithDeferal()
{
    return Observable.Defer(() =>
    {
        Console.WriteLine("正在进行一些启动工作...");
        return Observable.Range(1, 3);
    });
}
```

如果我们使用与第一个示例类似的代码来使用这个版本，我们会看到这样的输出：

```
调用工厂方法
第一次订阅
正在进行一些启动工作...
1
2
3
第二次订阅
正在进行一些启动工作...
1
2
3
```

有两个重要的区别。首先，`"正在进行一些启动工作..."`消息直到我们第一次订阅时才会出现，说明`Defer`已经做到了我们想要的。然而，注意到消息现在出现了两次：它会在我们每次订阅时执行这项工作。如果你想要这种延迟初始化，但也希望只执行一次，你应该看看[发布运算符章节](15_PublishingOperators.md)，它提供了多种方式，使得多个订阅者可以共享对底层源的单一订阅。

## 序列生成器

到目前为止，我们所看到的创建方法都很直接，它们要么生成非常简单的序列（如单元素或空序列），要么依赖于我们的代码来确切地指明要产生什么。现在我们将看一些可以生成更长序列的方法。

### Observable.Range

`Observable.Range(int, int)` 返回一个 `IObservable<int>`，它生成一个整数范围。第一个整数是初始值，第二个是要生成的值的数量。此示例将输出值 '10' 到 '24'，然后完成。

```csharp
IObservable<int> range = Observable.Range(10, 15);
range.Subscribe(Console.WriteLine, () => Console.WriteLine("Completed"));
```

### Observable.Generate

假设您想使用 `Observable.Create` 模拟 `Range` 工厂方法。您可能会尝试这样做：

```csharp
// 这不是最佳做法！
IObservable<int> Range(int start, int count) =>
    Observable.Create<int>(observer =>
        {
            for (int i = 0; i < count; ++i)
            {
                observer.OnNext(start + i);
            }

            return Disposable.Empty;
        });
```

这个方法可以工作，但它不尊重订阅取消的请求。这不会直接造成伤害，因为 Rx 检测到取消订阅，并将简单地忽略我们产生的任何进一步的值。但是，继续生成数字，尽管没人在听，这是对 CPU 时间（因此对电池寿命和/或环境影响）的浪费。这有多糟糕取决于请求的范围有多长。但是想象一下，如果您想要一个无限序列呢？也许对您来说，拥有一个生成斐波那契序列或素数的 `IObservable<BigInteger>` 是有用的。那么您会怎样用 `Create` 来写呢？在这种情况下，您肯定会希望有某种处理取消订阅的方式。如果我们要被通知取消订阅，我们需要回调返回（或我们可以提供一个 `async` 方法，但这里似乎不太合适）。

这里有一个更好的方法可以使用：`Observable.Generate`。`Observable.Generate` 的简单版本接受以下参数：

- 初始状态
- 定义序列何时终止的谓词
- 应用于当前状态以产生下一个状态的函数
- 将状态转换为所需输出的函数

```csharp
public static IObservable<TResult> Generate<TState, TResult>(
    TState initialState, 
    Func<TState, bool> condition, 
    Func<TState, TState> iterate, 
    Func<TState, TResult> resultSelector)
```

这展示了如何使用 `Observable.Generate` 来构建一个 `Range` 方法：

```csharp
// 仅示例代码
public static IObservable<int> Range(int start, int count)
{
    int max = start + count;
    return Observable.Generate(
        start, 
        value => value < max, 
        value => value + 1, 
        value => value);
}
```

`Generate` 方法会反复调用我们，直到我们的 `condition` 回调表示我们已完成，或观察者取消订阅。我们可以简单地通过从未说我们完成来定义一个无限序列：

```csharp
IObservable<BigInteger> Fibonacci()
{
    return Observable.Generate(
        (v1: new BigInteger(1), v2: new BigInteger(1)),
        value => true, // 它永不结束！
        value => (value.v2, value.v1 + value.v2),
        value => value.v1);
}
```

## 定时序列生成器

到目前为止，我们查看的大多数方法都返回立即产生所有值的序列。（唯一的例外是我们调用了`Observable.Create`并在准备好时产生值。）然而，Rx能够按计划生成序列。

如我们将看到，计划工作的操作符通过一种称为_调度器_的抽象来进行。如果你不指定调度器，它们将选择一个默认的调度器，但有时定时器机制是重要的。例如，有些定时器与UI框架集成，能在接收鼠标点击和其他输入的相同线程上发送通知，我们可能希望Rx的基于时间的操作符使用这些调度器。出于测试目的，虚拟化定时是有用的，这样我们可以验证在时间敏感的代码中发生了什么，而不必真的等待测试按实时执行。

调度器是一个复杂的主题，超出了本章的范围，但在后面关于[调度和线程](11_SchedulingAndThreading.md)的章节中有详细介绍。

有三种生成定时事件的方法。

### Observable.Interval

第一种是 `Observable.Interval(TimeSpan)`，它将根据你选择的频率从零开始发布增量值。

这个例子每250毫秒发布一次值。

```csharp
IObservable<long> interval = Observable.Interval(TimeSpan.FromMilliseconds(250));
interval.Subscribe(
    Console.WriteLine, 
    () => Console.WriteLine("completed"));
```

输出：

```
0
1
2
3
4
5
```

一旦订阅，你必须取消订阅来停止序列，因为`Interval`返回一个无限序列。Rx假设你可能有很大的耐心，因为`Interval`返回的序列类型是`IObservable<long>`（是`long`，不是`int`），这意味着如果你产生的事件超过了区区21.475亿（即超过了`int.MaxValue`），你不会遇到问题。

### Observable.Timer

第二种用于生成基于固定时间的序列的工厂方法是`Observable.Timer`。它有几个重载。最基本的一个就像`Observable.Interval`一样只接受一个`TimeSpan`。但与`Observable.Interval`不同，`Observable.Timer`在时间周期过后将发布一个值（数字0），然后它将结束。

```csharp
var timer = Observable.Timer(TimeSpan.FromSeconds(1));
timer.Subscribe(
    Console.WriteLine, 
    () => Console.WriteLine("completed"));
```

输出：

```
0
completed
```

或者，你可以为`dueTime`参数提供一个`DateTimeOffset`。这将在指定的时间生成值0并完成。

还有一组重载添加了一个`TimeSpan`，指示生成后续值的周期。这允许我们生成无限序列。这也显示了`Observable.Interval`实际上只是`Observable.Timer`的一个特例。`Interval`可以这样实现：

```csharp
public static IObservable<long> Interval(TimeSpan period)
{
    return Observable.Timer(period, period);
}
```

虽然`Observable.Interval`总是在生成第一个值前等待给定的周期，但这个`Observable.Timer`重载允许你选择何时开始序列。通过`Observable.Timer`，你可以编写以下内容，让序列立即开始：

```csharp
Observable.Timer(TimeSpan.Zero, period);
```

这将我们引向第三种也是最通用的生成定时相关序列的方式，回到`Observable.Generate`。

### 定时的 Observable.Generate

`Observable.Generate`有一个更复杂的重载，允许你提供一个函数来指定下一个值的到期时间。

```csharp
public static IObservable<TResult> Generate<TState, TResult>(
    TState initialState, 
    Func<TState, bool> condition, 
    Func<TState, TState> iterate, 
    Func<TState, TResult> resultSelector, 
    Func<TState, TimeSpan> timeSelector)
```

额外的`timeSelector`参数让我们告诉`Generate`何时生成下一个项目。我们可以使用这个来编写自己的`Observable.Timer`的实现（正如你已经看到的，这反过来又使我们能够编写自己的`Observable.Interval`）。

```csharp
public static IObservable<long> Timer(TimeSpan dueTime)
{
    return Observable.Generate(
        0l,
        i => i < 1,
        i => i + 1,
        i => i,
        i => dueTime);
}

public static IObservable<long> Timer(TimeSpan dueTime, TimeSpan period)
{
    return Observable.Generate(
        0l,
        i => true,
        i => i + 1,
        i => i,
        i => i == 0 ? dueTime : period);
}

public static IObservable<long> Interval(TimeSpan period)
{
    return Observable.Generate(
        0l,
        i => true,
        i => i + 1,
        i => i,
        i => period);
}
```

这展示了你如何使用`Observable.Generate`来生成无限序列。我将留给你这个读者，作为一个练习，使用`Observable.Generate`来生成变化速率的值。

## 可观测序列和状态

正如 `Observable.Generate` 特别明确表明的那样，可观测序列可能需要维护状态。使用这个操作符时，我们明确地传入初始状态，并提供一个回调来在每次迭代时更新它。许多其他操作符也会维护内部状态。`Timer` 需要记住其滴答计数，更微妙的是，它必须以某种方式追踪上一次事件触发的时间以及下一次事件的预定时间。正如您将在后续章节中看到的，许多其他操作符也需要记住它们已经看到的信息。

这引出了一个有趣的问题：如果一个进程关闭了怎么办？是否有办法保存这些状态，并在新进程中重新构建它。

对于普通的 Rx.NET，答案是否定的：所有这些状态都完全保存在内存中，没有办法获取这些状态，或要求运行中的订阅序列化它们的当前状态。这意味着如果您正在处理特别长时间运行的操作，您需要弄清楚如何重新启动，而不能依赖于 `System.Reactive` 来帮助您。然而，有一组相关的基于 Rx 的库，统称为[Reaqtive libraries](https://reaqtive.net/)。这些库提供了与 `System.Reactive` 大部分相同的操作符的实现，但以一种可以收集当前状态，并从之前保存的状态重新创建新订阅的形式。这些库还包括一个名为 Reaqtor 的组件，它是一个托管技术，可以管理自动检查点和崩溃后恢复，使得支持非常长时间运行的 Rx 逻辑成为可能，通过使订阅持久化和可靠。请注意，这目前还没有在任何产品化形式中，因此您需要做相当多的工作来使用它，但如果您需要一个可持久化的 Rx 版本，请注意它的存在。

## 将常见类型适配为 `IObservable<T>`

虽然我们现在已经看到了两种非常通用的生成任意序列的方法——`Create` 和 `Generate`——但如果您已经有了某种其他形式的现有信息源，您想把它作为 `IObservable<T>` 提供呢？Rx 为常见的源类型提供了一些适配器。

### 关于委托

`Observable.Start` 方法允许您将一个长时间运行的 `Func<T>` 或 `Action` 转换为单值可观测序列。默认情况下，处理将在 ThreadPool 线程上异步执行。如果您使用的是 `Func<T>` 的重载，则返回类型将是 `IObservable<T>`。当函数返回其值时，该值将被发布，然后序列完成。如果您使用采用 `Action` 的重载，则返回的序列将为类型 `IObservable<Unit>`。`Unit` 类型代表没有信息的状态，因此它在某种程度上类似于 `void`，除了您可以拥有一个 `Unit` 类型的实例。在 Rx 中，`Unit` 很有用，因为我们通常只关心某件事何时发生，并且可能除了时间之外没有任何信息。在这些情况下，我们通常使用 `IObservable<Unit>`，这样即使没有有意义的数据，也能产生明确的事件。 （这个名字来自函数式编程的世界，这种构造在那里使用很多。）在这种情况下，`Unit` 被用来发布一个确认 `Action` 已完成的信号，因为一个 `Action` 不返回任何信息。`Unit` 类型本身没有值；它只是作为 `OnNext` 通知的一个空载荷。以下是使用两种重载的一个示例。

```csharp
static void StartAction()
{
    var start = Observable.Start(() =>
        {
            Console.Write("Working away");
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(100);
                Console.Write(".");
            }
        });

    start.Subscribe(
        unit => Console.WriteLine("Unit published"), 
        () => Console.WriteLine("Action completed"));
}

static void StartFunc()
{
    var start = Observable.Start(() =>
    {
        Console.Write("Working away");
        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(100);
            Console.Write(".");
        }
        return "Published value";
    });

    start.Subscribe(
        Console.WriteLine, 
        () => Console.WriteLine("Action completed"));
}
```

注意 `Observable.Start` 与 `Observable.Return` 之间的差异。`Start` 方法仅在订阅时调用我们的回调，因此是一个“懒惰”的操作例子。相反，`Return` 要求我们事先提供值。

由 `Start` 返回的可观测对象可能看起来与 `Task` 或 `Task<T>`（取决于您使用的是 `Action` 还是 `Func<T>` 重载）有肤浅的相似之处。每个都代表可能需要一些时间才能最终完成的工作，可能会产生结果。然而，有一个重要的区别：`Start` 不会开始工作，直到您订阅它。此外，每次您订阅它时，它都会重新执行回调。所以它更像是一个类似任务的实体的工厂。

### 来自事件

正如我们在书的早期讨论过的，.NET 有一个内置到其类型系统中的事件模型。这早于 Rx（至少是因为 Rx 在 .NET 获得泛型之前是不可行的，即 .NET 2.0）所以很常见的是类型支持事件但不支持 Rx。为了能够与现有的事件模型集成，Rx 提供了将事件转换为可观测序列的方法。我之前在文件系统监视器示例中简要展示了这一点，但让我们更详细地检查一下。您可以使用几种不同的方式。这里展示了最简洁的形式：

```csharp
FileSystemWatcher watcher = new (@"c:\incoming");
IObservable<EventPattern<FileSystemEventArgs>> changeEvents = Observable
    .FromEventPattern<FileSystemEventArgs>(watcher, nameof(watcher.Changed));
```

如果您有一个提供事件的对象，您可以使用 `FromEventPattern` 的这个重载，传入对象和您希望使用 Rx 的事件名称。尽管这是将事件适配到 Rx 世界中最简单的方式，但它有一些问题。

首先，为什么我需要将事件名称作为字符串传递？使用字符串标识成员是一种容易出错的技术。如果第一和第二个参数之间存在不匹配（例如，如果我错误地传递了参数 `(somethingElse, nameof(watcher.Changed))`），编译器不会注意到。我能不能直接传递 `watcher.Changed` 本身？不幸的是不行——这是我在第一章中提到的一个问题的示例：.NET 事件不是一等公民。我们不能像使用其他对象或值那样使用它们。例如，我们不能将事件作为方法的参数传递。事实上，您可以对 .NET 事件做的唯一事情就是附加和移除事件处理程序。如果我想让某个其他方法附加处理程序到我选择的事件（例如，这里我希望 Rx 来处理事件），那么唯一的方法就是指定事件的名称，以便该方法（`FromEventPattern`）可以使用反射来附加其自己的处理程序。

这对某些部署场景来说是一个问题。在 .NET 中，越来越常见的做法是在构建时做额外的工作以优化运行时行为，依赖反射可能会妨碍这些技术。例如，我们可能使用 Ahead of Time (AOT) 机制而不是依赖于 Just In Time (JIT) 编译代码。.NET 的 Ready to Run (R2R) 系统使您能够在常规 IL 代码旁边包含针对特定 CPU 类型的预编译代码，避免了等待 .NET 编译 IL 成为可运行代码的需要。这可以对启动时间产生显著影响。在客户端应用中，它可以解决应用程序刚启动时表现迟缓的问题。它在服务器端应用中也很重要，尤其是在代码可能经常从一个计算节点移动到另一个节点的环境中，使得最小化冷启动成本变得重要。还有一些场景中，JIT 编译甚至不是一个选项，这种情况下，AOT 编译不仅仅是一个优化：它是代码能够运行的唯一方式。

反射的问题在于它使构建工具难以计算出哪些代码将在运行时执行。当它们检查到调用 `FromEventPattern` 时，它们只会看到类型为 `object` 和 `string` 的参数。这并不显而易见，这会在运行时导致反射驱动的调用 `FileSystemWatcher.Changed` 的 `add` 和 `remove` 方法。有一些属性可以用来提供线索，但这些的效果有限。有时构建工具可能无法确定需要被 AOT 编译以使这个方法能够在没有依赖运行时 JIT 的情况下执行的代码是什么。

还有一个相关的问题。.NET 构建工具支持一个称为“修剪”的功能，它们会移除未使用的代码。`System.Reactive.dll` 文件大小约为 1.3MB，但使用这个组件的应用很少会使用其中每一个成员的每一个功能。Rx 的基本使用可能只需要几十 KB。修剪的想法是计算出实际在使用的代码部分，并生成只包含那些代码的 DLL 副本。这可以大大减少可执行文件运行所需的代码量。这在客户端 Blazor 应用中尤其重要，其中 .NET 组件最终会被浏览器下载。如果必须下载完整的 1.3MB 组件，您可能会重新考虑是否使用它。但如果修剪意味着基本使用只需要几十 KB，而且只有在你更广泛使用组件时大小才会增加，这就可以合理地使用一个组件，如果没有修剪，就会因为其带来的过大负担而不足以证明其包含。但与 AOT 编译一样，修剪只有在工具能够确定哪些代码正在使用时才能工作。如果它们做不到这一点，这不仅仅是回落到更慢的路径，等待相关代码获取 JIT 编译器的问题。如果代码已经被修剪，它将在运行时不可用，您的应用程序可能会因 `MissingMethodException` 而崩溃。

所以，如果您正在使用任何这些技术，基于反射的 API 可能会有问题。幸运的是，有一个替代方案。我们可以使用一个接受几个委托的重载，Rx 将在添加或移除事件处理程序时调用这些委托:

```csharp
IObservable<EventPattern<FileSystemEventArgs>> changeEvents = Observable
    .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
        h => watcher.Changed += h,
        h => watcher.Changed -= h);
```

这段代码对 AOT 和修剪工具来说很容易理解。我们编写了明确添加和删除 `FileSystemWatcher.Changed` 事件的处理程序的方法，因此 AOT 工具可以预编译这两种方法，并且修剪工具知道它们不能移除这些事件的添加和删除处理程序。

缺点是这是一段相当繁琐的代码。如果您还没有完全接受使用 Rx 的想法，这可能足以让您想说“我还是坚持使用普通的 .NET 事件吧。”但复杂的本质是 .NET 事件所存在的问题的一个标志。如果事件是一等公民，我们就不必编写如此丑陋的代码。

不仅事件的二等地位意味着我们不能仅仅将事件本身作为一个参数传递，它还意味着我们不得不明确地指定类型参数。事件的委托类型（本例中为 `FileSystemEventHandler`）和其事件参数类型（这里为 `FileSystemEventArgs`）之间的关系通常是 C# 类型推断不能自动确定的，这就是为什么我们不得不明确指定两种类型。（使用泛型 `EventHandler<T>` 类型的事件更适合类型推断，并且可以使用稍微简洁一些的 `FromEventPattern` 版本。不幸的是，相对较少的事件实际上使用了那种。一些事件除了发生了某事这一事实外没有提供任何信息，使用基础 `EventHandler` 类型，而对于这些类型的事件，您可以实际上完全省略类型参数，使代码稍微不那么丑陋。您仍然需要提供添加和移除回调。）

注意在此示例中 `FromEventPattern` 的返回类型是：

`IObservable<EventPattern<FileSystemEventArgs>>`。

`EventPattern<T>` 类型封装了事件传递给处理程序的信息。大多数 .NET 事件遵循一个常见模式，其中处理程序方法接受两个参数：一个 `object sender`，它告诉你哪个对象引发了事件（如果您将一个事件处理程序附加到多个对象上很有用），然后是一个从 `EventArgs` 派生的某种类型的第二个参数，提供有关事件的信息。`EventPattern<T>` 只是将这两个参数打包成一个单一对象，提供 `Sender` 和 `EventArgs` 属性。在您实际上不想将一个处理程序附加到多个来源的情况下，您只需要那个 `EventArgs` 属性，这就是为什么早期的 `FileSystemWatcher` 示例继续只提取那个，得到一个更简单的结果 `IObservable<FileSystemEventArgs>`。它是通过使用 `Select` 操作符来完成的，我们稍后将更详细地介绍：

```csharp
IObservable<FileSystemEventArgs> changes = changeEvents.Select(ep => ep.EventArgs);
```

将属性更改事件公开为可观测序列是非常常见的。.NET 运行时库定义了一个用于宣传属性更改的基于 .NET 事件的接口 `INotifyPropertyChanged`，一些用户界面框架有更专门的系统，如 WPF 的 `DependencyProperty`。如果您正在考虑编写自己的封装来做这种事情，我强烈建议您首先查看 [Reactive UI libraries](https://github.com/reactiveui/ReactiveUI/)。它有一套[将属性包装为 `IObservable<T>` 的功能](https://www.reactiveui.net/docs/handbook/when-any/)。

### 来自 `Task`

`Task` 和 `Task<T>` 类型在 .NET 中非常广泛使用。主流的 .NET 语言内置了支持它们的特性（例如，C# 的 `async` 和 `await` 关键字）。任务和 `IObservable<T>` 在概念上有一些重叠：两者都代表可能需要一段时间才能完成的工作。从某种意义上说，`IObservable<T>` 是 `Task<T>` 的泛化：两者都代表可能长时间运行的工作，但 `IObservable<T>` 可以产生多个结果，而 `Task<T>` 只能产生一个结果。

由于 `IObservable<T>` 是更一般的抽象，我们应该能够将一个 `Task<T>` 表示为一个 `IObservable<T>`。Rx 定义了各种 `Task` 和 `Task<T>` 的扩展方法来做到这一点。这些方法都被称为 `ToObservable()`，它提供了各种重载，根据需要提供详细的控制，并且为最常见的情景提供简单性。

虽然它们在概念上相似，但 `Task<T>` 在细节上做了一些不同的事情。例如，您可以检索其[`Status` 属性](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.task.status)，它可能报告它处于取消或出错的状态。`IObservable<T>` 不提供一种询问来源状态的方式；它只是告诉您事情。所以 `ToObservable` 做出了一些决定，以一种在 Rx 世界中有意义的方式呈现状态：

- 如果任务是[取消](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskstatus#system-threading-tasks-taskstatus-canceled)，`IObservable<T>` 调用订阅者的 `OnError`，传递一个 `TaskCanceledException`
- 如果任务是[出错](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskstatus#system-threading-tasks-taskstatus-faulted)，`IObservable<T>` 调用订阅者的 `OnError`，传递任务的内部异常
- 如果任务尚未处于最终状态（即未[取消](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskstatus#system-threading-tasks-taskstatus-canceled)、[出错](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskstatus#system-threading-tasks-taskstatus-faulted)或[运行完成](https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.taskstatus#system-threading-tasks-taskstatus-rantocompletion)），`IObservable<T>` 在任务进入这些最终状态之前不会产生任何通知

无论任务在您调用 `ToObservable` 时是否已经处于最终状态，如果已经完成，`ToObservable` 将只返回代表该状态的序列。 （实际上，它使用了您之前看到的 `Return` 或 `Throw` 创建方法。）如果任务尚未完成，`ToObservable` 将附加一个延续到任务上，以便在它完成时检测结果。

任务有两种形式：`Task<T>`，产生一个结果，和 `Task`，不产生结果。但在 Rx 中，只有 `IObservable<T>`——没有无结果形式。我们之前在将 `Observable.Start` 方法需要能够[适配一个委托为 `IObservable<T>`](#from-delegates) 的情况下再次看到了这个问题，即使委托是一个不产生结果的 `Action`。解决方案是返回一个 `IObservable<Unit>`，这也正是您在对一个普通 `Task` 调用 `ToObservable` 时得到的结果。

扩展方法使用起来很简单：

```csharp
Task<string> t = Task.Run(() =>
{
    Console.WriteLine("Task running...");
    return "Test";
});
IObservable<string> source = t.ToObservable();
source.Subscribe(
    Console.WriteLine,
    () => Console.WriteLine("completed"));
source.Subscribe(
    Console.WriteLine,
    () => Console.WriteLine("completed"));
```

这是输出。

```
Task running...
Test
completed
Test
completed
```

请注意，即使有两个订阅者，任务也只运行一次。这不应该令人惊讶，因为我们只创建了一个任务。如果任务尚未完成，那么所有订阅者都将在它完成时收到结果。如果任务已经完成，则 `IObservable<T>` 本质上变成了一个单值冷观察对象。

#### 每次订阅一个任务

获取 `IObservable<T>` 的一种不同方式是。我可以将前面示例中的第一条语句替换为以下内容：

```csharp
IObservable<string> source = Observable.FromAsync(() => Task.Run(() =>
{
    Console.WriteLine("Task running...");
    return "Test";
}));
```

订阅两次产生略有不同的输出：

```
Task running...
Task running...
Test
Test
completed
completed
```

请注意，这执行了两次任务，每次调用 `Subscribe` 一次。`FromAsync` 可以这样做，因为我们没有传递一个 `Task<T>`，而是传递了一个返回 `Task<T>` 的回调。它在我们调用 `Subscribe` 时调用那个，因此每个订阅者本质上都获得了自己的任务。

如果我想使用 `async` 和 `await` 来定义我的任务，那么我就不需要费心使用 `Task.Run`，因为一个 `async` lambda 创建了一个 `Func<Task<T>>`，这正是 `FromAsync` 需要的类型：

```csharp
IObservable<string> source = Observable.FromAsync(async () =>
{
    Console.WriteLine("Task running...");
    await Task.Delay(50);
    return "Test";
});
```

这产生与前面完全相同的输出。这里有一个微妙的不同。当我使用 `Task.Run` 时，lambda 从一开始就在任务池线程上运行。但当我以这种方式写时，lambda 将从调用 `Subscribe` 的线程开始运行。只有在它遇到第一个 `await` 时，它才会返回（并且调用 `Subscribe` 随后会返回），其余的方法会在线程池上运行。

### 来自 `IEnumerable<T>`

Rx 定义了另一个扩展方法 `ToObservable`，这次是针对 `IEnumerable<T>`。在早期章节中我描述了 `IObservable<T>` 是设计来代表与 `IEnumerable<T>` 相同的基本抽象，唯一的区别在于我们用来获取序列中元素的机制：使用 `IEnumerable<T>`，我们编写代码从集合中_拉取_值（例如，一个 `foreach` 循环），而 `IObservable<T>` 通过调用我们的 `IObserver<T>` 上的 `OnNext` 向我们_推送_值。

我们可以编写代码来从_拉取_到_推送_的转换：

```csharp
// 仅示例代码 - 不要使用！
public static IObservable<T> ToObservableOversimplified<T>(this IEnumerable<T> source)
{
    return Observable.Create<T>(o =>
    {
        foreach (var item in source)
        {
            o.OnNext(item);
        }

        o.OnComplete();

        // 错误地忽略了取消订阅。
        return Disposable.Empty;
    });
}
```

这个粗糙的实现传达了基本的想法，但它很幼稚。它没有尝试处理取消订阅，而且在使用 `.Observable.Create` 的这种特定场景下修复这个问题并不容易。正如我们稍后在书中看到的，Rx 源可能尝试快速连续地交付大量事件应该与 Rx 的并发模型集成。Rx 提供的实现当然照顾了所有这些棘手的细节。这使得它相当复杂，但那是 Rx 的问题；你可以认为它在逻辑上等同于上面显示的代码，但没有缺点。

事实上，这是 Rx.NET 整体的一个反复出现的主题。许多内置的操作符之所以有用，不是因为它们做了一些特别复杂的事情，而是因为它们为您处理了许多微妙和棘手的问题。在考虑自己解决方案之前，您应该始终尝试找到 Rx.NET 中内置的东西来满足您的需求。

在从 `IEnumerable<T>` 到 `IObservable<T>` 的过渡中，您应该仔细考虑您真正想要实现什么。考虑到 `IEnumerable<T>` 的阻塞同步（拉）性质与 `IObservable<T>` 的异步（推）性质并不总是很好地混合。一旦有人订阅了这种方式创建的 `IObservable<T>`，它实际上是在请求遍历 `IEnumerable<T>`，立即产生所有的值。调用 `Subscribe` 可能不会返回，直到它到达 `IEnumerable<T>` 的末尾，使其类似于[本章开始时显示的非常简单的示例](#a-very-basic-iobservablet-implementation)。（我说“可能”，因为正如我们即将在调度程序中看到的，确切的行为取决于上下文。）`ToObservable` 不能施展魔法——总有某个地方需要执行相当于 `foreach` 循环的操作。

因此，尽管这可以是将数据序列引入 Rx 世界的一种方便方式，您应该仔细测试并衡量性能影响。

### 来自 APM

Rx 提供对古老的 [.NET 异步编程模型 (APM)](https://learn.microsoft.com/zh-cn/dotnet/standard/asynchronous-programming-patterns/asynchronous-programming-model-apm) 的支持。在 .NET 1.0 中，这是表示异步操作的唯一模式。它在 2010 年被 [.NET 4.0 引入的基于任务的异步模式 (TAP)](https://learn.microsoft.com/zh-cn/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap) 取代。旧的 APM 在 TAP 之上没有任何优势。此外，C# 的 `async` 和 `await` 关键字（以及其他 .NET 语言中的等价物）只支持 TAP，这意味着最好避免使用 APM。然而，TAP 在 2011 年 Rx 1.0 发布时仍然相对较新，因此它提供了将 APM 实现呈现为 `IObservable<T>` 的适配器。

今天没有人应该使用 APM，但为了完整性（以防万一您不得不使用只提供 APM 的古老库），我将简要说明 Rx 对此的支持。

调用 `Observable.FromAsyncPattern` 的结果_不_返回一个可观测序列。它返回一个返回可观测序列的委托。（因此，本质上是一个工厂工厂。）这个委托的签名将匹配调用 `FromAsyncPattern` 的泛型参数，除了返回类型将被包装在一个可观测序列中。下面的示例封装了 `Stream` 类的 `BeginRead`/`EndRead` 方法（这是 APM 的实现）。

**注意**：这仅用于说明如何包装 APM。实际操作中您永远不会这样做，因为 `Stream` 多年来一直支持 TAP。

```csharp
Stream stream = GetStreamFromSomewhere();
var fileLength = (int) stream.Length;

Func<byte[], int, int, IObservable<int>> read = 
            Observable.FromAsyncPattern<byte[], int, int, int>(
              stream.BeginRead, 
              stream.EndRead);
var buffer = new byte[fileLength];
IObservable<int> bytesReadStream = read(buffer, 0, fileLength);
bytesReadStream.Subscribe(byteCount =>
{
    Console.WriteLine(
        "Number of bytes read={0}, buffer should be populated with data now.", 
        byteCount);
});
```

## 主题（Subjects）

到目前为止，本章已经探讨了多种返回`IObservable<T>`实现的工厂方法。不过，还有另一种方法：`System.Reactive`定义了各种实现`IObservable<T>`的类型，我们可以直接实例化这些类型。但我们如何确定这些类型产生什么值呢？我们可以做到这一点是因为它们还实现了`IObserver<T>`，使我们能够将值推送到它们中，而且我们推入的值将正是观察者看到的值。

在Rx中，同时实现`IObservable<T>`和`IObserver<T>`的类型称为 _主题（subjects）_。此处有一个`ISubject<T>`来表示这一点。（这在`System.Reactive` NuGet包中，而不是像`IObservable<T>`和`IObserver<T>`那样内置在.NET运行时库中。）`ISubject<T>`看起来像这样：

```csharp
public interface ISubject<T> : ISubject<T, T>
{
}
```

如此看来，还有一个两个参数的`ISubject<TSource, TResult>`，以适应既是观察者又是被观察者的某个对象可能以某种方式转换通过它的数据的事实，这意味着输入和输出类型不一定相同。这里是两种类型参数的定义：

```csharp
public interface ISubject<in TSource, out TResult> : IObserver<TSource>, IObservable<TResult>
{
}
```

如你所见，`ISubject`接口本身不定义任何成员。它们只是继承自`IObserver<T>`和`IObservable<T>`——这些接口只不过是直接表达主题既是观察者又是被观察者的事实。

但这有什么用呢？你可以将`IObserver<T>`和`IObservable<T>`视为分别是'消费者'和'发布者'接口。那么，一个主题则既是消费者又是发布者。数据既流入又流出主题。

Rx提供了一些主题实现，在想要使`IObservable<T>`可用的代码中偶尔会有用。尽管通常首选使用`Observable.Create`，但有一种重要情况下使用主题可能更有意义：如果您有一些发现感兴趣事件的代码（例如，通过使用某些消息技术的客户端API）并希望通过`IObservable<T>`使它们可用，那么与使用`Observable.Create`或自定义实现相比，主题有时可以提供一种更方便的方法。

Rx提供了一些类型的主题。我们将从最直接的一个开始了解。

### `Subject<T>`

`Subject<T>`类型会立即将对其`IObserver<T>`方法的任何调用转发给当前订阅它的所有观察者。此示例显示了其基本操作：

```csharp
Subject<int> s = new();
s.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
s.Subscribe(x => Console.WriteLine($"Sub2: {x}"));

s.OnNext(1);
s.OnNext(2);
s.OnNext(3);
```

我创建了一个`Subject<int>`。我订阅了它两次，然后重复调用其`OnNext`方法。这产生了以下输出，说明`Subject<int>`将每个`OnNext`调用都转发给了两个订阅者：

```
Sub1: 1
Sub2: 1
Sub1: 2
Sub2: 2
Sub1: 3
Sub2: 3
```

我们可以将此用作将我们从某些API接收到的数据桥接到Rx世界的一种方法。你可以想象编写这样的内容：

```csharp
public class MessageQueueToRx : IDisposable
{
    private readonly Subject<string> messages = new();

    public IObservable<string> Messages => messages;

    public void Run()
    {
        while (true)
        {
            // 从某个假设的消息队列服务接收消息
            string message = MqLibrary.ReceiveMessage();
            messages.OnNext(message);
        }
    }

    public void Dispose()
    {
        messages.Dispose();
    }
}
```

不难将此修改为使用`Observable.Create`。但这种方法更简单的情况是，如果您需要提供多个不同的`IObservable<T>`源。想象我们根据其内容区分不同的消息类型，并通过不同的可观察对象发布它们。如果我们仍想使用单个循环从队列中拉出消息，那么使用`Observable.Create`来安排这一切可能会很困难。

`Subject<T>`还将对`OnCompleted`或`OnError`的调用分发给所有订阅者。当然，Rx的规则要求一旦您在任何`IObserver<T>`（以及任何`ISubject<T>`都是`IObserver<T>`，因此此规则适用于`Subject<T>`）上调用了这些方法中的任何一个，您就不能再次对该观察者调用`OnNext`、`OnError`或`OnComplete`。实际上，`Subject<T>`会忽略违反这一规则的调用——所以即使您的代码在内部没有完全遵守这些规则，您呈现给外部世界的`IObservable<T>`也会表现正确，因为Rx强制执行此规则。

`Subject<T>`实现了`IDisposable`。处理一个`Subject<T>`会将其置于一个状态，此状态会在调用其任何方法时抛出异常。文档还描述它会取消订阅所有观察者，但由于处理后的`Subject<T>`无论如何都无法产生进一步的通知，这实际上并不意味着什么。（请注意，在您处理它时，它不会调用观察者的`OnCompleted`。）唯一的实际效果是，其内部用于跟踪观察者的字段被重置为表示它已被处理的特殊哨兵值，这意味着“取消订阅”观察者的一个外部可观察的效果是，如果由于某种原因，您的代码在处理后仍持有对`Subject<T>`的引用，那么这将不再使所有订阅者可达垃圾收集（GC）目的。如果`Subject<T>`在不再使用后仍无限期可达，那么本身就是一种有效的内存泄漏，但处理至少会限制影响：只有`Subject<T>`本身会保持可达，而不是所有的订阅者。

`Subject<T>`是最直接的主题，但还有其他更专门的主题。

## `ReplaySubject<T>`

`Subject<T>`并不会记住任何事情：它会立即将接收到的值分发给订阅者。如果后来有新的订阅者加入，他们只能看到他们订阅之后发生的事件。而`ReplaySubject<T>`则可以记住它所见过的每一个值。如果新的主题出现，它将接收到迄今为止所有事件的完整历史。

这是在前面的[`Subject<T>`章节](#subjectt)中第一个示例的一个变体。它创建了一个`ReplaySubject<int>`而不是`Subject<int>`。并且不是立即订阅两次，而是首先创建一个初始订阅，然后在发出几个值后再创建第二个订阅。

```csharp
ReplaySubject<int> s = new();
s.Subscribe(x => Console.WriteLine($"Sub1: {x}"));

s.OnNext(1);
s.OnNext(2);

s.Subscribe(x => Console.WriteLine($"Sub2: {x}"));

s.OnNext(3);
```

这将产生以下输出：

```
Sub1: 1
Sub1: 2
Sub2: 1
Sub2: 2
Sub1: 3
Sub2: 3
```

正如你所期待的，我们最初只看到`Sub1`的输出。但当我们进行第二次订阅时，可以看到`Sub2`也接收到了前两个值。然后当我们报告第三个值时，两者都看到了。如果这个示例使用`Subject<int>`，我们将只看到以下输出：

```
Sub1: 1
Sub1: 2
Sub1: 3
Sub2: 3
```

这里有一个明显的潜在问题：如果`ReplaySubject<T>`记住了发布给它的每个值，那么我们不应该用它来处理无尽的事件源，因为这最终会导致我们耗尽内存。

`ReplaySubject<T>`提供了接受简单的缓存过期设置的构造函数，可以限制内存消耗。一种选择是指定记住的项目数量的最大值。下一个示例创建了一个缓冲大小为2的`ReplaySubject<T>`：

```csharp    
ReplaySubject<int> s = new(2);
s.Subscribe(x => Console.WriteLine($"Sub1: {x}"));

s.OnNext(1);
s.OnNext(2);
s.OnNext(3);

s.Subscribe(x => Console.WriteLine($"Sub2: {x}"));

s.OnNext(4);
```

由于第二次订阅是在我们已经产生了3个值之后进行的，它不再看到所有这些值。它只接收订阅前发布的最后两个值（但第一次订阅当然继续看到一切）：

```
Sub1: 1
Sub1: 2
Sub1: 3
Sub2: 2
Sub2: 3
Sub1: 4
Sub2: 4
```

或者，你可以通过传递一个`TimeSpan`到`ReplaySubject<T>`构造函数来指定基于时间的限制。

## `BehaviorSubject<T>`

与`ReplaySubject<T>`一样，`BehaviorSubject<T>`也有记忆，但它只记住恰好一个值。然而，它和缓冲大小为1的`ReplaySubject<T>`并不完全相同。`BehaviorSubject<T>`从来不处于一个空内存的状态，它总是_恰好_记住一个项目。在我们进行第一个`OnNext`调用之前，这是如何工作的呢？`BehaviorSubject<T>`通过要求我们在构造它时提供初始值来实现这一点。

因此，你可以将`BehaviorSubject<T>`视为一个_总是_有可用值的主题。如果你订阅了一个`BehaviorSubject<T>`，它将立即产生单个值。（它可能随后会产生更多值，但它总是立即产生一个。）顺便说一下，它还通过一个叫做`Value`的属性使该值可用，因此你不需要订阅一个`IObserver<T>`就能检索该值。

`BehaviorSubject<T>`可以被视为一个可观察属性。就像普通属性一样，每当你询问它时，它可以立即提供一个值。不同之处在于，它可以随后通知你每次其值发生变化时。如果你使用[ReactiveUI框架](https://www.reactiveui.net/)（一个基于Rx的构建用户界面的框架），`BehaviourSubject<T>`可以作为视图模型中属性的实现类型（该类型介于你的领域模型和用户界面之间进行调解）。它具有类似属性的行为，使你能够随时检索一个值，但它还提供了变更通知，这可以通过ReactiveUI来处理，以保持UI的更新。

当涉及到完成时，这种类比有些许不足。如果你调用`OnCompleted`， 它会立即对所有的观察者调用`OnCompleted`，如果有新的观察者订阅，他们也会立即完成——它并不会首先提供最后一个值。（因此，这是它与缓冲大小为1的`ReplaySubject<T>`不同的另一种方式。）

同样，如果你调用`OnError`，所有当前的观察者将收到`OnError`调用，任何后续的订阅者也将只收到`OnError`调用。

## `AsyncSubject<T>`

`AsyncSubject<T>`为所有观察者提供它接收到的最后一个值。由于它无法知道哪个是最终值，直到调用`OnCompleted`，所以在调用其`OnCompleted`或`OnError`方法之前，它不会对任何订阅者调用任何方法。（如果调用`OnError`，它只会将之转发给所有当前和未来的订阅者。）你通常会间接使用这个主题，因为它是[Rx与`await`关键词集成](13_LeavingIObservable.md#integration-with-async-and-await)的基础。（当你`await`一个可观察序列时，`await`返回源发出的最后一个值。）

如果在`OnCompleted`之前没有进行任何`OnNext`调用，则没有最终值，所以它将仅仅完成任何观察者而不提供值。

在这个示例中，由于序列从未完成，因此不会发布任何值。
控制台上不会显示任何值。

```csharp
AsyncSubject<string> subject = new();
subject.OnNext("a");
subject.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
subject.OnNext("b");
subject.OnNext("c");
```

在这个示例中，我们调用了`OnCompleted`方法，因此主题将产生最终值（'c'）：

```csharp
AsyncSubject<string> subject = new();

subject.OnNext("a");
subject.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
subject.OnNext("b");
subject.OnNext("c");
subject.OnCompleted();
subject.Subscribe(x => Console.WriteLine($"Sub2: {x}"));
```

这将产生以下输出：

```
Sub1: c
Sub2: c
```

如果你有一些在应用程序启动时需要完成的可能比较慢的工作，并且只需要完成一次，你可能会选择一个`AsyncSubject<T>`来使这项工作的结果可用。需要这些结果的代码可以订阅这个主题。如果工作尚未完成，他们将在结果可用时立即接收到结果。如果工作已经完成，他们将立即接收到。

## 主题工厂

最后值得注意的是，你也可以通过工厂方法创建一个主题。考虑到一个主题结合了`IObservable<T>`和`IObserver<T>`接口，合理的做法是应该有一个工厂方法允许你自己组合这两者。`Subject.Create(IObserver<TSource>, IObservable<TResult>)`工厂方法就提供了这种可能。

```csharp
// 从指定的观察者创建一个主题，这个观察者用于向主题发布消息
// 并从observable订阅从主题发送的消息
public static ISubject<TSource, TResult> Create<TSource, TResult>(
    IObserver<TSource> observer, 
    IObservable<TResult> observable)
{...}
```

注意，与刚才讨论的所有其他主题不同，这里创建的主题在输入和输出之间没有固有的关系。它只是将你提供的任何`IObserver<TSource>`和`IObserver<TResult>`实现包装在一个对象中。对主题的`IObserver<TSource>`方法的所有调用将直接传递给你提供的观察者。如果你想让值显现给相应的`IObservable<TResult>`的订阅者，那就需要你来实现。这实际上是用最少的代码将你提供的两个对象组合起来。

主题提供了一个方便的方式来探索Rx，并且在生产场景中偶尔也有用，但在大多数情况下不推荐使用它们。具体解释请参见[使用指南附录](C_UsageGuidelines.md)。而不是使用主题，更应该使用本章前面展示的工厂方法。

## 总结

我们已经查看了各种积极和懒惰的方法来创建序列。我们已经看到如何使用各种工厂方法来生产基于定时器的序列。我们还探讨了从其他同步和异步表示的过渡方式。

快速回顾一下：

- 工厂方法
  - Observable.Return
  - Observable.Empty
  - Observable.Never
  - Observable.Throw
  - Observable.Create
  - Observable.Defer

- 生成方法
  - Observable.Range
  - Observable.Generate
  - Observable.Interval
  - Observable.Timer

- 适应
  - Observable.Start
  - Observable.FromEventPattern
  - Task.ToObservable
  - Task&lt;T&gt;.ToObservable
  - IEnumerable&lt;T&gt;.ToObservable
  - Observable.FromAsyncPattern

创建一个可观察序列是我们应用Rx的第一步：创建序列然后将其供消费。现在我们已经对如何创建一个可观察的序列有了一个坚实的了解，我们可以更详细地查看允许我们描述要应用的处理的操作符，以构建更复杂的可观察序列。
