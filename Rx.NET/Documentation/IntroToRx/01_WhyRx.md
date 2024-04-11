# 第1部分 - 入门

Rx 是一个 .NET 库，用于处理事件流。你为什么会需要它？

## 为什么选择 Rx?

用户需要及时的信息。如果你在等待一个包裹到达，实时报告送货车的进展比一个不确定的2小时送达窗口给你更多的自由。金融应用依赖于持续更新的数据流。我们期待我们的手机和电脑为我们提供各种重要的通知。有些应用程序没有实时信息就无法工作。在线协作工具和多人游戏完全依赖于数据的快速分发和交付。

简而言之，我们的系统需要在有趣的事情发生时做出反应。

实时信息流是计算机系统的一个基本而普遍的元素。尽管如此，它们在编程语言中通常是二等公民。大多数语言通过类似数组的东西支持数据序列，这假设数据已经在内存中准备好让我们的代码在闲暇时读取。如果你的应用处理事件，数组可能适用于历史数据，但它们不是表示应用运行时发生的事件的好方法。尽管流数据是计算中一个相当古老的概念，它往往很笨拙，抽象层面常常通过与我们的编程语言类型系统集成不好的API体现出来。

这是不好的。现场数据对于广泛的应用至关重要。它应该像处理列表、字典和其他集合一样容易。

