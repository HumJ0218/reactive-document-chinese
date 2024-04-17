# 测试 Rx.NET

现代质量保证标准要求广泛的自动化测试，以帮助评估和预防缺陷。开发一套测试套件来验证正确的行为并将其作为构建过程的一部分运行，以便及早发现回归是一个好习惯。

`System.Reactive` 源代码包含了一个全面的测试套件。测试基于 Rx 的代码提出了一些挑战，特别是涉及时间敏感操作符时。Rx.NET 的测试套件包括许多设计用来处理棘手的边缘情况的测试，以确保在负载下的可预测行为。这只是因为 Rx.NET 被设计为可测试的。

在本章中，我们将展示如何利用 Rx 的可测试性在您自己的代码中。

## 虚拟时间

在 Rx 中处理时间是常见的。正如您所见，它提供了几个考虑时间的操作符，并且这呈现了一个挑战。我们不想引入慢速测试，因为这可能会使测试套件花费太长时间执行，但我们如何测试一个应用程序等待用户停止输入半秒再提交查询的情况呢？不确定性测试也可能是一个问题：当存在竞态条件时，可靠地重新创建这些情况可能非常困难。

[Scheduling and Threading](11_SchedulingAndThreading.md) 章节描述了调度器如何使用虚拟时间的表现。这对于使测试能够验证与时间相关的行为至关重要。它让我们可以控制 Rx 对时间进展的感知，使我们能够编写逻辑上需要几秒钟，但实际上在微秒内执行的测试。

考虑这个例子，我们创建一个每秒发布值持续五秒的序列。

```csharp
IObservable<long> interval = Observable
    .Interval(TimeSpan.FromSeconds(1))
    .Take(5);
```

一个简单的测试来确保这生成每秒间隔的五个值将需要五秒钟来运行。那可不行；我们想要数百甚至数千个测试在五秒钟内运行。另一个非常普遍的需求是测试超时。在这里，我们尝试测试一分钟的超时。

```csharp
var never = Observable.Never<int>();
var exceptionThrown = false;

never.Timeout(TimeSpan.FromMinutes(1))
     .Subscribe(
        i => Console.WriteLine("This will never run."),
        ex => exceptionThrown = true);

Assert.IsTrue(exceptionThrown);
```

看起来我们别无选择，只能让我们的测试等待一分钟再进行断言。实际上，我们可能想等待一分钟多一点，因为如果运行测试的计算机忙碌，可能稍后触发超时。这种情况臭名昭著，有时即使代码没有真正的问题，测试也会偶尔失败。

没有人想要慢速、不一致的测试。所以，让我们看看 Rx 如何帮助我们避免这些问题。

## TestScheduler

[Scheduling and Threading](11_SchedulingAndThreading.md) 章节解释了调度器决定何时以及如何执行代码，以及它们如何跟踪时间。我们在那一章中看到的大多数调度器处理各种线程问题，并且在计时方面，它们都试图在请求的时间运行工作。但 Rx 提供了 `TestScheduler`，它完全不同地处理时间。它利用调度器控制所有与时间相关的行为的事实，让我们能够模拟和控制时间。

**注意：** `TestScheduler` 不在主 `System.Reactive` 包中。您需要添加对 `Microsoft.Reactive.Testing` 的引用才能使用它。

任何调度器都维护一个要执行的动作队列。每个动作都被指定一个执行时的时间点。（有时这个时间是“尽快”，但基于时间的操作符通常会安排工作在将来的某个具体时间运行。）如果我们使用 `TestScheduler`，它将实际上表现得好像时间静止直到我们告诉它我们想让时间继续前进。

在这个例子中，我们使用最简单的 `Schedule` 重载来安排一个立即运行的任务。尽管这有效地要求工作尽快运行，`TestScheduler` 总是等我们告诉它我们准备好了才处理新排队的工作。我们将虚拟时钟向前推进一个时刻，此时它将执行该排队工作。（每当我们推进虚拟时间时，它都运行所有新排队的“尽快”工作。如果我们推进时间足够远，意味着以前在逻辑上是未来的工作现在可以运行，它也会运行那些工作。）

