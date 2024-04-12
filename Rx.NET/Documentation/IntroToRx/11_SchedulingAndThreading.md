# 调度和线程

Rx 主要是一个用于异步处理_运动中的数据_的系统。如果我们处理多个信息源，这些源可能会同时生成数据。在处理数据时，我们可能希望某种程度的并行性，以达到我们的可扩展性目标。因此，我们需要控制系统的这些方面。

到目前为止，我们已经设法避免了任何明确使用线程或并发。我们已经看到了一些必须处理时序以完成工作的方法。（例如，`Buffer`、`Delay` 和 `Sample` 需要安排在特定的计划上工作。）然而，我们依赖于默认行为，虽然默认值通常能完成我们想要的操作，但有时我们需要更多的控制。这一章将探讨 Rx 的调度系统，它提供了一种优雅的方式来管理这些问题。

## Rx、线程和并发

Rx 不限制我们使用哪些线程。一个 `IObservable<T>` 可以自由地在任何线程上调用它的订阅者的 `OnNext/Completed/Error` 方法，可能每次调用都在不同的线程上。尽管这样，Rx 有一个方面可以阻止混乱：可观察源必须在所有情况下遵守[Rx 序列的基本规则](02_KeyTypes.md#the-fundamental-rules-of-rx-sequences)。

当我们第一次探索这些规则时，我们关注的是它们如何确定对任何单个观察者的调用顺序。可以进行任意数量的 `OnNext` 调用，但一旦调用了 `OnError` 或 `OnCompleted`，就不能再有更多的调用。但现在我们看到并发，这些规则的另一个方面变得更重要：对于任何单一订阅，可观察源不得对该订阅的观察者进行并发调用。所以如果源调用了 `OnNext`，它必须等到该调用返回后才能再次调用 `OnNext` 或调用 `OnError` 或 `OnComplete`。

对于观察者来说，好处是只要你的观察者只涉及一次订阅，它将永远只需要一次处理一件事。无论它订阅的源是否是一个涉及许多不同操作符的长而复杂的处理链。即使您通过组合多个输入来构建该源（例如，使用 [`Merge`](09_CombiningSequences.md#merge)），基本规则要求，如果你只在一个 `IObservable<T>` 上调用了 `Subscribe` 一次，那么源永远不允许对你的 `IObserver<T>` 方法进行多个并发调用。

所以虽然每次调用可能在不同的线程上进行，这些调用是严格按顺序进行的（除非单个观察者涉及多个订阅）。

接收传入通知并产生通知的 Rx 操作符将在收到传入通知的线程上通知它们的观察者。假设你有一系列的操作符，如下所示：

```csharp
source
    .Where(x => x.MessageType == 3)
    .Buffer(10)
    .Take(20)
    .Subscribe(x => Console.WriteLine(x));
```

当调用 `Subscribe` 时，我们就得到了一连串的观察者。传递给 `Take` 返回的可观察对象的 Rx 提供的观察者将依次创建一个订阅到 `Buffer` 返回的可观察对象的观察者，然后又会创建一个订阅到 `Where` 可观察对象的观察者，而 `Where` 又创建了订阅到 `source` 的另一个观察者。

因此，当 `source` 决定生成一个项时，它会调用 `Where` 操作符的观察者的 `OnNext`。这将调用谓词，如果 `MessageType` 确实是 3，`Where` 观察者将在同一线程上调用 `Buffer` 观察者的 `OnNext`。`Where` 观察者的 `OnNext` 不会返回，直到 `Buffer` 观察者的 `OnNext` 返回。现在，如果 `Buffer` 观察者确定它已经完全填充了一个缓冲区（例如，它刚刚收到了第 10 个项），那么它还不会返回——它将调用 `Take` 观察者的 `OnNext`，只要 `Take` 还没有收到 20 个缓冲区，它将调用 Rx 提供的将调用我们回调的观察者的 `OnNext`。

因此，对于所有成功到达 `Console.WriteLine` 的源通知，在订阅中传递的回调都会在堆栈上有很多嵌套的调用：

```
`source` 调用：
  `Where` 观察者，它调用：
    `Buffer` 观察者，它调用：
      `Take` 观察者，它调用：
        `Subscribe` 观察者，它调用我们的 lambda
```

这一切都发生在一个线程上。大多数 Rx 操作符都没有任何特定的线程归属。它们只是在调用来自的任何线程上完成任务。这使得 Rx 相当高效。从一个操作符传递数据到下一个操作符只涉及一个方法调用，这些调用相当快。（事实上，通常还有更多层。Rx 倾向于添加一些包装来处理错误和提前取消订阅。所以调用栈会比我刚展示的复杂一些。但它通常还是只是方法调用。）

有时你会听到 Rx 被描述为有一个_自由线程_模型。这仅仅意味着操作符通常不关心它们使用哪个线程。正如我们将看到的，有例外，但这种由一个操作符直接调用下一个操作符是常规做法。

这的其中一个好处是，通常是原始源决定使用哪个线程。下面这个例子通过创建一个主题，然后在各种线程上调用 `OnNext` 并报告线程 id 来说明这一点：

```csharp
Console.WriteLine($"Main thread: {Environment.CurrentManagedThreadId}");
var subject = new Subject<string>();

subject.Subscribe(
    m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));

object sync = new();
ParameterizedThreadStart notify = arg =>
{
    string message = arg?.ToString() ?? "null";
    Console.WriteLine(
        $"OnNext({message}) on thread: {Environment.CurrentManagedThreadId}");
    lock (sync)
    {
        subject.OnNext(message);
    }
};

notify("Main");
new Thread(notify).Start("First worker thread");
new Thread(notify).Start("Second worker thread");
```

输出：

```
Main thread: 1
OnNext(Main) on thread: 1
Received Main on thread: 1
OnNext(First worker thread) on thread: 10
Received First worker thread on thread: 10
OnNext(Second worker thread) on thread: 11
Received Second worker thread on thread: 11
```

在每个案例中，传递给 `Subscribe` 的处理器都在发出 `subject.OnNext` 调用的同一线程上被调用。这种方式简单且高效。然而，事情并不总是这么简单。

## 定时调用

某些通知不会是源提供项的直接结果。例如，Rx 提供了一个 [`Delay`](12_Timing.md#delay) 操作符，它会延迟项的交付。以下例子基于前面的例子，主要区别在于我们不再直接订阅源。我们通过 `Delay` 进行订阅：

```csharp
Console.WriteLine($"Main thread: {Environment.CurrentManagedThreadId}");
var subject = new Subject<string>();

subject
    .Delay(TimeSpan.FromSeconds(0.25))
    .Subscribe(
    m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));

object sync = new();
ParameterizedThreadStart notify = arg =>
{
    string message = arg?.ToString() ?? "null";
    Console.WriteLine(
        $"OnNext({message}) on thread: {Environment.CurrentManagedThreadId}");
    lock (sync)
    {
        subject.OnNext(message);
    }
};

notify("Main 1");
Thread.Sleep(TimeSpan.FromSeconds(0.1));
notify("Main 2");
Thread.Sleep(TimeSpan.FromSeconds(0.3));
notify("Main 3");
new Thread(notify).Start("First worker thread");
Thread.Sleep(TimeSpan.FromSeconds(0.1));
new Thread(notify).Start("Second worker thread");

Thread.Sleep(TimeSpan.FromSeconds(2));
```

这也在发送源项之间等待了一段时间，所以我们可以看到 `Delay` 的效果。这是输出：

```
Main thread: 1
OnNext(Main 1) on thread: 1
OnNext(Main 2) on thread: 1
Received Main 1 on thread: 12
Received Main 2 on thread: 12
OnNext(Main 3) on thread: 1
OnNext(First worker thread) on thread: 13
OnNext(Second worker thread) on thread: 14
Received Main 3 on thread: 12
Received First worker thread on thread: 12
Received Second worker thread on thread: 12
```

注意，在这种情况下，每条 `Received` 消息都在线程 id 12 上，这与发出通知的任何三个线程都不同。

这不应该完全令人惊讶。Rx 在这里使用原始线程的唯一方法是 `Delay` 阻塞线程指定的时间（这里是四分之一秒）然后再转发调用。这对于大多数场景来说是不可接受的，所以 `Delay` 操作符安排在适当的延迟后进行回调。正如您从输出中看到的，这似乎都发生在一个特定的线程上。不管哪个线程调用了 `OnNext`，延迟通知都会在线程 id 12 上到达。但这不是 `Delay` 操作符创建的线程。这是因为 `Delay` 使用了一个_调度器_。

## 调度器

调度器主要有三项功能：

* 确定执行工作的上下文（例如，某个特定线程）
* 决定何时执行工作（例如，立即执行或延迟执行）
* 跟踪时间

以下是一个简单的例子，用来探索这些功能的前两项：

```csharp
Console.WriteLine($"Main thread: {Environment.CurrentManagedThreadId}");

Observable
    .Range(1, 5)
    .Subscribe(m => 
      Console.WriteLine(
        $"Received {m} on thread: {Environment.CurrentManagedThreadId}"));

Console.WriteLine("Subscribe returned");
Console.ReadLine();
```

这可能看起来与调度没有什么关系，但事实上，`Range` 总是使用调度器来完成工作。我们只是让它使用了它的默认调度器。这是输出结果：

```
Main thread: 1
Received 1 on thread: 1
Received 2 on thread: 1
Received 3 on thread: 1
Received 4 on thread: 1
Received 5 on thread: 1
Subscribe returned
```

从我们列出的调度器要做的前两项任务中，我们可以看到这项工作执行的上下文是我调用 `Subscribe` 的线程。至于它决定何时执行工作，它决定在 `Subscribe` 返回之前立即完成所有工作。所以你可能会认为 `Range` 立即产生了我们请求的所有项，然后才返回。然而，这并不像看起来那么简单。我们来看看如果我们同时运行多个 `Range` 实例会发生什么。这引入了一个额外的操作符：一个调用 `Range` 的 `SelectMany`：

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => Observable.Range(i * 10, 5))
    .Subscribe(m => 
      Console.WriteLine(
        $"Received {m} on thread: {Environment.CurrentManagedThreadId}"));
```

输出结果显示，`Range` 实际上并不一定立即产生它的所有项：

```
Received 10 on thread: 1
Received 11 on thread: 1
Received 20 on thread: 1
Received 12 on thread: 1
Received 21 on thread: 1
Received 30 on thread: 1
Received 13 on thread: 1
Received 22 on thread: 1
Received 31 on thread: 1
Received 40 on thread: 1
Received 14 on thread: 1
Received 23 on thread: 1
Received 32 on thread: 1
Received 41 on thread: 1
Received 50 on thread: 1
Received 24 on thread: 1
Received 33 on thread: 1
Received 42 on thread: 1
Received 51 on thread: 1
Received 34 on thread: 1
Received 43 on thread: 1
Received 52 on thread: 1
Received 44 on thread: 1
Received 53 on thread: 1
Received 54 on thread: 1
Subscribe returned
```

第一个嵌套的 `Range` 由 `SelectMany` 回调生成了一些值（10 和 11），但然后第二个才开始输出它的第一个值（20），在第一个生成第三个值（12）之前。你可以看到这里有一些交错的进展。所以尽管工作执行的上下文仍然是我们调用 `Subscribe` 的那个线程，调度器做出的第二个选择——何时执行工作——比最初看起来的要微妙。这告诉我们，`Range` 并不像这种幼稚实现那么简单：

```csharp
public static IObservable<int> NaiveRange(int start, int count)
{
    return System.Reactive.Linq.Observable.Create<int>(obs =>
    {
        for (int i = 0; i < count; i++)
        {
            obs.OnNext(start + i);
        }

        return Disposable.Empty;
    });
}
```

如果 `Range` 就像这样工作，这段代码将产生第一个由 `SelectMany` 回调返回的 `Range` 的所有项，然后再继续到下一个。实际上，Rx 确实提供了一个调度器，如果这是我们想要的，它就会给我们这种行为。这个例子将 `ImmediateScheduler.Instance` 传递给嵌套的 `Observable.Range` 调用：

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => Observable.Range(i * 10, 5, ImmediateScheduler.Instance))
    .Subscribe(
    m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));
```

这是结果：

```
Received 10 on thread: 1
Received 11 on thread: 1
Received 12 on thread: 1
Received 13 on thread: 1
Received 14 on thread: 1
Received 20 on thread: 1
Received 21 on thread: 1
Received 22 on thread: 1
Received 23 on thread: 1
Received 24 on thread: 1
Received 30 on thread: 1
Received 31 on thread: 1
Received 32 on thread: 1
Received 33 on thread: 1
Received 34 on thread: 1
Received 40 on thread: 1
Received 41 on thread: 1
Received 42 on thread: 1
Received 43 on thread: 1
Received 44 on thread: 1
Received 50 on thread: 1
Received 51 on thread: 1
Received 52 on thread: 1
Received 53 on thread: 1
Received 54 on thread: 1
Subscribe returned
```

通过在最内层调用 `Observable.Range` 时指定 `ImmediateScheduler.Instance`，我们请求了一个特定的策略：这会在调用者的线程上调用所有工作，并且总是立即这样做。这并不是 `Range` 的默认行为的几个原因之一。（其默认值是 `Scheduler.CurrentThread`，始终返回 `CurrentThreadScheduler` 的实例。）首先，`ImmediateScheduler.Instance` 可能导致相当深的调用堆栈。大多数其他调度器维护工作队列，因此如果某个操作符决定在另一个进行中做某些新的工作（例如，嵌套的 `Range` 操作符决定开始发射它的值），而不是立即开始该工作（这将涉及调用将执行工作的方法），那么该工作可以被放在队列中，使正在进行的工作先完成再开始下一件事。如果到处使用即时调度器，当查询变得复杂时可能导致栈溢出。第二个原因是 `Range` 未默认使用即时调度器的目的，是为了在多个可观察对象同时活跃时，它们都能取得一些进展——`Range` 会尽可能快地产生它的所有项，所以如果它不使用一个使操作符轮流进行的调度器，它可能会耗尽其他操作符的 CPU 时间。

注意，在两个例子中，“Subscribe returned” 消息都是最后出现的。所以尽管 `CurrentThreadScheduler` 并不像即时调度器那样急切，它仍然不会在完成所有未完成的工作之前返回给它的调用者。它维护一个工作队列，使公平性稍微提高一些，并避免栈溢出，但只要有人要求 `CurrentThreadScheduler` 做某事，它不会返回直到它排空了队列。

并不是所有的调度器都具备这个特点。这是早期示例的一个变体，我们只调用了一次 `Range`，没有嵌套的可观察对象。这次我要求它使用 `TaskPoolScheduler`。

```csharp
Observable
    .Range(1, 5, TaskPoolScheduler.Default)
    .Subscribe(
    m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));
```

这对于决定运行工作的上下文做出了不同的决定，比起即刻和当前线程的调度器，我们可以从它的输出中看出：

```
Main thread: 1
Subscribe returned
Received 1 on thread: 12
Received 2 on thread: 12
Received 3 on thread: 12
Received 4 on thread: 12
Received 5 on thread: 12
```

注意，通知都发生在与我们调用 `Subscribe` 的线程（id 1）不同的线程（id 12）上。这是因为 `TaskPoolScheduler` 的定义特性是通过任务并行库（TPL）的任务池调用所有工作。这就是我们看到不同线程 id 的原因：任务池并不拥有我们应用程序的主线程。在这种情况下，它没有看到有必要启动多个线程。这是合理的，这里只有一个来源依次提供一个项目。我们这种情况下没有得到更多线程是好事——线程池在单个线程依次处理工作项时最有效，因为它避免了上下文切换的开销，并且由于这里实际上没有并行工作的范围，如果它创建了多个线程，我们将一无所获。

这个调度器还有一个非常重要的不同之处：请注意，调用 `Subscribe` 的返回在任何通知到达我们的观察者之前。这是因为这是我们看到的第一个确实引入实际并行性的调度器。`ImmediateScheduler` 和 `CurrentThreadScheduler` 从不会自己启动新线程，无论执行的操作符可能希望执行多少并发操作。而尽管 `TaskPoolScheduler` 认为没有必要创建多个线程，它创建的一个线程与应用程序的主线程不同，这意味着主线程可以与此订阅并行运行。由于 `TaskPoolScheduler` 不会在启动工作的线程上做任何工作，它可以在将工作排队后立即返回，使 `Subscribe` 方法能够立即返回。

如果我们在嵌套的可观察对象的示例中使用 `TaskPoolScheduler` 会怎样？这只在对 `Range` 的内部调用中使用它，所以外部的一个仍然会使用默认的 `CurrentThreadScheduler`：

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => Observable.Range(i * 10, 5, TaskPoolScheduler.Default))
    .Subscribe(
    m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));
