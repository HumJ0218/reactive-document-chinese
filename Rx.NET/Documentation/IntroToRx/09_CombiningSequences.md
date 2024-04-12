# 序列组合

数据源无处不在，有时我们需要从不止一个数据源获取数据。具有多个输入的常见示例包括：价格源、传感器网络、新闻源、社交媒体聚合器、文件监视器、多点触摸表面、心跳/轮询服务器等。我们处理这些多种刺激的方式也各不相同。我们可能想要将所有数据作为一个整体的数据洪流消费，或者一次一个序列地作为顺序数据消费。我们也可以以有序的方式获取数据，将来自两个数据源的数据值配对进行处理，或者仅仅消费第一个响应请求的数据源的数据。

前面的章节也展示了一些数据处理的 _扇出和回聚_ 风格的示例，我们在其中划分数据，并在每个分区上进行处理，将高容量数据转换为低容量高价值的事件，然后重新组合。这种重构流的能力极大地增强了操作符组合的好处。如果Rx只能让我们应用组合作为一个简单的线性处理链，它的功能将大打折扣。能够拆分流给了我们更多的灵活性。因此，即使在事件来源单一的情况下，我们在处理过程中往往仍需组合多个可观察流。序列组合使您能够跨多个数据源创建复杂查询。这为编写一些非常强大且简洁的代码打开了可能性。