```csharp
var scheduler = new TestScheduler();
var wasExecuted = false;
scheduler.Schedule(() => wasExecuted = true);
Assert.IsFalse(wasExecuted);
scheduler.AdvanceBy(1); // 执行 1 个时刻的排队动作
Assert.IsTrue(wasExecuted);
```

`TestScheduler` 实现了 `IScheduler` 接口，还定义了允许我们控制和监视虚拟时间的方法。这显示了这些额外的方法：

```csharp
public class TestScheduler : // ...
{
    public bool IsEnabled { get; private set; }
    public TAbsolute Clock { get; protected set; }
    public void Start()
    public void Stop()
    public void AdvanceTo(long time)
    public void AdvanceBy(long time)
    
    ...
}
```

`TestScheduler` 以 [`TimeSpan.Ticks`](https://learn.microsoft.com/en-us/dotnet/api/system.timespan.ticks) 为单位工作。如果你想将时间向前推进 1 秒，你可以调用 `scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks)`。一刻对应于 100 纳秒，因此 1 秒是 10,000,000 刻。

### AdvanceTo

`AdvanceTo(long)` 方法将虚拟时间设置为指定的刻数。这将执行所有已安排到该绝对时间的动作。`TestScheduler` 使用刻作为其时间测量单位。在这个例子中，我们安排动作立即执行，以及在 10 刻和 20 刻（分别为 1 微秒和 2 微秒）执行。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // 立即安排
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.AdvanceTo(1);");
scheduler.AdvanceTo(1);

Console.WriteLine("scheduler.AdvanceTo(10);");
scheduler.AdvanceTo(10);

Console.WriteLine("scheduler.AdvanceTo(15);");
scheduler.AdvanceTo(15);

Console.WriteLine("scheduler.AdvanceTo(20);");
scheduler.AdvanceTo(20);
```

输出：

```
scheduler.AdvanceTo(1);
A
scheduler.AdvanceTo(10);
B
scheduler.AdvanceTo(15);
scheduler.AdvanceTo(20);
C
```

注意当我们推进到 15 刻时没有发生任何事情。所有在 15 刻之前安排的工作已经完成，我们还没有推进到可以执行下一个安排动作的时间。

### AdvanceBy

`AdvanceBy(long)` 方法允许我们将时钟向前推进一定时间量。与 `AdvanceTo` 不同，这里的参数是相对于当前虚拟时间的。再次，测量单位是刻。我们可以采用最后的例子并将其修改为使用 `AdvanceBy(long)`。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // 立即安排
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.AdvanceBy(1);");
scheduler.AdvanceBy(1);

Console.WriteLine("scheduler.AdvanceBy(9);");
scheduler.AdvanceBy(9);

Console.WriteLine("scheduler.AdvanceBy(5);");
scheduler.AdvanceBy(5);

Console.WriteLine("scheduler.AdvanceBy(5);");
scheduler.AdvanceBy(5);
```

输出：

```
scheduler.AdvanceBy(1);
A
scheduler.AdvanceBy(9);
B
scheduler.AdvanceBy(5);
scheduler.AdvanceBy(5);
C
```

### Start

`TestScheduler` 的 `Start()` 方法运行已经安排的所有事务，必要时推进虚拟时间来执行被安排到特定时间的工作项。我们使用相同的例子，将 `AdvanceBy(long)` 调用换成单个 `Start()` 调用。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // 立即安排
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();

Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

输出：

```
scheduler.Start();
A
B
C
scheduler.Clock:20
```

注意，当所有安排的动作都被执行后，虚拟时钟与我们最后安排的项目（20 刻）匹配。

我们进一步扩展我们的例子，安排一个在 `Start()` 已经被调用后才发生的新动作。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();

Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);

