# 筛选

Rx 为我们提供了工具，允许处理潜在的海量事件，并将这些事件处理为更高级别的见解。这通常涉及到减少体量。相对大量事件，如果较少量的事件流中的每个事件平均来说更具信息性，那么较少的事件可能更有用。达到这一目的的最简单机制就是简单地过滤掉我们不要的事件。Rx 定义了几个可以做到这一点的操作符。

在我们介绍新操作符之前，我们将迅速定义一个扩展方法来帮助阐明几个示例。这个 `Dump` 扩展方法订阅任何 `IObservable<T>`，并显示源产生的每个通知的消息。这个方法接受一个 `name` 参数，将作为每个消息的一部分显示，使我们能够在订阅了多个源的示例中看到事件的来源。

```csharp
public static class SampleExtensions
{
    public static void Dump<T>(this IObservable<T> source, string name)
    {
        source.Subscribe(
            value =>Console.WriteLine($"{name}-->{value}"), 
            ex => Console.WriteLine($"{name} failed-->{ex.Message}"),
            () => Console.WriteLine($"{name} completed"));
    }
}
```

## Where

对序列应用筛选器是一种极为普遍的操作，而 LINQ 中最直接的筛选器是 `Where` 操作符。与 LINQ 的常态一样，Rx 以扩展方法的形式提供其操作符。如果你已经熟悉 LINQ，Rx 的 `Where` 方法的签名对你来说不会感到陌生：

```csharp
IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
```

注意，`source` 参数的元素类型与返回类型相同。这是因为 `Where` 并不修改元素。它可以过滤掉一些元素，但它没有移除的那些元素将保持不变。

这个例子使用 `Where` 来过滤掉所有从 `Range` 序列生成的奇数值，这意味着只有偶数会出现。

```csharp
IObservable<int> xs = Observable.Range(0, 10); // 数字 0-9
IObservable<int> evenNumbers = xs.Where(i => i % 2 == 0);

evenNumbers.Dump("Where");
```

输出：

```
Where-->0
Where-->2
Where-->4
Where-->6
Where-->8
Where completed
```

`Where` 操作符是你会在所有 LINQ 提供者上找到的众多标准 LINQ 操作符之一。LINQ to Objects, `IEnumerable<T>` 的实现，例如，提供了一个等效方法。在大多数情况下，Rx 的操作符的行为就像它们在 `IEnumerable<T>` 实现中一样，尽管有一些例外，我们稍后将讨论。我们将讨论每种实现并解释任何变化。通过实现这些常见的操作符，Rx 也通过 C# 查询表达式语法免费获得语言支持。例如，我们本可以这样编写第一条语句，并且它将编译为实际相同的代码：

```csharp
IObservable<int> evenNumbers =
    from i in xs
    where i % 2 == 0
    select i;
```

本书中的示例大多使用扩展方法，而不是查询表达式，部分原因是 Rx 实现了一些没有相应查询语法的操作符，部分原因是方法调用方法有时可以更容易看清楚正在发生什么。

与大多数 Rx 操作符一样，`Where` 不会立即订阅其源。(Rx LINQ 操作符很像 LINQ to Objects：`IEnumerable<T>` 版本的 `Where` 在不尝试枚举其源的情况下返回。只有当有东西尝试枚举 `Where` 返回的 `IEnumerable<T>` 时，它才会轮询枚举源。)只有当某物调用 `Where` 返回的 `IObservable<T>` 上的 `Subscribe` 时，它才会调用其源的 `Subscribe`。而且对于每次这样的 `Subscribe` 调用，它都会这样做一次。更一般地说，当您将 LINQ 操作符连续使用时，对结果 `IObservable<T>` 的每次 `Subscribe` 调用都会导致一系列 `Subscribe` 调用沿着链向下传递。

这种级联 `Subscribe` 的一个副作用是，`Where`（像大多数其他 LINQ 操作符）既不是固有地 _hot_ 也不是 _cold_：因为它只是订阅其源，所以它的源是 hot 它就是 hot，而源是 cold 它就是 cold。

`Where` 操作符传递所有其 `predicate` 回调返回 `true` 的元素。更精确地说，当您订阅 `Where` 时，它将创建自己的 `IObserver<T>`，并将其作为参数传递给 `source.Subscribe`，而这个观察者会为每个 `OnNext` 调用调用 `predicate`。如果该谓语返回 `true`，则 `Where` 创建的观察者才会在您传递给 `Where` 的观察者上调用 `OnNext`。

`Where` 始终通过对 `OnComplete` 或 `OnError` 的最终调用传递。这意味着，如果你要写这样的代码：

```csharp
IObservable<int> dropEverything = xs.Where(_ => false);
```

尽管这会过滤掉所有元素（因为谓语忽略了其参数并总是返回 `false`，指示 `Where` 丢弃所有东西），但这不会过滤掉错误或完成。

事实上，如果您想要一个丢弃所有元素并只告诉您何时源完成或失败的操作符——有一个更简单的方法。

## IgnoreElements

`IgnoreElements` 扩展方法允许你只接收 `OnCompleted` 或 `OnError` 通知。它相当于使用总是返回 `false` 的谓词的 `Where` 操作符，正如这个例子所示：

