# 聚合

数据未经处理并非总是易于操作。有时候，我们需要归纳、整理、组合或压缩接收到的海量数据。这可能只是为了将数据量降低到一个可管理的水平。例如，考虑一下来自仪表、金融、信号处理和运营智能领域的快速移动数据。这种数据的变化速率可能每秒超过十个值，如果我们在观测多个源，则变化速率可能更高。人类真的能消化这么快的数据吗？对于人类消费者而言，平均值、最小值和最大值等聚合值可能更有用。

我们常常可以实现更多。我们组合和关联的方式可能使我们能够揭示模式，提供单个消息或者单一统计度量无法提供的洞察。Rx的可组合性使我们能够对数据流进行复杂和微妙的计算，这不仅可以减少用户需要处理的消息量，还可以增加每条消息给用户带来的价值。

我们将从最简单的聚合函数开始，这些聚合函数以某种特定方式将可观察序列简化成具有单个值的序列。然后我们将继续学习更通用的操作符，让你可以定义自己的聚合机制。

## 简单的数值聚合

Rx支持多种标准LINQ操作符，这些操作符将序列中的所有值归纳为一个数字结果。

### Count

`Count` 告诉你一个序列包含多少个元素。虽然这是一个标准的LINQ操作符，但Rx的版本与 `IEnumerable<T>` 的版本有所不同，因为Rx会返回一个可观察序列，而不是一个标量值。通常情况下，这是由于Rx的推送相关性质。Rx的 `Count` 不能要求其源立即提供所有元素，所以只能等到源表示已完成。`Count` 返回的序列始终是 `IObservable<int>` 类型，不管源的元素类型是什么。以下是使用 `Count` 与 `Range` 的例子，因为 `Range` 尽可能快地生成所有值，然后完成，这意味着我们可以立即从 `Count` 得到结果：

```csharp
IObservable<int> numbers = Observable.Range(0,3);
numbers.Count().Dump("count");
```

输出：

```
count-->3
count Complete
```

如果你预期你的序列会有超过32位有符号整数能够计数的值，你可以使用 `LongCount` 操作符。这与 `Count` 相同，但它返回一个 `IObservable<long>`。

### Sum

`Sum` 操作符将其源中的所有值相加，生成总和作为唯一输出。与 `Count` 一样，Rx的 `Sum` 与大多数其他LINQ提供程序不同，它不产生标量作为其输出。它产生一个什么也不做的可观测序列，直到其源完成。当源完成时，`Sum` 返回的可观测对象会产生一个值，然后立即完成。以下示例展示了它的使用：

```csharp
IObservable<int> numbers = Observable.Range(1,5);
numbers.Sum().Dump("sum");
```

输出显示 `Sum` 产生的单个结果：

```
sum-->15
sum completed
```