```

现在我们可以看到涉及更多线程：

```
Received 10 on thread: 13
Received 11 on thread: 13
Received 12 on thread: 13
Received 13 on thread: 13
Received 40 on thread: 16
Received 41 on thread: 16
Received 42 on thread: 16
Received 43 on thread: 16
Received 44 on thread: 16
Received 50 on thread: 17
Received 51 on thread: 17
Received 52 on thread: 17
Received 53 on thread: 17
Received 54 on thread: 17
Subscribe returned
Received 14 on thread: 13
Received 20 on thread: 14
Received 21 on thread: 14
Received 22 on thread: 14
Received 23 on thread: 14
Received 24 on thread: 14
Received 30 on thread: 15
Received 31 on thread: 15
Received 32 on thread: 15
Received 33 on thread: 15
Received 34 on thread: 15
```

由于我们在这个示例中只有一个观察者，Rx 的规则要求一次给出一个项目，所以实际上这里真的没有并行性的范围，但更复杂的结构会导致最初进入调度器队列的工作项比前面的示例更多，这可能是为什么这次的工作被多个线程接手。实际上，这些线程中的大多数可能会在 `SelectMany` 内部的代码中阻塞，确保它每次向目标观察者提供一个项目。项目的顺序有点令人惊讶没有更多的混乱。子范围本身似乎是随机出现的，但它几乎是按顺序产生项目的（项目 14 是唯一的例外）。这与 `Range` 如何与 `TaskPoolScheduler` 互动的一个怪癖有关。

我还没有讨论调度器的第三项工作：跟踪时间。这与 `Range` 无关，因为它试图尽可能快地产生所有的项目。但对于我在[定时调用](#timed-invocation)部分中展示的 `Delay` 操作符，时间显然是一个关键元素。实际上，这将是展示调度器提供的 API 的一个很好的时机：

```csharp
public interface IScheduler
{
    DateTimeOffset Now { get; }
    
    IDisposable Schedule<TState>(TState state, 
                                 Func<IScheduler, TState, IDisposable> action);
    
    IDisposable Schedule<TState>(TState state, 
                                 TimeSpan dueTime, 
                                 Func<IScheduler, TState, IDisposable> action);
    
