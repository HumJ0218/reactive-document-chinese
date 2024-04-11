# 分区

Rx可以将单个序列拆分为多个序列。这对于将项目分配给多个订阅者非常有用。在进行分析时，对分区进行聚合可能是有用的。您可能已经熟悉标准LINQ运算符`GroupBy`。Rx支持这一点，并且还定义了一些自己的。

## GroupBy

`GroupBy`运算符允许您像`IEnumerable<T>`的`GroupBy`运算符那样划分序列。再次打开开源[Ais.Net项目](https://github.com/ais-dotnet)可以提供一个有用的示例。它的[`ReceiverHost`类](https://github.com/ais-dotnet/Ais.Net.Receiver/blob/15de7b2908c3bd67cf421545578cfca59b24ed2c/Solutions/Ais.Net.Receiver/Ais/Net/Receiver/Receiver/ReceiverHost.cs)通过Rx提供AIS消息，定义了一个类型为`IObservable<IAisMessage>`的`Messages`属性。这是一个非常繁忙的源，因为它报告能够访问的每条消息。例如，如果您将接收器连接到挪威政府慷慨提供的AIS消息源，每次挪威海岸的任何船只广播AIS消息时，都会产生一个通知。挪威有很多船只在移动，所以这是一个信息量巨大的源。

如果我们确切知道我们感兴趣的船只，您已经在[过滤章节](05_Filtering.md)中看到了如何过滤此流。但如果我们不知道，但仍然希望能够执行与个别船只相关的处理呢？例如，我们可能想要发现任何船只何时更改其`NavigationStatus`（报告诸如`AtAnchor`或`Moored`之类的值）。[过滤章节的`Distinct`和`DistinctUntilChanged`部分](05_Filtering.md#distinct-and-distinctuntilchanged)展示了如何做到这一点，但它是从筛选单个船只的消息流开始的。如果我们尝试直接在全部船只流上使用`DistinctUntilChanged`，它将不会产生有意义的信息。如果船A停泊并且船B在锚位，如果我们从船A和船B接收到交替状态消息，`DistinctUntilChanged`将报告每条消息都是状态更改，尽管两艘船的状态都没有变化。

我们可以通过将“所有船只”的序列拆分为许多小序列来解决这个问题：

```csharp
IObservable<IGroupedObservable<uint, IAisMessage>> perShipObservables = 
   receiverHost.Messages.GroupBy(message => message.Mmsi);
```

此`perShipObservables`是可观察序列的序列。更具体地说，它是分组的可观察序列的可观察序列，但正如您从[`IGroupedObservable<TKey, T>`的定义](https://github.com/dotnet/reactive/blob/95d9ea9d2786f6ec49a051c5cff47dc42591e54f/Rx.NET/Source/src/System.Reactive/Linq/IGroupedObservable.cs#L18)中看到的，分组的可观测物只是一种特殊类型的可观测物：

```csharp
public interface IGroupedObservable<out TKey, out TElement> : IObservable<TElement>
{
    TKey Key { get; }
}
```

每次`receiverHost.Message`报告AIS消息时，`GroupBy`运算符将调用回调以找出此项目属于哪个组。我们将回调返回的值称为_键_，并且`GroupBy`会记住它已经看到的每个键。如果这是一个新键，`GroupBy`将创建一个新的`IGroupedObservable`，其`Key`属性将是回调刚刚返回的值。它从外部可观察对象（我们放在`perShipObservables`中的那个）发出此`IGroupedObservable`，然后立即使该新的`IGroupedObservable`发出产生该键的元素（在本例中为`IAisMessage`）。但是如果回调产生了`GroupBy`之前见过的键，它会找到已经为该键生成过的`IGroupedObservable`，并使其发出该值。

因此，在此示例中，效果是每当`receiverHost`报告来自我们之前未听说过的船只的消息时，`perShipObservables`将发出一个新的可观察对象，仅报告该船只的消息。我们可以使用此方法报告每次我们了解到新船只的时间：

```csharp
perShipObservables.Subscribe(m => Console.WriteLine($"New ship! {m.Key}"));
```

但这并不能做到我们无法通过`Distinct`实现的任何事情。`GroupBy`的力量在于我们在这里为每艘船获得一个可观察序列，因此我们可以继续设置一些每艘船的处理：

```csharp
IObservable<IObservable<IAisMessageType1to3>> shipStatusChangeObservables =
    perShipObservables.Select(shipMessages => shipMessages
        .OfType<IAisMessageType1to3>()
        .DistinctUntilChanged(m => m.NavigationStatus)
        .Skip(1));
```

这使用[`Select`](06_Transformation.md#select)（在转换章节中介绍）对从`perShipObservables`出来的每个组应用处理。记住，每个这样的组代表一艘不同的船，所以我们传递给`Select`的回调将为每艘船精确调用一次。这意味着现在我们可以使用`DistinctUntilChanged`了。此示例提供给`DistinctUntilChanged`的输入是代表来自一艘船的消息的序列，因此这将告诉我们该船何时更改其状态。现在能够做我们想做的事情，因为每艘船都有自己的`DistinctUntilChanged`实例。`DistinctUntilChanged`总是转发它接收到的第一个事件-它只会在它们与前一个项目相同时丢弃项目，而在这种情况下没有前一个项目。但这在这里不太可能是正确的行为。假设我们看到的一些名为`A`的船只的第一条消息报告其状态为`Moored`。在我们启动之前，它可能处于某种不同状态，而我们收到的第一条报告恰好代表状态更改。但更有可能的是，在我们启动之前，它已经停靠了一段时间。我们不能确定，但大多数状态报告并不代表更改，因此`DistinctUntilChanged`始终转发第一个事件的行为可能是错误的。因此，我们使用`Skip(1)`来删除来自每艘船的第一条消息。

此时，我们有了一个可观察序列的可观察序列。外部序列为我们看到的每艘不同的船产生了一个嵌套序列，并且该嵌套序列将报告该特定船只的`NavigationStatus`更改。

我要做一个小调整：

```csharp
IObservable<IAisMessageType1to3> shipStatusChanges =
    perShipObservables.SelectMany(shipMessages => shipMessages
        .OfType<IAisMessageType1to3>()
        .DistinctUntilChanged(m => m.NavigationStatus)
        .Skip(1));
```

我用[`SelectMany`](06_Transformation.md#selectmany)（也在转换章节中描述）替换了`Select`。如您所记得，`SelectMany`将嵌套的可观察对象展平为单一的平坦序列。您可以从返回类型中看到这一点：现在我们得到了一个`IObservable<IAisMessageType1to3>`，而不是序列的序列。

等一下！我刚才是否撤销了`GroupBy`所做的工作？我要求它按船只ID划分事件，那么为什么我现在要将其重新组合成单一平坦的流？这不是我开始的吗？

这是真的，流类型与我的原始输入具有相同的形状：这将是AIS消息的单一可观察序列。（它稍微专门一些——元素类型是`IAisMessageType1to3`，因为我可以从中获取`NavigationStatus`，但这些仍然实现了`IAisMessage`。）所有不同的船只都将在这一个流中混合在一起。但我实际上并没有否定`GroupBy`所做的工作。这个弹球图表示了发生了什么：

![一个表明输入可观察对象名为receiverHost.Messages如何扩展到组中，被处理后又被压缩回单一源的弹球图。输入可观察对象显示了来自三艘不同船只“A”、“B”和“C”的事件。每个事件都标有船只报告的状态。A的所有消息都报告状态为Moored。B首先进行了两次AtAnchor状态报告，然后是两次UnderwayUsingEngine报告。C两次报告UnderwaySailing，然后是AtAnchor，然后再次是UnderwaySailing。来自三艘船的事件是交织的：输入行上的顺序为A、B、C、B、A、C、B、C、C、B、A。下一部分标记为perShipObservables，这显示了按船只分组事件的效果。第一行只显示来自A的事件，第二行是B的，第三行是C的。下一节用前面示例中的处理代码标记，并显示了与前面图表部分中的三个组对应的三个更多可观察对象。但在这一个中，A的来源根本没有事件。第二行为B显示了一项事件，即它首次报告UnderwayUsingEngine的那一项。它为C显示了两项：它报告AtAnchor的那一项，然后是之后报告UnderwaySailing的那一项。图表的最后一行是单一来源，结合了前面部分图表中刚描述的事件。](GraphicsIntro/Ch08-Partitioning-Marbles-Status-Changes.svg)

`perShipObservables`部分显示了`GroupBy`是如何为每艘不同的船只创建一个单独的可观察对象的。（这个图表显示了三艘船只，分别命名为“A”、“B”和“C”。对于真实的源头，`GroupBy`会产生更多的可观察对象输出，但原理保持不变。）我们在这些组流上进行了一些工作，然后将其压平。正如已描述的，我们使用`DistinctUntilChanged`和`Skip(1)`来确保我们只在确定某艘船的状态已经变化时产生事件。（由于我们只看到“A”报告的状态为`Moored`，那么就我们所知，它的状态从未改变，这就是为什么它的流完全为空。）只有那时我们才将其压平回单一的可观察序列。

弹球图需要简单才能适应页面，所以现在让我们快速查看一些真实输出。这证实了这与原始的`receiverHost.Messages`非常不同。首先，我需要附加一个订阅者：

```csharp
shipStatusChanges.Subscribe(m => Console.WriteLine(
   $"Vessel {((IAisMessage)m).Mmsi} changed status to {m.NavigationStatus} at {DateTimeOffset.UtcNow}"));
```

如果我让接收器运行大约十分钟，我会看到这个输出：

```
Vessel 257076860 changed status to UnderwayUsingEngine at 23/06/2023 06:42:48 +00:00
Vessel 257006640 changed status to UnderwayUsingEngine at 23/06/2023 06:43:08 +00:00
Vessel 259005960 changed status to UnderwayUsingEngine at 23/06/2023 06:44:23 +00:00
Vessel 259112000 changed status to UnderwayUsingEngine at 23/06/2023 06:44:33 +00:00
Vessel 259004130 changed status to Moored at 23/06/2023 06:44:43 +00:00
Vessel 257076860 changed status to NotDefined at 23/06/2023 06:44:53 +00:00
Vessel 258024800 changed status to Moored at 23/06/2023 06:45:24 +00:00
Vessel 258006830 changed status to UnderwayUsingEngine at 23/06/2023 06:46:39 +00:00
Vessel 257428000 changed status to Moored at 23/06/2023 06:46:49 +00:00
Vessel 257812800 changed status to Moored at 23/06/2023 06:46:49 +00:00
Vessel 257805000 changed status to Moored at 23/06/2023 06:47:54 +00:00
Vessel 259366000 changed status to UnderwayUsingEngine at 23/06/2023 06:47:59 +00:00
Vessel 257076860 changed status to UnderwayUsingEngine at 23/06/2023 06:48:59 +00:00
Vessel 257020500 changed status to UnderwayUsingEngine at 23/06/2023 06:50:24 +00:00
Vessel 257737000 changed status to UnderwayUsingEngine at 23/06/2023 06:50:39 +00:00
Vessel 257076860 changed status to NotDefined at 23/06/2023 06:51:04 +00:00
Vessel 259366000 changed status to Moored at 23/06/2023 06:51:54 +00:00
Vessel 232026676 changed status to Moored at 23/06/2023 06:51:54 +00:00
Vessel 259638000 changed status to UnderwayUsingEngine at 23/06/2023 06:52:34 +00:00
```

关键是要理解，在十分钟内，`receiverHost.Messages`产生了_成千上万_条消息。（发生率会根据一天中的时间而变化，但通常每分钟超过一千条消息。当我运行它产生该输出时，代码大概处理了大约一万条消息。）但正如您所看到的，`shipStatusChanges`仅产生了19条消息。

这表明Rx如何以比仅聚合更强大的方式驯服高容量事件源。我们不仅仅是将数据简化为只能提供概览的某些统计度量。统计度量，如平均值或方差通常非常有用，但它们并不总能为我们提供我们想要的领域特定洞察。例如，它们无法告诉我们关于任何特定船只的任何事情。但在这里，每条消息都告诉我们关于特定船只的某些事情。尽管我们正在查看所有船只，我们仍然能够保留那个层次的细节，我们已经能够指导Rx告诉我们任何船只何时更改其状态。

这可能看起来像我在这件事上大惊小怪，但实现这个结果所花的努力如此之少，很容易忽略Rx在这里为我们做了多少工作。这段代码完成了以下所有操作：

- 监控挪威水域中的每一艘船只
- 提供每艘船的信息
- 报告人类可以合理应对的速率的事件

这可以从成千上万条消息中筛选出对我们真正重要的少数几条消息。

这是我在[转换章节的"The Significance of SelectMany"](06_Transformation.md#the-significance-of-selectmany)中描述的“分散出去，然后再聚合”的技术的一个例子。该代码使用`GroupBy`从单一的可观察对象分散到多个可观察对象。此步骤的关键是创建提供我们想要进行处理的正确细节级别的嵌套可观察对象。在这个例子中，该细节级别是“一个特定的船只”，但它不必总是这样。你可以想象按区域分组消息——我们可能对比较不同港口感兴趣，所以我们希望根据船只最接近的港口或目的地港口来分区源。（AIS提供了一种方法，让船只广播其预定目的地。）在根据我们需要的标准分区数据后，我们再定义要对每个组应用的处理。在本例中，我们仅监视`NavigationStatus`的更改。此步骤通常是减少体积的地方。例如，大多数船只最多每天只会更改其`NavigationStatus`几次。在将通知流减少到我们真正关心的事件后，我们可以将其重新组合成一个提供我们想要的高价值通知的单一流。

这种能力当然是有代价的。让Rx为我们完成这项工作并不需要太多代码，但我们让它做了相当多的工作：它需要记住到目前为止看到的每艘船，并为每艘船维护一个可观察源。如果我们的数据源足够广泛以接收来自数以万计的船只的消息，Rx将需要维护数以万计的可观察源，每艘船一个。所示示例没有任何类似活动超时的东西——即使是广播一条消息的船只也会被记住，只要程序运行。（恶意行为者伪造每个带有不同虚构标识符的AIS消息最终会导致此代码因内存耗尽而崩溃。）根据您的数据来源，您可能需要采取措施避免内存使用无限增长，因此真实的例子可能比这更复杂，但基本方法是非常强大的。

现在我们已经看到了一个例子，让我们更详细地看看`GroupBy`。它有几种不同的风味。我们只是使用了这个重载：

```csharp
public static IObservable<IGroupedObservable<TKey, TSource>> GroupBy<TSource, TKey>(
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector)
```

该重载使用您选择的键类型的默认比较行为。在我们的例子中，我们使用了`uint`（在AIS消息中唯一标识船只的`Mmsi`属性的类型），这只是一个数字，所以它本质上是一种可比较的类型。在某些情况下，您可能希望进行非标准比较。例如，如果您使用`string`作为键，您可能希望能够指定区域特定的不区分大小写的比较。对于这些情况，有一个接受比较器的重载：

```csharp
public static IObservable<IGroupedObservable<TKey, TSource>> GroupBy<TSource, TKey>(
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector, 
    IEqualityComparer<TKey> comparer)
```

还有两个重载扩展了前面两个，带有一个`elementSelector`参数：

```csharp
public static IObservable<IGroupedObservable<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector, 
    Func<TSource, TElement> elementSelector)
{...}

public static IObservable<IGroupedObservable<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector, 
    Func<TSource, TElement> elementSelector, 
    IEqualityComparer<TKey> comparer)
{...}
```

这在功能上等同于在`GroupBy`后使用`Select`运算符。

顺便说一下，使用`GroupBy`时，您可能会很想直接`Subscribe`到嵌套的可观察对象：

```csharp
// 不要这样做。使用前面的例子。
perShipObservables.Subscribe(shipMessages =>
  shipMessages
    .OfType<IAisMessageType1to3>()
    .DistinctUntilChanged(m => m.NavigationStatus)
    .Skip(1)
    .Subscribe(m => Console.WriteLine(
    $"Ship {((IAisMessage)m).Mmsi} changed status to {m.NavigationStatus} at {DateTimeOffset.UtcNow}")));
```

这似乎产生了相同的效果：`perShipObservables`在这里是`GroupBy`返回的序列，因此它将为每艘不同的船只产生一个可观察流。这个例子订阅了这一点，然后在每个嵌套序列上使用与之前相同的运算符，但与通过`SelectMany`收集结果输出到一个单一输出可观察对象的早期版本不同，这显式地为每个嵌套流调用`Subscribe`。

如果您不熟悉Rx，这似乎是一种更自然的工作方式。但虽然这似乎会产生相同的行为，它引入了一个问题：Rx不理解这些嵌套订阅与外部订阅之间的关系。在这个简单的例子中，这不一定会导致问题，但如果我们开始使用其他运算符，就可能会出现问题。考虑这个修改：

```csharp
IDisposable sub = perShipObservables.Subscribe(shipMessages =>
  shipMessages
    .OfType<IAisMessageType1to3>()
    .DistinctUntilChanged(m => m.NavigationStatus)
    .Skip(1)
    .Finally(() => Console.WriteLine($"Nested sub for {shipMessages.Key} ending"))
    .Subscribe(m => Console.WriteLine(
    $"Ship {((IAisMessage)m).Mmsi} changed status to {m.NavigationStatus} at {DateTimeOffset.UtcNow}")));
```

我为嵌套序列添加了`Finally`操作符。这使我们可以在序列因任何原因结束时调用一个回调。但即使我们取消外部序列的订阅（通过调用`sub.Dispose();`），这个`Finally`也不会触发任何事情。这是因为Rx无法知道这些内部订阅是外部订阅的一部分。

如果我们对之前版本进行同样的修改，在这个版本中，这些嵌套序列被`SelectMany`汇集到一个输出序列中，Rx就会理解这些内部序列的订阅只是因为对由`SelectMany`返回的序列的订阅而存在的。（事实上，是`SelectMany`订阅了这些内部序列。）因此，如果我们取消订阅那个例子中的输出序列，它会正确地运行任何内部序列的`Finally`回调。

更普遍地说，如果你有许多序列作为单个处理链的一部分而出现，通常最好让Rx从头到尾管理这个过程。

## Buffer

如果你需要成批处理事件，`Buffer`操作符非常有用。这对性能特别有帮助，尤其是当你正在存储有关事件的数据时。以AIS示例为例。如果你想将通知记录到持久存储中，存储单个记录的成本几乎与存储几个相同。大多数存储设备以几千字节的数据块大小运作，因此存储单个数据字节所需的工作量通常与存储几千字节的数据相同。在编程中，将数据缓冲到我们拥有相当大的工作块时的模式一直很普遍。.NET运行时库的`Stream`类内置了缓冲，正是出于这个原因，Rx内置了它。

出于效率的考虑并非是你希望批量处理多个事件的唯一原因。假设你想要生成一些数据源的持续更新统计流。通过使用`Buffer`将源划分为块，你可以计算，比如说，最近10个事件的平均值。

`Buffer`可以对源流中的元素进行分区，所以它是一种与`GroupBy`类似的操作符，但有几个重要区别。首先，`Buffer`不检查元素以确定如何划分它们——它仅基于元素出现的顺序来划分。其次，`Buffer`等待直到完全填满一个分区，然后将所有元素作为`IList<T>`呈现。这可以使某些任务变得更容易，因为分区中的所有内容都可用于立即使用——值不会嵌套在`IObservable<T>`中。第三，`Buffer`提供了一些重载，使得一个元素可以出现在不止一个“分区”中。（在这种情况下，`Buffer`不再严格分区数据，但正如您将看到的，这只是其他行为的一个小变体。）

使用`Buffer`的最简单方法是将相邻元素聚集到块中。（LINQ to Objects现在有一个等效操作符，它称为[`Chunk`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.chunk)。Rx没有使用相同的名称的原因是Rx在LINQ to Objects之前十多年就引入了这个操作符。所以真正的问题是为什么LINQ to Objects选择了一个不同的名字。可能是因为`Chunk`不支持Rx的`Buffer`所有变体，但你需要询问.NET运行时库团队。）这个重载的`Buffer`采用单个参数，指示你想要的块大小：

```csharp
public static IObservable<IList<TSource>> Buffer<TSource>(
    this IObservable<TSource> source, 
    int count)
{...}
```

这个例子使用它将导航消息分成4块，然后继续计算这4个读数的平均速度：

```csharp
IObservable<IList<IVesselNavigation>> navigationChunks = 
   receiverHost.Messages.Where(v => v.Mmsi == 235009890)
                        .OfType<IVesselNavigation>()
                        .Where(n => n.SpeedOverGround.HasValue)
                        .Buffer(4);

IObservable<float> recentAverageSpeed = 
    navigationChunks.Select(chunk => chunk.Average(n => n.SpeedOverGround.Value));
```

如果源完成，并且没有产生刚好是块大小的倍数的最终块将更小。我们可以用以下更人为的示例来观察这一点：

```csharp
Observable
    .Range(1, 5)
    .Buffer(2)
    .Select(chunk => string.Join(", ", chunk))
    .Dump("chunks");
```

正如您从这个输出中可以看到，最终块只有一个项目，即使我们一次要求2个：

```
chunks-->1, 2
chunks-->3, 4
chunks-->5
chunks completed
```

`Buffer`在此处别无选择，因为源已完成，如果它没有产生那个最终的未满块，我们将永远看不到最终项。但除了这种源结束的情况外，`Buffer`的这种重载在传递它之前会等待直到收集到足够的元素来填满指定大小的缓冲区。这意味着`Buffer`引入了延迟。如果源项目相隔很远（例如，当船只不移动时，它可能每隔几分钟只报告一次AIS导航数据），这可能导致很长时间的延迟。

在某些情况下，我们可能希望在源繁忙时批量处理多个事件，而在源操作较慢时不需要等待很长时间。这在用户界面中会很有用。如果你想提供最新信息，接受一个未满的块可能会更好，以便你可以提供更及时的信息。对于这些场景，`Buffer`提供了接受`TimeSpan`的重载：

```csharp
public static IObservable<IList<TSource>> Buffer<TSource>(
    this IObservable<TSource> source, 
    TimeSpan timeSpan)
{...}

public static IObservable<IList<TSource>> Buffer<TSource>(
    this IObservable<TSource> source, 
    TimeSpan timeSpan, 
    int count)
{...}
```

这些重载中的第一个仅基于时间对源进行分区。这将每秒发出一个块，无论`source`的产生速率如何：

```csharp
IObservable<IList<string>> output = source.Buffer(TimeSpan.FromSeconds(1));
```

如果`source`在任何特定块的生命周期内未发出任何值，`output`将发出空列表。

第二个重载，同时接受`timespan`和`count`，本质上设置了两个上限：您将不会超过`timespan`间隔等待块，且不会接收到超过`count`元素的块。与仅`timespan`重载一样，如果源没有在指定的时间内产生足够的元素来填满缓冲区，这可以传递未满甚至空的块。

### 重叠的缓冲区

在前面的部分中，我展示了一个示例，收集了特定船只的4个`IVesselNavigation`条目，并计算了平均速度。这种多个样本的平均值是一种有用的方式，可以平滑出读数中的轻微随机变化。因此，在这种情况下的目标不是出于效率进行批量处理，而是启用一种特定类型的计算。

但这个示例有一个问题：因为它平均了4个读数，所以每4个输入消息只产生一次输出。由于船只可能在不移动时每隔几分钟才报告一次速度，我们可能需要等待很长时间。

有一个`Buffer`的重载可以让我们做得更好：不是平均前四个读数，然后是之后的四个读数，然后是之后的四个，以此类推，我们可能希望在船只报告新读数时计算最后四个读数的平均值。

这有时被称为滑动窗口。我们希望处理读数1、2、3、4，然后是2、3、4、5，然后是3、4、5、6，依此类推。有一个`Buffer`的重载可以做到这一点。这个示例显示了之前平均速度示例中的第一条语句，但进行了一点小修改：

```csharp
IObservable<IList<IVesselNavigation>> navigationChunks = receiverHost.Messages
    .Where(v => v.Mmsi == 235009890)
    .OfType<IVesselNavigation>()
    .Where(n => n.SpeedOverGround.HasValue)
    .Buffer(4, 1);
```

这调用了一个接受两个`int`参数的`Buffer`重载。第一个参数与之前相同：它表示我们希望每个块中有4个项目。但第二个参数表示我们希望多久产生一次缓冲区。这表示我们希望每个元素（即，源产生的每个元素）产生一个缓冲区。（接受仅`count`的重载相当于向此重载传递相同值的两个参数。）

因此，这将等到源产生了4个合适的消息（即，满足这里的`Where`和`OfType`操作的消息），然后在第一个从`navigationChunks`出现的`IList<VesselNavigation>`中报告这四个读数。但源只需要产生一个更合适的消息，然后这将发出另一个`IList<VesselNavigation>`，其中包含第一个块中相同的3个值，然后是新值。当下一个合适的消息出现时，这将发出另一个列表，其中包含第三个、第四个、第五个和第六个消息，依此类推。

这个弹球图说明了`Buffer(4, 1)`的行为。

![弹球图显示了两个序列。第一个标记为"Range(1,6)"，显示数字1到6。第二个标记为".Buffer(4,1)"，显示了三个事件。颜色编码和水平位置表明这些在顶部图表的最后三个事件同时出现。第一个事件在这个第二个序列中包含一个数字列表"1,2,3,4"，第二个显示"2,3,4,5"，第三个显示"3,4,5,6"。](GraphicsIntro/Ch08-Partitioning-Marbles-Buffer-Marbles.svg)

如果我们将这个输入到之前示例中的相同`recentAverageSpeed`表达式中，我们仍然不会在第四个合适的消息从源出现时获得任何输出，但从那时起，源中的每个合适的消息都将发出一个新的平均值。这些平均值仍然始终报告最近报告的4个速度的平均值，但我们现在将比以前更频繁地获得这些平均值。

我们还可以使用这一点来改进之前报告船只更改其`NavigationStatus`的示例。上一个示例告诉您船舶刚进入的状态，但这引出了一个明显的问题：它之前的状态是什么？我们可以使用`Buffer(2, 1)`，这样每次我们看到指示状态变化的消息时，我们也可以访问前一次状态变化：

```csharp
IObservable<IList<IAisMessageType1to3>> shipStatusChanges =
    perShipObservables.SelectMany(shipMessages => shipMessages
        .OfType<IAisMessageType1to3>()
        .DistinctUntilChanged(m => m.NavigationStatus)
        .Buffer(2, 1));

IDisposable sub = shipStatusChanges.Subscribe(m => Console.WriteLine(
    $"Ship {((IAisMessage)m[0]).Mmsi} changed status from" +
    $" {m[1].NavigationStatus} to {m[1].NavigationStatus}" +
    $" at {DateTimeOffset.UtcNow}"));
```

正如输出所示，我们现在可以报告先前的状态以及刚进入的状态：

```
Ship 259664000 changed status from UnderwayUsingEngine to Moored at 30/06/2023
 13:36:39 +00:00
Ship 257139000 changed status from AtAnchor to UnderwayUsingEngine at 30/06/20
23 13:38:39 +00:00
Ship 257798800 changed status from UnderwayUsingEngine to Moored at 30/06/2023
 13:38:39 +00:00
```

这个更改使我们能够删除`Skip`。之前的例子有它，因为我们无法确定从任何特定船只在启动后收到的第一条消息是否代表一个变化。但由于我们告诉`Buffer`我们想要消息对，所以它在看到两个不同状态的消息之前不会给我们任何船只的信息。

您还可以使用此重载按时间而非计数定义滑动窗口：

```csharp
public static IObservable<IList<TSource>> Buffer<TSource>(
    this IObservable<TSource> source, 
    TimeSpan timeSpan, 
    TimeSpan timeShift)
{...}
```

`timeSpan`确定每个窗口涵盖的时间长度，`timeShift`确定启动新窗口的时间间隔。

## Window

`Window`操作符与`Buffer`非常相似。它可以根据元素计数或时间将输入分割成块，并且还提供对重叠窗口的支持。然而，它有不同的返回类型。使用`Buffer`在`IObservable<T>`上会返回一个`IObservable<IList<T>>`，而`Window`会返回一个`IObservable<IObservable<T>>`。这意味着`Window`不必等到填满一个完整的缓冲区才能产生输出。你可以说`Window`比`Buffer`更充分地拥抱反应性范式。然而，经过一些经验，你可能会得出结论，`Window`比`Buffer`更难使用，但在实践中很少更有用。

因为`Buffer`返回一个`IObservable<IList<T>>`，它在得到将要进入该块的所有元素之前无法产生一个块。`IList<T>`支持随机访问——你可以询问它有多少元素，并且可以通过数字索引检索任何元素，并且我们希望这些操作立即完成。（技术上可以编写一个表示尚未接收的数据的`IList<T>`的实现，并使其`Count`和索引器属性在数据可用前阻塞如果你尝试使用它们，但这将是一件奇怪的事情。开发者期望列表立即返回信息，Rx的`Buffer`生成的列表满足这一期望。）因此，如果你写的是`Buffer(4)`，它在获得构成第一块的所有4项之前不能产生任何东西。

但因为`Window`返回一个产生一个嵌套可观察对象来表示每个块的可观察对象，它可以在不必拥有所有元素之前发出它。实际上，一旦它知道将需要一个窗口，它就会发出一个新窗口。例如，如果你使用`Window(4, 1)`，它返回的可观察对象立即发出其第一个嵌套可观察对象。然后一旦源产生其第一个元素，那个嵌套可观察对象将发出那个元素，然后第二个嵌套可观察对象将被产生。我们将`1`作为`Window`的第二个参数传递，因此我们对于源产生的每个元素都会得到一个新窗口。一旦发出了第一个元素，源发出的下一个项目将出现在第二个窗口中（并且在这种情况下也出现在第一个窗口中，因为我们指定了重叠窗口），所以第二个窗口实际上是在第一个元素出现后立即_开启_的。因此`Window`返回的`IObservable<IObservable<T>>`在那时产生了一个新的`IObservable<T>`。

嵌套的可观察对象在它们可用时发出它们的项。一旦`Window`知道不会有更多的项在那个窗口中（即，与`Buffer`为那个窗口产生完成的`IList<T>`的时点相同），它们就完成了。

`Window`可能看起来比`Buffer`更好，因为它让你可以在单个项在块中可用时立即得到它们。但是，如果你正在执行需要块中每个单独项的计算，这并不一定帮助你。你要等到收到块中的每个项才能完成你的处理，所以你不会更早产生最终结果，而且你的代码可能更复杂，因为它不能再依靠一个`IList<T>`方便地同时访问所有项。不过，如果你正在计算块中项的某种聚合，`Window`可能更有效，因为它使你能够在每个项出现时处理它，然后丢弃它。如果一个块很大，`Buffer`需要保留块完成之前的所有项，这可能会使用更多内存。此外，在你不必在可以对这些项做一些有用的事情之前看到块中的每个项的情况下，`Window`可能使你避免引入处理延迟。

在AIS的`NavigationStatus`示例中，`Window`并没有帮助我们，因为那里的目标是报告每次变更的_之前_和_之后_状态。在我们知道_之后_值之前，我们不会从接收到_之前_值中获益，所以我们可能会使用`Buffer`，因为它更容易。但如果你想追踪到目前为止今天已报告移动的不同船只的数量，`Window`将是一种合适的机制：你可以设置它产生每天一个窗口，你将能够在每个窗口内开始看到信息，而无需等到一天结束。

除了支持简单的基于计数或持续时间的分割之外，还有更灵活的方式来定义窗口边界，例如这个重载：

```csharp

// 将一个可观察序列的每个元素投影到连续的不重叠的窗口中。
// windowClosingSelector : 被调用以定义所产生窗口的边界。当一个窗口关闭时，就会启动一个新窗口。
public static IObservable<IObservable<TSource>> Window<TSource, TWindowClosing>
(
    this IObservable<TSource> source, 
    Func<IObservable<TWindowClosing>> windowClosingSelector
)
```

这些复杂的重载中的第一个允许我们控制窗口何时关闭。每次创建窗口时都会调用`windowClosingSelector`函数，每个窗口将在来自`windowClosingSelector`的相应序列产生值时关闭。该值被忽略，所以序列值的类型并不重要；实际上，你可以通过完成`windowClosingSelector`的序列来关闭窗口。

在这个示例中，我们使用关闭选择器创建一个窗口。我们每次从该选择器返回相同的主题，然后从控制台在用户按Enter时从主题通知。

```csharp
int windowIdx = 0;
IObservable<long> source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(10);
var closer = new Subject<Unit>();
source.Window(() => closer)
      .Subscribe(window =>
       {
           int thisWindowIdx = windowIdx++;
           Console.WriteLine("--Starting new window");
           string windowName = $"Window{thisWindowIdx}";
           window.Subscribe(
              value => Console.WriteLine("{0} : {1}", windowName, value),
              ex => Console.WriteLine("{0} : {1}", windowName, ex),
              () => Console.WriteLine("{0} Completed", windowName));
       },
       () => Console.WriteLine("Completed"));

string input = "";
while (input != "exit")
{
    input = Console.ReadLine();
    closer.OnNext(Unit.Default);
}
```

输出（当我在显示'1'和'5'后按下Enter）：

```
--Starting new window
window0 : 0
window0 : 1

window0 Completed

--Starting new window
window1 : 2
window1 : 3
window1 : 4
window1 : 5

window1 Completed

--Starting new window
window2 : 6
window2 : 7
window2 : 8
window2 : 9

window2 Completed

Completed
```

最复杂的`Window`重载允许我们创建潜在重叠的窗口。

```csharp
// 将一个可观察序列的每个元素投影到零个或多个窗口中。
// windowOpenings : 表明新窗口创建的可观察序列。
// windowClosingSelector : 调用一个函数以定义每个产生的窗口的关闭。
public static IObservable<IObservable<TSource>> Window
    <TSource, TWindowOpening, TWindowClosing>
(
    this IObservable<TSource> source, 
    IObservable<TWindowOpening> windowOpenings, 
    Func<TWindowOpening, IObservable<TWindowClosing>> windowClosingSelector
)
```

这个重载接受三个参数：

1. 源序列
2. 表示何时应打开新窗口的序列
3. 一个函数，接受一个窗口打开值，并返回一个窗口关闭序列

这个重载在窗口的打开和关闭方式上提供了很大的灵活性。各个窗口可以相互独立；它们可以重叠，大小可以变化，甚至可以跳过源中的值。

为了便于我们进入这个更复杂的重载，让我们首先尝试使用它来重新创建`Window`的一个简化版本（接受计数的重载）。为此，我们需要在初始订阅时打开一个窗口，每次源产生指定计数时再次打开一个窗口。窗口需要在达到该计数时关闭。为了实现这一点，我们只需要源序列。我们将多次订阅它，但对于某些类型的源，这可能会导致问题，因此我们通过[`Publish`](15_PublishingOperators.md#publish)操作符进行订阅，它允许多个订阅者同时只订阅底层源一次。

```csharp
public static IObservable<IObservable<T>> MyWindow<T>(
    this IObservable<T> source, 
    int count)
{
    IObservable<T> shared = source.Publish().RefCount();
    IObservable<int> windowEdge = shared
        .Select((i, idx) => idx % count)
        .Where(mod => mod == 0)
        .Publish()
        .RefCount();
    return shared.Window(windowEdge, _ => windowEdge);
}
```

如果我们现在想要扩展此方法以提供跳过功能，我们需要两个不同的序列：一个用于打开窗口，一个用于关闭窗口。我们在订阅时打开一个窗口，之后再在`skip`项过后再次打开一个窗口。我们在窗口打开后过`count`项时关闭这些窗口。

```csharp
public static IObservable<IObservable<T>> MyWindow<T>(
    this IObservable<T> source, 
    int count, 
    int skip)
{
    if (count <= 0) throw new ArgumentOutOfRangeException();
    if (skip <= 0) throw new ArgumentOutOfRangeException();

    IObservable<T> shared = source.Publish().RefCount();
    IObservable<int> index = shared
        .Select((i, idx) => idx)
        .Publish()
        .RefCount();
 
    IObservable<int> windowOpen = index.Where(idx => idx % skip == 0);
    IObservable<int> windowClose = index.Skip(count-1);
 
    return shared.Window(windowOpen, _ => windowClose);
}
```

我们可以看到，每次打开窗口时都会重新订阅`windowClose`序列，因为它是从函数返回的。这使我们可以重新应用跳过（`Skip(count-1)`）对每个窗口。目前，我们忽略了`windowOpen`推送到`windowClose`选择器的值，但如果你需要它进行某些逻辑，它可以供你使用。

正如您所见，`Window`操作符可以非常强大。我们甚至可以使用`Window`来复制其他操作符；例如，我们可以通过让`SelectMany`操作符接受单个值（窗口）来产生零个或多个另一类型的值（在我们的情况下，为单个`IList<T>`）来创建我们自己的`Buffer`实现。为了在不阻塞的情况下创建`IList<T>`，我们可以应用`Aggregate`方法并使用一个新的`List<T>`作为种子。

```csharp
public static IObservable<IList<T>> MyBuffer<T>(this IObservable<T> source, int count)
{
    return source.Window(count)
        .SelectMany(window => 
            window.Aggregate(
                new List<T>(), 
                (list, item) =>
                {
                    list.Add(item);
                    return list;
                }));
}
```

您可能会发现尝试使用`Window`实现其他时间转换方法，比如`Sample`或`Throttle`，是一个有趣的练习。

我们已经看到了一些有用的方法来将单个项目流分散到多个输出序列中，使用的是数据驱动的分组标准，或者是使用`Buffer`或`Window`进行时间基础的块处理。在下一章中，我们将探讨可以将多个流中的数据组合在一起的操作符。