scheduler.Schedule(() => Console.WriteLine("D"));
```

输出：

```
scheduler.Start();
A
B
C
scheduler.Clock:20
```

注意输出完全相同；如果我们想要执行我们的第四个动作，我们将不得不再次调用 `Start()`（或 `AdvanceTo` 或 `AdvanceBy`）。

### Stop

有一个名为 `Stop()` 的方法，其名称似乎暗示了与 `Start()` 的某种对称性。这会将调度器的 `IsEnabled` 属性设置为 false，并且如果 `Start` 当前正在运行，这意味着它将停止检查队列中的进一步工作，并在当前正在处理的工作项完成后立即返回。

在这个例子中，我们展示了如何使用 `Stop()` 暂停安排动作的处理。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(15), scheduler.Stop);
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();
Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

输出：

```
scheduler.Start();
A
B
scheduler.Clock:15
```

注意，"C" 没有被打印出来，因为我们在 15 刻停止了时钟。

由于 `Start` 自动停止，当它耗尽工作队列时，你并不一定要调用 `Stop`。它存在的只是为了在测试过程中部分完成时暂停 `Start` 的处理。

### 安排冲突

当安排动作时，很可能会有许多动作被安排在同一时刻。这通常会发生在为 _现在_ 安排多个动作时。这也可能发生在预定未来同一时刻的多个动作时。`TestScheduler` 有一个简单的方法来处理这种情况。当动作被安排时，它们会被标记为它们被安排的时钟时间。如果多个项目被安排在同一时刻，它们会按照它们被安排的顺序排队；当时钟推进时，所有该时刻的项目都会按照它们被安排的顺序执行。

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();
Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

输出：

```
scheduler.AdvanceTo(10);
A
B
C
scheduler.Clock:10
```

注意虚拟时钟是在 10 刻，我们推进到的时间。

## 测试 Rx 代码

现在我们已经了解了一些关于 `TestScheduler` 的知识，让我们看看我们如何可以使用它来测试我们最初的两个使用 `Interval` 和 `Timeout` 的代码片段。我们希望尽可能快地执行测试，但仍然保持时间的语义。在这个例子中，我们生成我们的五个值，每个值间隔一秒，但传入我们的 `TestScheduler` 到 `Interval` 方法中使用而不是默认的调度器。

```csharp
[TestMethod]
public void Testing_with_test_scheduler()
{
    var expectedValues = new long[] {0, 1, 2, 3, 4};
    var actualValues = new List<long>();
    var scheduler = new TestScheduler();

    var interval = Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(5);
    
    interval.Subscribe(actualValues.Add);

    scheduler.Start();
    CollectionAssert.AreEqual(expectedValues, actualValues);
    // 在我的机器上执行时间少于 0.01 秒
}
```

虽然这个例子有点有趣，但我认为更重要的是我们将如何测试一个真实的代码片段。想象一下，一个 ViewModel 订阅了一个价格流。价格发布时，它会将它们添加到一个集合中。假设这是一个 WPF 实现，我们有权强制订阅在 `ThreadPool` 上完成，并且观察是在 `Dispatcher` 上执行的。

```csharp
public class MyViewModel : IMyViewModel
{
    private readonly IMyModel _myModel;
    private readonly ObservableCollection<decimal> _prices;

    public MyViewModel(IMyModel myModel)
    {
        _myModel = myModel;
        _prices = new ObservableCollection<decimal>();
    }

    public void Show(string symbol)
    {
        // TODO: resource mgt, exception handling etc...
        _myModel.PriceStream(symbol)
                .SubscribeOn(Scheduler.ThreadPool)
                .ObserveOn(Scheduler.Dispatcher)
                .Timeout(TimeSpan.FromSeconds(10), Scheduler.ThreadPool)
                .Subscribe(
                    Prices.Add,
                    ex =>
                        {
                            if(ex is TimeoutException)
                                IsConnected = false;
                        });
        IsConnected = true;
    }