    IDisposable Schedule<TState>(TState state, 
                                 DateTimeOffset dueTime, 
                                 Func<IScheduler, TState, IDisposable> action);
}
```

你可以看到，这些中只有一个与时间有关。只有第一个 `Schedule` 重载不是，当操作符希望建议工作尽可能快地运行时，会调用那个。这是 `Range` 使用的重载。（严格来说，`Range` 会询问调度器是否支持长时间运行的操作，在这种操作中，操作符可以暂时控制一个线程较长时间。它更喜欢在可能的情况下使用这种方式，因为它往往比为每个单独项目提交工作给调度器更有效。`TaskPoolScheduler` 支持长时间运行的操作，这解释了我们之前看到的略有意外的输出，但 `CurrentThreadScheduler`（`Range` 的默认选择）则不支持。所以默认情况下，`Range` 将调用第一个 `Schedule` 重载来为它希望产生的每个项目安排一次。）

`Delay` 使用第二个重载。具体实现相当复杂（主要是因为它如何在繁忙的来源导致其落后时有效地赶上），但本质上，每次新的项目进入 `Delay` 操作符时，它都会安排一个工作项在配置的延迟后运行，以便可以在预期的时间偏移后将该项目提供给其订阅者。

调度器必须负责管理时间，因为 .NET 有几种不同的计时器机制，而计时器的选择通常由你希望处理计时器回调的上下文来决定。由于调度器确定工作运行的上下文，这意味着它们还必须选择计时器类型。例如，UI框架通常提供计时器，可以在适合更新用户界面的上下文中调用它们的回调。Rx 提供了一些特定于 UI 框架的调度器，使用这些计时器，但这些对于其他场景来说可能是不合适的选择。因此，每个调度器都使用适合其将要运行工作项的上下文的计时器。

这有个有用的后果：因为 `IScheduler` 提供了一个抽象的计时相关细节，所以可以对时间进行虚拟化。这对于测试非常有用。如果你查看 [Rx 代码库](https://github.com/dotnet/reactive) 中的广泛测试套件，你会发现有许多测试验证了与时间相关的行为。如果这些测试以实时运行，测试套件将花费太长时间，并且也可能产生偶尔的错误结果，因为与测试同时在同一台机器上运行的后台任务偶尔会以某种方式改变执行速度，这可能会混淆测试。相反，这些测试使用了一种专门的调度器，可以完全控制时间的流逝。（有关更多信息，请参阅稍后的[测试调度器部分](#test-schedulers)，还有一个即将讨论的[测试章节](16_TestingRx.md)。）

注意，所有三个 `IScheduler.Schedule` 方法都需要一个回调。调度器将在它选择的时间和上下文中调用此回调。调度器回调的第一个参数是另一个 `IScheduler`。这在需要重复调用的场景中使用，我们稍后将看到。

Rx 提供了几种调度器。以下部分描述了最广泛使用的一些。

### ImmediateScheduler

`ImmediateScheduler` 是 Rx 提供的最简单的调度器。正如您在之前的部分中看到的，无论何时被要求安排一些工作，它只是立即运行它。它在其 `IScheduler.Schedule` 方法内部这样做。

这是一个非常简单的策略，它使得 `ImmediateScheduler` 非常高效。因此，许多操作符默认使用 `ImmediateScheduler`。然而，对于立即产生多个项目的操作符来说，这可能是个问题，尤其是当项目数量可能很大时。例如，Rx 为 `IEnumerable<T>` 定义了 [`ToObservable` 扩展方法](03_CreatingObservableSequences.md#from-ienumerablet)。当你订阅由此返回的 `IObservable<T>` 时，它会立即开始遍历集合，并且如果你告诉它使用 `ImmediateScheduler`，`Subscribe` 将不会返回直到它到达集合的末尾。这对于无限序列显然是个问题，这就是为什么这种类型的操作符默认不使用 `ImmediateScheduler`。

`ImmediateScheduler` 在调用采用 `TimeSpan` 的 `Schedule` 重载时也有潜在令人惊讶的行为。这要求调度器在指定的时间长度后运行一些工作。它实现这一点的方式是调用 `Thread.Sleep`。对于 Rx 的大多数调度器来说，这种重载将安排某种形式的计时器机制稍后运行代码，使当前线程可以继续进行其业务，但 `ImmediateScheduler` 在这里忠实地以其名字为标准，拒绝从事这种延迟执行。它只是阻塞当前线程，直到是时候做工作。这意味着像由 `Interval` 返回的基于时间的可观察对象将可以工作，如果你指定了这个调度器，但代价是阻止线程做任何其他事情。

采用 `DateTime` 的 `Schedule` 重载略有不同。如果你指定的时间小于未来 10 秒，它会像使用 `TimeSpan` 那样阻止调用线程。但如果你传递的 `DateTime` 更远的未来，它会放弃立即执行，并改用使用计时器。

### CurrentThreadScheduler

`CurrentThreadScheduler` 与 `ImmediateScheduler` 非常相似。不同之处在于它如何处理当前线程已经在处理现有工作项时的安排工作请求。如果你将多个使用调度器的操作符链接在一起，这种情况就会发生。

要理解发生了什么，了解产生多个项目的快速连续源是有帮助的，例如 `IEnumerable<T>` 的 [`ToObservable` 扩展方法](03_CreatingObservableSequences.md#from-ienumerablet) 或 [`Observable.Range`](03_CreatingObservableSequences.md#observablerange)，这些源如何使用调度器。这些类型的操作符不使用普通的 `for` 或 `foreach` 循环。它们通常为每次迭代安排一个新的工作项（除非调度器恰好为长时间运行的工作提供特殊规定）。而 `ImmediateScheduler` 将立即运行此类工作，`CurrentThreadScheduler` 会检查它是否已在此线程上处理工作项。我们在前面的示例中看到了这一点：

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => Observable.Range(i * 10, 5))
    .Subscribe(
        m => Console.WriteLine($"Received {m} on thread: {Environment.CurrentManagedThreadId}"));
```

让我们准确地跟踪这里发生了什么。首先，假设这段代码正常运行并且没有在任何不寻常的上下文中运行——也许在程序的 `Main` 入口点内。当这段代码调用由 `SelectMany` 返回的 `IObservable<int>` 的 `Subscribe` 时，它将依次调用由第一个 `Observable.Range` 返回的 `IObservable<int>` 的 `Subscribe`，这将依次安排一个工作项来生成范围中的第一个值（`1`）。

由于我们没有向 `Range` 明确传递调度器，它将使用其默认选择，即 `CurrentThreadScheduler`，并会问自己：“我是否已在此线程中处理一些工作项？”。在这种情况下，答案将是“否”，所以它会立即运行工作项（在 `Range` 操作符发出的 `Schedule` 调用返回之前）。然后，`Range` 操作符将产生其第一个值，调用 `SelectMany` 操作符提供的 `IObserver<int>` 的 `OnNext`。

`SelectMany` 操作符的 `OnNext` 方法现在将调用其 lambda，传入提供的参数（来自 `Range` 操作符的值 `1`）。您可以从上面的示例中看到，这个 lambda 再次调用 `Observable.Range`，返回一个新的 `IObservable<int>`。`SelectMany` 将立即订阅此（在其 `OnNext` 返回之前）。这是第二次调用由 `Range` 返回的 `IObservable<int>` 的 `Subscribe`（但这是不同的实例），`Range` 将再次默认使用 `CurrentThreadScheduler`，并再次安排一个工作项来执行第一次迭代。

所以再一次，`CurrentThreadScheduler` 会问自己：“我是否已在此线程中处理一些工作项？” 但这次，答案是肯定的。这就是行为不同于 `ImmediateScheduler` 的地方。`CurrentThreadScheduler` 为每个使用它的线程维护一个工作队列，并在这种情况下只是将新安排的工作添加到队列中，并立即从 `SelectMany` 操作符的 `OnNext` 返回。

`SelectMany` 现在已完成处理来自第一个 `Range` 的此项目（值 `1`）的处理，所以它的 `OnNext` 返回。此时，这个外部的 `Range` 操作符安排另一个工作项。再次，`CurrentThreadScheduler` 将检测到它当前正在处理一个工作项，所以它只是将此添加到队列中。

安排了生成其第二个值（`2`）的工作项之后，`Range` 操作符返回。请记住，此时运行的 `Range` 操作符中的代码是第一个安排的工作项的回调，所以它返回到 `CurrentThreadScheduler`——我们回到了其 `Schedule` 方法内部（由 range 操作符的 `Subscribe` 方法调用）。

此时，`CurrentThreadScheduler` 不会从 `Schedule` 返回，因为它会检查工作队列，并会看到现在队列中有两个项目。（有一个是嵌套的 `Range` 可观察对象安排的用于生成其第一个值的工作项，还有一个是顶级的 `Range` 可观察对象刚刚安排的用于生成其第二个值的工作项。）`CurrentThreadScheduler` 现在将执行其中的第一个：嵌套的 `Range` 操作符现在将生成其第一个值（将是 `10`），因此它调用由 `SelectMany` 提供的观察者的 `OnNext`，然后调用由示例中顶级的 `Subscribe` 调用提供的观察者，这个观察者只是调用我们传递给 `Subscribe` 的 lambda，导致我们的 `Console.WriteLine` 运行。返回之后，嵌套的 `Range` 操作符将安排另一个工作项来生成其第二个项目。同样，`CurrentThreadScheduler` 将意识到它已在此线程上处理一个工作项，因此它只是将其放在队列中，然后立即从 `Schedule` 返回。嵌套的 `Range` 操作符现在完成了这次迭代，因此它返回给调度器。调度器现在将拿起队列中的下一项，这种情况下是由顶级的 `Range` 添加的用于生成第二个项目的工作项。

并且它继续。当工作正在进行中时，工作项的排队就是使多个可观察来源能够并行进行的原因。

相比之下，`ImmediateScheduler` 会立即运行新的工作项，这就是我们没有看到这种并行进展的原因。