我们已经在前面的章节中使用了[`SelectMany`](06_Transformation.md#selectmany)。这是Rx中的一个基本操作符。正如我们在[变换章节](06_Transformation.md)中所看到的，可以从`SelectMany`构建出几个其他的操作符，其组合流的能力是其强大的一部分。但还有更多专门用于组合的操作符可用，使用这些操作符解决某些问题比使用`SelectMany`更容易。此外，我们之前看到的一些操作符（包括`TakeUntil`和`Buffer`）有我们尚未探索的重载版本，可以组合多个序列。

## 序列顺序组合

我们将从最简单的组合操作符开始，这些操作符不尝试同时组合。它们一次处理一个来源序列。

### Concat

`Concat`可以说是组合序列的最简单方式。它和其他LINQ提供者中同名函数的功能相同：它将两个序列连接起来。结果序列首先产生来自第一个序列的所有元素，然后是第二个序列的所有元素。`Concat`的最简单签名如下所示。

```csharp
public static IObservable<TSource> Concat<TSource>(
    this IObservable<TSource> first, 
    IObservable<TSource> second)
```

由于`Concat`是扩展方法，我们可以将其作为任意序列上的方法调用，将第二个序列作为唯一参数传入：

```csharp
IObservable<int> s1 = Observable.Range(0, 3);
IObservable<int> s2 = Observable.Range(5, 5);
IObservable<int> c = s1.Concat(s2);
IDisposable sub = c.Subscribe(Console.WriteLine, x => Console.WriteLine("Error: " + x));
```

此弹珠图展示了来自两个源 `s1` 和 `s2` 的项目，以及 `Concat` 如何将它们组合成结果 `c`：

![弹珠图展示了三个序列。第一个s1产生了值0、1和2，然后很快完成。第二个s2在s1完成后开始，产生值5、6、7、8和9，然后完成。最终的序列c产生了来自s1和s2的所有值，即：0、1、2、5、6、7、8和9。定位显示在c中的每个项目都与s1或s2中的相应值同时产生。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Marbles.svg)

Rx的`Concat`在有人订阅它返回的`IObservable<T>`之前不会对其源做任何事情。因此在此情况下，当我们调用`c`（`Concat`返回的源）上的`Subscribe`时，它将订阅其第一个输入`s1`，每次其产生一个值时，`c`可观察对象将向其订阅者发出同样的值。如果我们在`s1`完成之前调用`sub.Dispose()`，`Concat`将从第一个源取消订阅，并永远不会订阅`s2`。如果`s1`报告错误，`c`将向其订阅者报告相同的错误，并且再次，它永远不会订阅`s2`。只有在`s1`完成后，`Concat`操作符才会订阅`s2`，此时它将转发第二个输入产生的任何项目直至第二个源完成或失败，或应用程序取消订阅拼接的可观察对象。

尽管Rx的`Concat`和[LINQ to Objects的`Concat`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.concat)行为逻辑相同，但有一些Rx特有的细节需要注意。特别是在Rx中，时序通常比在其他LINQ实现中更为重要。例如，在Rx中，我们区分了 [_热源_ 和 _冷源_](02_KeyTypes.md#hot-and-cold-sources)。对于冷源来说，你订阅的时间通常是不重要的，但热源本质上是实时的，因此你只会被通知到你订阅期间发生的事情。这可能意味着热源可能与`Concat`不兼容。下面的弹珠图说明了这种情况可能产生具有潜在惊讶的结果：

![弹珠图展示了三个序列。第一个，标为'cold'，产生了值0、1和2，然后完成。第二个，标为'hot'，产生值A、B、C、D和E，但定位显示这些与'cold'序列部分重叠。特别是'hot'在'cold'的0和1项之间产生了A，而在1和2之间产生了B，这意味着这些A和B值在cold完成前发生。最终的序列，标为'cold.Concat(hot)'，仅显示了'cold'完成后'hot'产生的值，即仅C、D和E。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Hot-Marbles.svg)

由于`Concat`不会在第一个输入完成前订阅其第二个输入，因此它不会看到`hot`源在一开始就会交付给任何订阅者的前几个项目。这可能不是你所期望的行为：它当然看起来不像将第一个序列与第二个的全部项目连接起来。看起来它错过了`hot`的`A`和`B`。

#### 弹珠图的局限性

这最后的例子揭示了一个细节：弹珠图忽略了这样一个事实：要能够产生项目的可观察源需要一个订阅者。如果没有东西订阅一个`IObservable<T>`，那么它实际上并不产生任何东西。`Concat`在第一个完成之前不会订阅其第二个输入，因此可能更准确的高效能，而不是上面的图表。

![一张弹珠图展示了三个序列。第一个，标为'cold'，产生了值0、1和2，然后完成。第二个，标为'hot'，产出值C、D和E，定位显示这些是在'cold'序列完成后产生的。最终的序列，标为'cold.Concat(hot)'，展示了来自'cold'的所有三个值和来自'hot'的所有三个值，即0、1、2、C、D和E。这整个弹珠图几乎与前一个相同，除了'hot'序列没有产生A或B值，并且在'cold'完成后直接开始。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Hot-Marbles-SubOnly.svg)

这使我们更容易看到`Concat`产生输出的原因。但由于`hot`在这里是热源，这张图没有传达出`hot`完全按照自己的时间表产生项目的事实。在`hot`有多个订阅者的情形下，之前那张弹珠图可能更好，因为它正确地反映了`hot`提供的每个事件（不管此时有多少监听者可能订阅）。但是，尽管这种约定适用于热源，它并不适用于冷源，冷源通常在订阅时开始产生项目。[`Timer`](03_CreatingObservableSequences.md#observabletimer)返回的源按照定期计划产生项目，但该计划从订阅时刻开始。这意味着如果有多个订阅，就有多个计划。即使我只有一个由`Observable.Timer`返回的`IObservable<long>`，每个不同的订阅者也会按照其自己的时间表接收事件——订阅者接收事件的间隔是_从他们恰好订阅的时刻开始_。因此，对于冷可观察对象，通常有意义的是使用这第二张图表中的约定，我们正在查看订阅特定源的一个特定订阅者接收到的事件。

大多数时候我们可以忽略这种微妙之处，随意使用适合我们的任何约定。改编 [Humpty Dumpty 的话：当我使用弹珠图时，它的意思就是我选择它要表达的意思——不多也不少](https://www.goodreads.com/quotes/12608-when-i-use-a-word-humpty-dumpty-said-in-rather)。但当你将热源和冷源结合在一起时，可能没有一个明显的最佳方式来在弹珠图中表示这一点。我们甚至可以像这样做，描述`hot`代表的事件与特定订阅`hot`所看到的事件分开。

![这基本上组合了前面两个图表。它具有相同的第一个和最后一个序列。在这两者之间，它有一个标为'Events available from hot'的序列，展示了倒数第二个图表中的'hot'序列。然后它有一个标为'Concat subscription to hot'的序列，展示了前一个图的'hot'序列。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Hot-Marbles-SourceAndSub.svg)


我们在弹珠图中使用一个独特的'轨道'来表示特定订阅到源的事件。使用这种技术，我们还可以展示如果你将同一个冷源传递给`Concat`两次会发生什么：

![一张弹珠图展示了三个序列。第一个，标为'Concat subscription to cold'，产生了值0、1和2，然后完成。第二个，也被标为'Concat subscription to cold'，再次产生这些值，但定位显示这些是在第一个序列完成后产生的。最终的序列，标为'cold.Concat(cold)'，展示了值两次，即0、1、2、0、1和2。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Marbles-Cold-Twice.svg)

这突显了作为冷源的`cold`分别向每个订阅提供项目的事实。我们从同一个源看到相同的三个值，但在不同的时间出现。

#### 连接多个来源

如果你想要连接超过两个序列怎么办？`Concat`有一个接受多个可观察序列数组的重载。这是用`params`关键词注释的，因此你不需要显式构建数组。您可以传递任意数量的参数，C#编译器将生成创建数组的代码。还有一个接受`IEnumerable<IObservable<T>>`的重载，以防你想要连接的可观察对象已经在某个集合中。

```csharp
public static IObservable<TSource> Concat<TSource>(
    params IObservable<TSource>[] sources)

public static IObservable<TSource> Concat<TSource>(
    this IEnumerable<IObservable<TSource>> sources)
```

`IEnumerable<IObservable<T>>`重载会懒惰地评估`sources`。直到有人订阅`Concat`返回的可观察对象时，它才会开始请求源可观察对象，并且只有在当前源完成时才会再次调用`IEnumerator<IObservable<T>>`上的`MoveNext`，意味着它准备开始下一个。为了说明这一点，下面的示例是一个迭代器方法，返回一个序列的序列，并穿插了日志记录。它返回三个可观察序列，每个都有一个值[1]、[2]和[3]。每个序列在计时器延迟后返回其值。

```csharp
public IEnumerable<IObservable<long>> GetSequences()
{
    Console.WriteLine("GetSequences() called");
    Console.WriteLine("Yield 1st sequence");

    yield return Observable.Create<long>(o =>
    {
        Console.WriteLine("1st subscribed to");
        return Observable.Timer(TimeSpan.FromMilliseconds(500))
            .Select(i => 1L)
            .Finally(() => Console.WriteLine("1st finished"))
            .Subscribe(o);
    });

    Console.WriteLine("Yield 2nd sequence");

    yield return Observable.Create<long>(o =>
    {
        Console.WriteLine("2nd subscribed to");
        return Observable.Timer(TimeSpan.FromMilliseconds(300))
            .Select(i => 2L)
            .Finally(() => Console.WriteLine("2nd finished"))
            .Subscribe(o);
    });

    Thread.Sleep(1000); // 强制延迟

    Console.WriteLine("Yield 3rd sequence");

    yield return Observable.Create<long>(o =>
    {
        Console.WriteLine("3rd subscribed to");
        return Observable.Timer(TimeSpan.FromMilliseconds(100))
            .Select(i=>3L)
            .Finally(() => Console.WriteLine("3rd finished"))
            .Subscribe(o);
    });

    Console.WriteLine("GetSequences() complete");
}
```

我们可以调用这个`GetSequences`方法并将结果传递给`Concat`，然后使用我们的`Dump`扩展方法来观察发生了什么：

```csharp
GetSequences().Concat().Dump("Concat");
```

这里是输出：

```
GetSequences() called
Yield 1st sequence
1st subscribed to
Concat-->1
1st finished
Yield 2nd sequence
2nd subscribed to
Concat-->2
2nd finished
Yield 3rd sequence
3rd subscribed to
Concat-->3
3rd finished
GetSequences() complete
Concat completed
```

下面是应用于`GetSequences`方法的`Concat`操作符的弹珠图。's1'、's2'和's3'分别代表序列1、2和3。'rs'代表结果序列。

![一张弹珠图展示了4个序列。第一个s1稍作等待，然后生成单个值1，接着立即完成。第二个s2紧接在s1完成后开始。它等待稍短的时间间隔，生成值2，然后立即完成。第三个s3在s2完成一段时间后开始，等待更短的时间，生成值3，然后立即完成。最终的序列r与s1同时开始，1、2和3的值与这些值被前面的源生成的时刻完全相同，完成时与s3同时。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Concat-Marbles-Three.svg)

您应该注意到，一旦迭代器执行了第一次`yield return`以返回第一个序列后，迭代器不会继续执行，直到第一个序列完成。迭代器调用`Console.WriteLine`来显示文本`Yield 2nd sequence`紧跟在那第一个`yield return`之后，但你可以看到该消息直到我们看到`Concat-->1`消息显示Concat的第一次输出之后，并且也是`Finally`操作符产生的`1st finished`消息之后，这个操作符只在第一个序列完成后运行。（代码也使得第一个源延迟500毫秒才产生其值，因此如果你运行这个，你可以看到一切都因为第一个源产生其单一值然后完成而停了一下。）一旦第一个源完成，`GetSequences`方法继续（因为`Concat`会在第一个可观察源完成后询问它下一个项目）。当`GetSequences`用另一个`yield return`提供第二个序列时，`Concat`订阅了它，再次`GetSequences`在第二个可观察序列完成前不会有进一步的进展。当询问第三个序列时，迭代器自己会等待一秒钟才产生那第三个且最后一个值，你可以从`s2`结束和`s3`开始之间的间隔中看到。

### Prepend

有一个特殊场景是`Concat`支持的，但方式有点繁琐。有时候我们可能需要一个序列立即总是发出一些初始值。以本书中我经常使用的示例，船只传输AIS消息来报告其位置和其他信息：在某些应用中，你可能不想等到船只恰好传输消息。你可以想象一个应用，记录任何船只的最后已知位置。这将使应用有可能提供一个`IObservable<IVesselNavigation>`，它在订阅时立即报告最后已知的信息，如果船只产生任何新的消息，它还会继续提供。

我们应该如何实现这一点？我们希望最初像冷来源一样的行为，但过渡到热。因此我们可以只是连接两个来源。我们可以使用[`Observable.Return`](03_CreatingObservableSequences.md#observablereturn)创建一个单元素冷源，然后与实时流连接：

```csharp
IVesselNavigation lastKnown = ais.GetLastReportedNavigationForVessel(mmsi);
IObservable<IVesselNavigation> live = ais.GetNavigationMessagesForVessel(mmsi);

IObservable<IVesselNavigation> lastKnownThenLive = Observable.Concat(
    Observable.Return(lastKnown), live);
```

这是一个常见的需求，Rx提供了`Prepend`，它具有类似效果。我们可以将最后一行替换为：

```csharp
IObservable<IVesselNavigation> lastKnownThenLive = live.Prepend(lastKnown);
```

这个可观察对象将做完全相同的事情：订阅者会立即收到`lastKnown`，然后如果船只应该发出更多的导航消息，他们也会收到这些。顺便说一句，对于这种情况，你可能还想确保查找“最后已知”消息尽可能晚地发生。我们可以使用[`Defer`](03_CreatingObservableSequences.md#observabledefer)来延迟这一点直到订阅时：

```csharp
public static IObservable<IVesselNavigation> GetLastKnownAndSubsequenceNavigationForVessel(uint mmsi)
{
    return Observable.Defer<IVesselNavigation>(() =>
    {
        // 此lambda将在每次有人订阅时运行。
        IVesselNavigation lastKnown = ais.GetLastReportedNavigationForVessel(mmsi);
        IObservable<IVesselNavigation> live = ais.GetNavigationMessagesForVessel(mmsi);

        return live.Prepend(lastKnown);
    }
}
```

`StartWith` 可能会让你想起 [`BehaviorSubject<T>`](03_CreatingObservableSequences.md#behaviorsubjectt)，因为它同样确保消费者在订阅时立即收到一个值。它不完全相同：`BehaviorSubject<T>` 缓存其自身来源发出的最后一个值。你可能会认为这将是实现这个船舶导航示例的更好方式。然而，由于这个示例能够为任何船只返回一个来源（`mmsi` 参数是唯一标识船只的[海事移动服务身份](https://en.wikipedia.org/wiki/Maritime_Mobile_Service_Identity)），它将需要对你感兴趣的每一个船只保持一个 `BehaviorSubject<T>` 运行，这可能是不切实际的。

`BehaviorSubject<T>` 只能持有一个值，这对于这个 AIS 场景来说是可以的，而且 `Prepend` 也分享这个限制。但是如果你需要一个源以某个特定序列开始怎么办？

### StartWith

`StartWith`是`Prepend`的一个泛化版本，它允许我们提供任意数量的值，这些值会在订阅时立即发出。与`Prepend`一样，之后它将继续转发源产生的任何后续通知。

从其签名可以看出，这个方法接受一个`params`数组的值，因此你可以根据需要传入任意多或任意少的值：

```csharp
// 在一个可观察序列前加上一系列的值。
public static IObservable<TSource> StartWith<TSource>(
    this IObservable<TSource> source, 
    params TSource[] values)
```

还有一个重载接受`IEnumerable<T>`。注意，Rx将_不会_推迟对这个的枚举。`StartWith`会立即将`IEnumerable<T>`转换为数组后再返回。

`StartsWith`在LINQ中不是一个常见的操作符，它的存在是Rx特有的。如果你想象在LINQ to Objects中`StartsWith`的样子，它与[`Concat`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.concat)没有太大的不同。在Rx中有所不同，因为`StartsWith`有效地在_拉取_（pull）和_推送_（push）世界之间架起了一座桥梁。它有效地将我们提供的项转换成一个可观察对象，并将`source`参数连接到该对象上。

### Append

`Prepend` 的存在可能会导致你好奇是否有 `Append` 用于在任何 `IObservable<T>` 的末尾添加单个项。毕竟，这在 LINQ 中是一个常见的操作符；例如，[LINQ to Objects 有一个 `Append` 实现](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.append)。而且，Rx 确实提供了这样的功能：

```csharp
IObservable<string> oneMore = arguments.Append("And another thing...");
```

没有对应的 `EndWith`。并没有根本原因说明不能有这样的事物，只是显然需求不大——[Rx 仓库](https://github.com/dotnet/reactive) 还没有收到这样的功能请求。因此，尽管 `Prepend` 和 `Append` 的对称性确实表明可能存在类似于 `StartWith` 和未曾假设的 `EndWith` 之间的对称性，但这种对应部分的缺失似乎没有引起任何问题。能够创建总是立即产生有用输出的可观察来源具有明显的价值；`EndWith` 的用处不清晰，除了满足对称性的渴望之外。

### DefaultIfEmpty

我们接下来要检查的操作符并不严格执行顺序组合。然而，它与 `Append` 和 `Prepend` 非常相似。如同这些操作符，`DefaultIfEmpty` 会发出其来源的所有内容。与这些操作符一样，`DefaultIfEmpty` 接受一个额外的项目，不过它不会总是发出那个额外的项目。

与 `Prepend` 在开始时发出其额外项目和 `Append` 在结束时发出其额外项目不同，`DefaultIfEmpty` 仅在源完成时没有产生任何内容时发出额外的项目。因此，这提供了一种保证可观察对象不会为空的方法。

你无需为 `DefaultIfEmpty` 提供一个值。如果使用你未提供此类值的重载，它将仅使用 `default(T)`。这将是对于结构类型的零似值，对于引用类型则是 `null`。

### Repeat

最后一个顺序组合序列的操作符是 `Repeat`。它允许你简单地重复一个序列。它提供了你可以指定重复输入次数的重载，并且有一个无限重复的选项：

```csharp
// 重复指定次数的可观察序列。
public static IObservable<TSource> Repeat<TSource>(
    this IObservable<TSource> source, 
    int repeatCount)

// 无限期并顺序地重复可观察序列。
public static IObservable<TSource> Repeat<TSource>(
    this IObservable<TSource> source)
```

`Repeat` 会对于每个重复再次订阅源。这意味着这只会严格重复，如果源每次你订阅时都产生相同的项目。与 [`ReplaySubject<T>`](03_CreatingObservableSequences.md#replaysubjectt) 不同，这并不存储并重放源产生的项目。这意味着你通常不会想在热源上调用 `Repeat`。（如果你真的想要一个热源的输出重复，[`Replay`](15_PublishingOperators.md#replay) 和 `Repeat` 的组合可能适合。）

如果使用无限重复的重载，那么序列将会停止的唯一方式是发生错误或订阅被处理掉。指定重复次数的重载将在发生错误、取消订阅或达到该次数时停止。这个例子展示了序列 [0,1,2] 被重复三次。

```csharp
var source = Observable.Range(0, 3);
var result = source.Repeat(3);

result.Subscribe(
    Console.WriteLine,
    () => Console.WriteLine("Completed"));
```

输出：

```
0
1
2
0
1
2
0
1
2
Completed
```

## 并发序列

我们现在来看一下用于合并可能同时产生值的可观察序列的操作符。

### Amb

`Amb` 是一个有些奇怪的操作符名。它是 _ambiguous_ （模糊不清）的缩写，但这并没有比 `Amb` 本身透露出更多信息。如果你对这个名称感到好奇，可以阅读[关于 `Amb` 起源的附录 D](D_AlgebraicUnderpinnings.md#amb)，但现在，让我们看看它实际上是做什么的。
Rx 的 `Amb` 接受任意数量的 `IObservable<T>` 源作为输入，并等待看哪一个（如果有的话）首先产生某种输出。一旦发生这种情况，它会立即从所有其他源取消订阅，并转发第一个做出反应的源的所有通知。

这有什么用呢？

`Amb` 的一个常见用例是当你想要尽快得到某种结果，并且有多种获取该结果的选择，但你事先不知道哪一个会最快的时候。可能有多个服务器都能潜在地给你想要的答案，而预测哪个响应时间最短是不可能的。你可以向所有这些服务器发送请求，然后使用第一个响应的服务器的数据。如果你将每个单独的请求建模为自己的 `IObservable<T>`，`Amb` 可以为你处理这一切。请注意，这并不是很高效：你要求几个服务器做相同的工作，而你将丢弃其中大多数的结果。（因为一旦第一个源发生反应，`Amb` 就会取消订阅所有它不使用的源，有可能你可以向所有其他服务器发送取消请求的消息。但这仍然有些浪费。）但在追求及时性至关重要的场景中，为了更快的结果而容忍一些浪费的努力可能是值得的。

`Amb` 在很大程度上类似于 `Task.WhenAny`，因为它让你能够检测多个源中的首个做某事的源。然而，这种类比并不精确。`Amb` 会自动从所有其他源取消订阅，确保一切都被清理干净。而使用 `Task` 时，你应该始终确保最终观察到所有任务，以防其中任何一个出现故障。

为了说明 `Amb` 的行为，这里有一个示意图，展示了三个序列 `s1`、`s2` 和 `s3`，每个都能产生一系列值。标有 `r` 的线显示了将所有三个序列传入 `Amb` 的结果。如你所见，`r` 提供的通知与 `s1` 完全相同，因为在此示例中，`s1` 是第一个产生值的序列。

![一个示意图显示了4个序列。第一个，s1，产生值1、2、3和4。第二个，s2，与s1同时启动，但在s1产生1之后产生其第一个值99，以及在s1产生2之后产生其第二个值88，然后在s1产生2和3之间完成。第三个源，s3，在s2产生99之后和s1产生2之前产生其第一个值8，然后继续产生另外两个值7和6，与早期源的活动交错。最后一个序列，r，与s1完全相同。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Amb-Marbles.svg)

这段代码创建了刚刚在示意图中描述的情况，以验证这确实是 `Amb` 的行为：

```csharp
var s1 = new Subject<int>();
var s2 = new Subject<int>();
var s3 = new Subject<int>();

var result = Observable.Amb(s1, s2, s3);

result.Subscribe(
    Console.WriteLine,
    () => Console.WriteLine("Completed"));

s1.OnNext(1);
s2.OnNext(99);
s3.OnNext(8);
s1.OnNext(2);
s2.OnNext(88);
s3.OnNext(7);
s2.OnCompleted();
s1.OnNext(3);
s3.OnNext(6);
s1.OnNext(4);
s1.OnCompleted();
s3.OnCompleted();
```

输出：

```
1
2
3
4
Completed
```

如果我们更改顺序，使得 `s2.OnNext(99)` 在调用 `s1.OnNext(1)` 之前，那么s2将首先产生值，示意图将会如下所示。

![一个示意图显示了4个序列。第一个，s1，产生值1、2、3和4。第二个，s2，与s1同时启动，但在s1产生1之前产生其第一个值99（这是与前一个图表的主要区别），在s1产生2之后产生其第二个值88，并在s1产生2和3之间完成。第三个源，s3，在s2产生99之后和s1产生2之前产生其第一个值8，然后继续产生另外两个值7和6，与早期源的活动交错。最后一个序列，r，与s2完全相同（不同于前一个图表的s1）。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Amb-Marbles2.svg)

`Amb` 有几个重载。前面的例子使用了接受 `params` 数组序列的重载。还有一个重载接受恰好两个源，避免了使用 `params` 发生的数组分配。最后，你可以传入一个 `IEnumerable<IObservable<T>>`。（注意没有接受 `IObservable<IObservable<T>>` 的重载。`Amb` 要求提供所有的源可观察序列。）

```csharp
// 传播首个做出反应的可观察序列。
public static IObservable<TSource> Amb<TSource>(
    this IObservable<TSource> first, 
    IObservable<TSource> second)
{...}
public static IObservable<TSource> Amb<TSource>(
    params IObservable<TSource>[] sources)
{...}
public static IObservable<TSource> Amb<TSource>(
    this IEnumerable<IObservable<TSource>> sources)
{...}
```

重用 `Concat` 部分的 `GetSequences` 方法，我们可以看到 `Amb` 如何评估外部（IEnumerable）序列，然后再订阅它返回的任何序列。

```csharp
GetSequences().Amb().Dump("Amb");
```

输出：

```
GetSequences() called
Yield 1st sequence
Yield 2nd sequence
Yield 3rd sequence
GetSequences() complete
1st subscribed to
2nd subscribed to
3rd subscribed to
Amb-->3
Amb completed
```

这里是一个说明这段代码的行为的示意图：

![一个示意图展示了四个序列。前三个都在同一时间开始，但显著晚于第四个。第一个，s1，等了一会儿然后产生值1然后完成。第二个，s2，在s1产生其价值之前产生一个值2并立即完成。第三个，s3，在s1和s3完成后很久开始，稍微等一会儿，产生值3，然后立即完成。最后一个序列，r，从s1和s2开始时启动，当s3完成时结束，并且产生每一个其他三个源在同一时间产生的三个值，所以它显示2、1、然后是3。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Amb-Marbles3.svg)

记住，`GetSequences` 会在请求它们时立即产生它的前两个可观察序列，然后等待1秒钟再产生第三个和最后一个。但与 `Concat` 不同，`Amb` 不会订阅它的任何来源，直到它从迭代器中检索到所有的源为止，这就是为什么这个示意图显示所有三个源的订阅都是在1秒后开始的（前两个源更早可用 — `Amb` 会在订阅发生后立即开始枚举源，但它等到拥有全部三个之后才订阅，这就是为什么它们都出现在右边）。第三个序列在订阅后到产生其值之间的延迟最短，所以尽管它是最后返回的可观察序列，它还是能够最快地产生其值，即使有两个序列在一秒钟前就产生了（因为 `Thread.Sleep`）。

### Merge

`Merge` 扩展方法接受多个序列作为输入。任何时候任何输入序列产生一个值，`Merge` 返回的可观察序列也会产生相同的值。如果输入序列在不同线程上同时产生值，`Merge` 会安全地处理，确保一次只传递一个项目。

因为 `Merge` 返回一个包含所有输入序列的所有值的单个可观察序列，从这个意义上讲，它与 `Concat` 相似。但是，`Concat` 等待每个输入序列完成后再移动到下一个，`Merge` 支持同时活动的序列。只要你订阅由 `Merge` 返回的可观察序列，它就会立即订阅其所有输入，转发它们中的任何一个产生的所有内容。此示意图显示了两个并发运行的序列 `s1` 和 `s2`，其中 `r` 显示了用 `Merge` 合并这些的效果：两个源序列的值都从合并的序列中输出。

![一个示意图显示了三个序列。第一个，s1，连续三次产生值1，每个值之间有间隔。第二个，s2，也连续三次产生值2，并且与s1产生值的间隔相同，但稍微晚一点开始。第三个序列，c，包含s1和s2合并后的所有相同值，且与它们各自源序列产生这些值的时间相同。因此c产生1, 2, 1, 2, 1, 2。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Merge-Marbles.svg)

`Merge` 的结果只有在所有输入序列完成后才会完成。然而，如果任何输入序列错误地结束（此时它将取消订阅所有其他输入），`Merge` 运算符会报错。

如果你已经阅读了[创建可观察序列的章节](03_CreatingObservableSequences.md)，你已经看到了一个 `Merge` 的例子。我使用它将 `FileSystemWatcher` 提供的各种事件的单个序列在 ['Representing Filesystem Events in Rx'](03_CreatingObservableSequences.md#representing-filesystem-events-in-rx) 部分的末尾合并成一个流。另一个例子，让我们再次关注 AIS。目前没有公开可用的单一全球源能够提供全球范围内所有 AIS 消息作为 `IObservable<IAisMessage>`。任何单一源可能只覆盖一个区域，甚至可能只是一个单一的 AIS 接收器。使用 `Merge`，将这些合并成一个单一来源很简单：

```csharp
IObservable<IAisMessage> station1 = aisStations.GetMessagesFromStation("AdurStation");
IObservable<IAisMessage> station2 = aisStations.GetMessagesFromStation("EastbourneStation");

IObservable<IAisMessage> allMessages = station1.Merge(station2);
```

如果你想组合超过两个的源，你有几个选择：

- 将 `Merge` 操作符串连在一起，例如 `s1.Merge(s2).Merge(s3)`
- 传递一个 `params` 数组的序列给 `Observable.Merge` 静态方法，例如 `Observable.Merge(s1,s2,s3)`
- 将 `Merge` 操作符应用于 `IEnumerable<IObservable<T>>`。
- 将 `Merge` 操作符应用于 `IObservable<IObservable<T>>`。

重载看起来如下：

```csharp
// 将两个可观察序列合并成一个单一的可观察序列。
// 返回一个合并给定序列元素的序列。
public static IObservable<TSource> Merge<TSource>(
    this IObservable<TSource> first, 
    IObservable<TSource> second)
{...}

// 将所有可观察序列合并成一个单一的可观察序列。
// 合并可观察序列元素的可观察序列。
public static IObservable<TSource> Merge<TSource>(
    params IObservable<TSource>[] sources)
{...}

// 将一个可枚举的可观察序列序列合并成一个单一的可观察序列。
public static IObservable<TSource> Merge<TSource>(
    this IEnumerable<IObservable<TSource>> sources)
{...}

// 将一个可观察的可观察序列序列合并成一个可观察序列。
// 将所有内部序列中的元素合并进输出序列。
public static IObservable<TSource> Merge<TSource>(
    this IObservable<IObservable<TSource>> sources)
{...}
```

随着被合并的源数量增加，采用集合的操作符相对于第一个重载具有优势。（即，`s1.Merge(s2).Merge(s3)` 的性能略逊于 `Observable.Merge(new[] { s1, s2, s3 })` 或等效的 `Observable.Merge(s1, s2, s3)`。）然而，对于仅三四个源，差异很小，所以在实践中你可以根据个人风格偏好选择前两个重载中的任何一个。（如果你正在合并100个或更多的源，差异会更加明显，但到那时，你可能不会想使用链式调用风格。）第三和第四个重载允许你在运行时延迟评估合并序列。

使用序列的序列的最后一个 `Merge` 重载特别有趣，因为它使得被合并的源集可以随时间增加。只要你的代码继续订阅 `Merge` 返回的 `IObservable<T>`，`Merge` 就会持续订阅 `sources`。所以如果 `sources` 随时间发出越来越多的 `IObservable<T>`，这些都会被 `Merge` 包含进来。

这可能听起来很熟悉。[`SelectMany` 操作符](06_Transformation.md#selectmany)，它可以将多个可观察源展开回单个可观察源。这只是说明了为什么我将 `SelectMany` 描述为Rx中的一个基本操作符：严格来说，我们不需要Rx许多操作符，因为我们可以使用 `SelectMany` 构建它们。下面是使用 `SelectMany` 重新实现最后一个 `Merge` 重载的简单示例：

```csharp
public static IObservable<T> MyMerge<T>(this IObservable<IObservable<T>> sources) =>
    sources.SelectMany(source => source);
```

这也说明了我们技术上不需要 Rx 提供最后一个 `Merge`，这也是为什么它提供是有帮助的。一开始可能看不出这个做了什么。为什么我们要传递一个只返回其参数的 lambda 函数？除非你之前见过这个，否则可能需要花些时间来弄清楚 `SelectMany` 期望我们传递一个它为每个传入项调用的回调，但我们的输入项已经是嵌套序列，所以我们可以直接返回每个项，然后 `SelectMany` 会接着将其产生的所有内容合并到其输出流中。即使你已经完全内化了 `SelectMany` 以至于你立即知道这将简单地展平 `sources`，你可能仍然会发现 `Observable.Merge(sources)` 更直接地表达了意图。（同样，由于 `Merge` 是一个更专业的操作符，Rx 能够提供一个比上述 `SelectMany` 版本更有效的实现。）

如果我们再次重用 `GetSequences` 方法，我们可以看到 `Merge` 操作符如何与序列的序列一起工作。

```csharp
GetSequences().Merge().Dump("Merge");
```

输出：

```
GetSequences() called
Yield 1st sequence
1st subscribed to
Yield 2nd sequence
2nd subscribed to
Merge --> 2
Merge --> 1
Yield 3rd sequence
3rd subscribed to
GetSequences() complete
Merge --> 3
Merge completed
```

正如我们从示意图中看到的，s1 和 s2 被立即产生并订阅。s3 被延迟一秒产生，然后被订阅。一旦所有输入序列完成，结果序列完成。

![一个示意图显示了四个源。第一个，s1，等了一会儿然后产生值1并立即完成。第二个，s2，在s1同时开始，但立即产生单个值2 并完成，比s1早产生其值。第三个，s3，在s1和s2完成很久后开始，短暂等待后产生值3然后立即完成。最终源，r，当s1和s2开始时开始，当s3完成时完成，并产生每个其它三个源在相同时间产生的三个值，因此显示2、1、然后是3。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Merge-Marbles-Multi.svg)


对于接受变量数量源的每一个 `Merge` 重载（无论是通过数组、`IEnumerable<IObservable<T>>`，还是通过 `IObservable<IObservable<T>>`），都有一个额外的重载添加了一个 `maxConcurrent` 参数。例如：

```csharp
public static IObservable<TSource> Merge<TSource>(this IEnumerable<IObservable<TSource>> sources, int maxConcurrent)
```

这使你能够限制 `Merge` 在任何单一时间接受输入的源的数量。如果可用的源数量超过 `maxConcurrent`（无论是因为你传入了含有更多源的集合，还是因为你使用了基于 `IObservable<IObservable<T>>` 的重载并且源发出的嵌套源超过了 `maxConcurrent`），`Merge` 将等待现有源完成再移动到新的源。 `maxConcurrent` 为1时，`Merge` 的行为与 `Concat` 相同。

### Switch

Rx 的 `Switch` 操作符接受一个 `IObservable<IObservable<T>>`，并从最新的嵌套观察对象产生通知。每当其源产生新的嵌套 `IObservable<T>` 时，`Switch` 会取消订阅前一个嵌套源（除非这是第一个源，这种情况下不会有前一个源）并订阅最新的一个。

`Switch` 可以用于日历应用程序中的“是时候出发”类型功能。事实上，你可以看到 [Bing 如何提供（或至少曾经提供；实现可能已更改）通知，告诉你是时候出发去参加一个预约的源代码](https://github.com/reaqtive/reaqtor/blob/c3ae17f93ae57f3fb75a53f76e60ae69299a509e/Reaqtor/Samples/Remoting/Reaqtor.Remoting.Samples/DomainFeeds.cs#L33-L76)。由于那是一个真实的例子，它有点复杂，所以我会在这里仅描述其核心。

“是时候出发”通知的基本思想是，我们使用地图和路线查找服务来计算到达预约地点的预期行驶时间，并使用 [`Timer` 操作符](03_CreatingObservableSequences.md#observabletimer) 创建一个 `IObservable<T>`，它将在出发时产生通知。（具体来说，这段代码产生一个 `IObservable<TrafficInfo>`，它报告旅程的建议路线和预期行驶时间。）然而，有两件事可能会改变，使得最初预测的行程时间无效。首先，交通状况可能会改变。当用户创建他们的预约时，我们必须根据通常在相关时间段的通行情况来猜测预期行驶时间。然而，如果在当天交通状况非常糟糕，估计将需要向上修正，我们将需要更早地通知用户。

另一件可能变化的是用户的位置。这显然也会影响预测的行程时间。

为了处理这些，系统将需要可观察的源能报告用户位置的变化，以及影响建议旅程的交通条件变化。每次这些之一报告变化时，我们将需要产生一个新的估计行程时间和一个新的 `IObservable<TrafficInfo>`，该通知将在该时刻产生通知。

每次我们修正我们的估计时，我们想要放弃之前创建的 `IObservable<TrafficInfo>`。（否则，用户将收到大量混乱的通知，告诉他们出发，对应于我们每次重新计算行程时间。）我们只想使用最新的一个。而这正是 `Switch` 所做的。

你可以看到[该场景的示例在 Reaqtor 仓库中](https://github.com/reaqtive/reaqtor/blob/c3ae17f93ae57f3fb75a53f76e60ae69299a509e/Reaqtor/Samples/Remoting/Reaqtor.Remoting.Samples/DomainFeeds.cs#L33-L76)。在这里，我将呈现一个不同的、更简单的场景：实时搜索。当你输入时，文本被发送到搜索服务，结果作为一个可观察序列返回给你。大多数实现在发送请求之前都会稍作延迟，以防止不必要的操作。想象一下，我想搜索“Rx入门”。我很快输入了“Into to”，然后意识到我漏了字母‘r’。我稍停一下，将文本改为“Intro ”。到现在为止，已经发送了两次搜索请求到服务器。第一次搜索将返回我不想要的结果。此外，如果我收到的数据是第一次搜索和第二次搜索的结果合并在一起，对用户来说将是一种非常奇怪的体验。我真正只想要与最新的搜索文本相对应的结果。这种场景完美契合 `Switch` 方法。

在这个示例中，有一个 `IObservable<string>` 源，代表搜索文本——用户输入的每个新值都从这个源序列中产生。我们还有一个搜索函数，对给定的搜索词产生单个搜索结果：

```csharp
private IObservable<string> SearchResults(string query)
{
    ...
}
```

这只返回单个值，但我们将其建模为 `IObservable<string>`，一部分是处理搜索可能花费一些时间的事实，也是为了能够用Rx使用它。我们可以获取我们的搜索词源，然后使用 `Select` 将每个新的搜索值传递给这个 `SearchResults` 函数。这样就创建了我们的结果嵌套序列 `IObservable<IObservable<string>>`。

假设我们接着使用 `Merge` 来处理结果：

```csharp
IObservable<string> searchValues = ....;
IObservable<IObservable<string>> search = searchValues.Select(searchText => SearchResults(searchText));
                    
var subscription = search
    .Merge()
    .Subscribe(Console.WriteLine);
```

如果我们幸运的话，每个搜索在 `searchValues` 产生下一个元素之前完成，输出看起来会很合理。然而，很可能，多个搜索会导致重叠的搜索结果。这个示意图展示了 `Merge` 功能可能如何表现在这种情况下。

![一个示意图显示了6个源。第一个，searchValues，产生值I、In、Int和Intr，显示为图表表示的时间之外继续运行。第二个，'results (I)'，在 `searchValues` 产生其第一个值I时开始，然后稍后产生单个值Self，然后立即完成。值得注意的是，这个单个值是在searchValues源已经产生其第二个值In之后产出的。第三个源标记为 'results (In)'。它在searchValues产生其第二个值In时开始，稍后产生单个值Into，然后立即完成。值得注意的是，它在searchValues已经产生其第三个值Int之后产出其值。第四个源标记为 'results (Int)'。它在searchValues产生其第三个值Int时开始，稍后产生单个值42，然后立即完成。值得注意的是，在searchValues已经产生其第四个值Intr之后它才产生其值。第五个源标记为 'results (Intr)'。它在searchValues产生其第四个值Intr时开始，稍后产生单个值Start，然后立即完成。最终源标记为 'Merged results'。它与searchValues同时开始，并包含由第二、第三、第四和第五序列产生的每个项目。它未完成。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Switch-Marbles-Bad-Merge.svg)

注意搜索结果的值是如何混合在一起的。一些搜索词花费更长时间获取搜索结果也意味着它们的输出顺序错乱。这不是我们想要的。如果我们使用 `Switch` 扩展方法，我们将获得更好的结果。`Switch` 将订阅外部序列，并且当每个内部序列产生时，它将订阅新的内部序列并丢弃对之前内部序列的订阅。这将导致以下示意图：

![一个示意图显示了6个源。第一个，searchValues，产生值I、In、Int和Intr，显示为图表表示的时间之外继续运行。第二个，'results (I)'，在 `searchValues` 产生其第一个值I时开始，然后稍后产生单个值Self，然后立即完成。值得注意的是，这个单个值是在searchValues源已经产生其第二个值In之后产出的。第三个源标记为 'results (In)'。它在searchValues产生其第二个值In时开始，稍后产生单个值Into，然后立即完成。值得注意的是，它在searchValues已经产生其第三个值Int之后产出其值。第四个源标记为 'results (Int)'。它在searchValues产生其第三个值Int时开始，稍后产生单个值42，然后立即完成。值得注意的是，在searchValues已经产生其第四个值Intr之后它才产生其值。第五个源标记为 'results (Intr)'。它在searchValues产生其第四个值Intr时开始，稍后产生单个值Start，然后立即完成。最终源标记为 'Merged results'。它与searchValues同时开始，并且仅在 `results (Intr)` 源产生相同值的同时报告单个值Start。它未完成。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Switch-Marbles.svg)

现在，每次新的搜索词到达，引发新的搜索起始时，一个相应的新 `IObservable<string>` 为那次搜索结果出现，导致 `Switch` 从之前的结果中取消订阅。这意味着任何迟到的结果（即，当结果是针对一个不再在搜索框中的搜索词时）都会被丢弃。在这个特殊的例子中，这意味着我们只看到了最终搜索词的结果。用户输入时我们看到的所有中间值都没能停留很长时间，因为用户在我们收到前一个值的结果之前就继续按下了下一个键。只有在最后，当用户停止输入足够长的时间，让搜索结果回来之前它们没有过时，我们最终才看到 `Switch` 的一个值。总的效果是，我们消除了过时且令人困惑的结果。

这是另一个示意图，其中模糊的表述造成了一点问题。我展示了每次调用 `SearchResults` 产生的每个单值可观察对象，但实际上 `Switch` 在它们有机会产生值之前就取消了除最后一个之外的所有这些订阅。所以这个图表展示了这些源可能产生的值，而不是它们作为订阅的一部分实际交付的值，因为订阅被缩短了。

## 配对序列

之前的方法允许我们将共享相同类型的多个序列压平成同类型的结果序列（使用不同的策略来决定包含哪些元素以及舍弃哪些元素）。这一节中的操作符仍然接收多个序列作为输入，但尝试从每个序列中配对值，以生成输出序列的单个值。在某些情况下，它们还允许你提供不同类型的序列。

### Zip

`Zip` 操作符从两个序列中组合配对项。因此它的第一个输出是通过组合来自一个输入的第一个项与来自另一个输入的第一个项而创建的。第二个输出组合了来自每个输入的第二个项。依此类推。这个名称的意图是想象一下服装或包上的拉链，它一次将拉链的每半部分的齿配对在一起。

由于 `Zip` 以严格的顺序组合配对项，当序列中的第一个完成时，它将结束。如果某个序列已经到达其末端，即使另一个序列继续发出值，也没有与这些值配对的对象，因此 `Zip` 此时就会取消订阅，丢弃无法配对的值，并报告完成。

如果其中任一序列产生错误，`Zip` 返回的序列将报告相同的错误。

如果一个源序列的发布速度快于另一个序列，发布速率将由较慢的序列决定，因为它只有在每个来源都有一个项时才能发出一个项。

这里是一个例子：

```csharp
// 生成值 0,1,2 
var nums = Observable.Interval(TimeSpan.FromMilliseconds(250))
    .Take(3);

// 生成值 a,b,c,d,e,f 
var chars = Observable.Interval(TimeSpan.FromMilliseconds(150))
    .Take(6)
    .Select(i => Char.ConvertFromUtf32((int)i + 97));

// 将值通过 Zip 组合在一起
nums.Zip(chars, (lhs, rhs) => (lhs, rhs)))
    .Dump("Zip");
```

下面的弹珠图说明了这种效果：

![弹珠图显示三个序列。首个序列 s1 等待一会后产生值 0, 1 和 2，并在产生 2 后立即完成。第二个序列 s2 等待的时间较短，在 s1 产生 0 之前就产生值 a，然后在 s1 的 0 和 1 之间产生 b 和 c，在 s1 的 1 和 2 之间产生 d。大约在 s1 产生 2 的同时产生 e（在此示例中，这些是否在完全相同的时间发生，或是在彼此之前或之后并不重要），然后继续产生 f，随后立即完成。第三个序列 c 在 s1 产生 0 的同时显示值 '0,a'，在 s1 产生 1 时显示 '1,b'，并在 s1 产生 2 时显示 '2,c'，然后与 s1 同时完成。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Zip-Marbles.svg)

这是代码的实际输出：

```
{ Left = 0, Right = a }
{ Left = 1, Right = b }
{ Left = 2, Right = c }
```

需要注意的是，`nums` 序列在完成之前仅产生了三个值，而 `chars` 序列产生了六个值。结果序列产生了三个值，这是它所能组合的配对数量。

还值得注意的是，`Zip` 有第二个重载版本，它接受 `IEnumerable<T>` 作为第二个输入序列。

```csharp
// 将一个可观察序列和一个可枚举序列合并成一个可观察序列，
// 通过使用选择器函数逐对组合元素。
public static IObservable<TResult> Zip<TFirst, TSecond, TResult>(
    this IObservable<TFirst> first, 
    IEnumerable<TSecond> second, 
    Func<TFirst, TSecond, TResult> resultSelector)
{...}
```

这使得我们可以将 `IEnumerable<T>` 和 `IObservable<T>` 范式中的序列压缩在一起！

### SequenceEqual

另一个处理来自两个来源的项对的操作符是 `SequenceEqual`。但它与生成每对输入的输出不同，这会比较每对，并最终产生一个单一值，表明每对输入是否相等。

如果来源产生不同的值，`SequenceEqual` 会在检测到这种情况时立即产生一个 `false` 值。但如果来源相等，则只能在两者都完成时报告这一结果，因为在此之前，它还不知道之后是否会出现差异。这是一个示例，说明了它的行为：

```csharp
var subject1 = new Subject<int>();

subject1.Subscribe(
    i => Console.WriteLine($"subject1.OnNext({i})"),
    () => Console.WriteLine("subject1 completed"));

var subject2 = new Subject<int>();

subject2.Subscribe(
    i => Console.WriteLine($"subject2.OnNext({i})"),
    () => Console.WriteLine("subject2 completed"));

var areEqual = subject1.SequenceEqual(subject2);

areEqual.Subscribe(
    i => Console.WriteLine($"areEqual.OnNext({i})"),
    () => Console.WriteLine("areEqual completed"));

subject1.OnNext(1);
subject1.OnNext(2);

subject2.OnNext(1);
subject2.OnNext(2);
subject2.OnNext(3);

subject1.OnNext(3);

subject1.OnCompleted();
subject2.OnCompleted();
```

输出：

```
subject1.OnNext(1)
subject1.OnNext(2)
subject2.OnNext(1)
subject2.OnNext(2)
subject2.OnNext(3)
subject1.OnNext(3)
subject1 completed
subject2 completed
areEqual.OnNext(True)
areEqual completed
```

### CombineLatest

`CombineLatest` 操作符类似于 `Zip`，因为它从其来源中组合配对项。然而，不是配对第一个项，然后是第二个，依此类推，`CombineLatest` 在其输入中的任一输入产生新值时产生一个输出。对于从输入中浮现的每个新值，`CombineLatest` 使用它与另一个输入中最近看到的值。 (确切地说，在每个输入都产生了至少一个值之前，它不会产生任何东西，所以如果一个输入比另一个开始得慢，就会有一段时间 `CombineLatest` 实际上不会在其输入之一产生输出时就产生一个输出，因为它正在等待另一个产出其第一个值。) 其签名如下：

```csharp
// 通过使用选择器函数，将两个可观察序列合成为一个可观察序列，
// 每当其中一个可观察序列生成一个元素时。
public static IObservable<TResult> CombineLatest<TFirst, TSecond, TResult>(
    this IObservable<TFirst> first, 
    IObservable<TSecond> second, 
    Func<TFirst, TSecond, TResult> resultSelector)
{...}
```

下面的弹珠图显示了使用 `CombineLatest` 的情况，其中一个序列生成数字（`s1`），另一个生成字母（`s2`）。如果 `resultSelector` 函数仅将数字和字母组合成一对，这将产生底部线显示的结果。我已经为每个输出着色，以表示哪个来源导致它发出那个特定的结果，但如你所见，每个输出都包含了来自每个来源的一个值。

![弹珠图显示三个序列。第一个 s1, 等待一会然后产生值 1, 2, 和 3，时间间隔分布。第二个 s2, 与 s1 同时开始, 并且等待时间较短，在 s1 产生1 之前就生产了它的第一个值 a。然后在 s1 产生 2 后, s2 产生 b 然后是 c, 都在 s1 产生 3 之前。第三个序列 CombineLatest 在 s1 产生 1 时显示 '1,a'，在 s1 产生 2 时显示 `2,a`，当 s2 产生 b 时显示 `2,b`，然后是 `2,c` 按照 s2 产生 c，以及 `3,c` 和 s1 产生 3。所有三个序列在图中显示的时间内都没有结束。](GraphicsIntro/Ch09-CombiningSequences-Marbles-CombineLatest-Marbles.svg)

如果我们慢慢穿过上面的弹珠图，我们首先看到 `s2` 产生了字母 'a'。由于 `s1` 还没有产生任何值，因此没有可以配对的，意味着结果序列没有产生值。接下来，`s1` 产生数字 '1'，因此结果序列现在可以产生一对 '1,a'。然后我们收到来自 `s1` 的数字 '2'。上一个字母仍然是 'a'，因此下一对是 '2,a'。然后生成字母 'b'，创建了对 '2,b'，接着是 'c' 给出 '2,c'。最后产生数字 3，我们得到对 '3,c'。

这在你需要评估一些状态组合的情况下非常有用，这些状态需要在任何单个组件的状态发生变化时保持最新。一个简单的例子可能是监控系统。每个服务由一个序列表示，返回一个布尔值，指示该服务的可用性。如果所有服务都可用，则监控状态为绿色；我们可以通过让结果选择器执行逻辑与操作来实现这一点。
这里有一个例子：

```csharp
IObservable<bool> webServerStatus = GetWebStatus();
IObservable<bool> databaseStatus = GetDBStatus();

// 当两个系统都正常时返回 true。
var systemStatus = webServerStatus
    .CombineLatest(
        databaseStatus,
        (webStatus, dbStatus) => webStatus && dbStatus);
```

你可能已经注意到，这个方法可能会产生许多重复的值。例如，如果网络服务器发生故障，则结果序列将返回 '`false`'。如果数据库随后发生故障，另一个（不必要的）'`false`' 值将被生成。这将是使用 `DistinctUntilChanged` 扩展方法的恰当时机。修正后的代码如下例所示：

```csharp
// 当两个系统都正常且仅在状态变更时返回 true
var systemStatus = webServerStatus
    .CombineLatest(
        databaseStatus,
        (webStatus, dbStatus) => webStatus && dbStatus)
    .DistinctUntilChanged();
```

## 加入

`Join` 操作符允许你逻辑上联接两个序列。虽然 `Zip` 操作符会根据它们在序列中的位置将两个序列的值配对，但 `Join` 操作符允许你根据元素发出的时间来联接序列。

由于可观察源产生一个值在逻辑上是一个瞬时事件，联接使用相交窗口的模型。回想一下，使用 [`Window`](08_Partitioning.md#window) 操作符，你可以使用一个可观察序列定义每个窗口的持续时间。`Join` 操作符使用了类似的概念：对于每个源，我们可以定义一个时间窗口，在此时间窗口内每个元素被认为是“当前的”，如果它们的时间窗口重叠，则来自不同源的两个元素将被联接。与 `Zip` 操作符一样，我们还需要提供一个选择器函数，从每对值中生成结果项。这是 `Join` 操作符：

```csharp
public static IObservable<TResult> Join<TLeft, TRight, TLeftDuration, TRightDuration, TResult>
(
    this IObservable<TLeft> left,
    IObservable<TRight> right,
    Func<TLeft, IObservable<TLeftDuration>> leftDurationSelector,
    Func<TRight, IObservable<TRightDuration>> rightDurationSelector,
    Func<TLeft, TRight, TResult> resultSelector
)
```

这是一个复杂的签名，一次性理解可能很难，因此我们逐个参数来看。

`IObservable<TLeft> left` 是第一个源序列。`IObservable<TRight> right` 是第二个源序列。`Join` 旨在产生项对，每对包含一个来自 `left` 和一个来自 `right` 的元素。

`leftDurationSelector` 参数使我们能够为 `left` 中的每个项定义时间窗口。源项的时间窗口从源发出该项时开始。为了确定 `left` 的一个项的窗口何时应该关闭，`Join` 将调用 `leftDurationSelector`，传入 `left` 刚刚产生的值。这个选择器必须返回一个可观察的源。（这个源的元素类型完全不重要，因为 `Join` 只关心它何时做事。）该项的时间窗口一旦该项的 `leftDurationSelector` 返回的源产生一个值或完成后立即结束。

`rightDurationSelector` 参数定义了 `right` 的每个项的时间窗口。它的工作方式与 `leftDurationSelector` 完全相同。

最初，没有当前项。但随着 `left` 和 `right` 产生项，这些项的窗口将开始，所以 `Join` 可能会有多个项都有它们的窗口当前打开。每当 `left` 产生一个新的项时，`Join` 就会查看是否有来自 `right` 的任何项仍然有窗口打开。如果有，那么 `left` 现在与每个这样的项配对。（因此，一个源的单个项可能与另一个源的多个项联接。）`Join` 调用 `resultSelector` 为每一对这样的配对。同样，每当 `right` 产生一个项时，如果有来自 `left` 的任何项的窗口当前打开，那么来自 `right` 的新项将与这些每一个配对，并且再次，`resultSelector` 将为每一对这样的配对被调用。

`Join` 返回的可观察对象生成每次调用 `resultSelector` 的结果。

现在让我们想象一个场景，左序列的产值速度是右序列的两倍。想象此外我们从不关闭左窗口；我们可以通过始终从 `leftDurationSelector` 函数返回 `Observable.Never<Unit>()` 来实现这一点。想象我们使右窗口尽可能快地关闭，我们可以通过使 `rightDurationSelector` 返回 `Observable.Empty<Unit>()` 来实现这一点。下面的弹珠图说明了这一点：

![弹珠图显示五组序列。第一组，标记为 left，包含一个立即生成值 0 的单一序列，然后，在等间隔的时间，生成值 1, 2, 3, 4 和 5。第二组，标记为 'left durations'，显示了 left 产生的每个值的序列，每个都在 left 产生其中一个值的确切时刻开始。这些序列中没有任何一个产生任何值或完成。第三组标记为 right。它等到 left 产生了第二个值（1）之后才生成 A。在 left 的 3 和 4 之间，它产生 B。在 left 的 5 之后，它产生 C。下一组标记为 'right durations'，显示了三个序列，每个序列都在 right 产生其值的时刻开始，并且立即结束——这些实际上是瞬时短序列。最后一组，Join，显示了一个单一序列。当 right 产生 A 时，它立即产生 '0,A' 然后是 '1,A'。当 right 产生 B 时，它产生 '0,B', '1,B', '2,B', 和 `3,B`。然后 right 产生 C，Join 序列产生 '0,C', '1,C', '2,C', '3,C', '4,C', '5,C'。该图恰巧显示每组值垂直堆叠，但这只是因为它们产生得如此迅速，否则我们将无法看到它们。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Join-Marbles1.svg)

每次左持续时间窗口与右持续时间窗口相交时，我们就会得到一个输出。右持续时间窗口实际上都是零长度，但这并不妨碍它们与始终不结束的左持续时间窗口相交。因此，来自右的第一个项的（零长度）窗口落在两个 `left` 项的窗口内，因此 `Join` 产生了两个结果。我在图中将它们垂直堆叠以显示它们几乎是在同一时间发生的。当然，`IObserver<T>` 的规则意味着它们实际上不能同时发生：`Join` 必须等待消费者的 `OnNext` 处理完 `0,A` 才能继续产生 `1,A`。但只要一个源的单一事件与另一个源的多个窗口重叠，它就会尽可能快地产生所有配对。

如果我还通过返回 `Observable.Empty<Unit>` 或可能 `Observable.Return(0)` 立即关闭左窗口，窗口将永远不会重叠，因此永远不会产生任何配对。（理论上如果左右两者同时产生项，那么也许我们可能得到一对，但由于事件的时间从来不是绝对精确的，依赖于此设计系统将是一个坏主意。）

如果我想确保来自 `right` 的项只与 `left` 的单个值相交怎么办？在这种情况下，我需要确保左持续时间不重叠。一种方法是让我的 `leftDurationSelector` 始终返回我作为 `left` 序列传入的同一序列。这将导致 `Join` 对同一源进行多次订阅，对于某些类型的源来说可能会引入不希望的副作用，但 [`Publish`](15_PublishingOperators.md#publish) 和 [`RefCount`](15_PublishingOperators.md#refcount) 操作符提供了处理这种情况的方法，所以这实际上是一个合理的策略。如果我们这样做，结果看起来更像这样。

![弹珠图显示五组序列。第一组，标记为 left，包含一个立即产生值 0 的单一序列，然后，在等间隔的时间，产生值 1, 2, 3, 4 和 5。第二组，标记为 'left durations'，显示了 left 产生的每个值的序列，每个都在 left 产生其中一个值的确切时刻开始，并在下一个值开始时结束。第三组标记为 right。它等到 left 产生了第二个值（1）之后才生成 A。在 left 的 3 和 4 之间，它产生 B。在 left 的 5 之后，它产生 C。下一组标记为 'right durations'，显示了三个序列，每个序列都在 right 产生其值的时刻开始，并且立即结束——这些实际上是瞬时短序列。最后一组，Join，显示了一个单一序列。当 right 产生 A 时，它立即产生 '1,A'。当 right 产生 B 时，它产生 `3,B`。然后 right 产生 C，Join 序列产生 '5,C'。](GraphicsIntro/Ch09-CombiningSequences-Marbles-Join-Marbles2.svg)

最后一个例子与 [`CombineLatest`](12_CombiningSequences.md#CombineLatest) 非常相似，除了它只在右序列变化时产生一对。我们可以通过改变右持续时间的工作方式来使其与左持续时间的工作方式相同，从而轻松地使其工作相同。以下代码展示了这一点（包括使用 `Publish` 和 `RefCount` 以确保我们只对底层的 `left` 和 `right` 源进行一次订阅，尽管多次将它们提供给 `Join`）。

```csharp
public static IObservable<TResult> MyCombineLatest<TLeft, TRight, TResult>
(
    IObservable<TLeft> left,
    IObservable<TRight> right,
    Func<TLeft, TRight, TResult> resultSelector
)
{
    var refcountedLeft = left.Publish().RefCount();
    var refcountedRight = right.Publish().RefCount();

    return Observable.Join(
        refcountedLeft,
        refcountedRight,
        value => refcountedLeft,
        value => refcountedRight,
        resultSelector);
}
```

显然没有必要写这个——你可以直接使用内置的 `CombineLatest`。（而且这会稍微高效一些，因为它有一个专门的实现。）但它显示了 `Join` 是一个强大的操作符。

## GroupJoin

当 `Join` 操作符将时间窗口重叠的值配对时，它会将左右的标量值传递给 `resultSelector`。`GroupJoin` 操作符基于相同的重叠窗口概念，但其选择器的工作方式略有不同：`GroupJoin` 仍然从左侧源传递单个（标量）值，但它传递一个 `IObservable<TRight>` 作为第二个参数。这个参数表示在特定左值的窗口内发生的右序列中的所有值。

因此，这缺乏 `Join` 的对称性，因为左右源被不同地处理。对于 `left` 源产生的每个项目，`GroupJoin` 将精确地调用一次 `resultSelector`。当一个左值的窗口与多个右值的窗口重叠时，`Group` 会通过每次配对调用一次选择器来处理，但 `GroupJoin` 通过让作为 `resultSelector` 第二个参数传递的可观察对象发出与该左项重叠的每个右项来处理。 （如果一个左项与右侧的任何事物都不重叠，`resultSelector` 仍将在该项被调用时被调用，它只会传递一个不产生任何项的 `IObservable<TRight>`。）

`GroupJoin` 的签名与 `Join` 非常相似，但注意 `resultSelector` 参数中的区别。

```csharp
public static IObservable<TResult> GroupJoin<TLeft, TRight, TLeftDuration, TRightDuration, TResult>
(
    this IObservable<TLeft> left,
    IObservable<TRight> right,
    Func<TLeft, IObservable<TLeftDuration>> leftDurationSelector,
    Func<TRight, IObservable<TRightDuration>> rightDurationSelector,
    Func<TLeft, IObservable<TRight>, TResult> resultSelector
)
```

如果我们回到我们第一个 `Join` 示例，我们有：

* `left` 的产值是 `right` 的两倍快，
* 左侧永不过期，
* 右侧立即过期。

下图展示了这些相同输入，还展示了 `GroupJoin` 为 `left` 生成的每个项目传递给 `resultSelector` 的可观测对象：

![一个弹珠图，显示五组序列。第一组，标记为左侧，包含一个立即产生值 0 的序列，然后在均匀间隔时间产生值 1、2、3、4 和 5。第二组，标记为'左侧持续时间'，显示左侧产生每个值的序列，每个序列正好在左侧产生一个值的时刻开始。这些序列不产生任何值，也不完成。第三组标记为右侧。在左侧产生第二个值（1）之后，它产生 A。在左侧的 3 和 4 之间，它产生 B。在左侧的 5 之后，它产生 C。第四组标记为 '右侧持续时间'，它显示三个序列，每个序列都在右侧产生一个值的时间开始，并立即结束——这些实际上是瞬时短序列。最后一组包含 6 个序列，每个序列都有一个形式为 `为 0 选择通过的右侧可观察对象` 的标签，标签末尾的数字对于每个序列都会改变（所以 "...为 1 "然后是 "...为 2 "等等）。最后一组中的每个序列都与左侧序列的相应值同时开始。前两个同时显示 A、B 和 C 与右侧序列产生这些值的时间。接下来的两个在右侧产生 A 之后开始，所以他们只显示 B 和 C。最后两个在右侧产生 B 之后开始，所以他们只显示 C.](GraphicsIntro/Ch09-CombiningSequences-Marbles-GroupJoin-Marbles.svg)

这生成了与 `Join` 生成的所有相同事件相对应的事件，它们只是分布在六个不同的 `IObservable<TRight>` 源上。您可能已经想到，通过这种方式，

```csharp
public IObservable<TResult> MyJoin<TLeft, TRight, TLeftDuration, TRightDuration, TResult>(
    IObservable<TLeft> left,
    IObservable<TRight> right,
    Func<TLeft, IObservable<TLeftDuration>> leftDurationSelector,
    Func<TRight, IObservable<TRightDuration>> rightDurationSelector,
    Func<TLeft, TRight, TResult> resultSelector)
{
    return Observable.GroupJoin
    (
        left,
        right,
        leftDurationSelector,
        rightDurationSelector,
        (leftValue, rightValues) => 
          rightValues.Select(rightValue=>resultSelector(leftValue, rightValue))
    )
    .Merge();
}
```

您甚至可以用这段代码创建一个粗略的 `Window` 版本：

```csharp
public IObservable<IObservable<T>> MyWindow<T>(IObservable<T> source, TimeSpan windowPeriod)
{
    return Observable.Create<IObservable<T>>(o =>;
    {
        var sharedSource = source
            .Publish()
            .RefCount();

        var intervals = Observable.Return(0L)
            .Concat(Observable.Interval(windowPeriod))
            .TakeUntil(sharedSource.TakeLast(1))
            .Publish()
            .RefCount();

        return intervals.GroupJoin(
                sharedSource, 
                _ => intervals, 
                _ => Observable.Empty<Unit>(), 
                (left, sourceValues) => sourceValues)
            .Subscribe(o);
    });
}
```

Rx 通过允许您询问动态数据序列提供了另一种查询数据的方式。这使您能够解决在多个来源进行匹配时管理状态和并发性的固有复杂问题。通过封装这些低级操作，您可以利用 Rx 以富有表现力和可测试的方式设计软件。使用 Rx 运算符作为构建块，您的代码有效地成为许多简单操作符的组合。这允许领域代码的复杂性成为焦点，而不是其他附带的支持代码。

### And-Then-When

`Zip` 只能接受两个序列作为输入。如果这是一个问题，那么您可以使用三个方法 `And`/`Then`/`When` 的组合。这三种方法与大多数其他 Rx 方法略有不同的使用方式。在这三个方法中，`And` 是 `IObservable<T>` 的唯一扩展方法。与大多数 Rx 操作符不同，它不返回序列；相反，它返回神秘的类型 `Pattern<T1, T2>`。`Pattern<T1, T2>` 类型是公开的（显然），但它的所有属性都是内部的。您能够做的仅有两件（有用的）事情是调用其 `And` 或 `Then` 方法。在 `Pattern<T1, T2>` 上调用 `And` 方法会返回一个 `Pattern<T1, T2, T3>`。在该类型上，您也会找到 `And` 和 `Then` 方法。通用 `Pattern` 类型存在的目的是允许您将多个 `And` 方法链接在一起，每一个都通过一个扩展通用类型参数列表。然后您用 `Then` 方法的重载将它们全部整合在一起。`Then` 方法返回一个 `Plan` 类型。最后，您将这个 `Plan` 传递给 `Observable.When` 方法以创建您的序列。

这听起来可能非常复杂，但比较一些代码样本应该会使其更易于理解。它还将使您能够看到哪种风格您更喜欢使用。

要将三个序列打包在一起，您可以像这样使用链式的 `Zip` 方法：

```csharp
IObservable<long> one = Observable.Interval(TimeSpan.FromSeconds(1)).Take(5);
IObservable<long> two = Observable.Interval(TimeSpan.FromMilliseconds(250)).Take(10);
IObservable<long> three = Observable.Interval(TimeSpan.FromMilliseconds(150)).Take(14);

// lhs 代表 '左侧'
// rhs 代表 '右侧'
IObservable<(long One, long Two, long Three)> zippedSequence = one
    .Zip(two, (lhs, rhs) => (One: lhs, Two: rhs))
    .Zip(three, (lhs, rhs) => (lhs.One, lhs.Two, Three: rhs));

zippedSequence.Subscribe(
    v => Console.WriteLine($"One: {v.One}, Two: {v.Two}, Three: {v.Three}"),
    () => Console.WriteLine("Completed"));
```

或者使用更好的语法 `And`/`Then`/`When`：

```csharp
Pattern<long, long, long> pattern =
    one.And(two).And(three);
Plan<(long One, long Two, long Three)> plan =
    pattern.Then((first, second, third) => (One: first, Two: second, Three: third));
IObservable<(long One, long Two, long Three)> zippedSequence = Observable.When(plan);

zippedSequence.Subscribe(
    v => Console.WriteLine($"One: {v.One}, Two: {v.Two}, Three: {v.Three}"),
    () => Console.WriteLine("Completed"));
```

如果您愿意，这还可以进一步简化：

```csharp
IObservable<(long One, long Two, long Three)> zippedSequence = Observable.When(
    one.And(two).And(three)
        .Then((first, second, third) =>
            (One: first, Two: second, Three: third))
    );  

zippedSequence.Subscribe(
    v => Console.WriteLine($"One: {v.One}, Two: {v.Two}, Three: {v.Three}"),
    () => Console.WriteLine("Completed"));
```

`And`/`Then`/`When` 三重奏有更多重载，使您能够组合更多序列。他们还允许您提供不止一个“计划”（`Then` 方法的输出）。这为您提供了 `Merge` 功能，但在“计划”的集合上。如果这个功能对您来说很有趣，我建议您尝试使用它们。列举这些方法的所有组合的冗长将是低价值的。您将通过使用它们并自行发现获得更多价值。

## 总结

本章介绍了一组允许我们组合可观察序列的方法。这标志着第二部分的结束。我们已经查看了主要关注于定义我们希望对数据执行的计算的操作符。在第三部分中，我们将转向实际问题，例如管理调度、副作用和错误处理。