    public ObservableCollection<decimal> Prices
    {
        get { return _prices; }
    }

    public bool IsConnected { get; private set; }
}
```

### 注入调度器依赖

虽然上面的代码片段可能完成了我们想要的功能，但它很难测试，因为它是通过静态属性访问调度器的。您将需要一些方法在测试期间提供不同的调度器。在这个例子中，我们将为此目的定义一个接口：

```csharp
public interface ISchedulerProvider
{
    IScheduler CurrentThread { get; }
    IScheduler Dispatcher { get; }
    IScheduler Immediate { get; }
    IScheduler NewThread { get; }
    IScheduler ThreadPool { get; }
    IScheduler TaskPool { get; } 
}
```

我们在生产中运行的默认实现如下所示：

```csharp
public sealed class SchedulerProvider : ISchedulerProvider
{
    public IScheduler CurrentThread => Scheduler.CurrentThread;
    public IScheduler Dispatcher => DispatcherScheduler.Instance;
    public IScheduler Immediate => Scheduler.Immediate;
    public IScheduler NewThread => Scheduler.NewThread;
    public IScheduler ThreadPool => Scheduler.ThreadPool;
    public IScheduler TaskPool => Scheduler.TaskPool;
}
```

我们可以在测试中替换 `ISchedulerProvider` 的实现。例如：

```csharp
public sealed class TestSchedulers : ISchedulerProvider
{
    // 以 TestScheduler 类型提供调度器
    public TestScheduler CurrentThread { get; }  = new TestScheduler();
    public TestScheduler Dispatcher { get; }  = new TestScheduler();
    public TestScheduler Immediate { get; }  = new TestScheduler();
    public TestScheduler NewThread { get; }  = new TestScheduler();
    public TestScheduler ThreadPool { get; }  = new TestScheduler();
    
    // ISchedulerService 需要我们返回 IScheduler，但我们希望属性
    // 返回 TestScheduler 以方便测试代码的使用，所以我们提供
    // 所有属性的显式实现以匹配 ISchedulerService。
    IScheduler ISchedulerProvider.CurrentThread => CurrentThread;
    IScheduler ISchedulerProvider.Dispatcher => Dispatcher;
    IScheduler ISchedulerProvider.Immediate => Immediate;
    IScheduler ISchedulerProvider.NewThread => NewThread;
    IScheduler ISchedulerProvider.ThreadPool => ThreadPool;
}
```

注意 `ISchedulerProvider` 是显式实现的，因为该接口要求每个属性返回一个 `IScheduler`，但我们的测试将需要直接访问 `TestScheduler` 实例。我现在可以为我的 ViewModel 编写一些测试了。下面，我们测试了一个修改过的 `MyViewModel` 类，它接受一个 `ISchedulerProvider` 并使用它而不是 `Scheduler` 类的静态调度器。我们还使用流行的 [Moq](https://github.com/Moq) 框架提供适当的假实现模型。

```csharp
[TestInitialize]
public void SetUp()
{
    _myModelMock = new Mock<IMyModel>();
    _schedulerProvider = new TestSchedulers();
    _viewModel = new MyViewModel(_myModelMock.Object, _schedulerProvider);
}

[TestMethod]
public void Should_add_to_Prices_when_Model_publishes_price()
{
    decimal expected = 1.23m;
    var priceStream = new Subject<decimal>();
    _myModelMock.Setup(svc => svc.PriceStream(It.IsAny<string>())).Returns(priceStream);

    _viewModel.Show("SomeSymbol");
    
    // 安排 OnNext
    _schedulerProvider.ThreadPool.Schedule(() => priceStream.OnNext(expected));  

    Assert.AreEqual(0, _viewModel.Prices.Count);

    // 执行 OnNext 动作
    _schedulerProvider.ThreadPool.AdvanceBy(1);  
    Assert.AreEqual(0, _viewModel.Prices.Count);
    
    // 执行 OnNext 处理程序
    _schedulerProvider.Dispatcher.AdvanceBy(1);  
    Assert.AreEqual(1, _viewModel.Prices.Count);
    Assert.AreEqual(expected, _viewModel.Prices.First());
}