`Sum` 只能处理 `int`、`long`、`float`、`double`、`decimal` 或这些类型的可空版本。这意味着有些你可能期望能够 `Sum` 的类型却不能。例如，在 `System.Numerics` 命名空间中的 `BigInteger` 类型表示只受可用内存限制的整数值，以及你准备等待它进行计算的时间。（即使在有数百万位的数字上，基本算术也非常慢。）你可以使用 `+` 将这些数字加在一起，因为该类型为该操作符定义了一个重载。但 `Sum` 历史上无法找到它。[C# 11.0中引入的通用数学](https://learn.microsoft.com/en-us/dotnet/standard/generics/math#operator-interfaces)意味着技术上有可能引入一个版本的 `Sum`，适用于任何实现了 [`IAdditionOperators<T, T, T>`](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.iadditionoperators-3) 的类型 `T`。然而，这将意味着依赖于 .NET 7.0 或更高版本（因为旧版本中没有通用数学），而在撰写本文时，Rx 支持 .NET 7.0 通过其 `net6.0` 目标。它可以引入一个单独的 `net7.0` 和/或 `net8.0` 目标来启用这一功能，但尚未这样做。（公平来说，[`LINQ to Objects 中的 Sum` 也还没有支持这一点](https://github.com/dotnet/runtime/issues/64031)。）

如果你为 `Sum` 提供这些类型的可空版本（例如，你的源是一个 `IObservable<int?>`），那么 `Sum` 也会返回一个具有可空项目类型的序列，并且如果任何输入值为 `null`，它将产生 `null`。

尽管 `Sum` 只能处理一小部分固定列表的数值类型，你的源不一定要产生这些类型的值。`Sum` 提供了接受 lambda 的重载，该 lambda 从每个输入元素中提取适当的数值。例如，假设你想回答以下不太可能的问题：如果接下来10艘恰好通过AIS广播自身描述的船只并排放置，它们是否都能适应某个特定宽度的通道？我们可以通过过滤提供船只尺寸信息的AIS消息，使用 `Take` 收集接下来的10条此类消息，然后使用 `Sum`。Ais.NET库的 `IVesselDimensions` 接口没有实现加法（即使实现了，我们刚刚看到 Rx 也无法利用它），但没关系：我们所需要的只是提供一个 lambda，可以取一个 `IVesselDimensions` 并返回 `Sum` 可处理的某种数值类型：

```csharp
IObservable<IVesselDimensions> vesselDimensions = receiverHost.Messages
    .OfType<IVesselDimensions>();

IObservable<int> totalVesselWidths = vesselDimensions
    .Take(10)
    .Sum(dimensions => 
            checked((int)(dimensions.DimensionToPort + dimensions.DimensionToStarboard)));
```

（如果你想知道为什么这里使用类型转换和 `checked` 关键字，AIS 将这些值定义为无符号整数，因此 Ais.NET 库将它们报告为 `uint`，这不是 Rx 的 `Sum` 支持的类型。实际上，船只的宽度不太可能足以溢出一个32位有符号整数，所以我们只是将其转换为 `int`，并且在我们遇到超过21亿米宽的船只时，`checked` 关键字将抛出异常。）

### Average

标准LINQ操作符 `Average` 实际上计算的是 `Sum` 会计算的值，然后用 `Count` 会计算的值来除它。再次强调的是，虽然大多数LINQ实现会返回标量，但Rx的 `Average` 生成的是可观察的。

尽管 `Average` 可以处理与 `Sum` 相同的数值类型，但在某些情况下，输出类型将不同。如果来源是 `IObservable<int>`，或者你使用了一个采用 lambda 的重载，该 lambda 从来源提取值，并且 lambda 返回一个 `int`，则结果将是一个 `double`。这是因为一组整数的平均值不一定是一个整数。类似地，计算 `long` 值的平均值会产生一个 `double`。然而，`decimal` 类型的输入会产生 `decimal` 类型的输出，同样，`float` 输入会产生 `float` 输出。

就像 `Sum` 一样，如果 `Average` 的输入是可空的，输出也会是如此。

### Min 和 Max

Rx实现了标准LINQ `Min` 和 `Max` 操作符，这些操作符找到具有最高或最低值的元素。与本节中的所有其他操作符一样，这些操作符不返回标量，而是返回产生单个值的 `IObservable<T>`。

Rx为 `Sum` 和 `Average` 支持的相同数值类型定义了专门的实现。然而，与这些操作符不同的是，它还定义了一个重载，将接受任何类型的源项目。当你在Rx未定义专门实现的源类型上使用 `Min` 或 `Max` 时，它使用 [`Comparer<T>.Default`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.comparer-1.default) 来比较项目。还有一个重载允许你传递一个比较器。

与 `Sum` 和 `Average` 一样，有重载接受回调。如果你使用这些重载，`Min` 和 `Max` 将为每个源项目调用此回调，并将寻找你的回调返回的最低或最高值。请注意，它们最终产生的单一输出将是回调返回的值，而不是生成该值的原始源项目。看看这个例子：

```csharp
IObservable<int> widthOfWidestVessel = vesselDimensions
    .Take(10)
    .Max(dimensions => 
            checked((int)(dimensions.DimensionToPort + dimensions.DimensionToStarboard)));
```

`Max` 在这里返回一个 `IObservable<int>`，它将是接下来10条报告船只尺寸的消息中最宽船只的宽度。但如果你不只是想看宽度呢？如果你想要整个消息怎么办？

## MinBy 和 MaxBy

Rx提供了 `Min` 和 `Max` 上的两个微妙变体：`MinBy` 和 `MaxBy`。这些与我们刚刚看到的基于回调的 `Min` 和 `Max` 类似，它们使我们能够处理不是数值的元素序列，但可能具有数值属性。区别在于，它们不返回最小值或最大值，而是告诉你哪个源元素产生了该值。例如，假设我们不仅要发现最宽船的宽度，还要知道那实际上是哪艘船：

```csharp
IObservable<IVesselDimensions> widthOfWidestVessel = vesselDimensions
    .Take(10)
    .MaxBy(dimensions => 
              checked((int)(dimensions.DimensionToPort + dimensions.DimensionToStarboard)));
```

这与前一节中的示例非常相似。我们正在处理元素类型为 `IVesselDimensions` 的序列，因此我们提供了一个回调来提取用于比较目的的值。(实际上是上次的相同回调。)就像 `Max` 一样，`MaxBy` 正试图找出哪个元素在传递给这个回调时产生最高值。在来源完成之前，它无法知道哪个是最高的。如果来源尚未完成，它所能知道的只是到目前为止的最高值，但这可能会被未来的值超过。因此，与本章中我们所看到的所有其他操作符一样，这将在来源完成之前不产生任何内容，这就是为什么我在这里放了一个 `Take(10)`。

然而，我们得到的序列类型不同。`Max` 返回一个 `IObservable<int>`，因为它为源中的每个项目调用回调，然后产生我们的回调返回的最高值。但是 `MaxBy` 返回一个 `IObservable<IVesselDimensions>`，因为 `MaxBy` 告诉我们哪个源元素产生了那个值。

当然，可能有不止一个项目具有最大宽度——例如，可能有三艘相同大小的船。对于 `Max` 来说，这无关紧要，因为它只试图返回实际的值：有多少源项目具有最大值并不重要，因为所有情况下的值都是相同的。但是对于 `MaxBy`，我们得到的是产生最大值的原始项目，如果有三个都做到了这一点，我们不希望 Rx 任意挑选其中一个。


因此，与我们迄今为止看到的其他聚合操作符不同，`MinBy` 或 `MaxBy` 返回的可观察对象不一定只产生一个值。它可能产生几个。你可能会问，它真的是聚合操作符吗，既然它没有将输入序列简化为一个输出？但它确实将其简化为一个值：回调返回的最小值（或最大值）。它只是以稍微不同的方式呈现结果。它根据聚合过程的结果生成一个序列。你可以将其视为聚合和过滤的组合：它执行聚合以确定最小或最大值，然后将源序列过滤到只有回调产生该值的元素。

**注意**：LINQ to Objects 也定义了 [`MinBy`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.minby) 和 [`MaxBy`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.maxby) 方法，但它们有所不同。这些 LINQ to Objects 版本实际上会随意挑选一个源元素——如果有多个源值都产生最小（或最大）结果，LINQ to Objects 只给你一个，而 Rx 给你所有的。Rx 在 .NET 6.0 添加 LINQ to Objects 同名方法多年前就定义了这些操作符的版本，所以如果你想知道为什么 Rx 做法不同，真正的问题是，为什么 LINQ to Objects 没有遵循 Rx 的先例。

## 简单的布尔聚合

LINQ 定义了几个将整个序列简化为单个布尔值的标准操作符。

### Any

`Any` 操作符有两种形式。无参数重载实际上在询问“这个序列中有任何元素吗？”它返回一个可观察序列，如果源在未发出任何值的情况下完成，则会生成 `false` 值。如果源确实产生了一个值，那么当第一个值产生时，结果序列将立即产生 `true` 然后完成。如果它第一个接收到的通知是错误，那么它会将该错误传递下去。

```csharp
var subject = new Subject<int>();
subject.Subscribe(Console.WriteLine, () => Console.WriteLine("Subject completed"));
var any = subject.Any();

any.Subscribe(b => Console.WriteLine("The subject has any values? {0}", b));

subject.OnNext(1);
subject.OnCompleted();
```

输出：

```
1
The subject has any values? True
subject completed
```

如果我们现在删除 OnNext(1)，输出将变为以下内容

```
subject completed
The subject has any values? False
```

在源确实产生值的情况下，`Any` 会立即从源取消订阅。因此，如果源想要报告错误，`Any` 只会在它是第一个产生的通知时看到这个错误。

```csharp
var subject = new Subject<int>();
subject.Subscribe(Console.WriteLine,
    ex => Console.WriteLine("subject OnError : {0}", ex),
    () => Console.WriteLine("Subject completed"));

IObservable<bool> any = subject.Any();

any.Subscribe(b => Console.WriteLine("The subject has any values? {0}", b),
    ex => Console.WriteLine(".Any() OnError : {0}", ex),
    () => Console.WriteLine(".Any() completed"));

subject.OnError(new Exception());
```

输出：

```
subject OnError : System.Exception: Exception of type 'System.Exception' was thrown.
.Any() OnError : System.Exception: Exception of type 'System.Exception' was thrown.
```

但是，如果源先生成一个值再触发异常，例如：

```csharp
subject.OnNext(42);
subject.OnError(new Exception());
```

我们会看到这样的输出：

```
42
The subject has any values? True
.Any() completed
subject OnError : System.Exception: Exception of type 'System.Exception' was thrown.
```

虽然直接订阅源主题的处理程序仍然看到错误，我们的 `any` 观察对象报告了一个 `True` 的值然后完成了，这意味着它没有报告随后发生的错误。

`Any` 方法还有一个接受谓词的重载。这实际上在问一个稍微不同的问题：“序列中是否有满足这些条件的任何元素？” 其效果类似于使用 `Where` 后跟无参数形式的 `Any`。

```csharp
IObservable<bool> any = subject.Any(i => i > 2);
// 功能等同于 
IObservable<bool> longWindedAny = subject.Where(i => i > 2).Any();
```

### All

`All` 操作符类似于带有谓词的 `Any` 方法，除了所有值都必须满足谓词。只要谓词拒绝一个值，`All` 返回的可观察对象就会产生一个 `false` 值然后完成。如果源在不产生任何不满足谓词的元素的情况下达到其结束，那么 `All` 将推送 `true` 作为其值。（此情况的一个结果是，如果你在空序列上使用 `All`，结果将是产生 `true` 的序列。这与其他 LINQ 提供者中的 `All` 工作方式一致，但对于不熟悉所谓 [真空真理](https://en.wikipedia.org/wiki/Vacuous_truth) 的正式逻辑约定的人来说，这可能会令人惊讶。）

一旦 `All` 决定产生一个 `false` 值，它会立即从源取消订阅（就像 `Any` 一旦确定它可以产生 `true` 就会这样做）。如果在此之前源产生了错误，错误将被传递给 `All` 方法的订阅者。

```csharp
var subject = a new Subject<int>();
subject.Subscribe(Console.WriteLine, () => Console.WriteLine("Subject completed"));
IEnumerable<bool> all = subject.All(i => i < 5);
all.Subscribe(b => Console.WriteLine($"All values less than 5? {b}"));

subject.OnNext(1);
subject.OnNext(2);
subject.OnNext(6);
subject.OnNext(2);
subject.OnNext(1);
subject.OnCompleted();
```

输出：

```
1
2
6
All values less than 5? False
all completed
2
1
subject completed
```

### IsEmpty

LINQ 的 `IsEmpty` 操作符在逻辑上与无参数的 `Any` 方法相反。仅当源在未产生任何元素的情况下完成时，它才返回 `true`。如果源产生了一个项目，`IsEmpty` 会产生 `false` 并立即取消订阅。如果源产生了一个错误，这个错误会向前传递。

### Contains

`Contains` 操作符确定序列中是否存在特定元素。你可以使用 `Any` 实现它，只需提供一个回调，将每个项目与你正在查找的值进行比较。然而，直接写 `Contains` 通常会更简洁，并且可能更直接地表达意图。

```csharp
var subject = new Subject<int>();
subject.Subscribe(
    Console.WriteLine, 
    () => Console.WriteLine("Subject completed"));

IEnumerable<bool> contains = subject.Contains(2);

contains.Subscribe(
    b => Console.WriteLine("Contains the value 2? {0}", b),
    () => Console.WriteLine("contains completed"));

subject.OnNext(1);
subject.OnNext(2);
subject.OnNext(3);
    
subject.OnCompleted();
```

输出：

```
1
2
Contains the value 2? True
contains completed
3
Subject completed
```

`Contains` 还有一个重载，允许你指定除类型默认以外的 `IEqualityComparer<T>` 实现。如果你有一个自定义类型的序列，可能会根据用例有一些特殊的相等规则，这会非常有用。

## 构建你自己的聚合

如果前面章节中描述的内置聚合不满足你的需求，你可以构建自己的聚合。Rx 提供两种不同的方式来实现这一点。

### Aggregate

`Aggregate` 方法非常灵活：它可以构建本章中所示的任何操作符。你为它提供一个函数，它会为每个元素调用这个函数。但它不仅将元素传递给你的函数：它还提供了一种方式让你的函数 _聚合_ 信息。除了当前元素，它还传入一个 _累加器_。累加器可以是任何类型，这取决于你希望累积的信息类型。你的函数返回的任何值都将成为新的累加器值，并且它将与源的下一个元素一起传递给该函数。这里有几种变体，但最简单的重载如下：

```csharp
IObservable<TSource> Aggregate<TSource>(
    this IObservable<TSource> source, 
    Func<TSource, TSource, TSource> accumulator)
```

如果你想为 `int` 值产生自己的 `Count` 版本，你可以提供一个函数，只需每个源产生的值加1：

```csharp
IObservable<int> sum = source.Aggregate((acc, element) => acc + 1);
```

为了更准确地理解这是如何处理的，让我们看看 `Aggregate` 如何调用这个 lambda。为了更容易看到，假设我们将这个 lambda 放在自己的变量中：

```csharp
Func<int, int, int> c = (acc, element) => acc + 1;
```

现在假设源产生了一个值 100。`Aggregate` 会调用我们的函数：

```csharp
c(0, 100) // 返回 1
```

第一个参数是当前的累加器。`Aggregate` 使用了 `default(int)` 作为初始累加器值，即 0。我们的函数返回 1，这成为新的累加器值。因此，如果源产生第二个值，比如 200，`Aggregate` 将使用新的累加器以及来自源的第二个值：

```csharp
c(1, 200) // 返回 2
```

这个特定的函数完全忽略了它的第二个参数（源的元素）。它只是每次将累加器加一。因此，累加器仅仅是我们的函数被调用次数的记录。

现在，让我们看看如何使用 `Aggregate` 实现 `Sum`：

```csharp
Func<int, int, int> s = (acc, element) => acc + element;
IObservable<int> sum = source.Aggregate(s);
```

对于第一个元素，`Aggregate` 会再一次传递我们选择的累加器类型的默认值 `int`：0。它还会传递第一个元素值。所以如果第一个元素是 100，它会这样做：

```csharp
s(0, 100) // 返回 100
```

然后如果第二个元素是 200，`Aggregate` 将进行此调用：

```csharp
s(100, 200) // 返回 300
```

注意，这次第一个参数是 100，因为这是上一次调用 `s` 返回的值。因此，在这种情况下，看到元素 100 和 200 后，累加器的值是 300，这是所有元素的总和。

如果我们希望初始累加器值不是 `default(TAccumulator)`，有一个重载可以实现。例如，这里是如何使用 `Aggregate` 实现类似于 `All` 的东西：

```csharp
IObservable<bool> all = source.Aggregate(true, (acc, element) => acc && element);
```

顺便说一句，这与真正的 `All` 并不完全相同：它对错误的处理方式不同。`All` 一旦看到一个元素是 `false` 就会立即从其源取消订阅，因为它知道源产生的其他任何东西都不可能改变结果。这意味着，如果源即将产生错误，它将不再有机会这样做，因为 `All` 取消了订阅。但是 `Aggregate` 没有办法知道累加器是否已经进入了一个永远无法离开的状态，所以它将一直订阅源，直到源完成（或直到订阅 `IObservable<T>` 的代码取消订阅为止）。这意味着，如果源先产生 `true`，然后是 `false`，`Aggregate` 会与 `All` 不同，仍然订阅源，所以如果源继续调用 `OnError`，`Aggregate` 会收到这个错误，并将其传递给它的订阅者。

这是一些人发现有用的关于 `Aggregate` 的思考方式。如果你的源产生了 1 到 5 的值，而我们传递给 `Aggregate` 的函数被称为 `f`，那么一旦源完成，`Aggregate` 产生的值将是这样的：

```csharp
T result = f(f(f(f(f(default(T), 1), 顺序=headers]))), 5);
```

因此，在我们重新创建 `Count` 的情况下，累加器类型是 `int`，变成了：

```csharp
int sum = s(s(s(s(s(0, 1), 2), 3), 4), 5);
// 注意：Aggregate 不会直接返回这个 -
// 它返回一个 IObservable<int>，这个值产生这个值。
```

Rx 的 `Aggregate` 不会一次执行所有这些调用：它在源产生每个元素时调用函数，因此计算将分散在一段时间内。如果你的回调是一个_纯函数_——一个不受全局变量和其他环境因素影响的函数，且对于任何特定的输入总是返回相同结果的函数——这无关紧要。`Aggregate` 的结果将与它一直在一个大表达式中进行的情况相同。但如果你的回调行为受到全局变量或文件系统的当前内容等因素的影响，那么它在源产生每个值时被调用的事实可能更为重要。

顺便说一下，`Aggregate` 在某些编程系统中被称为 `reduce`。它也常被称为_折叠_。（具体来说是_左折叠_。右折叠是反向进行的。按照惯例，它的函数接受反向顺序的参数，因此看起来像 `s(1, s(2, s(3, s(4, s(5, 0)))))`。Rx 没有提供内置的右折叠。它不是自然的选择，因为它必须等到接收到最后一个元素才能开始，这意味着它需要保存整个序列中的每个元素，然后在序列完成时一次计算整个折叠。)

你可能已经注意到，在我追求用 `Aggregate` 重新实现一些内置的聚合操作符的过程中，我直接从 `Sum` 跳到 `Any`。`Average` 怎么样呢？事实证明，我们不能用到目前为止向你展示的重载来完成。这是因为 `Average` 需要累积两个信息——总和和计数——它还需要在最后执行一次最终步骤：它需要将总和除以计数。利用到目前为止所示的重载，我们只能完成一部分：

```csharp
IObservable<int> nums = Observable.Range(1, 5);

IObservable<(int Count, int Sum)> avgAcc = nums.Aggregate(
    (Count: 0, Sum: 0),
    (acc, element) => (Count: acc.Count + 1, Sum: acc.Sum + element));
```

此处使用一个元组作为累加器，使其能够累积两个值：计数和总和。但最终的累加器值成为结果，这不是我们想要的。我们缺少通过除以计数来计算平均值的最后一步。幸运的是，`Aggregate` 提供了第三个重载，使我们能够提供这个最后的步骤。我们传递第二个回调，当来源完成时将被调用一次。`Aggregate` 将最终的累加器值传递给这个 lambda，然后它返回的任何东西都会成为 `Aggregate` 返回的可观察对象产生的单个项目。

```csharp
IObservable<double> avg = nums.Aggregate(
    (Count: 0, Sum: 0),
    (acc, element) => (Count: acc.Count + 1, Sum: acc.Sum + element),
    acc => ((double) acc.Sum) / acc.Count);
```

我展示了如何使用 `Aggregate` 重新实现一些内置的聚合操作符，以说明它是一个功能强大且非常通用的操作符。然而，我们使用它的原因并不是这个。`Aggregate` 之所以有用，是因为它让我们定义自定义聚合。

例如，假设我想要构建一个列表，列出通过AIS广播其详细信息的所有船只的名称。这里有一种方法可以做到这一点：

```csharp
IObservable<IReadOnlySet<string>> allNames = vesselNames
    .Take(10)
    .Aggregate(
        ImmutableHashSet<string>.Empty,
        (set, name) => set.Add(name.VesselName));
```

我在这里使用了 `ImmutableHashSet<string>`，因为它的使用模式恰好很好地适应了 `Aggregate`。一个普通的 `HashSet<string>` 也可以工作，但使用起来稍微不那么方便，因为它的 `Add` 方法不返回集合，所以我们的函数需要额外的语句来返回累积的集合：

```csharp
IObservable<IReadOnlySet<string>> allNames = vesselNames
    .Take(10)
    .Aggregate(
        new HashSet<string>(),
        (set, name) =>
        {
            set.Add(name.VesselName);
            return set;
        });
```

无论是哪种实现，`vesselNames` 都会生成一个值，这是一个 `IReadOnlySet<string>`，包含前10条报告名字的消息中看到的每个船只的名字。

我在这两个最后的例子中已经简化了一个问题。我让它们在首次出现的10条合适的消息中工作。想想如果我没有在那里加入 `Take(10)` 会发生什么。代码会编译，但我们会遇到一个问题。我在各种示例中使用的AIS消息源是一个无休止的源。船只将在可预见的未来继续在海洋中移动。Ais.NET库没有包含任何代码来检测文明的终结，或技术的发明将使使用船只过时，所以它永远不会对其订阅者调用 `OnCompleted`。`Aggregate` 返回的可观测对象在其源完成或失败之前不会报告任何内容。所以如果我们去掉那个 `Take(10)`，行为将与 `Observable.Never<IReadOnlySet<string>>` 相同。我不得不强制输入到 `Aggregate` 的结束才使它产生一些东西。但还有另一种方式。

### Scan

虽然 `Aggregate` 允许我们将完整序列简化为单个最终值，但有时这不是我们需要的。如果我们正在处理一个无尽的源，我们可能需要类似于实时总计的东西，每次接收到值时都会更新。`Scan` 操作符正是为这种需求设计的。`Scan` 和 `Aggregate` 的签名相同；区别在于 `Scan` 不会等待其输入的结束。它在每个项目之后都会生成聚合值。

我们可以使用它来建立船只名称集合，就像前一节中所述，但使用 `Scan` 我们不必等到结束。这将在每次接收到包含名称的消息时报告当前列表：

```csharp
IObservable<IReadOnlySet<string>> allNames = vesselNames
    .Scan(
        ImmutableHashSet<string>.Empty,
        (set, name) => set.Add(name.VesselName));
```

请注意，即使没有变化，这个 `allNames` 可观测对象也会产生元素。如果累积的名称集已经包含刚从 `vesselNames` 中产生的名称，对 `set.Add` 的调用将不起作用，因为该名称已经在集合中了。但 `Scan` 为每个输入产生一个输出，并不关心累加器是否没有改变。这是否重要将取决于你打算如何使用这个 `allNames` 可观测对象，但如果需要，你可以很容易地用[第五章中展示的 `DistinctUntilChanged` 操作符](05_Filtering.md#distinct-and-distinctuntilchanged)修复这个问题。

你可以将 `Scan` 视为是展示其工作过程的 `Aggregate` 版本。如果我们想看到计算平均值的过程如何累积计数和总和，我们可以写下这个：

```csharp
IObservable<int> nums = Observable.Range(1, 5);

IObservable<(int Count, int Sum)> avgAcc = nums.Scan(
    (Count: 0, Sum: 0),
    (acc, element) => (Count: acc.Count + 1, Sum: acc.Sum + element));

avgAcc.Dump("acc");
```

这产生了这样的输出：

```
acc-->(1, 1)
acc-->(2, 3)
acc-->(3, 6)
acc-->(4, 10)
acc-->(5, 15)
acc completed
```

你可以清楚地看到 `Scan` 在源每次产生值时都发出当前累积值。

与 `Aggregate` 不同，`Scan` 不提供一个采用第二个函数来转换累加器到结果的重载。因此我们在这里可以看到包含计数和总和的元组，但不是我们想要的实际平均值。但我们可以通过使用[变换章节中描述的 `Select`](06_Transformation.md#select) 操作符来实现这一点：

```csharp
IObservable<double> avg = nums.Scan(
    (Count: 0, Sum: 0),
    (acc, element) => (Count: acc.Count + 1, Sum: acc.Sum + element))
    .Select(acc => ((double) acc.Sum) / acc.Count);

avg.Dump("avg");
```

现在我们得到了这样的输出：

```
avg-->1
avg-->1.5
avg-->2
avg-->2.5
avg-->3
avg completed
```

`Scan` 是比 `Aggregate` 更一般化的操作符。你可以通过将 `Scan` 与[过滤章节中描述的 `TakeLast()` 操作符](05_Filtering.md#takelast) 结合使用来实现 `Aggregate`。

```csharp
source.Aggregate(0, (acc, current) => acc + current);
// 等效于
source.Scan(0, (acc, current) => acc + current).TakeLast();
```

聚合是用于减少数据量或结合多个元素以产生平均值或其他包含多个元素信息的度量的一种有用方法。但是，要执行某些类型的分析，我们还需要在计算聚合值之前对我们的数据进行切割或以其他方式重新组织。因此，在下一章中，我们将探讨 Rx 提供的用于分区数据的各种机制。