```csharp
IObservable<int> xs = Observable.Range(1, 3);
IObservable<int> dropEverything = xs.IgnoreElements();

xs.Dump("Unfiltered");
dropEverything.Dump("IgnoreElements");
```

正如输出所显示的，`xs` 源生成数字 1 到 3 然后完成，但如果我们通过 `IgnoreElements` 运行，我们所看到的只是 `OnCompleted`。

```
Unfiltered-->1
Unfiltered-->2
Unfiltered-->3
Unfiltered completed
IgnoreElements completed
```

## OfType

一些观察序列生成各种类型的项。例如，考虑一个应用程序，该应用程序希望追踪船只的移动。通过 AIS 接收器即可实现。AIS 是自动识别系统，大多数远洋船只都使用它来报告其位置、航向、速度和其他信息。AIS 消息有多种类型。一些报告船的位置和速度，但其名称则在另一种消息中报告。（这是因为大多数船只的移动频率比它们更换名称的频率更高，因此它们以非常不同的间隔广播这两种信息。）

想象一下这在 Rx 中可能是什么样子。实际上你不必这样想。开源项目 [Ais.Net project](https://github.com/ais-dotnet) 包括一个 [`ReceiverHost` 类](https://github.com/ais-dotnet/Ais.Net.Receiver/blob/15de7b2908c3bd67cf421545578cfca59b24ed2c/Solutions/Ais.Net.Receiver/Ais/Net/Receiver/Receiver/ReceiverHost.cs) ，通过 Rx 提供 AIS 消息。`ReceiverHost` 定义了一个类型为 `IObservable<IAisMessage>` 的 `Messages` 属性。由于 AIS 定义了众多消息类型，这个可观察源可以生成许多不同类型的对象。它发出的一切都将实现 [`IAisMessage` 接口](https://github.com/ais-dotnet/Ais.Net.Receiver/blob/15de7b2908c3bd67cf421545578cfca59b24ed2c/Solutions/Ais.Net.Models/Ais/Net/Models/Abstractions/IAisMessage.cs)，该接口报告船只的唯一标识符，但没有太多其他内容。但是 [`Ais.Net.Models` 库](https://www.nuget.org/packages/Ais.Net.Models/) 定义了许多其他接口，包括 [`IVesselNavigation`](https://github.com/ais-dotnet/Ais.Net.Receiver/blob/15de7b2908c3bd67cf421545578cfca59b24ed2c/Solutions/Ais.Net.Models/Ais/Net/Models/Abstractions/IVesselNavigation.cs)，它报告位置、速度和航向，以及 [`IVesselName`](https://github.com/ais-dotnet/Ais.Net.Receiver/blob/15de7b2908c3bd67cf421545578cfca59b24ed2c/Solutions/Ais.Net.Models/Ais/Net/Models/Abstractions/IVesselName.cs)，它告诉你船只的名称。

假设你只对水中船只的位置感兴趣，对船只的名称不感兴趣。你希望看到实现 `IVesselNavigation` 接口的所有消息，并忽略所有未实现的消息。您可以尝试使用 `Where` 操作符来实现这一点：

```csharp
// Won't compile!
IObservable<IVesselNavigation> vesselMovements = 
   receiverHost.Messages.Where(m => m is IVesselNavigation);
```

然而，这将无法编译。你会得到这个错误：

```
Cannot implicitly convert type 
'System.IObservable<Ais.Net.Models.Abstractions.IAisMessage>' 
to 
'System.IObservable<Ais.Net.Models.Abstractions.IVesselNavigation>'
```

请记住，`Where` 的返回类型始终与其输入相同。由于 `receiverHost.Messages` 的类型为 `IObservable<IAisMessage>`，这也是 `Where` 将返回的类型。碰巧我们的谓语确保只有实现 `IVesselNavigation` 的那些消息通过，但没有办法让 C# 编译器理解预设的关系与输出之间的关系。（至于它所知，`Where` 可能做的恰好相反，包括只有那些谓语返回 `false` 的元素。实际上编译器无法猜测 `Where` 可能如何使用它的谓语。）

幸运的是，Rx 为这种情况提供了一个专门的操作符。`OfType` 过滤项将其减少到仅属于特定类型的项。这些项必须是指定类型的准确类型、继承自它，或者，如果是接口，则必须实现它。这使我们能够修复最后一个示例：

```csharp
IObservable<IVesselNavigation> vesselMovements = 
   receiverHost.Messages.OfType<IVesselNavigation>();
```

## 位置筛选

有时，我们并不关心元素是什么，而是关心它在序列中的位置。Rx 定义了几个可以帮助我们解决这个问题的操作符。

### FirstAsync 和 FirstOrDefaultAsync

LINQ 提供方通常实现一个 `First` 操作符，该操作符提供序列的第一个元素。Rx 也不例外，但 Rx 的性质意味着我们通常需要以稍微不同的方式工作。对于数据静止（例如 LINQ to Objects 或 Entity Framework Core）的提供者，源元素已经存在，因此检索第一个项目只是阅读它的问题。但是对于 Rx，源在选择的时候产生数据，因此无法知道第一个项目何时可用。

因此，在 Rx 中，我们通常使用 `FirstAsync`。这将返回一个 `IObservable<T>`，它将产生从源序列中出现的第一个值，然后完成。 (Rx 还提供了一个更传统的 `First` 方法，但它可能会出问题。有关详细信息，请参见稍后的 [**First/Last/Single[OrDefault] 的阻塞版本**](#blocking-versions-of-firstlastsingleordefault) 部分。)

例如，此代码使用前面介绍的 AIS.NET 源报告特定船只（恰当命名为 HMS Example）第一次报告其移动的时间：

```csharp
uint exampleMmsi = 235009890;
IObservable<IVesselNavigation> moving = 
   receiverHost.Messages
    .Where(v => v.Mmsi == exampleMmsi)
    .OfType<IVesselNavigation>()
    .Where(vn => vn.SpeedOverGround > 1f)
    .FirstAsync();
```

除了使用 `FirstAsync` 外，这还使用了前面描述的其他一些过滤元素。它从一个 [`Where`](#where) 步骤开始，该步骤将消息过滤掉仅限于我们感兴趣的一艘船。（具体来说，我们基于该船的 [Maritime Mobile Service Identity，或 MMSI](https://en.wikipedia.org/wiki/Maritime_Mobile_Service_Identity) 进行过滤。）然后我们使用 [`OfType`](#oftype) 以便我们只查看那些报告船只是如何/是否移动的消息。然后我们使用另一个 `Where` 子句，以便我们可以忽略指示船只实际上没有移动的消息，最后，我们使用 `FirstAsync`，这样我们就只获得第一个指示移动的消息。一旦船只移动，这个 `moving` 源将发出单个 `IVesselNavigation` 事件，然后立即完成。

我们可以稍微简化那个查询，因为 `FirstAsync` 选择性地接受一个谓词。这使我们能够将最后一个 `Where` 和 `FirstAsync` 折叠为一个操作符：

```csharp
IObservable<IVesselNavigation> moving = 
   receiverHost.Messages
    .Where(v => v.Mmsi == exampleMmsi)
    .OfType<IVesselNavigation>()
    .FirstAsync(vn => vn.SpeedOverGround > 1f);
```

如果 `FirstAsync` 的输入为空怎么办？如果它在不产生任何项的情况下完成，`FirstAsync` 会调用其订阅者的 `OnError`，传递一个 `InvalidOperationException`，并带有错误消息报告序列不包含元素。如果我们使用接受谓词的形式（如在这第二个示例中），并且没有匹配谓词的元素出现，情况也是如此。这与 LINQ to Objects 的 `First` 操作符一致。（请注意，我们不希望这种情况发生在刚刚显示的示例中，因为源将继续报告 AIS 消息，只要应用程序在运行，就没有理由它会完成。）

有时，我们可能希望能够容忍这种事件的缺失。大多数 LINQ 提供者不仅提供 `First`，还提供 `FirstOrDefault`。我们可以通过修改前面的示例来使用它。这个示例使用 [`TakeUntil` 操作符](#skipuntil-and-takeuntil) 引入一个截止时间：这个示例准备等待 5 分钟，但在此之后放弃。（因此，尽管 AIS 接收器可以无休止地生成消息，这个示例已经决定不会永远等待。）而且因为这意味着我们可能会在没有看到船只移动的情况下完成，所以我们用 `FirstOrDefaultAsync` 替换了 `FirstAsync`：

```csharp
IObservable<IVesselNavigation?> moving = 
   receiverHost.Messages
    .Where(v => v.Mmsi == exampleMmsi)
    .OfType<IVesselNavigation>()
    .TakeUntil(DateTimeOffset.Now.AddMinutes(5))
    .FirstOrDefaultAsync(vn => vn.SpeedOverGround > 1f);
```

如果在 5 分钟内，我们没有看到船只发出以 1 节或更快的速度移动的消息，`TakeUntil` 将取消订阅其上游源并调用 `FirstOrDefaultAsync` 提供的观察者的 `OnCompleted`。虽然 `FirstAsync` 会将此视为错误，但 `FirstOrDefaultAsync` 会产生其元素类型的默认值（在这种情况下是 `IVesselNavigation`；接口类型的默认值是 `null`），将其传递给其订阅者的 `OnNext`，然后调用 `OnCompleted`。

简而言之，这个 `moving` 可观察对象将总是产生正好一个项目。它要么产生表示船只已移动的 `IVesselNavigation`，要么产生 `null` 表示这在这段代码允许的 5 分钟内没有发生。

这种产生 `null` 的方式可能是表示某事未发生的一种可以接受的方式，但这有些笨拙：消费这个 `moving` 源的任何东西现在都必须弄清楚一个通知是否表示感兴趣的事件或事件的缺失。如果这对您的代码来说恰好是方便的，那么很好，但 Rx 提供了一种更直接的表示事件缺失的方式：一个空序列。

您可以想象一个 _first or empty_ 操作符，它会以这种方式工作。由于 LINQ 提供者返回的是一个实际的值，例如 LINQ to Objects 的 `First` 返回 `T`，而不是 `IEnumerable<T>`，因此没有办法让它返回一个空序列。但是因为 Rx 提供的 `First` 类操作符返回 `IObservable<T>`，所以技术上有可能有一个操作符返回第一个项目或根本没有项目。尽管没符合 Rx 的内置操作符，我们可以通过使用更通用的 `Take` 操作符获得完全相同的效果。

### Take

`Take` 是一个标准的 LINQ 操作符，它从序列的开始取出几个项目然后丢弃剩余的部分。

从某种意义上说，`Take` 是 `First` 的泛化：`Take(1)` 只返回第一个项目，所以你可以认为 LINQ 的 `First` 是 `Take` 的一个特例。这并不严格正确，因为这些操作符对缺失元素的反应不同：正如我们刚才看到的，`First`（和 Rx 的 `FirstAsync`）坚持至少接收一个元素，如果你提供一个空序列，它会产生一个 `InvalidOperationException`。即使是更宽容存在的 `FirstOrDefault` 仍坚持产生某种东西。`Take` 的工作方式略有不同。

如果 `Take` 的输入在产生指定数量的元素之前完成，`Take` 不会抱怨——它只是转发源所提供的内容。如果源只做了调用 `OnCompleted`，那么 `Take` 也只是在其观察者上调用 `OnCompleted`。如果我们使用 `Take(5)`，但源产生了三个项目然后完成，`Take(5)` 将把这三个项目转发给其订阅者，然后完成。这意味着我们可以使用 `Take` 来实现前一节中讨论的假设的 `FirstOrEmpty`：

```csharp
public static IObservable<T> FirstOrEmpty<T>(this IObservable<T> src) => src.Take(1);
```

现在是提醒您大多数 Rx 操作符（以及本章中的所有操作符）都不是固有的热源或冷源的好时机。他们遵循其源。给定一些热 `source`，`source.Take(1)` 也是热的。我一直在这些示例中使用的这个 AIS.NET `receiverHost.Messages` 源是热的（因为它报告船只的实时消息广播），因此从它衍生的可观察序列也是热的。为什么现在讨论这个是好时机？因为这使我能够提出以下绝对糟糕的双关语：

```csharp
IObservable<IAisMessage> hotTake = receiverHost.Messages.Take(1);
```

谢谢。我整周都在这里。

`FirstAsync` 和 `Take` 操作符从序列的开始工作。如果我们只对尾部感兴趣怎么办？

### LastAsync, LastOrDefaultAsync 和 PublishLast

LINQ 提供者通常提供 `Last` 和 `LastOrDefault`。这些与 `First` 或 `FirstOrDefault` 几乎做同样的事情，只是顾名思义，它们返回最后一个元素而不是第一个。与 `First` 一样，Rx 的性质意味着与处理静止数据的 LINQ 提供者不同，最后一个元素可能不只是坐在那里等待被获取。因此，就像 Rx 提供 `FirstAsync` 和 `FirstOrDefault` 一样，它提供 `LastAsync` 和 `LastOrDefaultAsync`。（它也提供 `Last`，但是再次，正如 [**First/Last/Single[OrDefault] 的阻塞版本**](#blocking-versions-of-firstlastsingleordefault) 一节讨论的那样，这可能会有问题。）

还有 [`PublishLast`](15_PublishingOperators.md#publishlast)。这与 `LastAsync` 的语义相似，但它对多个订阅的处理不同。每次您订阅 `LastAsync` 返回的 `IObservable<T>` 时，它都会订阅底层源，但 `PublishLast` 只对底层源进行一次 `Subscribe` 调用。（为了控制确切的发生时间，`PublishLast` 返回一个 `IConnectableObservable<T>`。正如 [第 2 章的热与冷源一节](02_KeyTypes.md#hot-and-cold-sources) 所述，这提供了一个 `Connect` 方法，而 `PublishLast` 返回的可连接可观察对象在您调用此方法时订阅其底层源。）一旦这个单一订阅从源接收到 `OnComplete` 通知，它将向所有订阅者发送最终值。（它还记住了最终值，所以如果在产生最终值后有新的观察者订阅，他们在订阅时将立即接收到该值。）最终值紧接着是一个 `OnCompleted` 通知。这是基于更详细地描述在后面章节中的 [`Multicast`](15_PublishingOperators.md#multicast) 操作符的一系列操作符之一。

`LastAsync` 和 `LastOrDefaultAsync` 与 `FirstAsync` 和 `FirstOrDefaultAsync` 的区别与之前相同。如果源在未产生任何元素的情况下完成，`LastAsync` 会报告错误，而 `LastOrDefaultAsync` 会发出其元素类型的默认值然后完成。`PublishLast` 对空源的处理再次不同：如果源在未产生任何元素的情况下完成，`PublishLast` 返回的可观察对象也会这样做：在这种情况下，它不会产生错误也不会产生默认值。

报告序列的最后一个元素带来了 `First` 不面临的挑战。知道您从源接收到第一个元素是非常容易的：如果源产生了一个元素，并且之前没有产生元素，那么就是第一个元素。这意味着像 `FirstAsync` 这样的操作符可以立即报告第一个元素。但是 `LastAsync` 和 `LastOrDefaultAsync` 没有这种奢侈。

如果你从源接收到一个元素，你怎么知道它是最后一个元素？一般来说，当你接收到它的时候，你是不知道的。只有当源接着调用你的 `OnCompleted` 方法时，你才会知道你已经接收到了最后一个元素。这不一定会立即发生。之前的一个示例使用 `TakeUntil(DateTimeOffset.Now.AddMinutes(5))` 在 5 分钟后结束一个序列，如果你这样做了，很可能在最后一个元素发出和 `TakeUntil` 关闭之间会有相当长的时间间隔。在 AIS 的场景中，船只可能每几分钟只发出一次消息，所以我们很可能最终让 `TakeUntil` 转发一条消息，然后几分钟后发现截止时间已到，没有进一步的消息进入。最终的 `OnNext` 和 `OnComplete` 之间可能有几分钟时间间隔。

因为这个原因。`LastAsync` 和 `LastOrDefaultAsync` 在其源完成之前根本不会发出任何东西。**这有一个重要的后果：** 在 `LastAsync` 接收到源的最后一个元素与它将该元素转发给其订阅者之间可能会有相当长的延迟。

### TakeLast

前面我们看到 Rx 实现了标准的 `Take` 操作符，它从序列的开始转发指定数量的元素然后停止。`TakeLast` 转发序列末尾的元素。例如，`TakeLast(3)` 请求源序列的最后 3 个元素。与 `Take` 一样，`TakeLast` 对产生的项目过少的来源很宽容。如果源产生的项目少于 3 个，`TaskLast(3)` 将只转发整个序列。

`TakeLast` 面临与 `Last` 相同的挑战：它不知道何时接近序列的结束。因此它必须保持最近看到的值的副本。它需要内存来保存您指定的任何值数量。如果你写 `TakeLast(1_000_000)`，它需要分配足够大的缓冲区来存储 1,000,000 个值。它不知道它接收到的第一个元素是否会是最后一百万个中的一个。在源最终完成时，`TakeLast` 将知道最后一百万个元素是什么，并需要逐个将它们全部传递给其订阅者的 `OnNext` 方法。

### Skip 和 SkipLast

如果我们想要 `Take` 或 `TakeLast` 操作符的完全相反怎么办？也许我想丢弃源的前 5 个项目而不是接受它们？也许我有一些 `IObservable<float>` 从传感器读取数据，我发现传感器在前几次读数时产生垃圾值，所以我想忽略这些，并只在它稳定下来后开始监听。我可以用 `Skip(5)` 实现这一点。

`SkipLast` 在序列的末尾做同样的事情：它省略指定数量的元素。与我们刚才看到的一些其他操作符一样，这必须处理一个问题，即它无法判断何时接近序列的末尾。它只能在源发出所有元素并随后调用 `OnComplete` 后才能发现最后（比方说）4 个元素是哪些。因此，`SkipLast` 将引入延迟。如果您使用 `SkipLast(4)`，它将不会转发源产生的第一个元素，直到源产生第 5 个元素。因此，它不必等待 `OnCompleted` 或 `OnError` 才能开始做事情，它只需要等到它确定一个元素不是我们想要丢弃的元素之一。

其他关键的过滤方法非常相似，我认为我们可以将它们视为一个大组。首先我们将查看 `Skip` 和 `Take`。这些行为就像它们在 `IEnumerable<T>` 实现中一样。这些是 Skip/Take 方法中最简单且可能最常用的。两种方法都只有一个参数；跳过或接受的值数量。

### SingleAsync 和 SingleOrDefaultAsync

LINQ 操作符通常提供一个 `Single` 操作符，用于当源应该提供正好一个项目时使用，如果它包含更多或为空则为错误。这里适用和 `First` 以及 `Last` 相同的 Rx 考虑因素，所以你可能不会感到惊讶，Rx 提供了一个 `SingleAsync` 方法，返回一个 `IObservable<T>`，它要么正好调用其观察者的 `OnNext` 一次，要么调用其 `OnError` 表示源报告了一个错误，或者源没有产生正好一个项目。

使用 `SingleAsync`，如果源为空，你会得到一个错误，就像使用 `FirstAsync` 和 `LastAsync` 一样，但如果源包含多个项目，你也会得到一个错误。有一个 `SingleOrDefault`，就像其 first/last 对应物一样，它可以容忍空输入序列，在这种情况下会生成带有元素类型默认值的单个元素。

`Single` 和 `SingleAsync` 与 `Last` 和 `LastAsync` 共享的特性是它们在接收源的项目时最初不知道它们是否应该是输出。这可能看起来很奇怪：因为 `Single` 要求源流提供正好一个项目，所以它必须知道它将交付给其订阅者的项目将是它接收的第一个项目。这是事实，但当它接收到第一个项目时它还不知道的事情是源是否会产生第二个。除非和直到源完成，它不能转发第一个项目。我们可以说 `SingleAsync` 的任务是首先验证源是否包含正好一个项目，然后如果确实如此，则转发该项目，如果不是，则报告错误。在错误情况下，`SingleAsync` 如果接收到第二个项目，就会立即知道出错了，因此可以马上在其订阅者上调用 `OnError`。但在成功情景下，直到源确认没有更多内容通过完成时，它才能知道一切正常。只有那时 `SingleAsync` 才会发出结果。

### 阻塞版本的 First/Last/Single[OrDefault]

前面几节中描述的几个操作符的名称以 `Async` 结尾。这有点奇怪，因为通常 .NET 中以 `Async` 结尾的方法会返回一个 `Task` 或 `Task<T>`，而这些方法却都返回一个 `IObservable<T>`。此外，如前所述，每种方法都对应一个标准的 LINQ 操作符，这些操作符通常不以 `Async` 结尾。（再增加一点混乱，一些 LINQ 提供者，如 Entity Framework Core，确实包括了一些这样的 `Async` 版本操作符，但它们不同。与 Rx 不同，这些实际上的确返回 `Task<T>`，因此它们仍然产生单一值，而不是 `IQueryable<T>` 或 `IEnumerable<T>`。）这种命名源于 Rx 早期设计中的一个不幸选择。

如果今天从头开始设计 Rx，相关操作符在前面一节中将只有普通的名称：`First` 和 `FirstOrDefault` 等。所有这些都以 `Async` 结尾的原因是这些在 Rx 2.0 中添加，而 Rx 1.0 已经定义了这些名称的操作符。这个示例使用 `First` 操作符：

```csharp
int v = Observable.Range(1, 10).First();
Console.WriteLine(v);
```

这将输出值 `1`，这是这里 `Range` 的第一个项目。但请注意那个变量 `v` 的类型。它不是 `IObservable<int>`，而仅仅是一个 `int`。如果我们在一个 Rx 操作符上使用这个，而该操作符不会立即在订阅时产生值会怎样？这里有一个例子：

```csharp
long v = Observable.Timer(TimeSpan.FromSeconds(2)).First();
Console.WriteLine(v);
```

如果您运行这个，您会发现 `First` 的调用在产生值之前不会返回。它是一个 _阻塞_ 操作符。我们通常在 Rx 中避免使用阻塞操作符，因为很容易用它们创建死锁。Rx 的全部要点是我们可以创建对事件做出反应的代码，因此只是坐着等待特定的可观察源产生值并不符合这种精神。如果你发现自己想这样做，通常有更好的方法来实现你想要的结果。（或许 Rx 并不适合你正在做的事情。）

如果你真的需要像这样等待一个值，使用 `Async` 形式结合 Rx 对 C# 的 `async`/`await` 的集成支持可能会更好：

```csharp
long v = await Observable.Timer(TimeSpan.FromSeconds(2)).FirstAsync();
Console.WriteLine(v);
```

这在逻辑上具有相同的效果，但因为我们使用了 `await`，这将不会在等待可观察源产生值时阻塞调用线程。这可能会降低死锁的可能性。

能够使用 `await` 这个事实让 `Async` 结尾的方法有些道理，但你可能想知道这里发生了什么。我们已经看到这些方法都返回 `IObservable<T>`，而不是 `Task<T>`，所以我们怎么能使用 `await`？在 [离开 Rx 的世界章节](13_LeavingIObservable.md#integration-with-async-and-await) 中有完整的解释，但简短的答案是 Rx 提供了扩展方法，使这能够工作。当你 `await` 一个可观察序列时，`await` 将在源完成后完成，并返回源产生的最终值。这对于像 `FirstAsync` 和 `LastAsync` 这样的操作符来说效果很好，它们产生确切一个项目。

注意，在某些情况下，值可以立即可用。例如，[第 3 章中的 `BehaviourSubject<T>` 一节](03_CreatingObservableSequences.md#behaviorsubjectt) 显示，`BehaviourSubject<T>` 的定义特征是它总是有一个当前值。这意味着 Rx 的 `First` 方法实际上不会阻塞——它将订阅 `BehaviourSubject<T>`，而 `BehaviourSubject<T>.Subscribe` 在返回前将调用其订阅者的可观察对象上的 `OnNext`。这使得 `First` 可以立即返回一个值而无需阻塞。（当然，如果你使用接受谓词的 `First` 的重载，如果 `BehaviourSubject<T>` 的值不满足谓词，`First` 将随后阻塞。）

### ElementAt

还有另一个标准的 LINQ 操作符用于从源中选择一个特定元素：`ElementAt`。你为它提供一个数字，表示所需元素在序列中的位置。在数据静止的 LINQ 提供者中，这在逻辑上等同于通过索引访问数组元素。Rx 实现了这个操作符，但是虽然大多数 LINQ 提供者的 `ElementAt<T>` 实现返回一个 `T`，Rx 的返回一个 `IObservable<T>`。与 `First`、`Last` 和 `Single` 不同，Rx 并未提供 `ElementAt<T>` 的阻塞形式。但由于你可以等待任何 `IObservable<T>`，你总是可以这样做：

```csharp
IAisMessage fourth = await receiverHost.Message.ElementAt(4);
```

如果你的源序列只产生五个值，我们请求 `ElementAt(5)`，`ElementAt` 返回的序列将在源完成时向其订阅者报告一个 `ArgumentOutOfRangeException` 错误。我们可以用以下三种方式处理这个：

- 优雅地处理 OnError
- 使用 `.Skip(5).Take(1);` 这将忽略前 5 个值，只取第 6 个值。
如果序列少于 6 个元素，我们只会得到一个空的序列，但不会有错误。
- 使用 `ElementAtOrDefault`

`ElementAtOrDefault` 扩展方法将在索引超出范围时保护我们，通过推送 `default(T)` 值。当前没有提供自定义默认值的选项。

## 时间过滤

`Take` 和 `TakeLast` 运算符让我们过滤除了最开始或最后的元素之外的所有东西（`Skip` 和 `SkipLast` 让我们看到除这些之外的所有东西），但这些都要求我们知道确切的元素数量。如果我们想指定截止时间而不是元素数量的方式怎么办？

事实上，你已经看到了一个示例：之前我使用了 `TakeUntil` 将一个无休止的 `IObservable<T>` 转换为一个在五分钟后完成的。这是一族操作符中的一个。

### SkipWhile 和 TakeWhile

在[**Skip 和 SkipLast 一节**](#skip-and-skip-last)中，我描述了一个传感器，在前几次读数中产生垃圾值的情况。这是很常见的。例如，气体监测传感器在能产生准确读数之前，往往需要将某些组件加热到正确的操作温度。在那一节中的示例中，我使用 `Skip(5)` 来忽略前几次读数，但这是一种粗糙的解决方案。我们怎么知道 5 次就足够了？或者它可能更早就准备好了，那么 5 次就太少了。

我们真正想要做的是丢弃读数，直到我们知道读数将是有效的。而这正是 `SkipWhile` 可以派上用场的情景。假设我们有一个传感器，它报告某种特定气体的浓度，但它也报告进行检测的传感器板的温度。而不是希望跳过 5 次读数是一个合理的数字，我们可以表达实际的需求：

```csharp
const int MinimumSensorTemperature = 74;
IObservable<SensorReading> readings = sensor.RawReadings
    .SkipUntil(r => r.SensorTemperature >= MinimumSensorTemperature);
```

这直接表达了我们所需的逻辑：这将丢弃读数，直到设备达到其最低操作温度。

接下来的一组方法允许您在谓词评估为真时跳过或接受序列中的值。对于 `SkipWhile` 操作，这将过滤掉所有值，直到某个值使谓词失败，然后可以返回其余序列。

```csharp
var subject = new Subject<int>();
subject
    .SkipWhile(i => i < 4)
    .Subscribe(Console.WriteLine, () => Console.WriteLine("Completed"));
subject.OnNext(1);
subject.OnNext(2);
subject.OnNext(3);
subject.OnNext(4);
subject.OnNext(3);
subject.OnNext(2);
subject.OnNext(1);
subject.OnNext(0);

subject.OnCompleted();
```

输出：

```
4
3
2
1
0
Completed

```

`TakeWhile` 将在谓词通过的情况下返回所有值，当第一个值失败时序列将完成。

```csharp
var subject = new Subject<int>();
subject
    .TakeWhile(i => i < 4)
    .Subscribe(Console.WriteLine, () => Console.WriteLine("Completed"));
subject.OnNext(1);
subject.OnNext(2);
subject.OnNext(3);
subject.OnNext(4);
subject.OnNext(3);
subject.OnNext(2);
subject.OnNext(1);
subject.OnNext(0);

subject.OnCompleted();
```

输出：

```
1
2
3
Completed

```

### SkipUntil 和 TakeUntil

除了 `SkipWhile` 和 `TakeWhile`，Rx 还定义了 `SkipUntil` 和 `TakeUntil`。这些可能听起来像是表达相同思想的另一种方式：你可能期望 `SkipUntil` 几乎与 `SkipWhile` 做同样的事情，唯一的区别是 `SkipWhile` 持续进行，直到其谓词返回 `true`，而 `SkipUntil` 持续进行，直到其谓词返回 `false`。确实有一个 `SkipUntil` 的重载确实如此（以及一个相应的 `TakeUntil`）。如果这就是它们的全部，那么它们将不会很有趣。然而，`SkipUntil` 和 `TakeUntil` 的重载使我们能够做一些我们无法用 `SkipWhile` 和 `TakeWhile` 做的事情。

你已经看到了一个例子。前面的 [`FirstAsync` 和 `FirstOrDefaultAsync` 一节](#firstasync-and-firstordefaultasync) 包括一个使用接受 `DateTimeOffset` 的 `TakeUntil` 重载的示例。这封装任何 `IObservable<T>`，返回一个 `IObservable<T>`，它将转发来自源的一切，直到指定的时间，此时它将立即完成（并将从底层源取消订阅）。

我们无法使用 `TakeWhile` 实现这一点，因为它仅在源产生项目时咨询其谓词。如果我们想让源在特定时间完成，我们唯一能做的就是希望其源在我们想要完成的确切时刻恰好产生一个项目。`TakeWhile` 将仅在其源产生项目的结果下完成。`TakeUntil` 可以异步完成。如果我们指定未来 5 分钟的时间，无论源在那个时间到达时是否完全空闲，`TakeUntil` 都会完成。（它依赖于 [调度器](11_SchedulingAndThreading.md#schedulers) 能够做到这一点。）

我们不必使用时间，`TakeUntil` 提供一个接受第二个 `IObservable<T>` 的重载。这使我们能够告诉它在发生某些有趣的事情时停止，而无需提前知道确切的发生时间。这种重载的 `TakeUntil` 从源转发项目，直到第二个 `IObservable<T>` 产生一个值。`SkipUntil` 提供了一个类似的重载，其中第二个 `IObservable<T>` 确定何时开始从源转发项目。

**注意**：这些重载要求第二个 observable 产生一个值以触发开始或结束。如果第二个 observable 在不产生单个通知的情况下完成，则它不会产生任何效果——`TakeUntil` 将继续进行，`SkipUntil` 将永远不会产生任何东西。换句话说，这些操作符会将 `Observable.Empty<T>()` 视为与 `Observable.Never<T>()` 等效。

### Distinct 和 DistinctUntilChanged

`Distinct` 是另一个标准的 LINQ 操作符。它从序列中移除重复项。为了做到这一点，它需要记住其源曾经产生的所有值，以便可以过滤掉以前看到的任何项目。Rx 包含 `Distinct` 的实现，并且此示例使用它来显示生成 AIS 消息的船只的唯一标识符，但确保我们只在第一次看到每个标识符时显示：

```csharp
IObservable<uint> newIds = receiverHost.Messages
    .Select(m => m.Mmsi)
    .Distinct();

newIds.Subscribe(id => Console.WriteLine($"New vessel: {id}"));
```

（这有点超前了——它使用了 `Select`，我们将在[序列转换章节](06_Transformation.md)中讲到。然而，这是一个非常广泛使用的 LINQ 操作符，所以你可能已经熟悉了。我在这里使用它来仅从消息中提取 MMSI——船只标识符。）

如果我们只对船只的标识符感兴趣，这个示例就可以了。但如果我们想检查这些消息的细节呢？我们如何保持查看我们以前从未听说过的船只的消息的能力，但仍然能够查看那些消息中的信息？使用 `Select` 提取 id 阻止了我们做这件事。幸运的是，`Distinct` 提供了一个重载，使我们能够改变它确定唯一性的方式。而不是让 `Distinct` 查看它正在处理的值，我们可以提供一个函数，让我们选择我们喜欢的任何特征。因此，而不是将流过滤为以前从未看到的值，我们可以将流过滤为我们以前从未看到的特定属性或属性组合的值。这里有一个简单的例子：

```csharp
IObservable<IAisMessage> newVesselMessages = 
   receiverHost.Messages.Distinct(m => m.Mmsi);
```

在这里，传递给 `Distinct` 的输入现在是一个 `IObservable<IAisMessage>`。（在前面的示例中，它实际上是 `IObservable<uint>`，因为 `Select` 子句只挑选了 MMSI。）因此现在 `Distinct` 每次接收到一个源发出的 `IAisMessage` 时都会接收到整个 `IAisMessage`。但是因为我们提供了一个回调，它不会尝试将完整的 `IAisMessage` 消息与彼此比较。相反，每当它接收到一个消息时，它都会将该消息传递给我们的回调，然后查看我们的回调返回的值，并将其与回调为所有以前看到的消息返回的值进行比较，并且只有在该值是新的情况下才让消息通过。

因此，效果与之前类似。只有在 MMSI 之前未见过的消息才被允许通过。但区别在于这里的 `Distinct` 操作符的输出是 `IObservable<IAisMessage>`，所以当 `Distinct` 允许一个项目通过时，整个原始消息仍然可用。

除了标准的 LINQ `Distinct` 操作符外，Rx 还提供了 `DistinctUntilChanged`。这仅在有变化时才允许通知通过，它通过仅过滤掉相邻的重复项来实现。例如，给定序列 `1,2,2,3,4,4,5,4,3,3,2,1,1` 它将产生 `1,2,3,4,5,4,3,2,1`。与 `Distinct` 记住曾经产生的每一个值不同，`DistinctUntilChanged` 仅记住最近发出的值，并且仅在新值与最近的值匹配时才过滤掉新值。

这个例子使用 `DistinctUntilChanged` 来检测特定船只报告 `NavigationStatus` 的变化。

```csharp
uint exampleMmsi = 235009890;
IObservable<IAisMessageType1to3> statusChanges = receiverHost.Messages
    .Where(v => v.Mmsi == exampleMmsi)
    .OfType<IAisMessageType1to3>()
    .DistinctUntilChanged(m => m.NavigationStatus)
    .Skip(1);
```

例如，如果该船一直在报告 `AtAnchor` 状态，`DistinctUntilChanged` 将因状态与之前相同而丢弃每条这样的消息。但如果状态改变为 `UnderwayUsingEngine`，那么这将使 `DistinctUntilChanged` 让报告该状态的第一条消息通过。然后，它将不再允许任何进一步的消息通过，直到值再次改变，要么回到 `AtAnchor`，要么变为其他如 `Moored`。(`Skip(1)` 在末尾是因为 `DistinctUntilChanged` 总是让它看到的第一条消息通过。我们无法知道那实际上是否代表了状态的变化，但很可能不是：船只每几分钟报告一次状态，但它们更少改变那个状态，因此我们第一次收到船只状态的报告时，它可能并不代表状态变化。通过丢弃第一个项目，我们确保 `statusChanges` 仅在我们可以确定状态发生变化时提供通知。)

这就是我们快速浏览 Rx 中可用的过滤方法。尽管它们相对简单，正如我们已经开始看到的，Rx 的功能在于其操作符的可组合性。

过滤操作符是我们管理这个信息丰富时代可能面临的数据洪流的第一站。现在我们知道如何应用各种标准来移除数据。接下来，我们将转向可以转换数据的操作符。