[TestMethod]
public void Should_disconnect_if_no_prices_for_10_seconds()
{
    var timeoutPeriod = TimeSpan.FromSeconds(10);
    var priceStream = Observable.Never<decimal>();
    _myModelMock.Setup(svc => svc.PriceStream(It.IsAny<string>())).Returns(priceStream);

    _viewModel.Show("SomeSymbol");

    _schedulerProvider.ThreadPool.AdvanceBy(timeoutPeriod.Ticks - 1);
    Assert.IsTrue(_viewModel.IsConnected);
    _schedulerProvider.ThreadPool.AdvanceBy(timeoutPeriod.Ticks);
    Assert.IsFalse(_viewModel.IsConnected);
}
```

输出：

```
2 passed, 0 failed, 0 skipped, took 0.41 seconds (MSTest 10.0).
```

这两个测试确保了五件事：

* `Price` 属性随着模型产生的价格而增加
* 该序列在 ThreadPool 上被订阅
* `Price` 属性在 Dispatcher 上更新，即序列在 Dispatcher 上被观察
* 价格之间的 10 秒超时会将 ViewModel 设置为断开连接
* 测试运行速度快。

尽管运行测试的时间并不那么让人印象深刻，但大部分时间似乎都花在了热身测试工具上。而且，将测试数量增加到 10 只增加了 0.03 秒。一般来说，现代 CPU 应该能够每秒执行上千个单元测试。

在第一个测试中，我们可以看到只有在 `ThreadPool` 和 `Dispatcher` 调度器都运行后，我们才会得到结果。在第二个测试中，它有助于验证超时不少于 10 秒。

在某些情况下，您可能不感兴趣的是调度器而您希望专注于测试其他功能。如果是这种情况，那么您可能希望创建另一个 `ISchedulerProvider` 的测试实现，该实现为其成员返回 `ImmediateScheduler`。这可以帮助减少您测试中的干扰。

```csharp
public sealed class ImmediateSchedulers : ISchedulerService
{
    public IScheduler CurrentThread => Scheduler.Immediate;
    public IScheduler Dispatcher => Scheduler.Immediate;
    public IScheduler Immediate => Scheduler.Immediate;
    public IScheduler NewThread => Scheduler.Immediate;
    public IScheduler ThreadPool => Scheduler.Immediate;
}
```

## 高级功能 - ITestableObserver

`TestScheduler` 提供了更多高级功能。当测试设置的某些部分需要在特定的虚拟时间运行时，这些功能可能会有用。

### `Start(Func<IObservable<T>>)` 方法

`Start` 有三种重载，用于在给定时间开始观察一个可观察序列，记录它发出的通知，并在给定时间处置订阅。起初可能会让人困惑，因为无参数重载的 `Start` 与此完全无关。这三种重载返回一个 `ITestableObserver<T>`，它允许你记录可观察序列的通知，很像我们在 [Transformation chapter](06_Transformation.md#materialize-and-dematerialize) 中看到的 `Materialize` 方法。

```csharp
public interface ITestableObserver<T> : IObserver<T>
{
    // 获取观察者接收到的记录通知。
    IList<Recorded<Notification<T>>> Messages { get; }
}
```

虽然有三个重载，但我们首先看最具体的一个。这个重载接受四个参数：

* 一个可观察序列工厂委托
* 调用工厂的时间点
* 订阅从工厂返回的可观察序列的时间点
* 处置订阅的时间点

最后三个参数的 _时间_ 都以刻为单位，与 `TestScheduler` 的其余成员一致。

```csharp
public ITestableObserver<T> Start<T>(
    Func<IObservable<T>> create, 
    long created, 
    long subscribed, 
    long disposed)
{...}
```

我们可以使用这个方法来测试 `Observable.Interval` 工厂方法。这里，我们创建一个每秒产生一个值持续 4 秒的可观察序列。我们使用 `TestScheduler.Start` 方法立即创建并订阅它（通过为第二和第三个参数传递 0）。我们在 5 秒后取消我们的订阅。一旦 `Start` 方法运行完毕，我们输出我们记录的内容。

```csharp
var scheduler = new TestScheduler();
var source = Observable.Interval(TimeSpan.FromSeconds(1), scheduler)
    .Take(4);