（严格准确地说，在某些情况下，`ImmediateScheduler` 无法立即运行工作。在这些迭代情形中，它实际上提供了一个稍有不同的调度器，操作符用于安排第一个项目之后的所有工作，这将检查它是否被要求同时处理多个工作项。如果是这样的话，它将退回到类似于 `CurrentThreadScheduler` 的排队策略，反之是一个局部于初始项目的队列，而不是每个线程的队列。这避免了由于多线程引起的问题，也避免了迭代操作符在当前工作项的处理程序内部安排新的工作项导致的栈溢出。因为队列不是跨线程中所有工作共享的，这仍然具有确保由工作项排队的任何嵌套工作在调用 `Schedule` 返回之前完成的效果。所以即使这种排队踢踏，我们通常也不会看到像我们在 `CurrentThreadScheduler` 中所做的那样不同来源的工作交织在一起。例如，如果我们告诉嵌套的 `Range` 使用 `ImmediateScheduler`，这种排队行为将在 `Range` 开始迭代时踢踏，但因为队列是局部于由那个嵌套的 `Range` 执行的初始工作项的，它将在返回之前产生所有嵌套的 `Range` 项目。）

### DefaultScheduler

`DefaultScheduler` 旨在用于可能需要分散时间进行或可能需要并发执行的工作。这些功能意味着这不能保证在任何特定线程上运行工作，并且实际上它通过 CLR 的线程池安排工作。这是 Rx 的所有基于时间的操作符默认的调度器，也是可以将 .NET 方法作为 `IObservable<T>` 包装的 `Observable.ToAsync` 操作符的默认调度器。

尽管如果你希望工作不在当前线程上发生，这个调度器很有用——也许你正在编写一个带有用户界面的应用程序，希望避免在负责更新UI和响应用户输入的线程上做太多工作——但它可能在任何线程上运行工作的事实可能会使情况变得复杂。如果你想所有工作都在一个线程上进行，只是不是你现在所在的线程怎么办？有另一个调度器适用于此。

### EventLoopScheduler

`EventLoopScheduler` 提供一次一个的调度，将新安排的工作项排入队列。这类似于如果你只从一个线程使用它的话，`CurrentThreadScheduler` 的工作方式。不同之处在于 `EventLoopScheduler` 为此工作创建了一个专用线程，而不是使用你恰好从中安排工作的线程。

与我们迄今为止考察的调度器不同，没有静态属性可以获得 `EventLoopScheduler`。这是因为每个都有自己的线程，所以你需要显式创建一个。它提供了两个构造函数：

```csharp
public EventLoopScheduler()
public EventLoopScheduler(Func<ThreadStart, Thread> threadFactory)
```

第一个为您创建一个线程。第二个允许您控制线程创建过程。它会调用您提供的回调，并将向此传递其自己的回调，您需要在新创建的线程上运行此回调。

`EventLoopScheduler` 实现了 `IDisposable`，调用 Dispose 将允许线程终止。这可以很好地与 `Observable.Using` 方法一起使用。以下示例展示了如何使用 `EventLoopScheduler` 在专用线程上迭代 `IEnumerable<T>` 的所有内容，并确保我们完成后线程退出：

```csharp
IEnumerable<int> xs = GetNumbers();
Observable
    .Using(
        () => new EventLoopScheduler(),
        scheduler => xs.ToObservable(scheduler))
    .Subscribe(...);
```

### NewThreadScheduler

`NewThreadScheduler` 创建一个新线程来执行它给定的每个工作项。这在大多数情况下可能没有意义。但是，在您想要执行一些长时间运行的工作，并通过 `IObservable<T>` 表示其完成时，这可能是有用的。`Observable.ToAsync` 正是这样做的，并且通常会使用 `DefaultScheduler`，这意味着它会在线程池线程上运行工作。但如果工作可能需要一两秒以上，线程池可能不是一个好的选择，因为它针对短执行时间进行了优化，其管理线程池大小的启发式算法并不适用于长时间运行的操作。在这种情况下，`NewThreadScheduler` 可能是一个更好的选择。

尽管每次调用 `Schedule` 都会创建一个新线程，`NewThreadScheduler` 在工作项回调中传递了一个不同的调度器，这意味着任何尝试进行迭代工作的东西都不会为每次迭代创建一个新线程。例如，如果您使用 `NewThreadScheduler` 和 `Observable.Range`，您将在每次订阅生成的 `IObservable<int>` 时获得一个新线程，但您不会为每个项目获得一个新线程，即使 `Range` 确实为每个值产生的工作项安排了一个新的工作项。它通过嵌套调度器将这些按值工作项调度到回调中，嵌套调度器由 `NewThreadScheduler` 提供，并且这些情况下的嵌套调度器在同一线程上调用所有这样的嵌套工作项。

### SynchronizationContextScheduler

这通过 [`SynchronizationContext`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.synchronizationcontext) 调用所有工作。这在用户界面场景中很有用。大多数 .NET 客户端用户界面框架都提供了一个 `SynchronizationContext`，可以用来在适合进行 UI 更改的上下文中调用回调。（通常这涉及在正确的线程上调用它们，但各个实现可以决定什么构成适当的上下文。）

### TaskPoolScheduler

通过使用 [TPL 任务](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) 通过线程池调用所有工作。TPL 是在 CLR 线程池之后许多年引入的，现在是通过线程池启动工作的推荐方式。在添加 TPL 时，线程池会在您通过任务安排工作时使用与依赖较旧的线程池 API 时略有不同的算法。这种新算法使其在某些情况下更有效。文档现在关于这一点相当含糊，因此不清楚这些差异在现代 .NET 上是否仍然存在，但任务继续是使用线程池的推荐机制。出于向后兼容的原因，Rx 的 DefaultScheduler 使用较旧的 CLR 线程池 API。在性能关键的代码中，如果有很多工作在线程池线程上运行，您可以尝试在这些情况下使用 `TaskPoolScheduler` 看看它是否为您的工作负载提供任何性能优势。

### ThreadPoolScheduler

