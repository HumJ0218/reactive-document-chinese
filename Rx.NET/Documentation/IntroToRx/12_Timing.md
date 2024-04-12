# 基于时间的序列

在事件源中，时间常常很重要。在某些情况下，对于某个事件来说，唯一感兴趣的信息可能就是它发生的时间。核心的 `IObservable<T>` 和 `IObserver<T>` 接口在它们的方法签名中根本没有提到时间，但它们不需要这样做，因为源可以决定何时调用观察者的 `OnNext` 方法。订阅者知道事件发生的时间，因为它正在发生。这并不总是处理时间最方便的方式，因此 Rx 库提供了一些与时间相关的操作符。我们已经看到了几个提供可选基于时间操作的操作符：[`Buffer`](08_Partitioning.md#buffer) 和 [`Window`](08_Partitioning.md#window)。本章将探讨各种完全基于时间的操作符。

## 时间戳和时间间隔

由于可观察序列是异步的，知道元素接收的时间很方便。显然，订阅者可以一直使用 `DateTimeOffset.Now`，但如果你想把到达时间作为更大查询的一部分，那么 `Timestamp` 扩展方法是一个方便的便利方法，它为每个元素附加一个时间戳。它将其源序列中的元素包装在轻量级的 `Timestamped<T>` 结构中。`Timestamped<T>` 类型是一个结构体，展示了它包装的元素的值，同时显示了 `Timestamp` 操作符接收到它时的 `DateTimeOffset`。

在这个示例中，我们创建了一个每隔一秒产生一个值的序列，然后将其转换为带时间戳的序列。

```csharp
Observable.Interval(TimeSpan.FromSeconds(1))
          .Take(3)
          .Timestamp()
          .Dump("Timestamp");
```

如你所见，`Timestamped<T>` 实现的 `ToString()` 为我们提供了一个可读的输出。

```
Timestamp-->0@07/08/2023 10:03:58 +00:00
Timestamp-->1@07/08/2023 10:03:59 +00:00
Timestamp-->2@07/08/2023 10:04:00 +00:00
TimeStamp completed
```

我们可以看到数值 0、1 和 2 每隔一秒产生一次。

Rx 还提供了 `TimeInterval`。与其说是报告项目到达的时间，不如说它报告项目之间的间隔（对于第一个元素来说，是从订阅后到出现所需的时间）。与 `Timestamp` 方法类似，元素被包装在轻量级结构中。但与 `Timestamped<T>` 装饰每个项目的到达时间不同，`TimeInterval` 用 `TimeInterval<T>` 类型包装每个元素，该类型添加了一个 `TimeSpan`。我们可以修改前一个示例以使用 `TimeInterval`：

```csharp
Observable.Interval(TimeSpan.FromSeconds(1))
          .Take(3)
          .TimeInterval()
          .Dump("TimeInterval");
```

如你所见，现在输出的是元素之间的时间而不是它们接收的时间：

```
Timestamp-->0@00:00:01.0183771
Timestamp-->1@00:00:00.9965679
Timestamp-->2@00:00:00.9908958
Timestamp completed
```

从输出中可以看出，计时并不完全是每隔一秒，但非常接近。其中一些可能是 `TimeInterval` 操作符中的测量噪声，但大多数可变性可能来自 `Observable.Interval` 类。始终存在调度程序无法精确遵守其计时请求的限制。一些调度程序引入的变化比其他调度程序更大。通过 UI 线程传递工作的调度程序最终受限于该线程的消息循环响应的速度。但即使在最有利的条件下，调度程序也受限于 .NET 不是为实时系统（以及 Rx 可用的大多数操作系统）构建的事实。因此，在本节中的所有操作符中，你应该意译，在 Rx 中，时机始终是一个 _最佳努力_ 的事件。

事实上，时间的固有变化可以使 `Timestamp` 特别有用。简单地查看 `DateTimeOffset.Now` 的问题在于，处理事件需要一定的时间，因此你每次尝试在处理一个事件期间读取当前时间时，可能会看到略有不同的时间。通过一次性附加时间戳，我们捕获了观察到的事件时间，然后下游处理添加的任何延迟都无关紧要。该事件将被注释一个固定的时间，表明它通过 `Timestamp` 传递时的时间。

## 延迟

`Delay` 扩展方法将整个序列向时间上移动。`Delay` 试图保持值之间的相对时间间隔。它做到这一点的精确度是有限的——它不会重新创建纳秒级的时间。确切的精度由你使用的调度程序决定，通常在重负载下会变得更差，但它通常会将时间复制到几毫秒以内。

`Delay` 有多种重载方式，提供了不同的指定时间移位的方法。（在所有选项中，你可以选择传递调度器，但如果你调用不带调度器的重载，它默认为 [`DefaultScheduler`](11_SchedulingAndThreading.md#defaultscheduler)。）最直接的方法是传递一个 `TimeSpan`，它将按指定的时间量延迟序列。还有一些延迟接受 `DateTimeOffset` 的方法，它将等到指定的时间到来，然后开始回放输入。（这第二种基于绝对时间的方法本质上等同于 `TimeSpan` 重载。你可以通过从当前时间中减去目标时间来获得一个 `TimeSpan` 以获得几乎相同的效果，除了 `DateTimeOffset` 版本试图处理 `Delay` 被调用和指定时间到达之间发生的系统时钟的变化。）

为了展示 `Delay` 方法的作用，此示例创建了一个间隔一秒的值序列并对其进行时间戳标记。这将表明，并不是订阅被延迟，而是通知实际转发到我们的最终订阅者。

```csharp
IObservable<Timestamped<long>> source = Observable
    .Interval(TimeSpan.FromSeconds(1))
    .Take(5)
    .Timestamp();

IObservable<Timestamped<long>> delay = source.Delay(TimeSpan.FromSeconds(2));

delay.Subscribe(value => 
   Console.WriteLine(
     $"Item {value.Value} with timestamp {value.Timestamp} received at {DateTimeOffset.Now}"),
   () => Console.WriteLine("delay Completed"));
```

如果你查看输出中的时间戳，可以看到 `Timestamp` 捕获的时间比订阅报告的时间早两秒：

```
Item 0 with timestamp 09/11/2023 17:32:20 +00:00 received at 09/11/2023 17:32:22 +00:00
Item 1 with timestamp 09/11/2023 17:32:21 +00:00 received at 09/11/2023 17:32:23 +00:00
Item 2 with timestamp 09/11/2023 17:32:22 +00:00 received at 09/11/2023 17:32:24 +00:00
Item 3 with timestamp 09/11/2023 17:32:23 +00:00 received at 09/11/2023 17:32:25 +00:00
Item 4 with timestamp 09/11/2023 17:32:24 +00:00 received at 09/11/2023 17:32:26 +00:00
delay Completed
```

注意，`Delay` 不会时间位移 `OnError` 通知。这些将立即传播。

## 抽样

`Sample` 方法在你请求的任何间隔产生项。每次它产生一个值时，它报告从源中出现的最后一个值。如果你有一个数据产生速率比你需要的高的源（例如，假设你有一个加速度计每秒报告100次测量，但你只需要每秒采样10次），`Sample` 提供了一种简单的方式来减少数据率。此示例展示了 `Sample` 的操作。

```csharp
IObservable<long> interval = Observable.Interval(TimeSpan.FromMilliseconds(150));
interval.Sample(TimeSpan.FromSeconds(1)).Subscribe(Console.WriteLine);
```

输出：

```
5
12
18
```

如果你仔细看这些数字，你可能会注意到它们之间的间隔并不是每次都相同。我选择了 150ms 的源间隔和 1 秒的抽样间隔，以突出抽样可能需要仔细处理的一个方面：如果源生成项的速率与抽样率不整齐对齐，这可能意味着 `Sample` 引入的不规则性是源中不存在的。如果我们列出底层数列产生值的时间和 `Sample` 每次取值的时间，我们可以看到，这些特定的时间，抽样间隔只与源时间对齐每 3 秒一次。

| Relative time (ms) | Source value | Sampled value |
| :----------------- | :----------- | :------------ |
| 0                  |              |               |
| 50                 |              |               |
| 100                |              |               |
| 150                | 0            |               |
| 200                |              |               |
| 250                |              |               |
| 300                | 1            |               |
| 350                |              |               |
| 400                |              |               |
| 450                | 2            |               |
| 500                |              |               |
| 550                |              |               |
| 600                | 3            |               |
| 650                |              |               |
| 700                |              |               |
| 750                | 4            |               |
| 800                |              |               |
| 850                |              |               |
| 900                | 5            |               |
| 950                |              |               |
| 1000               |              | 5             |
| 1050               | 6            |               |
| 1100               |              |               |
| 1150               |              |               |
| 1200               | 7            |               |
| 1250               |              |               |
| 1300               |              |               |
| 1350               | 8            |               |
| 1400               |              |               |
| 1450               |              |               |
| 1500               | 9            |               |
| 1550               |              |               |
| 1600               |              |               |
| 1650               | 10           |               |
| 1700               |              |               |
| 1750               |              |               |
| 1800               | 11           |               |
| 1850               |              |               |
| 1900               |              |               |
| 1950               | 12           |               |
| 2000               |              | 12            |
| 2050               |              |               |
| 2100               | 13           |               |
| 2150               |              |               |
| 2200               |              |               |
| 2250               | 14           |               |
| 2300               |              |               |
| 2350               |              |               |
| 2400               | 15           |               |
| 2450               |              |               |
| 2500               |              |               |
| 2550               | 16           |               |
| 2600               |              |               |
| 2650               |              |               |
| 2700               | 17           |               |
| 2750               |              |               |
| 2800               |              |               |
| 2850               | 18           |               |
| 2900               |              |               |
| 2950               |              |               |
| 3000               | 19           | 19            |

由于第一次抽样是在源发出五个之后，并且在产生六个之后的间隙过了三分之二的时间，有一种感觉，"正确" 当前值像是 5.67，但 `Sample` 不尝试进行任何此类插值。它只是报告从源中出现的最后一个值。相关的一个后果是，如果抽样间隔足够短，以至于你要求 `Sample` 以比源中出现的速度更快的速度报告值，它只会重复值。

## 节流

`Throttle` 扩展方法提供了一种对产生值速率变化并且有时太快的序列的一种保护。与 `Sample` 方法一样，`Throttle` 将在一段时间内返回最后一个抽样值。与 `Sample` 不同，`Throttle` 的期间是一个滑动窗口。每次 `Throttle` 接收到一个值时，窗口就会重置。只有在时间段过去后，最后一个值才会传播。这意味着 `Throttle` 方法只对产生值速率变化的序列有用。对于以恒定速率产生值的序列（如 `Interval` 或 `Timer`），如果它们产生值的速度超过节流期，它们的所有值将被压制，而如果它们产生值的速度慢于节流期，它们的所有值都会被传播。

```csharp

// 忽略来自一个可观察序列的值，这些值在 dueTime 之前被另一个值跟随。
public static IObservable<TSource> Throttle<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime)
{...}
public static IObservable<TSource> Throttle<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime, 
    IScheduler scheduler)
{...}
```

我们可以将 `Throttle` 应用于使用实时搜索功能，当您输入时提供建议。我们通常希望等到用户停止输入一段时间后再搜索建议，否则，我们可能会连续启动几次搜索，在用户每次按下另一个键时取消上一次搜索。只有在有暂停时，我们才可以根据他们迄今为止输入的内容执行搜索。`Throttle` 适用于这种场景，因为如果源的产生值速度快于指定的速率，它根本不允许任何事件通过。

注意到 RxJS 库决定使他们的节流版本工作不同，所以如果你发现自己既在使用 Rx.NET 又在使用 RxJS，请注意它们的工作方式不同。在 RxJS 中，当源超过指定的速率时，节流不会完全关闭：它只是丢弃足够的项，使输出永远不会超过指定的速率。所以 RxJS 的节流实现是一种速率限制器，而 Rx.NET 的 `Throttle` 更像是一个在超载期间完全关闭的自重置断路器。

## 超时

`Timeout` 操作符方法允许我们在源未在指定期间产生任何通知时以错误终止序列。我们可以指定一个滑动窗口的 `TimeSpan`，或者通过提供一个 `DateTimeOffset` 指定序列必须在该时间之前完成。

```csharp

// 返回源可观察序列或在值之间的最大持续时间过去后的 TimeoutException。
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime)
{...}
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime, 
    IScheduler scheduler)
{...}


// 返回源可观察序列或如果 dueTime 过去后的 TimeoutException。
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    DateTimeOffset dueTime)
{...}
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    DateTimeOffset dueTime, 
    IScheduler scheduler)
{...}
```

如果我们提供一个 `TimeSpan` 并且在该时间跨度内没有产生任何值，那么序列就会因 `TimeoutException` 而失败。

```csharp
var source = Observable.Interval(TimeSpan.FromMilliseconds(100))
                       .Take(5)
                       .Concat(Observable.Interval(TimeSpan.FromSeconds(2)));

var timeout = source.Timeout(TimeSpan.FromSeconds(1));
timeout.Subscribe(
    Console.WriteLine, 
    Console.WriteLine, 
    () => Console.WriteLine("Completed"));
```

最初这足够频繁地产生值来满足 `Timeout`，所以 `Timeout` 由源返回的可观察对象只是转发源的项。但一旦源停止产生项，我们就得到了一个 OnError：

```
0
1
2
3
4
System.TimeoutException: The operation has timed out.
```

另外，我们可以传给 `Timeout` 一个绝对时间；如果序列在该时间之前没有完成，则产生一个错误。

```csharp
var dueDate = DateTimeOffset.UtcNow.AddSeconds(4);
var source = Observable.Interval(TimeSpan.FromSeconds(1));
var timeout = source.Timeout(dueDate);
timeout.Subscribe(
    Console.WriteLine, 
    Console.WriteLine, 
    () => Console.WriteLine("Completed"));
```

输出：

```
0
1
2
System.TimeoutException: The operation has timed out.
```

`Timeout` 还具有其他重载允许我们在发生超时时替换另一个替代序列。

```csharp

// 返回源可观察序列或如果值之间的最大持续时间过去后的其他可观察序列。
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime, 
    IObservable<TSource> other)
{...}

public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    TimeSpan dueTime, 
    IObservable<TSource> other, 
    IScheduler scheduler)
{...}


// 返回源可观察序列或如果 dueTime 过去后的其他可观察序列。
public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    DateTimeOffset dueTime, 
    IObservable<TSource> other)
{...}  

public static IObservable<TSource> Timeout<TSource>(
    this IObservable<TSource> source, 
    DateTimeOffset dueTime, 
    IObservable<TSource> other, 
    IScheduler scheduler)
{...}
```

正如我们现在所见，Rx 提供了管理反应式范式中的时间的功能。数据可以根据你的需求进行定时、节流或抽样。整个序列可以用延迟功能在时间上进行移位，并且可以使用 `Timeout` 操作符断言数据的及时性。

接下来，我们将探讨 Rx 与外部世界之间的界限。