var testObserver = scheduler.Start(
    () => source, 
    0, 
    0, 
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("{0} @ {1}", message.Value, message.Time);
}
```

输出：

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000001
OnNext(1) @ 20000001
OnNext(2) @ 30000001
OnNext(3) @ 40000001
OnCompleted() @ 40000001
```

请注意，`ITestObserver<T>` 记录 `OnNext` 和 `OnCompleted` 通知。如果序列以错误终止，`ITestObserver<T>` 会记录 `OnError` 通知。

我们可以改变输入变量以查看它产生的影响。我们知道 `Observable.Interval` 方法是一个冷可观察对象，因此创建的虚拟时间并不重要。改变虚拟订阅时间可以改变我们的结果。如果我们将其改为 2 秒，我们会注意到，如果我们让处置时间维持在 5 秒，我们会错过一些消息。

```csharp
var testObserver = scheduler.Start(
    () => Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(4), 
    0,
    TimeSpan.FromSeconds(2).Ticks,
    TimeSpan.FromSeconds(5).Ticks);
```

输出：

```
Time is 50000000 ticks
Received 2 notifications
OnNext(0) @ 30000000
OnNext(1) @ 40000000
```

我们在第 2 秒开始订阅；`Interval` 在每秒后产生值（即第 3 秒和第 4 秒），在第 5 秒我们取消了订阅。所以我们错过了另外两个 `OnNext` 消息以及 `OnCompleted` 消息。

此 `TestScheduler.Start` 方法的其他两种重载如下所示。

```csharp
public ITestableObserver<T> Start<T>(Func<IObservable<T>> create, long disposed)
{
    if (create == null)
    {
        throw new ArgumentNullException("create");
    }
    else
    {
        return this.Start<T>(create, 100L, 200L, disposed);
    }
}

public ITestableObserver<T> Start<T>(Func<IObservable<T>> create)
{
    if (create == null)
    {
        throw new ArgumentNullException("create");
    }
    else
    {
        return this.Start<T>(create, 100L, 200L, 1000L);
    }
}
```

正如您所见，这些重载只是调用我们一直在看的变体，但传递了一些默认值。这些默认值在创建和订阅之间以及实际工作运行之前提供了足够的间隙。这些默认值没有什么特别神奇的，但如果你重视简洁性超过完全明显的事实，并且愿意依赖于约定的无形效果，那么你可能更喜欢这种方式。Rx 源代码本身包含数千个测试，其中大量使用最简单的 `Start` 重载，日复一日在代码库中工作的开发人员很快就习惯了在时间 100 创建，在时间 200 订阅，以及在时间 1000 之前测试你需要测试的所有东西的想法。

### CreateColdObservable

就像我们可以记录一个可观察序列一样，我们也可以使用 `CreateColdObservable` 来回放一组 `Recorded<Notification<int>>`。`CreateColdObservable` 的签名只需接受一个 `params` 数组的记录通知。

```csharp
// 从一系列通知中创建一个冷可观察对象。
// 返回显示指定消息行为的冷可观察对象。
public ITestableObservable<T> CreateColdObservable<T>(
    params Recorded<Notification<T>>[] messages)
{...}
```

