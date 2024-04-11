# 关键类型

Rx 是一个强大的框架，可以大大简化响应事件的代码。但是要编写良好的反应式代码，你必须理解基本概念。Rx 的基本构建块是一个名为 `IObservable<T>` 的接口。理解这一点及其对应的 `IObserver<T>` 是使用 Rx 成功的关键。

前一章展示了这个 LINQ 查询表达式作为第一个示例：

```csharp
var bigTrades =
    from trade in trades
    where trade.Volume > 1_000_000;
```

大多数 .NET 开发者至少会熟悉 [LINQ](https://learn.microsoft.com/en-us/dotnet/csharp/linq/) 的多种流行形式，例如 [LINQ to Objects](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/linq-to-objects) 或 [Entity Framework Core 查询](https://learn.microsoft.com/en-us/ef/core/querying/)。大多数 LINQ 实现允许你查询 _静态数据_。LINQ to Objects 作用于数组或其他集合，Entity Framework Core 中的 LINQ 查询针对数据库中的数据，但 Rx 不同：它提供了在实时事件流上定义查询的能力，这可以称为 _动态数据_。

如果你不喜欢查询表达式语法，你可以通过直接调用 LINQ 操作符来编写完全等效的代码：

```csharp
var bigTrades = trades.Where(trade => trade.Volume > 1_000_000);
```

无论我们使用哪种风格，这都是 LINQ 表示我们希望 `bigTrades` 只包含 `trades` 中 `Volume` 属性大于一百万的项的方式。

我们无法确切知道这些示例的作用，因为我们看不到 `trades` 或 `bigTrades` 变量的类型。这些代码的含义将根据这些类型的不同而有很大差异。如果我们使用的是 LINQ to objects，这些都可能是 `IEnumerable<Trade>`。这将意味着这些变量都指代表示我们可以用 `foreach` 循环枚举内容的集合的对象。这将代表 _静态数据_，我们的代码可以直接检查这些数据。

但让我们通过明确类型来清楚地说明代码的意义：

```csharp
IObservable<Trade> bigTrades = trades.Where(trade => trade.Volume > 1_000_000);
```

这消除了歧义。现在很清楚我们不是在处理静态数据。我们正在处理一个 `IObservable<Trade>`。但究竟是什么呢？

## `IObservable<T>`
    
[`IObservable<T>` 接口](https://learn.microsoft.com/en-us/dotnet/api/system.iobservable-1) 代表 Rx 的基本抽象：某种类型 `T` 的值序列。从非常抽象的意义上说，这意味着它代表与 [`IEnumerable<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) 相同的事物。

区别在于代码如何消费这些值。`IEnumerable<T>` 使代码能够检索值（通常使用 `foreach` 循环），而 `IObservable<T>` 提供值可用时的值。这种区别有时被描述为 _推送_ 与 _拉取_。我们可以通过执行 `foreach` 循环来从 `IEnumerable<T>` 中 _拉取_ 值，但 `IObservable<T>` 将 _推送_ 值进入我们的代码。

`IObservable<T>` 如何将其值推送到我们的代码中呢？如果我们想要这些值，我们的代码必须 _订阅_ `IObservable<T>`，这意味着提供一些它可以调用的方法。事实上，订阅是 `IObservable<T>` 直接支持的唯一操作。以下是接口的整个定义：

```csharp
public interface IObservable<out T>
{
    IDisposable Subscribe(IObserver<T> observer);
}
```

你可以在 GitHub 上查看 [`IObservable<T>` 的源代码](https://github.com/dotnet/runtime/blob/b4008aefaf8e3b262fbb764070ea1dd1abe7d97c/src/libraries/System.Private.CoreLib/src/System/IObservable.cs)。注意它是 .NET 运行时库的一部分，而不是 `System.Reactive` NuGet 包的一部分。`IObservable<T>` 代表如此基本的重要抽象，以至于它被内置于 .NET 中。（所以你可能想知道 `System.Reactive` NuGet 包是做什么的。.NET 运行时库只定义了 `IObservable<T>` 和 `IObserver<T>` 接口，而没有定义 LINQ 实现。`System.Reactive` NuGet 包为我们提供了 LINQ 支持，并且还处理了线程问题。）

这个接口的唯一方法清楚地表明了我们可以通过 `IObservable<T>` 做些什么：如果我们想要接收它提供的事件，我们可以 _订阅_ 它。（我们也可以取消订阅：`Subscribe` 方法返回一个 `IDisposable`，如果我们调用它的 `Dispose`，它会取消我们的订阅。）`Subscribe` 方法要求我们传入 `IObserver<T>` 的实现，我们将很快介绍这一点。

细心的读者可能已经注意到，前一章中的一个示例看起来似乎是行不通的。该代码创建了一个每秒产生事件的 `IObservable<long>`，然后用以下代码订阅它：

```csharp
ticks.Subscribe(
    tick => Console.WriteLine($"Tick {tick}"));
```

这是传递一个委托，而不是 `IObservable<T>.Subscribe` 所需的 `IObserver<T>`。我们将很快讨论 `IObserver<T>`，但这里发生的情况是该示例使用了来自 `System.Reactive` NuGet 包的扩展方法：

```csharp
// 来自 System.Reactive 库的 ObservableExtensions 类
public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext)
```

这是一个辅助方法，它将委托包装在 `IObserver<T>` 的实现中，然后将其传递给 `IObservable<T>.Subscribe`。其效果是我们可以只编写一个简单的方法（而不是完整的 `IObserver<T>` 实现），并且当可观察源希望提供值时，它将调用我们的回调。使用这种类型的辅助方法比我们自己实现 Rx 的接口更常见。

### 热源和冷源

由于 `IObservable<T>` 在我们订阅之前不能向我们提供值，因此我们订阅的时间可能很重要。设想一个描述某些市场中发生的交易的 `IObservable<Trade>`。如果它提供的信息是实时的，它不会告诉你在你订阅之前发生的任何交易。在 Rx 中，这种类型的源被描述为 _热源_。

并非所有源都是 _热源_。没有什么能阻止一个 `IObservable<T>` 始终向任何订阅者提供完全相同的事件序列，无论何时调用 `Subscribe`。（想象一个 `IObservable<Trade>`，它不是报告实时信息，而是根据记录的历史交易数据生成通知。）无论何时你订阅，情况都无关紧要的源被称为 _冷源_。

以下是一些可能表示为热观察对象的源：

* 传感器的测量值
* 交易交换的价格变动
* 即时分发事件的事件源，如 Azure Event Grid
* 鼠标移动
* 定时器事件
* 像 ESB 通道或 UDP 网络数据包这样的广播

一些可能成为良好冷观察对象的源的示例：

* 集合的内容（例如由 `IEnumerable<T>` 返回的 [`ToObservable` 扩展方法](03_CreatingObservableSequences.md#from-ienumerablet)）
* 固定范围的值，例如 [`Observable.Range`](03_CreatingObservableSequences.md#observablerange) 产生的
* 基于算法生成的事件，例如 [`Observable.Generate`](03_CreatingObservableSequences.md#observablegenerate) 产生的
* 异步操作的工厂，例如 [`FromAsync`](03_CreatingObservableSequences.md#from-task) 返回的
* 通过运行常规代码（如循环）生成的事件；你可以使用 [`Observable.Create`](03_CreatingObservableSequences.md#observablecreate) 创建此类源
* 一个流事件提供者，如 Azure Event Hub 或 Kafka（或任何其他保留过去的事件以便能够从流中的特定时刻交付事件的流式源；因此_不_是 Azure Event Grid 风格的事件源）

并不是所有的源都严格完全是 _热_ 或 _冷_。例如，你可以想象一个对实时 `IObservable<Trade>` 的轻微变化，其中源总是向新订阅者报告最近的交易。订阅者可以立即收到一些东西，并将随着新信息的到来而保持最新。这种情况下新订阅者始终会收到（可能相当旧的）信息是一个 _冷_ 的特征，但只有那第一个事件是 _冷_ 的。它仍然可能是一个全新的订阅者错过了很多早期订阅者可以获得的信息，使得这个源比 _冷_ 更 _热_。

有一个有趣的特例是，一个事件源被设计为能够使应用程序以确切的顺序接收每一个事件一次。事件流系统，如 Kafka 或 Azure Event Hub 具有这种特性——它们会保留事件一段时间，以确保消费者即使偶尔落后也不会错过。进程的标准输入（_stdin_）也具有这种特性：如果你运行一个命令行工具并在它准备处理输入之前开始输入，操作系统会将该输入保留在缓冲区中，以确保不会丢失任何内容。Windows 对桌面应用程序也有类似的做法：每个应用程序线程都有一个消息队列，所以如果你在应用程序无法响应时点击或输入，输入最终将被处理。我们可能会认为这些源是 _冷_ - 然后 - _热_。它们像 _冷_ 源一样，因为我们不会错过任何东西，仅仅因为我们花了一些时间才开始接收事件，但一旦我们开始检索数据，我们通常无法回到开始。所以一旦我们启动并运行，它们更像是 _热_ 事件。

这类 _冷_ - 然后 - _热_ 的源可能会在我们想要附加多个订阅者时出现问题。如果源在订阅发生后立即开始提供事件，那么对于第一个订阅者来说没问题：它会收到在等待我们开始时积累的任何事件。但是如果我们想要附加多个订阅者，我们就会遇到问题：第一个订阅者可能会收到一些挂起的缓冲区中等待的所有通知，而我们设法附加的第二个订阅者将错过。

在这些情况下，我们真正想要的是在开始之前设置好所有的订阅者。我们希望订阅与开始的行为是分开的。通常情况下，订阅一个源意味着我们希望它开始，但 Rx 定义了一个专门的接口可以给我们更多控制：[`IConnectableObservable<T>`](https://github.com/dotnet/reactive/blob/f4f727cf413c5ea7a704cdd4cd9b4a3371105fa8/Rx.NET/Source/src/System.Reactive/Subjects/IConnectableObservable.cs)。它派生自 `IObservable<T>`，只增加了一个方法，`Connect`：

```csharp
public interface IConnectableObservable<out T> : IObservable<T>
{
    IDisposable Connect();
}
```

在这些场景中，将有一些过程来获取或生成事件，并且我们需要确保在这一切开始之前我们已经准备好了。因为 `IConnectableObservable<T>` 在你调用 `Connect` 之前不会开始，所以它为你提供了一种方式，在事件开始流动之前附加任意多的订阅者。

源的“温度”并不一定从它的类型中显而易见。即使底层源是 `IConnectableObservable<T>`，这通常也可能隐藏在代码层之后。所以无论一个源是热的，冷的还是介于之间的某种状态，大多数时候我们只看到一个 `IObservable<T>`。由于 `IObservable<T>` 只定义了一个方法 `Subscribe`，你可能会想知道我们如何用它做一些有趣的事情。力量来自 `System.Reactive` NuGet 库提供的 LINQ 操作符。

### LINQ 操作符和组合

到目前为止，我只展示了一个非常简单的 LINQ 示例，使用 `Where` 操作符过滤符合某些条件的事件。为了让您了解如何通过组合构建更先进的功能，我将介绍一个示例场景。

假设您想编写一个程序，监视文件系统上的某个文件夹，并在该文件夹中的任何内容发生变化时执行自动处理。例如，网站开发人员通常希望在编辑器中保存更改时触发客户端代码的自动重建，以便他们可以迅速看到更改的效果。文件系统更改通常成批出现。文本编辑器在保存文件时可能执行几个不同的操作。（有些在保存修改时保存到新文件，然后在这个过程完成后执行几次重命名，因为这样可以避免在保存文件的那一刻发生电源故障或系统崩溃时数据丢失。）所以你通常不会希望在检测到文件活动后立即采取行动。最好是等一会儿看看是否还有更多的活动发生，只有在一切都平息后才采取行动。

所以我们不应该直接对文件系统活动作出反应。我们希望在一阵活动后的某个时刻采取行动。Rx 并没有直接提供这项功能，但我们可以通过结合一些内置的操作符来创建一个自定义操作符。以下代码定义了一个检测并报告此类情况的 Rx 操作符。如果你是 Rx 的新手（如果你在阅读这本书，这似乎很有可能），它的工作方式可能一下子不太明显。这比我迄今为止展示的示例复杂得多，因为这是来自一个真实应用程序的示例。但我将一步一步地向你解释，所以一切都会变得清晰。

```csharp
static class RxExt
{
    public static IObservable<IList<T>> Quiescent<T>(
        this IObservable<T> src,
        TimeSpan minimumInactivityPeriod,
        IScheduler scheduler)
    {
        IObservable<int> onoffs =
            from _ in src
            from delta in 
               Observable.Return(1, scheduler)
                         .Concat(Observable.Return(-1, scheduler)
                                           .Delay(minimumInactivityPeriod, scheduler))
            select delta;
        IObservable<int> outstanding = onoffs.Scan(0, (total, delta) => total + delta);
        IObservable<int> zeroCrossings = outstanding.Where(total => total == 0);
        return src.Buffer(zeroCrossings);
    }
}
```

关于这方面要说的第一件事是我们实际上是在定义一个自定义的 LINQ 风格操作符：这是一个扩展方法，就像 Rx 提供的所有 LINQ 操作符一样，它接受一个 `IObservable<T>` 作为其隐式参数，并产生另一个可观察源作为其结果。返回类型略有不同：它是 `IObservable<IList<T>>`。这是因为一旦我们回到不活跃状态，我们将希望处理刚刚发生的所有事情，所以这个操作符将产生一个包含源在最近一次活动中报告的所有值的列表。

当我们想展示一个 Rx 操作符的行为时，我们通常会绘制一个“弹珠”图。这是一个图表，显示一个或多个 `IObservable<T>` 事件源，每个源都由一条水平线表示。源产生的每个事件都用一圈（或“弹珠”）在该行上表示，水平位置表示时间。通常，该行的左侧有一个垂直条，表示应用程序订阅源的瞬间，除非它立即产生事件，否则将从一个弹珠开始。如果该行的右侧有一个箭头，这表明可观察对象的生命周期超出了图表。这里有一个图表，显示了上面的 `Quiescent` 操作符对某个输入的响应：

![一个 Rx 弹珠图，说明了两个可观察对象。第一个标记为“source”，显示了六个事件，用数字标记。这些事件分为三组：事件 1 和 2 紧挨在一起，然后是一个间隙。然后事件 3、4 和 5 紧挨在一起。然后在另一个间隙后，事件 6 发生，与任何事件都不接近。第二个可观测对象标记为“source.Quiescent(TimeSpan.FromSeconds(2), Scheduler.Default)”。它显示了三个事件。第一个标记为“1, 2”，其水平位置显示它发生在“source”可观测对象上的“2”事件之后一点。第二个可观测对象的第二个事件标记为“3,4,5”，发生在“source”可观测对象上的“5”事件之后一点。第二个可观测对象上的第三个事件来自标记为“6”，发生在“source”可观测对象上的“6”事件之后一点。图像传达了这样的想法：每次源产生一些事件然后停止时，第二个可观测对象将在源停止后不久产生一个事件，其中包含一个列表，列表中包含源在最近一次活动中的所有事件。](GraphicsIntro/Ch02-Quiescent-Marbles-Input-And-Output.svg)

这表明源（上面的线）产生了一对事件（在这个例子中是值 `1` 和 `2`），然后停了一会儿。它停止后不久，`Quiescent` 操作符返回的可观测对象（下面的线）产生了一个单一事件，包含这两个事件的列表（`[1,2]`）。然后，源再次启动，相当快地产生了值 `3`、`4` 和 `5`，然后静止了一会儿。再次，一旦静止时间足够长，由 `Quiescent` 返回的源产生了一个包含这第二批源事件的单一事件（`[3,4,5]`）。然后，这个图中源的最后一部分活动包括一个单一事件 `6`，之后是更多的不活动，再次，一旦不活动持续了足够长的时间，`Quiescent` 源产生了一个单一事件来报告这一点。由于源的最后一次“活动”只包含一个事件，所以这个 `Quiescent` 可观测对象的最后输出是一个只包含单一值 `[6]` 的列表。

那么，如何实现这个代码呢？首先要注意的是 `Quiescent` 方法只是使用了其他 Rx LINQ 操作符（`Return`、`Scan`、`Where` 和 `Buffer` 操作符是显而易见的，查询表达式将使用 `SelectMany` 操作符，因为这是 C# 查询表达式在包含两个 `from` 子句时所做的）的组合，产生了最终的 `IObservable<IList<T>>` 输出。

这是 Rx 的 _组合_ 方法，这是我们通常使用 Rx 的方式。我们使用一系列的操作符，通过组合（_组合_）以一种产生我们想要的效果的方式。

但这种特定的组合是如何产生我们想要的效果的呢？这里有几种方法可以从 `Quiescent` 操作符中得到我们正在寻找的行为，但这种特定实现的基本思想是它保持了最近发生的事件的计数，然后在这个数字回落到零时产生一个结果。`outstanding` 变量引用了跟踪最近事件数量的 `IObservable<int>`，这个弹珠图显示了它对同样的 `source` 事件的响应：

![如何使用 Quiescent 操作符计算未决事件的数量。一个 Rx 弹珠图，展示了两个可观察对象。第一个标记为“source”，展示了与前一个图表中相同的六个事件，按数字标记，但这次也进行了颜色编码，以便每个事件都有不同的颜色。与以前一样，这些事件分为三组：事件 1 和 2 紧挨在一起，然后是一个间隙。然后事件 3、4 和 5 紧挨在一起。然后在另一个间隙后，事件 6 发生，与任何事件都不接近。第二个可观测对象标记为“outstanding”，对于“source”可观测对象上的每个事件，它显示了两个事件。每对的第一个事件出现在其在“source”线上对应事件的正下方，并且有一个数字，总是比它的直接前任高 1；最先出现的项显示数字 1。来自第二对的第一个项是接下来出现在这条线上的，因此有一个数字 2。但然后第一对的第二个项出现，这将数字降低回 1，它后面是第二对的第二个项，显示 0。由于第一行上第二批事件比较接近，我们看到的值是 1、2、1、2、1，然后是 0。第一行上的最后一个事件，标记为 6，在第二行上有一对相应的事件，报告值为 1 和 0。这整体效果是，第二个，“outstanding”线上的每个值告诉我们过去 2 秒内“source”线产生了多少个项目。](GraphicsIntro/Ch02-Quiescent-Marbles-Outstanding.svg)

这次我用颜色对事件进行了编码，这样我就可以显示 `source` 事件与 `outstanding` 产生的对应事件之间的关系。每次 `source` 产生一个事件时，`outstanding` 会同时产生一个事件，其值比前一个由 `outstanding` 产生的值高 1。但每个这样的 `source` 事件还会导致 `outstanding` 在两秒后产生另一个事件。（之所以是两秒，是因为这些示例中，我假设 `Quiescent` 的第一个参数是 `TimeSpan.FromSeconds(2)`，如第一个弹珠图所示。）这第二个事件总是产生一个比前一个值低 1 的值。

这意味着，从 `outstanding` 中涌现的每个事件都告诉我们在过去两秒内 `source` 产生了多少个事件。这个图表以稍微不同的方式显示了同样的信息：它显示了 `outstanding` 产生的最新值的图表。你可以看到，每次 `source` 产生一个新值时，该值就会上升。每次 `source` 产生的值两秒后，它又会下降。

![未决事件数量的图表。一个 Rx 弹珠图，展示了“source”可观测对象，和前一个图表中的第二个可观测对象，这次展示为一个条形图表，显示最新的值。这让我们更容易看到“outstanding”值在“source”产生新值时上升，然后两秒后再次下降，当值紧挨在一起时这个累积总数会更高。它还清楚地表明，值在活动的“阵发”之间下降到零。](GraphicsIntro/Ch02-Quiescent-Marbles-Outstanding-Value.svg)

简单的情况，如最后一个事件 `6`，在大约那个时间它是唯一发生的事件，`outstanding` 的值在事件发生时上升了 1，然后两秒后再次下降。图片的左侧稍微复杂一些：我们看到两个相对快速的事件，所以 `outstanding` 的值先上升到 1，然后再上升到 2，再降回 1，然后再降到 0。中间部分看起来有点混乱——当 `source` 产生事件 `3` 时，计数上升 1，然后当事件 `4` 进来时上升到 2。在 `3` 事件发生后两秒，它又回落到 1，但另一个事件 `5` 进来，总数又回到 2。在那之后不久，因为已经过了 `4` 事件两秒，它又降到 1。然后稍后，当 `5` 事件发生两秒后，它又降到 0。

中间部分最混乱，但也是这个操作符设计的那种活动最能代表的类型。记住，这里的全部重点是，我们希望看到活动的阵发，如果这些代表的是文件系统活动，它们往往会略显混乱，因为存储设备并不总是有完全可预测的性能特征（特别是如果它是带有移动部件的磁存储设备，或者在其中可能出现可变网络延迟的远程存储）。

有了这个最近活动的度量，我们可以通过寻找 `outstanding` 回落到零的时间来发现活动阵发的结束，这就是代码中 `zeroCrossing` 引用的可观测对象所做的。（这只是使用 `Where` 操作符过滤掉除 `outstanding` 的当前值回到零以外的所有事件。）

但 `outstanding` 本身是如何工作的呢？这里的基本方法是，每次 `source` 产生一个值时，我们实际上会创建一个全新的 `IObservable<int>`，它会产生两个确切的值。它立即产生值 1，然后在指定的时间跨度后（这些示例中是 2 秒，但通常是 `minimumInactivityPeriod`）产生值 -1。这就是查询表达式中的这个子句发生的事情：

```csharp
from delta in Observable
    .Return(1, scheduler)
    .Concat(Observable
        .Return(-1, scheduler)
        .Delay(minimumInactivityPeriod, scheduler))
```

我说过 Rx 是关于组合的，这里绝对是这样。我们正在使用非常简单的 `Return` 操作符创建一个 `IObservable<int>`，它立即产生一个单一值然后终止。这段代码调用了两次，一次是产生值 `1`，另一次是产生值 `-1`。它使用 `Delay` 操作符，以便我们不是立即获得 -1 值，而是获得一个等待指定时间段的可观察对象（在这些示例中是 2 秒，但通常是任何 `minimumInactivityPeriod`），然后再产生该值。然后我们使用 `Concat` 将这两个合并成一个单一的 `IObservable<int>`，它产生值 1，然后两秒后产生值 -1。

虽然这为每个 `source` 事件产生了一个全新的 `IObservable<int>`，但上面显示的 `from` 子句是一个形式为 `from ... from .. select` 的查询表达式的一部分，C# 编译器将其转换为对 `SelectMany` 的调用，这具有将这些全部压平回到单个可观察对象的效果，这正是 `onoffs` 变量所指的。这个弹珠图说明了这一点：

![未决事件数量的图表。显示了几个 Rx 弹珠图，从先前图表中的“source”可观察对象开始，接着是前面示例中的 LINQ 查询表达式标记的一条线，显示了 6 个单独的弹珠图，每个都对应于“source”产生的元素之一。每个由两个事件组成：第一个事件的值为 1，位于“source”上相应事件的正下方，表示它们同时发生，然后第二个事件的值为 -1，两秒后发生。在这之下是一个标记为“onoffs”的弹珠图，其中包含前一个图中所有的事件，但合并成一个单一的序列。这些都进行了颜色编码，以便更容易看出这些事件与“source”上的原始事件的对应关系。最后，我们有前一个图中完全相同的“outstanding”弹珠图。](GraphicsIntro/Ch02-Quiescent-Marbles-On-Offs.svg)

这也显示了 `outstanding` 可观测对象，但现在我们可以看到它从何而来：它只是 `onoffs` 可观测对象发出的值的累积总和。这个运行总数可观测对象是用以下代码创建的：

```csharp
IObservable<int> outstanding = onoffs.Scan(0, (total, delta) => total + delta);
```

Rx 的 `Scan` 操作符的工作方式类似于标准 LINQ 的 [`Aggregate`](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/aggregation-operations) 操作符，它将一个操作（这里是加法）累积地应用于序列中的每个项目。不同的是，`Aggregate` 一旦到达序列的末尾就只产生最终结果，而 `Scan` 显示其所有工作，每个输入后产生迄今为止的累积值。因此，这意味着每次 `onoffs` 产生一个事件时，`outstanding` 也会产生一个事件，该事件的值将是到目前为止从 `onoffs` 中的所有值的总和。

这就是 `outstanding` 如何告诉我们在过去两秒内（或指定的 `minimumActivityPeriod`）`source` 产生了多少事件。

最后一个难题是如何从提到的 `zeroCrossings`（每次源变得沉寂时产生一个事件）转到输出 `IObservable<IList<T>>`，它提供最近一次活动中发生的所有事件。这里我们只是在使用 Rx 的 `Buffer` 操作符，这个操作符正是为这种场景设计的：它将其输入切成块，为每个块产生一个事件，其值是一个 `IList<T>`，包含该块的项。`Buffer` 可以以几种方式切分事物，但在这种情况下，我们使用的是那种在某个 `IObservable<T>` 产生一个项目时就开始一个新的块的形式。具体来说，我们告诉 `Buffer` 通过在 `zeroCrossings` 产生新事件时切分 `source` 来切分它。

（最后一个细节，以防你看到它并想知道，这个方法需要一个 `IScheduler`。这是一个 Rx 抽象，用于处理定时和并发。我们需要它，因为我们需要能够在一秒钟的延迟之后生成事件，这种以时间驱动的活动需要一个调度器。）

我们将在后面的章节中更详细地了解这些操作符和调度器的工作原理。现在，关键点是我们通常通过创建一系列 LINQ 操作符的组合，这些操作符处理和组合 `IObservable<T>` 源来定义我们需要的逻辑。

请注意，在该示例中没有任何内容实际调用 `IObservable<T>` 定义的唯一方法（`Subscribe`）。总会有某个地方最终消费事件，但使用 Rx 的大部分工作往往涉及声明性地定义我们需要的 `IObservable<T>`。

现在您已经看到了 Rx 编程的示例，我们可以解决一些关于为什么 Rx 根本存在的明显问题。

### .NET 事件有什么问题？

.NET 从第一个版本开始就内置了对事件的支持——事件是 .NET 类型系统的一部分。C# 语言对此有内置支持，以 `event` 关键字的形式，以及用于订阅事件的专门语法。那么，当 Rx 十年后出现时，为什么感到有必要发明自己的事件流表示呢？`event` 关键字有什么问题？

.NET 事件的基本问题是它们从 .NET 类型系统中获得了特殊处理。讽刺的是，这使它们比没有内置事件支持时的灵活性更低。如果没有 .NET 事件，我们就需要某种基于对象的事件表示，此时你可以对事件做所有可以对其他对象做的事情：你可以将它们存储在字段中，将它们作为方法参数传递，定义它们的方法等等。

公平地说，.NET 版本 1，它在没有泛型的情况下实际上是不可能定义出一个良好的基于对象的事件表示的，而 .NET 直到版本 2 才获得了泛型（在 .NET 1.0 发布三年半后）。不同的事件来源需要能够按类型参数化事件，.NET 事件提供了一种方式。但是一旦有了泛型，像 `IObservable<T>` 这样的类型就变得可能定义了，此时事件提供的主要优势就消失了。（另一个好处是对实现和订阅事件的一些语言支持，但原则上这是可以为 Rx 做的事情，它不需要事件从根本上与类型系统的其他功能不同。）

考虑我们刚刚处理过的示例。能够定义我们自己的自定义 LINQ 操作符 `Quiescent`，因为 `IObservable<T>` 像任何其他接口一样，意味着我们可以为其编写扩展方法。你不能为事件编写扩展方法。

此外，我们能够包装或适配 `IObservable<T>` 源。`Quiescent` 将一个 `IObservable<T>` 作为输入，并结合各种 Rx 操作符生成另一个可观察对象作为输出。其输入是可以订阅的事件源，其输出也是可以订阅的事件源。你不能用 .NET 事件这样做——你不能写一个接受事件作为参数的方法，或者返回一个事件。

这些限制有时被描述为 .NET 事件不是 _一等公民_。有些事情你可以用 .NET 中的值或引用做，但你不能用事件做。

如果我们将事件源表示为一个普通的接口，那么它就 _是_ 一个一等公民：它可以使用我们希望与其他对象和值一样的所有功能，正是因为它不是特殊的。

### 那么流呢？

我将 `IObservable<T>` 描述为表示事件的 _流_。这引起了一个明显的问题：.NET 已经有了 [`System.IO.Stream`](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream)，那为什么不直接使用呢？

简短的答案是流很奇怪，因为它们代表着计算的一个古老概念，早在第一个 Windows 操作系统发布之前就已经存在了，因此它们带有相当多的历史包袱。这意味着即使是如“我有一些数据，想立即让所有感兴趣的方都能访问它”这样简单的场景，在 `Stream` 类型中实现起来也出奇地复杂。

此外，`Stream` 无法表明哪种类型的数据将出现——它只知道字节。由于 .NET 的类型系统支持泛型，我们自然希望表示事件流的类型可以通过类型参数指示事件类型。

所以即使你确实在实现中使用了 `Stream`，你也会希望引入某种形式的包装抽象。如果 `IObservable<T>` 不存在，你就需要发明它。

当然，可能使用 IO 流在 Rx 中，但它们不是正确的主抽象。

（如果你不信服，请参阅[附录 A：传统 IO 流的问题所在](A_IoStreams.md)，它对为什么 `Stream` 不适合这项任务进行了更详细的解释。）

现在我们已经看到了为什么需要 `IObservable<T>`，我们需要看看它的对等接口 `IObserver<T>`。

## `IObserver<T>`

之前，我展示了 `IObservable<T>` 的定义。如你所见，它只有一个方法，`Subscribe`。而这个方法只接受一个参数，类型为 [`IObserver<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.iobserver-1)。所以如果你想观察 `IObservable<T>` 所提供的事件，你必须为其提供一个 `IObserver<T>`。到目前为止，我们只是提供了一个简单的回调，并且 Rx 为我们包装成了 `IObserver<T>` 的实现，但即使这通常是我们实际接收通知的方式，你仍然需要了解 `IObserver<T>` 以有效使用 Rx。这不是一个复杂的接口：

```csharp
public interface IObserver<in T>
{
    void OnNext(T value);
    void OnError(Exception error);
    void OnCompleted();
}
```

就像 `IObservable<T>` 一样，你可以在 .NET 运行时 GitHub 仓库中找到 [`IObserver<T>` 的源代码](https://github.com/dotnet/runtime/blob/7cf329b773fa5ed544a9377587018713751c73e3/src/libraries/System.Private.CoreLib/src/System/IObserver.cs)，因为这两个接口都内置于运行时库中。

如果我们想创建一个将值打印到控制台的观察者，那么做起来就像这样简单：

```csharp
public class MyConsoleObserver<T> : IObserver<T>
{
    public void OnNext(T value)
    {
        Console.WriteLine($"Received value {value}");
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"Sequence faulted with {error}");
    }

    public void OnCompleted()
    {
        Console.WriteLine("Sequence terminated");
    }
}
```

在前一章中，我使用了一个 `Subscribe` 扩展方法，它接受了一个当源产生一个项目时调用的委托。这个方法由 Rx 的 `ObservableExtensions` 类定义，它还定义了 `IObservable<T>` 的各种其他扩展方法。它包括 `Subscribe` 的重载，使我能够编写代码，其效果与前面的示例相同，而无需提供我自己的 `IObserver<T>` 实现：

```csharp
source.Subscribe(
    value => Console.WriteLine($"Received value {value}"),
    error => Console.WriteLine($"Sequence faulted with {error}"),
    () => Console.WriteLine("Sequence terminated")
);
```

我们没有传递所有三个方法的 `Subscribe` 重载（例如，我之前的示例只提供了一个对应于 `OnNext` 的单个回调），相当于编写了一个 `IObserver<T>` 实现，其中一个或多个方法只是空的。无论我们是觉得编写自己的 `IObserver<T>` 实现更方便，还是只是为其 `OnNext`、`OnError` 和 `OnCompleted` 方法中的一些或全部提供回调，基本行为都是相同的：`IObservable<T>` 源通过调用 `OnNext` 报告每个事件，并通过调用 `OnError` 或 `OnCompleted` 告诉我们事件已结束。

如果你想知道 `IObservable<T>` 与 `IObserver<T>` 之间的关系是否与 [`IEnumerable<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) 和 [`IEnumerator<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerator-1) 之间的关系相似，那么你是对的。`IEnumerable<T>` 和 `IObservable<T>` 都表示 _潜在_ 序列。对于这两个接口，它们只有在我们要求它们提供数据时才会提供数据。要从 `IEnumerable<T>` 获取值，需要存在一个 `IEnumerator<T>`；类似地，要从 `IObservable<T>` 获取值，需要一个 `IObserver<T>`。

差异反映了 `IEnumerable<T>` 与 `IObservable<T>` 之间根本的 _拉取与推送_ 差异。而 `IEnumerable<T>` 允许源为我们创建一个 `IEnumerator<T>`，我们随后可以使用它来检索项目（这是 C# `foreach` 循环所做的），在 `IObservable<T>` 中，源不 _实现_ `IObserver<T>`：它期望 _我们_ 提供一个 `IObserver<T>`，然后将其值推送到该观察者。

那么为什么 `IObserver<T>` 有这三种方法呢？记得我说过从抽象意义上说 `IObserver<T>` 代表与 `IEnumerable<T>` 相同的东西吗？我的意思是这是准确的：`IObservable<T>` 和 `IObserver<T>` 的设计目的是保留 `IEnumerable<T>` 和 `IEnumerator<T>` 的确切含义，只改变消费的具体机制。

要了解这意味着什么，可以考虑当你遍历 `IEnumerable<T>`（比如说，使用 `foreach` 循环）时会发生什么。在每次迭代（更准确地说，每次调用枚举器的 [`MoveNext`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.ienumerator.movenext) 方法时），可能会发生以下三种情况之一：

* `MoveNext` 可能返回 `true`，表示枚举器的 [`Current`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerator-1.current) 属性中有一个可用的值
* `MoveNext` 可能抛出一个异常
* `MoveNext` 可能返回 `false`，表示你已经到达了集合的末尾

这三种结果正好对应于 `IObserver<T>` 定义的三种方法。我们可以用稍微更抽象的术语来描述这些：

* 这是另一个项目
* 全部出错了
* 没有更多的项目了

这描述了在消费 `IEnumerable<T>` 或 `IObservable<T>` 时下一步可能发生的三件事之一。唯一的区别是消费者发现这一点的方式。对于 `IEnumerable<T>` 来说，每次调用 `MoveNext` 会告诉我们这三者中的哪一个适用。而对于 `IObservable<T>` 来说，它会通过调用你的 `IObserver<T>` 实现的相应成员告诉你这三者中的哪一个。

## Rx 序列的基本规则

注意上面列表中的两个结果是终结性的。如果你在 `IEnumerable<T>` 上进行 `foreach` 循环，并且它抛出了异常，`foreach` 循环将终止。C# 编译器理解如果 `MoveNext` 抛出异常，`IEnumerator<T>` 现在已完成，所以它会将其处理并允许异常传播。同样，如果你到达序列的末尾，那么你也完成了，编译器理解这一点：`foreach` 循环检测到 `MoveNext` 返回 `false` 后，它会处理枚举器，然后转到循环之后的代码。

这些规则可能看起来非常明显，以至于我们在遍历 `IEnumerable<T>` 序列时可能从未想过它们。可能不那么立即明显的是 `IObservable<T>` 序列适用完全相同的规则。如果可观察源告诉观察者序列已经结束，或报告错误，那么在任何一种情况下，这都是源允许对观察者做的最后一件事。

这意味着这些示例将违反规则：

```csharp
public static void WrongOnError(IObserver<int> obs)
{
    obs.OnNext(1);
    obs.OnError(new ArgumentException("This isn't an argument!"));
    obs.OnNext(2);  // 违反规则！我们已经报告失败，所以迭代必须停止
}

public static void WrongOnCompleted(IObserver<int> obs)
{
    obs.OnNext(1);
    obs.OnCompleted();
    obs.OnNext(2);  // 违反规则！我们已经说完了，所以迭代必须停止
}

public static void WrongOnErrorAndOnCompleted(IObserver<int> obs)
{
    obs.OnNext(1);
    obs.OnError(new ArgumentException("A connected series of statements was not supplied"));

    // 下一个调用违反了规则，因为我们报告了一个错误，并且在做了那之后不允许再进行任何调用。
    obs.OnCompleted();
}

public static void WrongOnCompletedAndOnError(IObserver<int> obs)
{
    obs.OnNext(1);
    obs.OnCompleted();

    // 下一个调用违反了规则，因为我们已经说我们完成了。
    // 当你终止一个序列时，你必须在 OnCompleted 或 OnError 之间选择
    obs.OnError(new ArgumentException("Definite proposition not established"));
}
```

这些在 `IEnumerable<T>` 中有明显对应：

* `WrongOnError`：如果枚举器从 `MoveNext` 抛出异常，那么它就完成了，你不得再调用 `MoveNext`，这样你就不会再从它那里获取更多项
* `WrongOnCompleted`：如果枚举器从 `MoveNext` 返回 `false`，那么它就完成了，你不得再调用 `MoveNext`，这样你就不会再从它那里获取更多项
* `WrongOnErrorAndOnCompleted`：如果枚举器从 `MoveNext` 抛出异常，这意味着它已完成，并且你不得再调用 `MoveNext`，这意味着它将不会有机会通过从 `MoveNext` 返回 `false` 来告诉它已经完成
* `WrongOnCompletedAndOnError`：如果枚举器从 `MoveNext` 返回 `false`，那么它就完成了，你不得再调用 `MoveNext`，这意味着它也不会有机会抛出异常

因为 `IObservable<T>` 是基于推送的，遵守所有这些规则的责任落在了可观察源上。对于 `IEnumerable<T>`，这是基于拉取的，使用 `IEnumerator<T>` 的代码（例如 `foreach` 循环）负责遵守这些规则。但它们本质上是相同的规则。

`IObserver<T>` 还有一个附加规则：如果你调用 `OnNext`，你必须等待它返回才能进行更多方法调用到同一个 `IObserver<T>`。这意味着以下代码违反了规则：

```csharp
public static void EverythingEverywhereAllAtOnce(IObserver<int> obs)
{
    Random r = new();
    for (int i = 0; i < 10000; ++i)
    {
        int v = r.Next();
        Task.Run(() => obs.OnNext(v)); // 违反规则！
    }
}
```

这在 `obs` 上调用了 `OnNext` 10,000 次，却将这些调用作为单独的任务在线程池上运行。线程池设计为能够并行执行工作，这里是个问题，因为没有任何东西确保一个 `OnNext` 调用在下一个开始之前完成。我们违反了规则，规则是说我们必须等待每次调用 `OnNext` 返回再调用 `OnNext`、`OnError` 或 `OnComplete`。

这个规则是 Rx.NET 内置的唯一形式的反压力：由于规则禁止在上一次 `OnNext` 调用仍在进行中时调用 `OnNext`，这使得 `IObserver<T>` 能够限制项目到达的速率。如果你只是等到从 `OnNext` 返回时才准备好，那么源就被迫要等待。然而，这里还有一些问题。一旦涉及[调度器](11_SchedulingAndThreading.md)，底层源可能不直接连接到最终的观察者。如果你使用类似 [`ObserveOn`](11_SchedulingAndThreading.md#subscribeon-and-observeon) 的东西，那么直接订阅源的 `IObserver<T>` 可能只是将项目放在队列中然后立即返回，然后这些项目将在不同的线程上交付给真正的观察者。在这些情况下，由于花长时间从 `OnNext` 返回而造成的“反压力”只会传播到从队列中拉取项目的代码那里。

尽管存在某些 Rx 操作符（例如 [`Buffer`](08_Partitioning.md#buffer) 或 [`Sample`](12_Timing.md#sample)）可以缓解这一点，但没有内置机制用于跨线程传播反压力。其他平台上的一些 Rx 实现尝试提供了这种整合解决方案；过去当 Rx.NET 开发社区研究这个问题时，一些人认为这些解决方案存在问题，并且没有关于什么是好的解决方案的共识。所以在 Rx.NET 上，如果你需要安排源在你难以跟上时减慢速度，你将需要引入你自己的一些机制。(即使是提供内置反压力的 Rx 平台，它们也无法提供一种通用的解决方案来回答这个问题：我们如何使这个源提供事件更慢？如何（甚至是否）你可以做到这一点将取决于源的性质。所以在任何情况下，一些定制的调整可能都是必要的。)

这个规则必须在 `OnNext` 返回之前等待是微妙并且棘手的。它可能不如其他规则那么明显，因为这种破坏这一规则的机会只出现在源向应用程序推送数据时。你可能会看着上面的示例并想：“嗯，谁会那么做？”然而，多线程只是表明技术上有可能破坏这一规则的一种简单方式。更难的情况是单线程重入发生时。考虑这段代码：

```csharp
public class GoUntilStopped
{
    private readonly IObserver<int> observer;
    private bool running;

    public GoUntilStopped(IObserver<int> observer)
    {
        this.observer = observer;
    }

    public void Go()
    {
        this.running = true;
        for (int i = 0; this.running; ++i)
        {
            this.observer.OnNext(i);
        }
    }

    public void Stop()
    {
        this.running = false;
        this.observer.OnCompleted();
    }
}
```

这个类接受一个 `IObserver<int>` 作为构造函数参数。当你调用它的 `Go` 方法时，它会重复调用观察者的 `OnNext`，直到有东西调用它的 `Stop` 方法。

你能看出这里的 bug 吗？

我们可以通过提供一个 `IObserver<int>` 实现来查看会发生什么：

```csharp
public class MyObserver : IObserver<int>
{
    private GoUntilStopped? runner;

    public void Run()
    {
        this.runner = new(this);
        Console.WriteLine("Starting...");
        this.runner.Go();
        Console.WriteLine("Finished");
    }

    public void OnCompleted()
    {
        Console.WriteLine("OnCompleted");
    }

    public void OnError(Exception error) { }

    public void OnNext(int value)
    {
        Console.WriteLine($"OnNext {value}");
        if (value > 3)
        {
            Console.WriteLine($"OnNext calling Stop");
            this.runner?.Stop();
        }
        Console.WriteLine($"OnNext returning");
    }
}
```

注意 `OnNext` 方法查看其输入，如果大于 3，它会告诉 `GoUntilStopped` 对象停止。

让我们看看输出：

```
Starting...
OnNext 0
OnNext returning
OnNext 1
OnNext returning
OnNext 2
OnNext returning
OnNext 3
OnNext returning
OnNext 4
OnNext calling Stop
OnCompleted
OnNext returning
Finished
```

问题正是在最后的部分，具体在这两行：

```
OnCompleted
OnNext returning
```

这告诉我们调用观察者的 `OnCompleted` 发生在正在进行的 `OnNext` 调用返回之前。这不需要多个线程来发生。它是因为 `OnNext` 中的代码决定它是否想要继续接收事件，并且当它想停止时，它会立即调用 `GoUntilStopped` 对象的 `Stop` 方法。这样做没有错。观察者被允许在 `OnNext` 内对其他对象进行出站调用，实际上观察者在检查传入事件并决定它是否想要停止时这样做是很常见的。

问题在于 `GoUntilStopped.Stop` 方法。这个调用 `OnCompleted`，但它没有尝试确定 `OnNext` 是否正在进行。

解决这个问题可能出奇地棘手。假设 `GoUntilStopped` _确实_ 检测到 `OnNext` 正在进行中。那么接下来该怎么办？在多线程的情况下，我们可以使用 `lock` 或其他同步原语来确保按顺序对观察者进行调用，但这在这里行不通：调用 `Stop` 是在调用 `OnNext` 的_同一个线程_ 上进行的。当 `Stop` 被调用并希望调用 `OnCompleted` 时，调用堆栈可能看起来像这样：

```
`GoUntilStopped.Go`
  `MyObserver.OnNext`
    `GoUntilStopped.Stop`
```
 
我们的 `GoUntilStopped.Stop` 方法需要等待 `OnNext` 返回后再调用 `OnCompleted`。但请注意，`OnNext` 方法无法返回，直到我们的 `Stop` 方法返回。我们设法用单线程代码创建了死锁！

在这种情况下不难修复：我们可以修改 `Stop`，使其只将 `running` 字段设置为 `false`，然后将对 `OnComplete` 的调用移到 `Go` 方法中，放在 `for` 循环之后。但一般来说，这可能是一个很难解决的问题，这是使用 `System.Reactive` 库而不是尝试直接实现 `IObservable<T>` 和 `IObserver<T>` 的原因之一。Rx 有解决这类问题的通用机制。（我们将在[调度](11_SchedulingAndThreading.md)中看到这些。）此外，Rx 提供的所有实现都利用了这些机制来帮你。

如果你通过声明性地组合内置的操作符使用 Rx，你永远不必考虑这些规则。你可以依赖这些规则在你的回调中接收事件，并且保持规则大部分是 Rx 的问题。所以这些规则的主要影响是它使得消费事件的代码生活变得更简单。

这些规则有时被表达为一个_语法_。例如，考虑这个正则表达式：

```
(OnNext)*(OnError|OnComplete)
```

这正式捕获了基本思想：可以有任意数量的 `OnNext` 调用（甚至可能是零次调用），这些调用按顺序发生，后面跟着 `OnError` 或 `OnComplete` 中的一个，但不是两者都有，并且这两个之后不得有任何东西。

最后一点：序列可能是无限的。这对 `IEnumerable<T>` 是真的。完全可以对枚举器每次 `MoveNext` 返回 `true`，在这种情况下，迭代遍历它的 `foreach` 循环将永远不会到达末尾。它可能选择停止（使用 `break` 或 `return`），或者一些非枚举器产生的异常可能导致循环终止，但一个 `IEnumerable<T>` 产生项目只要你继续要求它们是完全可以接受的。`IObservable<T>` 也是如此。如果你订阅了一个可观察源，并且在程序退出时你还没有收到对 `OnComplete` 或 `OnError` 的调用，这不是一个错误。

因此，你可能会认为这是更好地正式描述规则的方式：

```
(OnNext)*(OnError|OnComplete)?
```

更微妙的是，可观察源允许什么都不做。实际上，为了节省开发人员编写不做任何事情的源的努力，提供了一个内置实现：如果你调用 `Observable.Never<int>()`，它将返回一个 `IObservable<int>`，如果你订阅它，它将永远不会调用你的观察者的任何方法。这可能看起来不是立刻有用——它在逻辑上等同于一个 `IEnumerable<T>`，其中枚举器的 `MoveNext` 方法从不返回，这可能无法区分它是否崩溃。与 Rx 不同，因为当我们对这种“永远不会产生任何项”的行为进行建模时，我们不需要永远阻塞一个线程来做到这一点。我们只需要决定永远不调用观察者的任何方法。这可能看起来很傻，但正如你在 `Quiescent` 示例中看到的，有时我们创建可观察源不是因为我们想要实际产生的项，而是因为我们对发生有趣事物的时刻感兴趣。能够对“从不发生有趣事情”案例进行建模有时可能很有用。例如，如果你编写了一些代码来检测意外的不活动（例如，停止产生值的传感器），并希望建立那段代码的测试，你的测试可以使用 `Never` 源而不是真实的一个，来模拟损坏的传感器。

我们还没有完全完成 Rx 的规则，但最后一个规则只适用于我们在自然结束之前选择取消订阅源的情况。

## 订阅的生命周期

还有一个需要理解的观察者和可观察者之间关系的方面：订阅的生命周期。

你已经从 `IObserver<T>` 的规则中了解到，调用 `OnComplete` 或 `OnError` 表示序列的结束。我们将 `IObserver<T>` 传递给了 `IObservable<T>.Subscribe`，现在订阅已经结束了。但如果我们想提前停止订阅怎么办？

我之前提到过 `Subscribe` 方法返回一个 `IDisposable`，它使我们能够取消我们的订阅。也许我们之所以订阅一个源，是因为我们的应用程序打开了一个显示某个进程状态的窗口，我们想根据该进程的进展情况更新窗口。如果用户关闭了这个窗口，我们就不再需要通知了。尽管我们可以忽略所有后续通知，但如果我们监控的东西永远不会自然结束，这可能会成问题。我们的观察者会继续接收通知，直到应用程序的生命周期结束。这是对 CPU 功率（从而对电池寿命和环境影响）的浪费，也可能阻止垃圾收集器回收应该变得自由的内存。

因此，我们可以通过调用 `Subscribe` 返回的对象的 `Dispose` 来表示我们不再希望接收通知。然而，有几个不明显的细节。

### 订阅的处理是可选的

你不必在 `Subscribe` 返回的对象上调用 `Dispose`。显然，如果你想在整个过程的生命周期内保持对事件的订阅，这是有意义的：你从未停止使用该对象，因此你当然不会处理它。但可能不那么明显的是，如果你订阅了一个确实结束的 `IObservable<T>`，它会自动进行清理。

`IObservable<T>` 的实现不允许假设你一定会调用 `Dispose`，因此如果它们通过调用观察者的 `OnCompleted` 或 `OnError` 停止，它们需要执行任何必要的清理工作。这很不寻常。在大多数情况下，当一个 .NET API 代表你创建了一个实现了 `IDisposable` 的全新对象时，如果不处理它，那是一个错误。但代表 Rx 订阅的 `IDisposable` 对象是这一规则的一个例外。只有当你想要它们比否则更早地停止时，你才需要处理它们。

### 取消订阅可能会很慢甚至无效

`Dispose` 不一定会立即生效。显然，在你的代码调用 `Dispose` 和 `Dispose` 实现达到实际做些什么的地方之间，会有一段非零的时间。不那么明显的是，一些可观察来源可能需要做一些非平凡的工作来关闭事情。

一个源可能会创建一个线程来监控并报告它代表的任何事件。（例如，在 .NET 8 上运行 Linux 时，上面显示的文件系统源会创建自己的线程。）关闭线程可能需要一段时间。

`IObservable<T>` 表示一些底层工作是相当常见的做法。例如，Rx可以将任何返回 `Task<T>` 的工厂方法包装为 `IObservable<T>`。它将为每个调用 `Subscribe` 的调用调用工厂，因此如果一个单一的 `IObservable<T>` 有多个订阅者，每个订阅者实际上都会获得自己的 `Task<T>`。这个包装器能够向工厂提供一个 `CancellationToken`，如果观察者在任务自然运行完成之前取消订阅通过调用 `Dispose`，它将将该 `CancellationToken` 置于已取消状态。这可能会导致任务停止，但这只有在任务恰好在监视 `CancellationToken` 的情况下才行。即使是这样，完全停下来也可能需要一些时间。至关重要的是，`Dispose` 调用不会等待取消完成。它将尝试启动取消，但它可能在取消完成之前返回。

### 取消订阅时 Rx 序列的规则

前面描述的 Rx 序列的基本规则仅考虑了源决定何时（或是否）停止的情况。如果订阅者提前取消订阅会怎样？只有一条规则：

一旦 `Dispose` 调用返回，源将不会对相关观察者进行任何进一步的调用。如果你在 `Subscribe` 返回的对象上调用 `Dispose`，那么一旦该调用返回，你可以确定你传递的观察者将不会再接收其三种方法（`OnNext`、`OnError` 或 `OnComplete`）的任何调用。

这似乎很清楚，但它留下了一个灰色地带：当你已经调用了 `Dispose` 但它还没有返回时会发生什么？规则允许源在这种情况下继续发出事件。事实上，它们很好地要求这样做：`Dispose` 实现需要花费一些非零长度的时间才能产生任何效果，因此在多线程的环境中，总是有可能发生在调用 `Dispose` 开始后的某个时间点，但在调用产生任何效果之前产生事件。唯一可以依赖不会再发出事件的情况是，如果你的观察者没有在 `OnNext` 处理程序内部调用 `Dispose`。在这种情况下，源将已经注意到 `OnNext` 调用正在进行中，因此在调用 `Dispose` 开始之前，进一步的调用已被阻止。

但假设你的观察者没有正在进行 `OnNext` 调用，任何以下行为都是合法的：

- 几乎在 `Dispose` 开始后立即停止对 `IObserver<T>` 的调用，即使关闭任何相关的底层进程需要相对较长的时间，这种情况下，你的观察者将永远不会接收 `OnCompleted` 或 `OnError`
- 产生反映关闭过程的通知（包括在尝试整洁地停止时发生错误时调用 `OnError`，或如果它在没有问题的情况下停止则调用 `OnCompleted`）
- 在调用 `Dispose` 开始后一段时间内继续产生一些通知，但在某个任意时间点切断它们，可能丢失了一些重要的东西，例如在尝试停止时发生的错误

事实上，Rx 倾向于选择第一个选项。如果你正在使用由 `System.Reactive` 库实现的 `IObservable<T>`（例如，通过 LINQ 操作符返回的一个），它很有可能有这种特性。这部分是为了避免观察者在其通知回调中尝试对其源进行操作的棘手情况。重入往往难以处理，Rx 通过确保在开始关闭订阅的工作之前已经停止向观察者传递通知来避免处理这种特定形式的重入。

有时这会让人措手不及。如果你需要能够取消你正在观察的一些过程，但你需要能够观察到它停止之前的一切，那么你不能使用取消订阅作为关闭机制。一旦你调用了 `Dispose`，返回 `IDisposable` 的 `IObservable<T>` 就不再有义务告诉你任何事情了。这可能很令人沮丧，因为 `Subscribe` 返回的 `IDisposable` 有时看起来是如此自然且易于关闭某事的方式。但基本事实是：一旦你开始取消订阅，你就不能依赖于获取与该订阅关联的任何进一步的通知。你_可能_会收到一些——源被允许在 `Dispose` 调用返回之前继续提供项目。但你不能依赖它——源也被允许立即沉默，这是大多数由 Rx 实现的源会做的事情。

一种微妙的后果是，如果订阅者在源报告错误后取消订阅，这个错误可能会丢失。源可能会在其观察者上调用 `OnError`，但如果是 Rx 提供的包装器并且与已经处置的订阅相关，它只是忽略了异常。所以最好将提前取消订阅视为本质上是混乱的，有点像中止一个线程：它可以做到，但信息可能会丢失，并且会出现破坏正常异常处理的竞争条件。

简而言之，如果你取消订阅，那么源就没有义务告诉你事情何时停止，在大多数情况下它肯定不会告诉你。

### 订阅生命周期和组合

我们通常结合多个 LINQ 操作符来表达我们在 Rx 中的处理需求。这对订阅生命周期意味着什么？

例如，考虑这个：

```csharp
IObservable<int> source = GetSource();
IObservable<int> filtered = source.Where(i => i % 2 == 0);
IDisposable subscription = filtered.Subscribe(
    i => Console.WriteLine(i),
    error => Console.WriteLine($"OnError: {error}"),
    () => Console.WriteLine("OnCompleted"));
```

我们在由 `Where` 返回的可观测对象上调用 `Subscribe`。当我们这样做时，它反过来会在我们为其调用 `source.Subscribe`（存储在 `source` 变量中）时调用 `Subscribe`。因此这里实际上有一条订阅链。（我们只能访问由 `filtered.Subscribe` 返回的 `IDisposable`，但返回的对象将存储它在调用 `source.Subscribe` 时接收到的 `IDisposable`。）

如果源自行结束（通过调用 `OnCompleted` 或 `OnError`），这将通过链上传递。因此，`source` 将在 `Where` 操作符提供的 `IObserver<int>` 上调用 `OnCompleted`。而后者反过来将在传递给 `filtered.Subscribe` 的 `IObserver<int>` 上调用 `OnCompleted`，而这将引用我们传递的三个方法中的一个，因此它将调用我们的完成处理程序。因此，你可以说 `source` 完成了，它告诉 `filtered` 它已完成，这触发了我们的完成处理程序。 （实际上这是一个非常轻微的简化，因为 `source` 实际上没有告诉 `filtered` 任何事情；它实际上是在与 `filtered` 提供的 `IObserver<T>` 交谈。如果你同时激活了相同的一系列可观测对象的多个订阅，这种区别很重要。但在这种情况下，更简单的描述方法已经足够好，即使它不是绝对精确。）

简而言之，完成从源冒泡到观察者，穿过所有的操作符，并到达我们的处理程序。

如果我们通过调用 `subscription.Dispose()` 提前取消订阅会怎样呢？在这种情况下，一切都是相反的。由 `filtered.Subscribe` 返回的 `subscription` 是第一个知道我们正在取消订阅的，但它随后将调用它在为我们调用 `source.Subscribe` 时收到的对象的 `Dispose`。

无论哪种方式，从源到观察者，包括在两者之间的任何操作符，都会被关闭。

现在我们理解了 `IObservable<T>` 源与接收事件通知的 `IObserver<T>` 接口之间的关系，我们可以看看如何创建一个 `IObservable<T>` 实例来表示我们应用程序中的感兴趣事件。