通过使用旧的、在 [TPL](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) 之前的 API 通过线程池调用所有工作。这种类型是一个历史遗留物，可以追溯到并非所有平台都提供相同类型的线程池的时候。在几乎所有情况下，如果您想要这种行为，您应该使用 `DefaultScheduler`（尽管 [`TaskPoolScheduler`](#taskpoolscheduler) 提供了不同的行为，可能是）。使用 `ThreadPoolScheduler` 唯一有差异的场景是编写 UWP 应用程序。`System.Reactive` v6.0 的 UWP 目标为这个类提供了不同于您为所有其他目标获取的不同实现。它使用 `Windows.System.Threading.ThreadPool`，而所有其他目标使用 `System.Threading.ThreadPool`。UWP 版本提供了让您配置 UWP 线程池的特定功能的属性。

实际上，最好在新代码中避免使用这个类。UWP 目标拥有不同的实现的唯一原因是 UWP 过去不提供 `System.Threading.ThreadPool`。但这在 UWP 支持 .NET Standard 2.0 的 Windows 版本 10.0.19041 更改时就发生了。不再有任何好的理由存在 UWP 特定的 `ThreadPoolScheduler`，它是一个引起困惑的来源，因为这种类型在 UWP 目标中非常不同，但它必须因向后兼容的目的而保留。（它可能会被弃用，因为 Rx 7 将解决一些问题，这些问题来自于 `System.Reactive` 组件当前直接依赖 UI 框架的事实。）如果您使用 `DefaultScheduler`，无论您在哪个平台上运行，都将使用 `System.Threading.ThreadPool`。

### UI 框架调度器：ControlScheduler，DispatcherScheduler 和 CoreDispatcherScheduler

尽管 `SynchronizationContextScheduler` 将适用于 .NET 中所有广泛使用的客户端 UI 框架，Rx 提供了更专业的调度器。`ControlScheduler` 适用于 Windows Forms 应用程序，`DispatcherScheduler` 适用于 WPF，`CoreDispatcherScheduler` 适用于 UWP。

这些更专门的类型提供了两个好处。首先，您不必一定要在目标 UI 线程上才能获得这些调度器的实例。而与 `SynchronizationContextScheduler` 不同，通常获取此所需的 `SynchronizationContext` 的唯一方法是在 UI 线程上运行时检索 `SynchronizationContext.Current`。但这些其他 UI 框架特定的调度器可以传递一个合适的 `Control`、`Dispatcher` 或 `CoreDispatcher`，这是可以从非 UI 线程获得的。其次，`DispatcherScheduler` 和 `CoreDispatcherScheduler` 提供了使用 `Dispatcher` 和 `CoreDispatcher` 类型支持的优先级机制的方法。

### 测试调度器

Rx 库定义了几个虚拟化时间的调度器，包括 `HistoricalScheduler`、`TestScheduler`、`VirtualTimeScheduler` 和 `VirtualTimeSchedulerBase`。我们将在[测试章节](16_TestingRx.md)中研究这类调度器。

## SubscribeOn 和 ObserveOn

到目前为止，我已经讨论了为什么一些Rx源需要访问调度器。这对于与时间相关的行为是必要的，也适用于尽快生成项目的源。但请记住，调度器控制三件事：

- 确定执行工作的上下文（例如，某个特定线程）
- 决定何时执行工作（例如，立即执行或推迟执行）
- 跟踪时间

到目前为止的讨论主要集中在第2点和第3点上。当涉及到我们自己的应用程序代码时，我们最有可能使用调度器来控制第一个方面。Rx为`IObservable<T>`定义了两个扩展方法：`SubscribeOn`和`ObserveOn`。这两种方法都需要一个`IScheduler`参数，并返回一个`IObservable<T>`，因此你可以在这些方法之后链式调用更多的操作符。

这些方法的作用与它们的名称所暗示的一样。如果你使用`SubscribeOn`，那么当你在结果的`IObservable<T>`上调用`Subscribe`时，它会安排通过指定的调度器调用原始的`IObservable<T>`的`Subscribe`方法。这里有一个例子：

```csharp
Console.WriteLine($"[T:{Environment.CurrentManagedThreadId}] Main thread");

Observable
    .Interval(TimeSpan.FromSeconds(1))
    .SubscribeOn(new EventLoopScheduler((start) =>
    {
        Thread t = new(start) { IsBackground = false };
        Console.WriteLine($"[T:{t.ManagedThreadId}] Created thread for EventLoopScheduler");
        return t;
    }))
    .Subscribe(tick => 
          Console.WriteLine(
            $"[T:{Environment.CurrentManagedThreadId}] {DateTime.Now}: Tick {tick}"));

Console.WriteLine($"[T:{Environment.CurrentManagedThreadId}] {DateTime.Now}: Main thread exiting");
```

这将调用`Observable.Interval`（默认情况下使用`DefaultScheduler`），但是而不是直接订阅它，而是先取`Interval`返回的`IObservable<T>`并调用`SubscribeOn`。我使用了一个`EventLoopScheduler`，并且我传递了一个工厂回调来创建它将使用的线程，以确保这是一个非后台线程。（默认情况下，`EventLoopScheduler`会创建一个后台线程，这意味着该线程不会强制进程保持活动状态。通常这就是你想要的，但我在这个示例中改变它是为了展示正在发生的事情。）

当我在`SubscribeOn`返回的`IObservable<long>`上调用`Subscribe`时，它会在我提供的`EventLoopScheduler`上调用`Schedule`，在该工作项的回调中，它接着调用原始`Interval`源的`Subscribe`。所以效果是对底层源的订阅不是在我的主线程上发生的，而是在为我的`EventLoopScheduler`创建的线程上发生的。运行程序产生以下输出：

```
[T:1] Main thread
[T:12] Created thread for EventLoopScheduler
[T:1] 21/07/2023 14:57:21: Main thread exiting
[T:6] 21/07/2023 14:57:22: Tick 0
[T:6] 21/07/2023 14:57:23: Tick 1
[T:6] 21/07/2023 14:57:24: Tick 2
...
```

注意我的应用程序的主线程在源开始产生通知之前就退出了。但也请注意新创建的线程的线程id为12，而我的通知却是在不同的线程上，线程id为6！这是怎么回事？

这经常让人们感到困惑。你订阅一个可观察源的调度器并不一定会影响该源一旦运行起来后的行为。记住之前我说过的，`Observable.Interval`默认使用`DefaultScheduler`？好了，我们在这里没有为`Interval`指定调度器，所以它将使用那个默认值。它不在乎我们从哪个上下文调用它的`Subscribe`方法。所以实际上，这里引入`EventLoopScheduler`的唯一影响是保持进程活动，即使它的主线程已经退出。那个调度器线程在它进行最初的`Subscribe`调用进入由`Observable.Interval`返回的`IObservable<long>`后，再也没有被使用过。它只是耐心地等待进一步调用`Schedule`的电话，但这些电话永远也不会来。

不过，并不是所有的源都完全不受调用它们`Subscribe`的上下文影响。例如，如果我用这行代码：

```csharp
    .Interval(TimeSpan.FromSeconds(1))
```

替换为这样：

```csharp
    .Range(1, 5)
```

然后我们得到这样的输出：

```
[T:1] Main thread
[T:12] Created thread for EventLoopScheduler
[T:12] 21/07/2023 15:02:09: Tick 1
[T:1] 21/07/2023 15:02:09: Main thread exiting
[T:12] 21/07/2023 15:02:09: Tick 2
[T:12] 21/07/2023 15:02:09: Tick 3
[T:12] 21/07/2023 15:02:09: Tick 4
[T:12] 21/07/2023 15:02:09: Tick 5
```

现在所有的通知都在线程12上进来，这是为`EventLoopScheduler`创建的线程。注意即使在这里，`Range`也没有使用那个调度器。不同之处在于，`Range`默认使用`CurrentThreadScheduler`，所以它会从你调用它的任何线程产生输出。所以即使它实际上没有使用`EventLoopScheduler`，它确实结束了使用那个调度器的线程，因为我们用那个调度器来订阅`Range`。

所以这说明了`SubscribeOn`确实做了它承诺的事情：它确实确定了从哪个上下文调用`Subscribe`。只是这不总是重要的。如果`Subscribe`做了一些重要的工作，这可能很重要。例如，如果你使用[`Observable.Create`](03_CreatingObservableSequences.md#observablecreate)来创建一个自定义序列，`SubscribeOn`决定了调用你传递给`Create`的回调的上下文。但Rx没有一个'current'调度器的概念——没有办法询问"我是从哪个调度器被调用的？"——所以Rx操作符不会仅仅继承它们被订阅时的上下文中的调度器。

在发出项目时，Rx提供的大多数源归为三类。首先，那些响应来自上游源的输入而产生输出的操作符（例如，`Where`、`Select`或`GroupBy`）通常会在它们自己的`OnNext`里调用它们的观察者方法。所以无论它们的源可观察对象在调用`OnNext`时处于什么上下文，那就是操作符将使用的上下文来调用它的观察者。其次，那些基于迭代或时间产生项目的操作符将使用调度器（无论是明确提供的，还是在没有指定时使用默认类型）。第三，有些源只是从它们喜欢的任何上下文产生项目。例如，如果一个`async`方法使用`await`并指定`ConfigureAwait(false)`，那么在`await`完成后它可能在几乎任何线程和任何上下文中，然后可能继续调用观察者的`OnNext`。

只要一个源遵循[Rx序列的基本规则](02_KeyTypes.md#the-fundamental-rules-of-rx-sequences)，它就可以从它喜欢的任何上下文调用它的观察者的方法。它可以选择接受一个调度器作为输入并使用它，但它并没有这样做的义务。如果你有这样一个难以驾驭的源，你想要驯服它，这时就需要用到`ObserveOn`扩展方法了。考虑以下相当愚蠢的例子：

```csharp
Observable
    .Interval(TimeSpan.FromSeconds(1))
    .SelectMany(tick => Observable.Return(tick, NewThreadScheduler.Default))
    .Subscribe(tick => 
      Console.WriteLine($"{DateTime.Now}-{Environment.CurrentManagedThreadId}: Tick {tick}"));
```

这故意使每个通知都在不同的线程上到达，如此输出所示：

```
Main thread: 1
21/07/2023 15:19:56-12: Tick 0
21/07/2023 15:19:57-13: Tick 1
21/07/2023 15:19:58-14: Tick 2
21/07/2023 15:19:59-15: Tick 3
...
```

（它通过为从`Interval`中产生的每一个tick调用`Observable.Return`，并告诉`Return`使用`NewThreadScheduler`来实现这一点。每次对`Return`的调用都将创建一个新线程。这是一个糟糕的主意，但这是让一个源每次都从不同的上下文中调用的简单方法。）如果我想施加一些顺序，我可以添加一个调用到`ObserveOn`：

```csharp
Observable
    .Interval(TimeSpan.FromSeconds(1))
    .SelectMany(tick => Observable.Return(tick, NewThreadScheduler.Default))
    .ObserveOn(new EventLoopScheduler())
    .Subscribe(tick => 
      Console.WriteLine($"{DateTime.Now}-{Environment.CurrentManagedThreadId}: Tick {tick}"));
```

我在这里创建了一个`EventLoopScheduler`，因为它创建了一个单一的线程，并在那个线程上运行每个计划的工作项目。输出现在显示每次都是同一个线程id（13）：

```
Main thread: 1
21/07/2023 15:24:23-13: Tick 0
21/07/2023 15:24:24-13: Tick 1
21/07/2023 15:24:25-13: Tick 2
21/07/2023 15:24:26-13: Tick 3
...
```

所以虽然每个由`Observable.Return`创建的新可观察对象都创建了一个全新的线程，但`ObserveOn`确保了我的观察者的`OnNext`（以及在调用`OnCompleted`或`OnError`的情况下）通过指定的调度器被调用。

### 在UI应用程序中使用SubscribeOn和ObserveOn

如果你在用户界面中使用Rx，当你处理的信息源不在UI线程上提供通知时，`ObserveOn`非常有用。你可以用`ObserveOn`包装任何`IObservable<T>`，传递一个`SynchronizationContextScheduler`（或特定于框架的类型，如`DispatcherScheduler`），以确保你的观察者在UI线程上接收通知，这样可以安全地更新UI。

`SubscribeOn`在用户界面中也很有用，它可以确保可观察源进行初始化工作时不会在UI线程上进行。

大多数UI框架指定一个特定线程来接收来自用户的通知以及更新UI，对于任何一个窗口来说，避免阻塞这个UI线程是至关重要的，因为这将导致糟糕的用户体验——如果你在UI线程上进行工作，它将无法响应用户输入，直到工作完成。一般来说，如果你导致用户界面无响应时间超过100ms，用户将会变得烦躁，所以你不应该在UI线程上执行任何超过这个时间的工作。当Microsoft首次引入其应用商店（随Windows 8推出）时，它们规定了一个更严格的限制：如果你的应用程序阻塞UI线程超过50ms，它可能不会被允许进入商店。随着现代处理器提供的处理能力，你可以在50ms内完成很多处理。即使在移动设备上相对低功率的处理器上，这也足以执行数百万条指令。不过，任何涉及I/O（读写文件或等待来自任何类型网络服务的响应）的事情都不应该在UI线程上完成。创建响应式UI应用程序的一般模式是：

- 接收关于某种用户操作的通知
- 如果需要慢速工作，请在后台线程上进行
- 将结果传回UI线程
- 更新UI

这非常适合Rx：响应事件，可能组合多个事件，将数据传递给链式方法调用。随着调度的加入，我们甚至有了在UI线程之外和回到UI线程上的能力，以实现用户所需的响应式应用感觉。

考虑一个使用Rx来填充`ObservableCollection<T>`的WPF应用程序。你可以使用`SubscribeOn`确保主要工作不是在UI线程上完成的，接着使用`ObserveOn`确保你被通知回到正确的线程。如果你未使用`ObserveOn`方法，那么你的`OnNext`处理程序将在发出通知的同一个线程上被调用。在大多数UI框架中，这将导致某种不支持/跨线程异常。在这个例子中，我们订阅`Customers`序列。我使用`Defer`，这样如果`GetCustomers`在返回其`IObservable<Customer>`之前做了一些慢速的初始工作，这不会发生在我们订阅之前。然后我们使用`SubscribeOn`调用那个方法，并在任务池线程上执行订阅。然后我们确保当我们接收到`Customer`通知时，我们将它们添加到`Dispatcher`的`Customers`集合中。

```csharp
Observable
    .Defer(() => _customerService.GetCustomers())
    .SubscribeOn(TaskPoolScheduler.Default)
    .ObserveOn(DispatcherScheduler.Instance) 
    .Subscribe(Customers.Add);
```

Rx还为`IObservable<T>`提供了`SubscribeOnDispatcher()`和`ObserveOnDispatcher()`扩展方法，它们自动使用当前线程的`Dispatcher`（以及`CoreDispatcher`的等价物）。虽然这些可能稍微方便一些，但它们可能使你的代码更难测试。我们在[测试Rx](16_TestingRx.md)章节中解释了为什么。

## 并发的陷阱

在您的应用程序中引入并发会增加其复杂性。如果通过添加并发层不能显著改善您的应用程序，那么您应该避免这样做。并发应用程序可能会展示出维护问题，其症状会在调试、测试和重构的领域中显现出来。

并发引入的常见问题是不可预测的时间安排。不可预测的时间安排可能是由系统的变化负荷引起的，以及系统配置的变化（例如，不同的核心时钟速度和处理器的可用性）。这些最终可能会导致[死锁](http://en.wikipedia.org/wiki/Deadlock)、[活锁](http://en.wikipedia.org/wiki/Deadlock#Livelock)和状态损坏。

引入并发到应用程序的一个特别显著的危险是你可以无声无息地引入错误。由不可预测的时间引起的错误是非常难以检测的，这使得这类缺陷很容易从开发、QA和UAT中溜过，只有在生产环境中才会显现出来。然而，Rx在简化可观察序列的并发处理方面做得很好，许多这些问题可以得到缓解。你仍然可以创建问题，但如果你遵循指南，你可以在知识上感觉更安全，因为你已经大大减少了不希望的竞态条件的可能性。

在后面的章节，[测试Rx](16_TestingRx.md)，我们将看到Rx如何改善你测试并发工作流的能力。

### 锁住

Rx可以简化并发处理，但它并不免疫死锁。一些调用（如`First`、`Last`、`Single`和`ForEach`）是阻塞的——它们不会返回，直到它们等待的某些事情发生。以下示例表明，这使得死锁非常容易发生：

```csharp
var sequence = new Subject<int>();

Console.WriteLine("Next line should lock the system.");

IEnumerable<int> value = sequence.First();
sequence.OnNext(1);

Console.WriteLine("I can never execute....");
```

`First`方法将不会返回，直到其源发射一个序列。但是导致此源发射序列的代码位于调用`First`之后的行。因此，源在`First`返回之前无法发射序列。这种死锁的风格，有两方各自无法进行直到另一方进行，通常被称为_致命拥抱_。正如这段代码所示，即使在单线程代码中，也完全可能发生致命拥抱。事实上，这段代码的单线程特性是使死锁成为可能的原因：我们有两个操作（等待第一个通知和发送第一个通知），只有一条线程。这本不必是一个问题。如果我们使用了`FirstAsync`并将观察者附加到它上面，`FirstAsync`将在源`Subject<int>`调用其`OnNext`时执行其逻辑。但这比直接调用`First`并将结果赋值给一个变量更复杂。

这是一个过于简化的示例，用于说明行为，并且我们永远不会在生产中编写这样的代码。（即使我们这样做了，它也会很快并一致地失败，我们将立即意识到问题。）但在实际应用程序代码中，这类问题可能更难发现。竞态条件常常在集成点悄然进入系统，所以问题并不一定在任何一段代码中明显：时序问题可能由于我们如何将多块代码拼接在一起而产生。

接下来的例子可能更难检测，但只比我们第一个不切实际的例子稍难一点。基本思想是，我们有一个代表用户界面中按钮点击的主题。表示用户输入的事件处理程序由UI框架调用。我们只提供框架与事件处理方法，它会在感兴趣的事件发生时（如按钮被点击时）调用它们。这段代码在代表点击的主题上调用了`First`，但这里可能导致问题的可能性不像在前一个例子中那么明显：

```csharp
public Window1()
{
    InitializeComponent();
    DataContext = this;
    Value = "Default value";
    
    // 死锁！我们需要分派器继续允许我点击按钮以产生一个值
    Value = _subject.First();
    
    // 这将产生预期的效果，但由于它不会阻塞，
    // 我们可以在UI线程上调用它而不会造成死锁。
    //_subject.FirstAsync(1).Subscribe(value => Value = value);
}

private void MyButton_Click(object sender, RoutedEventArgs e)
{
    _subject.OnNext("New Value");
}

public string Value
{
    get { return _value; }
    set
    {
        _value = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
    }
}
```

早期的例子在`First`返回之后调用了主题的`OnNext`，相对容易看出如果`First`没有返回，那么主题就不会发出通知。但在这里并不那么明显。`MyButton_Click`事件处理程序将在调用`InitializeComponent`时设置（如WPF代码中常见），所以显然我们已经做了必要的设置以启动事件流。在我们到达这个调用`First`的地方时，UI框架已经知道，如果用户点击`MyButton`，它应该调用`MyButton_Click`，而该方法将导致主题发出一个值。

使用`First`并没有本质上的错误。（有风险，是的，但在某些情况下这种准确的代码是绝对可以的。）问题是我们使用它的上下文。这段代码在UI元素的构造函数中，而这些总是在与该窗口的UI元素关联的特定线程上运行。（这恰好是一个WPF示例，但其他UI框架的工作方式也是一样的。）而这是同一个线程，UI框架将使用它来传递有关用户输入的通知。如果我们阻塞这个UI线程，我们就阻止了UI框架调用我们的按钮点击事件处理程序。因此，这种阻塞性调用等待的事件只能从它正在阻塞的线程中被触发，从而创造了一个死锁。

你可能开始觉得我们应该试图避免在Rx中使用阻塞性调用。这是一个好的经验法则。我们可以通过注释掉使用`First`的行，并取消下面这行中包含此代码的注释来修复上面的代码：

```csharp
_subject.FirstAsync(1).Subscribe(value => Value = value);
```

这使用了`FirstAsync`，它完成同样的工作，但采用了不同的方法。它实现了相同的逻辑，但它返回一个`IObservable<T>`，我们必须订阅它，如果我们想在第一个值出现时接收到第一个值。这比只是将`First`的结果赋给`Value`属性更复杂，但它更适应于我们无法知道那个源何时会产生一个值的事实。

如果你进行了大量的UI开发，那么最后一个例子对你来说可能显然是错误的：我们在一个窗口的构造函数中编写了代码，这些代码不允许构造函数完成，直到用户点击该窗口中的一个按钮。直到构造完成，窗口甚至都不会出现，所以等待用户点击按钮是没有意义的。这个按钮在我们的构造函数完成之前甚至都不会在屏幕上可见。此外，经验丰富的UI开发人员知道，你不能只是停下来等待用户进行特定的操作。（即使是模态对话框，实际上也是要求在继续之前得到回应，也不会阻塞UI线程。）但正如下一个示例所示，问题可能更难看清。在这个例子中，按钮的单击处理程序将尝试从一个通过接口公开的可观察序列中获取第一个值。

```csharp
public partial class Window1 : INotifyPropertyChanged
{
    //Imagine DI here.
    private readonly IMyService _service = new MyService(); 
    private int _value2;

    public Window1()
    {
        InitializeComponent();
        DataContext = this;
    }

    public int Value2
    {
        get { return _value2; }
        set
        {
            _value2 = value;
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(nameof(Value2)));
        }
    }

    #region INotifyPropertyChanged Members
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion

    private void MyButton2_Click(object sender, RoutedEventArgs e)
    {
        Value2 = _service.GetTemperature().First();
    }
}
```

与前一个示例不同，这并没有试图在构造函数中阻塞进度。阻塞调用`First`发生在按钮点击处理程序（最后附近的`MyButton2_Click`方法）中。这个例子更有趣，因为这种情况并不一定错。应用程序经常在点击处理程序中执行阻塞操作：当我们点击按钮保存文档副本时，我们期望应用程序执行所有必要的IO工作将我们的数据写出到存储中。有了现代固态存储设备，这通常发生得如此之快以至于看起来是瞬间完成的，但在机械硬盘时代，当应用程序保存我们的文档时变得短暂无响应是不寻常的。即使今天，如果您的存储是远程的，并且网络问题正在造成延迟，这种情况也可能发生。

因此，即使我们已经学会对诸如`First`之类的阻塞性操作持怀疑态度，它在这个示例中可能是可以的。仅凭查看这段代码本身并无法确定。这一切都取决于`GetTemperature`返回的observable是什么样的，以及它产生其项目的方式。那个调用`First`将会在UI线程阻塞，直到第一个条目变得可用，所以如果产生第一个条目需要访问UI线程，这将会产生死锁。这里有一种略显牵强的方法来创建这个问题：

```csharp
class MyService : IMyService
{
    public IObservable<int> GetTemperature()
    {
        return Observable.Create<int>(
            o =>
            {
                o.OnNext(27);
                o.OnNext(26);
                o.OnNext(24);
                return () => { };
            })
            .SubscribeOnDispatcher();
    }
}
```

这仿造了预期模拟实际温度传感器的行为，通过进行一系列`OnNext`调用。但它做了一些奇特的显式调度：它调用`SubscribeOnDispatcher`。这是一个扩展方法，有效地调用`SubscribeOn(DispatcherScheduler.Current.Dispatcher)`。这实际上告诉Rx，当有人试图订阅`GetTemperature`返回的`IObservable<int>`时，这个订阅调用应该通过一个WPF特定的调度器在UI线程上执行。 （严格来说，WPF确实允许多个UI线程，所以更精确地说，这段代码只在你在UI线程上调用它时有效，如果这样的话，调度器将确保工作项被调度到同一个UI线程上。）

当我们的点击处理程序调用`First`时，它会反过来订阅`GetTemperature`返回的`IObservable<int>`，因为那使用了`SubscribeOnDispatcher`，这不会立即调用传递给`Observable.Create`的回调。相反，它安排了一个工作项，将在UI线程（即我们正在运行的线程）变得空闲时执行。现在它不被认为是空闲的，因为它正在处理按钮点击。将这个工作项交给调度器后，`Subscribe`调用返回给`First`方法。`First`现在坐下来等待第一个条目出现。由于它不会返回直到那发生，UI线程将不被认为是可用的，直到那时，被安排的工作项才能运行，我们就有了死锁。

这归结为与第一个涉及`First`的死锁示例相同的基本问题。我们有两个过程：项目的生成和等待项目出现。这些需要同时进行——我们需要“等待第一个条目”的逻辑在源发出其第一个条目时正在运行。这些示例都只使用了一个线程，这使得使用单个阻塞性调用（`First`）既设置观察第一个条目的过程，也等待那发生是个坏主意。但即使在所有三种情况下都是相同的基本问题，随着代码变得更加复杂，它变得更难看出。在实际应用代码中，看到死锁的根本原因通常比这难得多。

迄今为止，这一章可能似乎在说并发全部是厄运和阴暗，专注于你可能面临的问题，以及它们在实践中往往难以察觉的事实；然而这并不是意图。
尽管采用Rx不能神奇地避免经典的并发问题，但如果你遵循以下两条规则，Rx可以使得正确处理它们变得更容易。

- 只有顶层订阅者应该做出调度决策
- 避免使用阻塞性调用：例如`First`、`Last`和`Single`

最后一个例子因为一个简单的问题而失败；`GetTemperature`服务在实际上无权这样做时，决定了调度模型。代表温度传感器的代码不应该需要知道我正在使用特定的UI框架，当然也不应该单方面决定它将在WPF用户界面线程上运行某些工作。

开始使用Rx时，可能很容易说服自己将调度决策嵌入到较低层是在'有帮助'。你可能会说：“看！不仅我提供了温度读数，我还使它自动在UI线程上通知你，所以你不必费心使用`ObserveOn`。”意图可能是好的，但很容易造成线程噩梦。

只有设置订阅并消费其结果的代码才能全面了解并发需求，所以这是选择哪些调度器使用的正确层级。较低层次的代码不应该试图介入；它们应该只是照做。 （Rx在某种程度上可能稍微违反了这个规则，通过选择在需要时使用默认调度器。但它做出了非常保守的选择，旨在最小化死锁的机会，并总是允许应用程序通过指定调度器来控制。）

注意，遵循上述任何一条规则都足以防止这个例子中的死锁。但最好遵循两条规则。

这确实留下了一个未解答的问题：_怎样_ 顶层订阅者应该做出调度决策？我已经确定了需要做出决策的代码区域，但决策应该是什么？这将取决于你正在编写的应用程序类型。对于UI代码，这个模式通常效果很好："在后台线程上订阅；在UI线程上观察"。对于UI代码来说，死锁的风险在于，UI线程实际上是一个共享资源，对该资源的争用可以产生死锁。因此，策略是尽可能避免需要该资源：不需要在该线程上的工作不应该在该线程上进行，这就是为什么在工作线程上进行订阅（例如，使用`TaskPoolScheduler`）可以减少死锁的风险。

如果你有可观察的来源决定何时产生事件（例如，计时器或代表来自外部信息来源或设备的输入的来源），你也会希望这些计划在工作线程上运行。只有当我们需要更新用户界面时，我们才需要我们的代码在UI线程上运行，因此我们通过使用`ObserveOn`与合适的UI感知调度器（如WPF `DispatcherScheduler`）一起延迟这一步，直到最后一刻。这样，只有最后一步，更新UI，将需要访问UI线程。在此运行时，所有复杂的处理都将完成，因此这应该能够非常快速地运行，几乎立即放弃对UI线程的控制，提高应用程序响应性，并降低死锁的风险。

其他场景将需要其他策略，但与死锁有关的一般原则始终相同：了解哪些共享资源需要独占访问。例如，如果你有一个传感器库，它可能会创建一个专用线程来监视设备并报告新的测量数据，如果它规定某些工作必须在该线程上完成，这将非常类似于UI场景：有一个特定的线程你需要避免阻塞。这里可能适用同样的方法。但这不是唯一的情况。

你可以想象一个数据处理应用程序，在这种情况下，某些数据结构是共享的。在这些情况下，通常允许从任何线程访问这些数据结构，但要求一次一个线程访问。通常我们会使用线程同步原语来防止这些关键数据结构的并发使用。在这些情况下，死锁的风险不是因为使用特定的线程。相反，它们来自于一个线程无法进展因为另一个线程正在使用一个共享数据结构，但那个线程正在等待第一个线程做些什么，并且在那发生之前不会释放它对那个数据结构的锁。这里避免问题的最简单方法是尽可能避免阻塞。避免像`First`这样的方法，转而使用它们的非阻塞等效项如`FirstAsync`。（如果有情况你无法避免阻塞，尽量避免在拥有保护访问共享数据的锁的同时这样做。而如果你真的无法避免这样做，那么也没有简单的答案。你现在必须开始考虑锁层次结构以系统地避免死锁，就像你不使用Rx一样。）非阻塞式是使用Rx的自然方式，这也是Rx在这些情况下帮助你避免与并发相关的问题的主要方式。

## 调度器的高级功能

调度器提供了一些主要在编写需要与调度器交互的可观察源时感兴趣的功能。使用调度器最常见的方式是在设置订阅时使用，要么在创建可观察源时将它们作为参数提供，要么将它们传递给 `SubscribeOn` 和 `ObserveOn`。但是，如果你需要编写一个可观察源，该源选择自己的调度产生项目（例如，假设你正在编写代表某些外部数据源的库，并且你想将其呈现为 `IObservable<T>`），你可能需要使用这些更高级的功能。

### 传递状态

`IScheduler` 定义的所有方法都接受一个 `state` 参数。这里再次给出接口定义：

```csharp
public interface IScheduler
{
    DateTimeOffset Now { get; }

    IDisposable Schedule<TState>(TState state, 
                                 Func<IScheduler, TState, IDisposable> action);
    
    IDisposable Schedule<TState>(TState state, 
                                 TimeSpan dueTime, 
                                 Func<IScheduler, TState, IDisposable> action);
    
    IDisposable Schedule<TState>(TState state, 
                                 DateTimeOffset dueTime, 
                                 Func<IScheduler, TState, IDisposable> action);
}
```

调度器并不关心 `state` 参数中的内容。它只是在执行工作项时未经修改地将其传递给你的回调。这提供了一种为该回调提供上下文的方式。这并不是绝对必要的：我们传递的 `action` 委托可以包含我们需要的任何状态。做到这一点的最简单方法是在 lambda 中捕获变量。但是，如果你查看 [Rx 源代码](https://github.com/dotnet/reactive/)，你会发现它通常不这样做。例如，`Range` 操作符的核心是一个名为 `LoopRec` 的方法，如果你查看 [`LoopRec` 的源代码](https://github.com/dotnet/reactive/blob/95d9ea9d2786f6ec49a051c5cff47dc42591e54f/Rx.NET/Source/src/System.Reactive/Linq/Observable/Range.cs#L55-L73)，你会看到它包括以下这行：

```csharp
var next = scheduler.Schedule(this, static (innerScheduler, @this) => @this.LoopRec(innerScheduler));
```

逻辑上，`Range` 只是一个循环，每产生一项就执行一次。但为了实现并发执行并避免栈溢出，它通过将循环的每次迭代安排为单独的工作项来实现这一点。（该方法被称为 `LoopRec`，因为它在逻辑上是一个递归循环：它通过调用 `Schedule` 启动，每次调度器调用这个方法时，它都会再次调用 `Schedule` 来请求执行下一项。这实际上并不会导致 Rx 的内置调度器发生递归，即使是 `ImmediateScheduler`，因为它们都会检测到这一点并安排在当前项返回后运行下一项。但是如果你编写了最简单的调度器，这实际上会在运行时导致递归，如果你试图创建一个大序列，很可能会导致栈溢出。）

注意传递给 `Schedule` 的 lambda 已经用 `static` 注释。这告诉 C# 编译器我们的意图是_不_捕获任何变量，任何尝试这样做的行为都应该导致编译错误。这样做的优点是编译器能够生成代码，重用每次调用的同一个委托实例。第一次运行时，它会创建一个委托并将其存储在一个隐藏字段中。在随后的执行中（无论是同一范围的未来迭代还是完全新的范围实例），它都可以一次又一次地使用同一个委托。这是因为委托不捕获任何状态。这避免了每次循环都分配一个新对象。

Rx 库是否可以使用更直接的方法？我们可以选择不使用状态，向调度器传递一个 `null` 状态，然后在回调中丢弃传递给我们的状态参数：

```csharp
// Less weird, but less efficient:
var next = scheduler.Schedule<object?>(null, (innerScheduler, _) => LoopRec(innerScheduler));
```

这避免了之前示例中传递我们自己的 `this` 参数的奇怪做法：现在我们以普通方式调用 `LoopRec` 实例成员：我们隐式使用了作用域中的 `this` 引用。所以这将创建一个捕获该隐式 `this` 引用的委托。这是有效的，但它效率低下：它将迫使编译器生成分配几个对象的代码。它创建一个持有捕获的 `this` 的字段的对象，然后需要创建一个持有对该捕获对象引用的独特委托实例。

实际上 `Range` 实现中的更复杂代码避免了这一点。它通过用 `static` 注释 lambda 来禁用捕获。这防止了依赖隐式 `this` 引用的代码。所以它必须安排 `this` 引用对回调可用。这正是 `state` 参数存在的原因。它提供了一种传递每个工作项状态的方法，使你可以避免在每次迭代时捕获变量的开销。

### 未来的调度

我之前谈到了基于时间的操作符，还谈到了 `ISchedule` 的两个基于时间的成员，这些成员实现了这一点，但我还没有展示如何使用它们。这些允许你安排未来某个时刻执行的动作。（这依赖于进程继续运行所需的时间。如前几章所述，`System.Reactive` 不支持持久的、持续的订阅。所以如果你想为未来几天安排某事，你可能想看看 [Reaqtor](https://reaqtive.net/)。）你可以通过调用接受 `DateTimeOffset` 的 `Schedule` 重载来指定动作应被调用的确切时间点，或者你可以通过使用基于 `TimeSpan` 的重载来指定等待动作被调用的时间段。

你可以像这样使用 `TimeSpan` 重载：

```csharp
var delay = TimeSpan.FromSeconds(1);
Console.WriteLine("Before schedule at {0:o}", DateTime.Now);

scheduler.Schedule(delay, () => Console.WriteLine("Inside schedule at {0:o}", DateTime.Now));
Console.WriteLine("After schedule at  {0:o}", DateTime.Now);
```

输出：

```
Before schedule at 2012-01-01T12:00:00.000000+00:00
After schedule at 2012-01-01T12:00:00.058000+00:00
Inside schedule at 2012-01-01T12:00:01.044000+00:00
```

这说明了调度在这里是非阻塞的，因为“前”和“后”的调用在时间上非常接近。（对于大多数调度器都是这样，但如前所述，`ImmediateScheduler` 的工作方式不同。在这种情况下，你会在“Inside”之后看到“After”消息。这就是为什么没有定时操作符默认使用 `ImmediateScheduler` 的原因。）你还可以看到，大约一秒钟后，动作被调用。

你可以使用 `DateTimeOffset` 重载来指定安排任务的特定时间点。如果出于某种原因，你指定的时间点在过去，则会尽快安排该动作。要注意系统时钟的变化会使事情复杂化。Rx 的调度器确实做了一些调整以应对时钟漂移，但系统时钟的突然大变化可能会造成一些短期的混乱。

### 取消

每个 `Schedule` 的重载都返回一个 `IDisposable`，调用 `Dispose` 会取消已安排的工作。在前面的例子中，我们安排了一秒后调用的工作。我们可以通过处理返回值来取消这项工作。

```csharp
var delay = TimeSpan.FromSeconds(1);
Console.WriteLine("Before schedule at {0:o}", DateTime.Now);

var workItem = scheduler.Schedule(delay, 
   () => Console.WriteLine("Inside schedule at {0:o}", DateTime.Now));

Console.WriteLine("After schedule at  {0:o}", DateTime.Now);

workItem.Dispose();
```

输出：

```
Before schedule at 2012-01-01T12:00:00.000000+00:00
After schedule at 2012-01-01T12:00:00.058000+00:00
```

注意，由于我们几乎立即取消了它，所以预定的动作从未发生。

当用户在调度器能够调用它之前取消预定的动作方法时，该动作只是从工作队列中移除。这就是我们在上面的例子中看到的情况。可以取消已经正在运行的计划工作，这就是为什么工作项回调需要返回 `IDisposable` 的原因：如果在你试图取消工作项时工作已经开始，Rx 会调用你的工作项回调返回的 `IDisposable` 的 `Dispose` 方法。这为用户提供了一种取消可能已经在运行的工作的方式。这项工作可能是某种 I/O、繁重计算或可能使用 `Task` 来执行某些工作。

你可能想知道这种机制如何有用：工作项回调需要已经返回，Rx 才能调用它返回的 `IDisposable`。这种机制只有在工作在返回给调度器后继续进行时才能实际使用。你可以启动另一个线程，这样工作就可以并行进行，尽管我们通常尽量避免在 Rx 中创建线程。另一种可能性是，如果计划的工作项调用了某个异步 API 并在不等待其完成的情况下返回，则你可以返回一个取消它的 `IDisposable`。

为了说明取消操作，这个略显不切实际的例子将一些工作作为 `Task` 运行，以使其在我们的回调返回后继续进行。它只是通过执行旋转等待和将值添加到 `list` 参数来伪造一些工作。这里的关键是我们创建了一个 `CancellationToken` 以便能够告诉任务我们希望它停止，并且我们返回一个将此令牌置于取消状态的 `IDisposable`。

```csharp
public IDisposable Work(IScheduler scheduler, List<int> list)
{
    CancellationTokenSource tokenSource = new();
    CancellationToken cancelToken = tokenSource.Token;
    Task task = new(() =>
    {
        Console.WriteLine();
   
        for (int i = 0; i < 1000; i++)
        {
            SpinWait sw = new();
   
            for (int j = 0; j < 3000; j++) sw.SpinOnce();
   
            Console.Write(".");
   
            list.Add(i);
   
            if (cancelToken.IsCancellationRequested)
            {
                Console.WriteLine("Cancellation requested");
                
                // cancelToken.ThrowIfCancellationRequested();
                
                return;
            }
        }
    }, cancelToken);
   
    task.Start();
   
    return Disposable.Create(tokenSource.Cancel);
}
```

这段代码调度上述代码，并允许用户通过按 Enter 取消处理工作：

```csharp
List<int> list = new();
Console.WriteLine("Enter to quit:");

IDisposable token = scheduler.Schedule(list, Work);
Console.ReadLine();

Console.WriteLine("Cancelling...");

token.Dispose();

Console.WriteLine("Cancelled");
```

输出：

```
Enter to quit:
........
Cancelling...
Cancelled
Cancellation requested
```

这里的问题是，我们引入了显式使用 `Task`，因此我们以一种调度器无法控制的方式增加了并发。Rx 库通常允许通过接受调度器参数控制并发的方式。如果目标是启用长时间运行的迭代工作，我们可以避免启动新线程或任务，而使用 Rx 的递归调度器功能。我已经在[传递状态](#passing-state)一节中稍微谈到过这个话题，但有几种方法可以做到这一点。

### 递归

除了 `IScheduler` 方法外，Rx 还定义了以扩展方法形式的 `Schedule` 的各种重载。其中一些参数采用了一些看起来很奇怪的委托。特别注意这些 `Schedule` 扩展方法重载中的最后一个参数。

```csharp
public static IDisposable Schedule(
    this IScheduler scheduler, 
    Action<Action> action)
{...}

public static IDisposable Schedule<TState>(
    this IScheduler scheduler, 
    TState state, 
    Action<TState, Action<TState>> action)
{...}

public static IDisposable Schedule(
    this IScheduler scheduler, 
    TimeSpan dueTime, 
    Action<Action<TimeSpan>> action)
{...}

public static IDisposable Schedule<TState>(
    this IScheduler scheduler, 
    TState state, 
    TimeSpan dueTime, 
    Action<TState, Action<TState, TimeSpan>> action)
{...}

public static IDisposable Schedule(
    this IScheduler scheduler, 
    DateTimeOffset dueTime, 
    Action<Action<DateTimeOffset>> action)
{...}

public static IDisposable Schedule<TState>(
    this IScheduler scheduler, 
    TState state, DateTimeOffset dueTime, 
    Action<TState, Action<TState, DateTimeOffset>> action)
{...}   
```

这些重载中的每一个都接受一个委托“动作”，允许你递归地调用“动作”。这可能看起来是一个非常奇怪的签名，但它允许我们以类似于你在[传递状态](#passing-state)部分看到的逻辑上递归的迭代方法实现，但可能以一种更简单的方式。

这个例子使用最简单的递归重载。我们有一个可以递归调用的 `Action`。

```csharp
Action<Action> work = (Action self) =>
{
    Console.WriteLine("Running");
    self();
};

var token = s.Schedule(work);
    
Console.ReadLine();
Console.WriteLine("Cancelling");

token.Dispose();

Console.WriteLine("Cancelled");
```

输出：

```
Enter to quit:
Running
Running
Running
Running
Cancelling
Cancelled
Running
```

注意我们没有在委托中编写任何取消代码。Rx 代表我们处理了循环并检查了取消。由于每个单独的迭代都被安排为单独的工作项，没有长时间运行的工作，所以让调度器完全处理取消就足够了。

这些重载与直接使用 `IScheduler` 方法的主要区别在于，你不需要直接将另一个回调传递给调度器。你只需调用提供的 `Action`，它将安排对你的方法的另一次调用。它们还使你无需传递状态参数，就可以不使用任何状态参数。

正如之前部分所提到的，尽管这在逻辑上代表递归，Rx 保护我们免受栈溢出的影响。调度器通过等待方法返回再执行递归调用来实现这种递归风格的调度。

这结束了我们关于调度和线程的探讨。接下来，我们将看看与此相关的计时主题。