`CreateColdObservable` 返回一个 `ITestableObservable<T>`。这个接口通过暴露 "订阅" 列表和它将产生的消息列表，扩展了 `IObservable<T>`。

```csharp
public interface ITestableObservable<T> : IObservable<T>
{
    // 获取对可观察对象的订阅。
    IList<Subscription> Subscriptions { get; }

    // 获取可观察对象发送的记录通知。
    IList<Recorded<Notification<T>>> Messages { get; }
}
```

使用 `CreateColdObservable`，我们可以模拟我们之前使用 `Observable.Interval` 的测试。

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateColdObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
    new Recorded<Notification<long>>(20000000, Notification.CreateOnNext(1L)),
    new Recorded<Notification<long>>(30000000, Notification.CreateOnNext(2L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnNext(3L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnCompleted<long>())
    );

var testObserver = scheduler.Start(
    () => source,
    0,
    0,
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("  {0} @ {1}", message.Value, message.Time);
}
```

输出：

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000001
OnNext(1) @ 20000001
OnNext(2) @ 30000001
OnNext(3) @ 40000001
OnCompleted() @ 40000001
```

请注意，我们的输出与 `Observable.Interval` 的前一个示例完全相同。

### CreateHotObservable

我们也可以使用 `CreateHotObservable` 方法创建热测试可观察序列。它具有与 `CreateColdObservable` 相同的参数和返回值；区别在于，每条消息的虚拟时间现在是相对于可观察对象创建时的时间，而不是与 `CreateColdObservable` 方法一样的订阅时间。

这个例子正是最后那个“冷”样本，但创建了一个热可观察序列。

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateHotObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
// ...    
```

输出：

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000000
OnNext(1) @ 20000000
OnNext(2) @ 30000000
OnNext(3) @ 40000000
OnCompleted() @ 40000000
```

注意，输出几乎与冷相似。安排创建和订阅不会影响热可观察对象，因此通知比冷同位数提前 1 刻发生。

通过修改虚拟创建时间和虚拟订阅时间为不同值，我们可以看到热可观察对象的主要不同之处。对于冷可观察对象来说，虚拟创建时间并没有真正的影响，因为订阅是启动任何动作的。这意味着我们不能错过冷可观察对象的任何早期消息。对于热可观察对象，如果我们订阅太晚，我们可能会错过消息。在这里，我们立即创建了热可观察对象，但只在 1 秒后订阅它（因此错过了第一个消息）。

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateHotObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
    new Recorded<Notification<long>>(20000000, Notification.CreateOnNext(1L)),
    new Recorded<Notification<long>>(30000000, Notification.CreateOnNext(2L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnNext(3L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnCompleted<long>())
    );

var testObserver = scheduler.Start(
    () => source,
    0,
    TimeSpan.FromSeconds(1).Ticks,
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("  {0} @ {1}", message.Value, message.Time);
}
```

输出：

```
Time is 50000000 ticks
Received 4 notifications
OnNext(1) @ 20000000
OnNext(2) @ 30000000
OnNext(3) @ 40000000
OnCompleted() @ 40000000
```

### CreateObserver 

最后，如果您不想使用 `TestScheduler.Start` 方法，而需要对您的观察者进行更细粒度的控制，您可以使用 `TestScheduler.CreateObserver()`。这将返回一个 `ITestObserver`，您可以使用它来管理对您的可观察序列的订阅。此外，您仍然可以看到记录的消息和任何订阅者。

现代行业标准要求广泛覆盖自动化单元测试以满足质量保证标准。然而，并发编程通常是一个测试良好的难题领域。Rx 提供了一个设计良好的测试功能实现，允许确定性和高吞吐量的测试。`TestScheduler` 提供了控制虚拟时间和生成用于测试的可观察序列的方法。这种轻松可靠地测试并发系统的能力使 Rx 与许多其他库区别开来。