[.NET 的 Reactive Extensions](https://github.com/dotnet/reactive)（简称 Rx.NET 或 Rx，可通过 [`System.Reactive` NuGet 包](https://www.nuget.org/packages/System.Reactive/) 获取）将实时数据源提升为一等公民。Rx 不需要任何特殊的编程语言支持。它利用 .NET 的类型系统来表示数据流，这种方式使得 C#、F# 和 VB.NET 等 .NET 语言都能像使用集合类型一样自然地工作。

（一个简短的语法插曲：虽然“Reactive Extensions”是复数形式，但当我们将其简化为 Rx.NET 或 Rx 时，我们将其视为单数名词。这是不一致的，但说“Rx are...”听起来很怪。）

例如，C# 提供了集成查询功能，我们可能使用它来找到列表中满足某些标准的所有条目。如果我们有一个 `List<Trade> trades` 变量，我们可能会写这样的代码：

```csharp
var bigTrades =
    from trade in trades
    where trade.Volume > 1_000_000;
```

使用 Rx，我们可以用完全相同的代码处理实时数据。`trades` 变量不再是 `List<Trade>`，而是 `IObservable<Trade>`。`IObservable<T>` 是 Rx 中的基本抽象，本质上是 `IEnumerable<T>` 的实时版本。在这种情况下，`bigTrades` 也将是一个 `IObservable<Trade>`，一个能够立即通知我们所有 `Volume` 超过一百万的交易的实时数据源。

Rx 是一个功能强大的开发工具。它使开发人员能够使用所有 .NET 开发人员熟悉的语言功能来处理实时事件流。它采用了一种声明式的方法，这种方法往往允许我们以更优雅和更少的代码来表示复杂的行为，而不使用 Rx 是不可能的。

Rx 建立在 LINQ (语言集成查询) 之上。这使我们能够使用上面显示的查询语法（或者你可以使用一些 .NET 开发人员更喜欢的显式函数调用方法）。LINQ 在 .NET 中广泛用于数据访问（例如，在 Entity Framework Core 中），但也用于处理内存中的集合（使用 LINQ to Objects），这意味着有经验的 .NET 开发人员将会感到在 Rx 中很自在。关键是，LINQ 是一种高度可组合的设计：你可以以任意组合连接操作符，以直观的方式表达可能很复杂的处理。这种可组合性来自于其设计的数学基础，尽管你可以了解这一方面的 LINQ（如果你愿意），但这并不是一个先决条件：不感兴趣的开发人员可以享受 LINQ 提供者如 Rx 提供的一系列组件可以以无数不同的方式组合在一起，而这一切都能正常工作的事实。

LINQ 已经证明了其在处理非常高的数据量方面的卓越表现。微软在其一些系统的内部实施中广泛使用了 LINQ，包括支持数千万活跃用户的服务。

## 何时使用 Rx 适当？

Rx 设计用于处理事件序列，这意味着它适用于某些场景比其他场景更适合。下面的部分将描述其中的一些场景，以及在不太明显的情况下仍值得考虑使用 Rx 的案例。最后，我们将描述一些可以使用 Rx 的情况，但可能有更好的替代方案的情况。

### 与 Rx 相匹配的好处

Rx 非常适合表示来自代码外部的事件，以及你的应用需要响应的事件，例如：

- 集成事件，如来自消息总线的广播，或来自 WebSockets API 的推送事件，或通过 MQTT 或其他低延迟中间件如 [Azure 事件网格](https://azure.microsoft.com/en-gb/products/event-grid/)、[Azure 事件中心](https://azure.microsoft.com/en-gb/products/event-hubs/) 和 [Azure 服务总线](https://azure.microsoft.com/en-gb/products/service-bus/) 收到的消息，或非供应商特定的表示，如 [cloudevents](https://cloudevents.io/)
- 来自监测装置的遥测数据，如水务设施中流量传感器的数据，或宽带提供商网络设备中的监控和诊断功能
- 来自移动系统的位置数据，如自船舶的 [AIS](https://github.com/ais-dotnet/) 消息，或汽车遥测数据
- 操作系统事件，如文件系统活动或 WMI 事件
- 道路交通信息，如事故或平均速度变化的通知
- 与 [复杂事件处理 (CEP)](https://en.wikipedia.org/wiki/Complex_event_processing) 引擎集成
- UI 事件，如鼠标移动或按钮点击

Rx 也是建模域事件的好方法。这些可能是由上述事件引起的，但在处理它们之后，产生更直接代表应用概念的事件。这可能包括：

- 域对象上的属性或状态更改，如“订单状态更新”或“注册接受”
- 域对象集合的更改，如“新注册创建”

事件还可以代表从传入事件（或稍后分析的历史数据）中得出的洞察，例如：

- 一位宽带客户可能无意中成为 DDoS 攻击的参与者
- 两艘远洋船只从事与非法活动通常相关的移动模式（例如，长时间紧密并行移动，足以转移货物或人员，同时在公海上）
- [CNC](https://en.wikipedia.org/wiki/Numerical_control) [铣床](https://en.wikipedia.org/wiki/Milling_(machining)) MFZH12 的第4轴轴承磨损速度显著高于标称剖面
- 如果用户想及时到达城市另一边的会议，当前的交通状况表明他们应该在接下来的10分钟内出发

这三组示例展示了应用程序可能如何逐步提升信息的价值，从原始事件开始，然后增强以产生特定于域的事件，最后进行分析以产生应用程序用户真正关心的通知。每个处理阶段都会增加传出消息的价值。每个阶段通常也会减少消息的量。如果我们将第一类的原始事件直接呈现给用户，他们可能会被消息的数量所压倒，这使得辨识重要事件变得不可能。但如果我们只在我们的处理检测到重要内容时通知他们，这将使他们能够更有效和准确地工作，因为我们已经大大提高了信噪比。

[`System.Reactive` 库](https://www.nuget.org/packages/System.Reactive) 提供了构建这种增值过程的工具，通过该过程，我们可以驯服高容量的原始事件来源以产生高价值、实时、可操作的洞察。它提供了一套操作符，使我们的代码能够声明性地表达这种处理，如你将在随后的章节中看到。

Rx 也非常适合引入和管理并发，以便于_卸载_。
也就是说，同时执行一组给定的工作，以便检测到事件的线程不必也是处理该事件的线程。
其中一个非常流行的用途是维持一个响应式 UI。 (UI 事件处理已经成为 Rx 在 .NET 中（以及在 [RxJS](https://rxjs.dev/) 中，它起源于 Rx.NET ——这是非常流行的使用，你可能会以为这就是它的用途。但它在这方面的成功不应该让我们对它更广泛的适用性视而不见。)

如果您有一个现有的 `IEnumerable<T>`，它试图模拟实时事件。
虽然 `IEnumerable<T>` _可以_ 通过像 `yield return` 这样的延迟评估来模拟数据运动，但存在一个问题。如果使用集合的代码已经到了需要下一个项的地步（例如，因为 `foreach` 循环刚刚完成一个迭代），但是还没有可用的项，`IEnumerable<T>` 实现别无选择，只能在其 `MoveNext` 中阻塞调用线程，直到数据可用为止，这 在某些应用程序中会导致可扩展性问题。即使在线程阻塞是可以接受的情况下（或者如果您使用更新的 `IAsyncEnumerable<T>`，它可以利用 C# 的 `await foreach` 功能在这些情况下避免阻塞线程），`IEnumerable<T>` 和 `IAsyncEnumerable<T>` 是用来代表实时信息来源的误导类型。这些接口代表了一种“拉”编程模型：代码请求序列中的下一个项。对于自然产生信息的信息来源，Rx 是一种更自然的选择模型。

### 可能适合使用 Rx 的情况

Rx 可用于表示异步操作。.NET 的 `Task` 或 `Task<T>` 实际上代表一个单一事件，而 `IObservable<T>` 可以被认为是这种情况的一种泛化，即事件序列。（可以说，`Task<int>` 与 `IObservable<int>` 的关系类似于 `int` 与 `IEnumerable<int>` 的关系。）

这意味着有些情况可以使用任务和 `async` 关键词来处理，也可以通过 Rx 来处理。如果在处理过程中的任何时刻你需要同时处理多个值和单个值，Rx 可以同时处理；任务则不擅长处理多个项目。你可以有一个 `Task<IEnumerable<int>>`，这使你能够 `await` 一个集合，如果集合中的所有项目可以一步获取，那么这很好。但这种方式的限制是，一旦任务产生了它的 `IEnumerable<int>` 结果，你的 `await` 就完成了，你就回到了对该 `IEnumerable<int>` 的非异步迭代。如果数据不能一步获取—可能 `IEnumerable<int>` 代表的是每次获取 100 项的 API 中的数据—它的 `MoveNext` 将不得不在每次需要等待时阻塞你的线程。

在 Rx 存在的最初 5 年中，将不一定立即可用的集合表示为 Rx 可能是最佳方式。然而，通过在 .NET Core 3.0 和 C# 8 中引入 [`IAsyncEnumerable<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1)，我们提供了一种在 `async`/`await` 的世界中处理序列的方法（[`Microsoft.Bcl.AsyncInterfaces` NuGet 包](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces/) 使这在 .NET Framework 和 .NET Standard 2.0 上可用）。因此，现在选择使用 Rx 的决定往往归结为一个“拉”模型（通过 `foreach` 或 `await foreach` 示例）或一个“推”模型（其中代码提供由事件源在项目可用时调用的回调）更适合正在建模的概念。

.NET 自 Rx 首次出现以来加入的另一个相关特性是 [channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)。这些允许一个源产生对象并由一个消费者来处理它们，因此有一个与 Rx 明显的表面相似的特性。然而，Rx 的一个区别特征是它通过一组广泛的操作符支持组合，这是 channels 没有的直接等价物。另一方面，channels 提供了更多适应生产和消费率变化的选项。

之前我提到了 _卸载_：使用 Rx 将工作推送到其他线程。尽管这种技术可以使 Rx 引入和管理并发，以达到 _扩展_ 或进行 _并行_ 计算的目的，但其他专用框架，如 [TPL（任务并行库）Dataflow](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) 或 [PLINQ](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/introduction-to-plinq)，更适合执行并行计算密集型工作。然而，TPL Dataflow 通过其 [`AsObserver`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.dataflow.dataflowblock.asobserver) 和 [`AsObservable`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.dataflow.dataflowblock.asobservable) 扩展方法提供了与 Rx 的某种集成。因此，将 TPL Dataflow 与你的应用其他部分集成的常见方式是使用 Rx。

### 不适合使用 Rx 的情况

Rx 的 `IObservable<T>` 不是 `IEnumerable<T>` 或 `IAsyncEnumerable<T>` 的替代品。将本质上是基于拉动的东西强制变成基于推送的做法是一个错误。

此外，有些情况下，Rx 的编程模型的简单性可能对你不利。例如，一些消息队列技术，如 MSMQ，从定义上来看是顺序的，并且可能看起来很适合 Rx。然而，它们通常是因为它们的事务处理支持而被选择的。Rx 没有直接的方式来表面事务语义，因此在需要这种事务的场景中，你可能更好地直接使用相关技术的 API 工作。（不过，[Reaqtor](https://reaqtive.net/) 为 Rx 增加了持久性和持续性，所以你可能能够使用它将这类队列系统与 Rx 集成。）

选择最适合工作的工具可以使你的代码更易于维护，很可能提供更好的性能，并且你可能会获得更好的支持。

## Rx 实际运用

你可以很快开始一个简单的 Rx 示例。如果你安装了 .NET SDK，你可以在命令行运行以下命令：

```ps1
mkdir TryRx
cd TryRx
dotnet new console
dotnet add package System.Reactive
```

或者，如果你安装了 Visual Studio，创建一个新的 .NET 控制台项目，然后使用 NuGet 包管理器添加对 `System.Reactive` 的引用。

此代码创建一个可观察源（`ticks`）每秒产生一次事件。代码还向该源传递一个处理程序，为每个事件在控制台上写入消息：

```csharp
using System.Reactive.Linq;

IObservable<long> ticks = Observable.Timer(
    dueTime: TimeSpan.Zero,
    period: TimeSpan.FromSeconds(1));

ticks.Subscribe(
    tick => Console.WriteLine($"Tick {tick}"));

Console.ReadLine();
```

如果这看起来不是很激动人心，那是因为它是可以创建的基本示例之一，而 Rx 的核心就是一个非常简单的编程模型。其力量来自组合——我们可以使用 `System.Reactive` 库中的构建块来描述将我们从原始、低层级的事件带到高价值洞察的处理过程。但要做到这一点，我们必须首先了解[Rx 的关键类型 `IObservable<T>` 和 `IObserver<T>`](02_KeyTypes.md)。