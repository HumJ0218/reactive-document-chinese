# 离开Rx的世界

可观察序列是一个有用的结构，尤其是当我们使用LINQ来组合复杂查询时。尽管我们认识到可观察序列的好处，有时我们必须离开`IObservable<T>`范式。当我们需要与现有的非Rx基础API（例如使用事件或`Task<T>`的API）集成时，这是必要的。如果您发现这样做更便于测试，或者您可能发现在可观察范式与更熟悉的范式之间来回移动，更容易学习Rx。

Rx的组合性是其力量的关键，但当你需要与不理解Rx的组件集成时，它可能看起来像一个问题。到目前为止，我们已经看到的大多数Rx库功能都将其输入和输出表达为可观察对象。你怎样才能把现实世界中的事件源转变为一个可观察对象呢? 你应该如何对可观察对象的输出做出有意义的处理呢?

你已经看到了这些问题的一些答案。[创建可观察序列章节](03_CreatingObservableSequences.md)展示了各种创建可观察源的方式。但是，当涉及到处理从`IObservable<T>`中出现的项时，我们真正看到的只是如何实现[`IObserver<T>`](02_KeyTypes.md#iobserver)，以及[如何使用基于回调的`Subscribe`扩展方法订阅`IObservable<T>`](02_KeyTypes.md#iobservable)。

在本章中，我们将查看Rx中允许你离开`IObservable<T>`世界的方法，这样你就可以根据从Rx源中出现的通知采取行动。

## 与`async`和`await`集成

你可以使用C#的`await`关键字与任何`IObservable<T>`一起使用。我们之前看到了这一点，通过[`FirstAsync`](05_Filtering.md#blocking-versions-of-firstlastsingleordefault)：

```csharp
long v = await Observable.Timer(TimeSpan.FromSeconds(2)).FirstAsync();
Console.WriteLine(v);
```

尽管`await`最常与`Task`、`Task<T>`或`ValueTask<T>`一起使用，但它实际上是一个可扩展的语言特性。通过提供一个名为`GetAwaiter`的方法（通常作为扩展方法），以及一个适当的类型供`GetAwaiter`返回，可以使`await`适用于几乎任何类型，为C#提供`await`所需的功能。这正是Rx所做的。如果你的源文件包括一个`using System.Reactive.Linq;`指令，一个合适的扩展方法将可用，因此你可以`await`任何任务。

这实际上是如何工作的是，相关的`GetAwaiter`扩展方法将`IObservable<T>`包装在一个`AsyncSubject<T>`中，这为C#支持`await`提供了所需的一切。这些包装器的工作方式是，每次你对`IObservable<T>`执行`await`时，都会调用`Subscribe`。

如果源通过调用其观察者的`OnError`报告错误，Rx的`await`集成通过将任务置于故障状态来处理此问题，以便`await`将重新抛出异常。

序列可以是空的。它们可能在从未调用`OnNext`的情况下调用`OnCompleted`。然而，由于无法从源的类型中判断它将是空的，这与`await`范式不太契合。使用任务时，你可以在编译时知道你是否会获得结果，方法是查看你是否正在等待一个`Task`或`Task<T>`，因此编译器能够知道特定的`await`表达式是否产生一个值。但当你`await`一个`IObservable<T>`时，没有编译时区分，因此Rx唯一能报告序列为空的方式就是在你`await`时抛出一个`InvalidOperationException`，报告序列不包含任何元素。

正如你可能从第3章的[`AsyncSubject<T>`部分](03_CreatingObservableSequences.md#asyncsubjectt)中回忆起的，`AsyncSubject<T>`只向其源报告最后一个值。因此，如果你`await`一个报告多个项的序列，除最后一个项之外的所有项都将被忽略。如果你想看到所有的项目，但你仍然想用`await`来处理完成和错误怎么办？

## ForEachAsync

`ForEachAsync`方法支持`await`，但它提供了一种处理每个元素的方法。你可以将其视为前一节中描述的`await`行为与基于回调的`Subscribe`的混合。我们仍然可以使用`await`来检测完成和错误，但我们提供了一个回调来使我们能够处理每个项：

```csharp
IObservable<long> source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(5);
await source.ForEachAsync(i => Console.WriteLine($"received {i} @ {DateTime.Now}"));
Console.WriteLine($"finished @ {DateTime.Now}");
```

输出：

```
received 0 @ 02/08/2023 07:53:46
received 1 @ 02/08/2023 07:53:47
received 2 @ 02/08/2023 07:53:48
received 3 @ 02/08/2023 07:53:49
received 4 @ 02/08/2023 07:53:50
finished @ 02/08/2023 07:53:50
```

注意，`finished`这行最后出现，正如你所期望的那样。让我们将此与`Subscribe`扩展方法进行比较，该方法也让我们为处理项目提供一个回调：

```csharp
IObservable<long> source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(5);
source.Subscribe(i => Console.WriteLine($"received {i} @ {DateTime.Now}"));
Console.WriteLine($"finished @ {DateTime.Now}");
```

正如输出所示，`Subscribe`立即返回。我们的逐项回调像之前一样被调用，但这一切都稍后发生：

```
finished @ 02/08/2023 07:55:42
received 0 @ 02/08/2023 07:55:43
received 1 @ 02/08/2023 07:55:44
received 2 @ 02/08/2023 07:55:45
received 3 @ 02/08/2023 07:55:46
received 4 @ 02/08/2023 07:55:47
```

在批处理风格的程序中，这可能很有用，这些程序执行一些工作然后退出。在那种情况下使用`Subscribe`的问题是，我们的程序可能很容易在没有完成其开始的工作的情况下退出。这可以通过使用`ForEachAsync`来避免，因为我们只需使用`await`来确保我们的方法在工作完成之前不会完成。

当我们使用`await`直接针对`IObservable<T>`，或通过`ForEachAsync`时，我们实际上选择以一种常规方式处理序列完成，而不是一种反应式方式。错误和完成处理不再是基于回调的——Rx为我们提供了`OnCompleted`和`OnError`处理程序，而是通过C#的awaiter机制来表示这些。（具体来说，当我们直接`await`一个源时，Rx提供了一个自定义的awaiter，当我们使用`ForEachAsync`时，它只返回一个`Task`。）

请注意，有些情况下`Subscribe`会阻塞直到其源完成。[`Observable.Return`](03_CreatingObservableSequences.md#observablereturn)默认会这样做，[`Observable.Range`](03_CreatingObservableSequences.md#observablerange)也会。我们可以尝试通过指定不同的调度器来使最后一个示例这样做：

```csharp
// 不要这样做!
IObservable<long> source = 
   Observable.Interval(TimeSpan.FromSeconds(1), ImmediateScheduler.Instance)
             .Take(5);
source.Subscribe(i => Console.WriteLine($"received {i} @ {DateTime.Now}"));
Console.WriteLine($"finished @ {DateTime.Now}");
```

然而，这突显了非异步阻塞调用的危险：尽管这看起来应该能工作，但实际上在当前版本的Rx中会死锁。Rx不认为`ImmediateScheduler`适合定时操作，这就是为什么它不是默认的，这种情况是为什么的一个好例子。（根本问题是，取消预定工作项的唯一方法是在调度调用返回的对象上调用`Dispose`。`ImmediateScheduler`根据定义不会在完成工作后返回，意味着它实际上无法支持取消。因此，对`Interval`的调用实际上创建了一个无法取消的周期性预定工作项，因此注定要永远运行。）

这就是为什么我们需要`ForEachAsync`。这看起来我们可以通过巧妙使用调度器获得相同的效果，但实际上如果你需要等待一些异步事件发生，使用`await`总是比使用一种涉及阻塞调用线程的方法更好。

## ToEnumerable

我们到目前为止探讨的两种机制将完成和错误处理从Rx的回调机制转换为由`await`启用的更常规的方法，但我们仍然需要提供一个回调来能够处理每个单独的项目。但是`ToEnumerable`扩展方法更进一步：它使整个序列能够使用常规的`foreach`循环消耗：

```csharp
var period = TimeSpan.FromMilliseconds(200);
IObservable<long> source = Observable.Timer(TimeSpan.Zero, period).Take(5);
IEnumerable<long> result = source.ToEnumerable();

foreach (long value in result)
{
    Console.WriteLine(value);
}

Console.WriteLine("done");
```

输出：

```
0
1
2
3
4
done
```

源可观察序列将在你开始遍历序列时（即延迟地）订阅。
如果还没有可用的元素，或者你已经消耗了所有到目前为止产生的元素，`foreach`对枚举器的`MoveNext`的调用将阻塞，直到源产生一个元素。因此，这种方法依赖于源能够从其他线程生成元素。（在这个例子中，`Timer`默认使用[`DefaultScheduler`](11_SchedulingAndThreading.md#defaultscheduler)，它在线程池上运行定时工作。）如果序列生成值的速度比你消耗它们的速度快，它们将为你排队。（这意味着在使用`ToEnumerable`时，从技术上讲，可以在同一个线程上消耗和生成项目，但这将依赖于生产者始终保持领先。这将是一种危险的方法，因为如果`foreach`循环追上了，它会然后死锁。）

像`await`和`ForEachAsync`一样，如果源报告错误，这将被抛出，因此你可以使用普通的C#异常处理，正如这个例子所示：

```csharp
try 
{ 
    foreach (long value in result)
    { 
        Console.WriteLine(value); 
    } 
} 
catch (Exception e) 
{ 
    Console.WriteLine(e.Message);
} 
```

## 到一个单一集合

有时你会想要源产生的所有项目作为一个单一列表。例如，也许你不能只处理单个元素，因为你会有时需要回顾之前接收到的元素。下面几节中描述的四个操作将所有项目聚集到一个单一集合中。它们仍然产生一个`IObservable<T>`（例如，一个`IObservable<int[]>`或一个`IObservable<Dictionary<string, long>>`），但这些都是单元素可观察对象，正如你已经看到的，你可以使用`await`关键字来获取这个单一输出。

### ToArray和ToList

`ToArray`和`ToList`将一个可观察序列打包成一个数组或一个`List<T>`的实例。与所有单一集合操作一样，这些返回一个等待其输入序列完成的可观察源，然后产生数组或列表作为单一值，之后它们立即完成。这个例子使用`ToArray`将源序列中的所有5个元素收集到一个数组中，并使用`await`从`ToArray`返回的序列中提取该数组：

```csharp
TimeSpan period = TimeSpan.FromMilliseconds(200);
IObservable<long> source = Observable.Timer(TimeSpan.Zero, period).Take(5);
IObservable<long[]> resultSource = source.ToArray();

long[] result = await resultSource;
foreach (long value in result)
{
    Console.WriteLine(value);
}
```

输出：

```
0
1
2
3
4
```

由于这些方法仍然返回可观察序列，你也可以使用正常的Rx`Subscribe`机制，或将这些作为其他操作符的输入。

如果源生成值然后出错，你将不会接收到这些值。到那时为止接收的所有值将被丢弃，操作符将调用其观察者的`OnError`（在上面的例子中，这将导致从`await`中抛出异常）。四个操作符（`ToArray`、`ToList`、`ToDictionary`和`ToLookup`）都像这样处理错误。

### ToDictionary和ToLookup

Rx可以将一个可观察序列打包成一个字典或查找表，通过`ToDictionary`和`ToLookup`方法。这两种方法采取与`ToArray`和`ToList`方法相同的基本方法：它们返回一个单元素序列，一旦输入源完成，就生成集合。

`ToDictionary`提供了四个重载，它们直接对应于LINQ to Objects为`IEnumerable<T>`定义的`ToDictionary`扩展方法：

```csharp

// 根据指定的键选择器函数，比较器和元素选择器函数从可观察序列创建字典。
public static IObservable<IDictionary<TKey, TElement>> ToDictionary<TSource, TKey, TElement>(
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector, 
    Func<TSource, TElement> elementSelector, 
    IEqualityComparer<TKey> comparer) 
{...} 


// 根据指定的键选择器函数和元素选择器函数从可观察序列创建字典。
public static IObservable<IDictionary<TKey, TElement>> ToDictionary<TSource, TKey, TElement>( 
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector, 
    Func<TSource, TElement> elementSelector) 
{...} 


// 根据指定的键选择器函数和比较器从可观察序列创建字典。
public static IObservable<IDictionary<TKey, TSource>> ToDictionary<TSource, TKey>( 
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector,
    IEqualityComparer<TKey> comparer) 
{...} 


// 根据指定的键选择器函数从可观察序列创建字典。
public static IObservable<IDictionary<TKey, TSource>> ToDictionary<TSource, TKey>( 
    this IObservable<TSource> source, 
    Func<TSource, TKey> keySelector) 
{...} 
```

`ToLookup`扩展提供了几乎相同的外观重载，不同之处在于返回类型（当然还有名称）。它们都返回一个`IObservable<ILookup<TKey, TElement>>`。与LINQ to Objects一样，字典和查找表之间的区别在于`ILookup<TKey, TElement>>`接口允许每个键有任意数量的值，而字典将每个键映射到一个值。

## ToTask

尽管Rx直接支持将`await`与`IObservable<T>`一起使用，有时获取代表`IObservable<T>`的`Task<T>`可能很有用。这很有用，因为一些API期望一个`Task<T>`。你可以在任何`IObservable<T>`上调用`ToTask()`，这将订阅那个可观察对象，并返回一个`Task<T>`，该任务在任务完成时完成，产生序列的最终输出作为任务的结果。如果源在没有生成元素的情况下完成，任务将进入故障状态，带有一个`InvalidOperation`异常，抱怨输入序列不包含元素。

你可以选择传递一个取消令牌。如果你在可观察序列完成之前取消这个，Rx将从源取消订阅，并将任务置于取消状态。

这是一个如何使用`ToTask`操作符的简单示例。
注意，`ToTask`方法位于`System.Reactive.Threading.Tasks`命名空间中。

```csharp
IObservable<long> source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(5);
Task<long> resultTask = source.ToTask();
long result = await resultTask; // 将花费5秒。
Console.WriteLine(result);
```

输出：

```
4
```

如果源序列调用`OnError`，Rx将任务置于故障状态，使用提供的异常。

一旦你拥有了任务，你当然可以使用TPL的所有功能，如连续性。

## ToEvent

正如你可以使用[`FromEventPattern`](03_CreatingObservableSequences.md#from-events)把事件作为可观察序列的源一样，你也可以使用`ToEvent`扩展方法让你的可观察序列看起来像一个标准的.NET事件。

```csharp
// 将可观察序列公开为具有.NET事件的对象。
public static IEventSource<unit> ToEvent(this IObservable<Unit> source)
{...}

// 将可观察序列公开为具有.NET事件的对象。
public static IEventSource<TSource> ToEvent<TSource>(this IObservable<TSource> source) 
{...}
```

`ToEvent`方法返回一个`IEventSource<T>`，它有一个成员：一个`OnNext`事件。

```csharp
public interface IEventSource<T> 
{ 
    event Action<T> OnNext; 
} 
```

当我们使用`ToEvent`方法转换可观察序列时，我们可以通过提供一个`Action<T>`来订阅，我们在这里使用了一个lambda。

```csharp
var source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(5); 
var result = source.ToEvent(); 
result.OnNext += val => Console.WriteLine(val);
```

输出：

```
0
1
2
3
4
```

尽管这是将Rx通知转换为事件的最简单方法，它并不遵循标准的.NET事件模式。如果我们希望如此，我们可以采用略有不同的方法。

### ToEventPattern

通常，.NET事件会向其处理程序提供`sender`和`EventArgs`参数。在上面的示例中，我们只获取值。如果你想将你的序列作为遵循标准模式的事件公开，你将需要使用`ToEventPattern`。

```csharp
// 将可观察序列公开为具有.NET事件的对象。
public static IEventPatternSource<TEventArgs> ToEventPattern<TEventArgs>(
    this IObservable<EventPattern<TEventArgs>> source) 
    where TEventArgs : EventArgs 
```

`ToEventPattern`将接受一个`IObservable<EventPattern<TEventArgs>>`并将其转换为`IEventPatternSource<TEventArgs>`。这些类型的公共接口非常简单。

```csharp
public class EventPattern<TEventArgs> : IEquatable<EventPattern<TEventArgs>>
    where TEventArgs : EventArgs 
{ 
    public EventPattern(object sender, TEventArgs e)
    { 
        this.Sender = sender; 
        this.EventArgs = e; 
    } 
    public object Sender { get; private set; } 
    public TEventArgs EventArgs { get; private set; } 
    //...equality overloads
} 

public interface IEventPatternSource<TEventArgs> where TEventArgs : EventArgs
{ 
    event EventHandler<TEventArgs> OnNext; 
} 
```

要使用这个，我们需要一个适当的`EventArgs`类型。你可能能够使用.NET运行时库提供的一个，但如果不行，你可以很容易地编写你自己的：

`EventArgs`类型：

```csharp
public class MyEventArgs : EventArgs 
{ 
    private readonly long _value; 
    
    public MyEventArgs(long value) 
    { 
        _value = value; 
    } 

    public long Value 
    { 
        get { return _value; } 
    } 
} 
```

然后我们可以通过使用`Select`应用一个简单的转换来从Rx使用这个：

```csharp
IObservable<EventPattern<MyEventArgs>> source = 
   Observable.Interval(TimeSpan.FromSeconds(1))
             .Select(i => new EventPattern<MyEventArgs>(this, new MyEventArgs(i)));
```

现在我们有了一个兼容的序列，我们可以使用`ToEventPattern`，转而使用标准的事件处理程序。

```csharp
IEventPatternSource<MyEventArgs> result = source.ToEventPattern(); 
result.OnNext += (sender, eventArgs) => Console.WriteLine(eventArgs.Value);
```

现在我们知道如何回归到.NET事件，让我们暂时退步，回忆为什么Rx是一个更好的模型。

- 事件难以组合
- 事件不能作为参数传递或存储在字段中
- 事件难以随时间查询
- 事件没有报告错误的标准模式
- 事件没有表示值序列结束的标准模式
- 事件对管理并发或多线程应用程序几乎没有帮助

## Do

生产系统的非功能需求常常要求高可用性、质量监控功能和低缺陷解决时间。日志记录、调试、检测和记录是实现非功能需求的常见选择。为了实现这些，'深入'你的Rx查询，使它们在正常操作的同时提供监控和诊断信息可能非常有用。

`Do`扩展方法允许你注入副作用行为。从Rx的角度看，`Do`似乎什么也没做：你可以将它应用于任何`IObservable<T>`，它返回另一个`IObservable<T>`，完全报告与其源相同的元素和错误或完成。然而，它的各种重载接受看起来就像`Subscribe`的回调参数：你可以为个别项目、完成和错误提供回调。与`Subscribe`不同，`Do`不是终点——`Do`的回调看到的一切也会转发到`Do`的订阅者。这使得它对于日志记录和类似的仪器化非常有用，因为你可以使用它来报告信息如何通过Rx查询流动，而不改变查询的行为。

当然，你要小心一点。使用`Do`将影响性能。如果你提供给`Do`的回调执行了任何可能改变它所属Rx查询输入的操作，你将创建了一个反馈循环，使行为变得更难理解。

让我们首先定义一些我们可以继续在示例中使用的日志方法：

```csharp
private static void Log(object onNextValue)
{
    Console.WriteLine($"Logging OnNext({onNextValue}) @ {DateTime.Now}");
}

private static void Log(Exception error)
{
    Console.WriteLine($"Logging OnError({error}) @ {DateTime.Now}");
}

private static void Log()
{
    Console.WriteLine($"Logging OnCompleted()@ {DateTime.Now}");
}
```

这段代码使用`Do`来引入一些使用上述方法的日志记录。

```csharp
IObservable<long> source = Observable.Interval(TimeSpan.FromSeconds(1)).Take(3);
IObservable<long> loggedSource = source.Do(
    i => Log(i),
    ex => Log(ex),
    () => Log());

loggedSource.Subscribe(
    Console.WriteLine,
    () => Console.WriteLine("completed"));
```

输出：

```
Logging OnNext(0) @ 01/01/2012 12:00:00
0
Logging OnNext(1) @ 01/01/2012 12:00:01
1
Logging OnNext(2) @ 01/01/2012 12:00:02
2
Logging OnCompleted() @ 01/01/2012 12:00:02
completed
```

注意，因为`Do`是查询的一部分，它必然比`Subscribe`更早看到值，这就是为什么日志消息出现在由`Subscribe`回调产生的行之前。我喜欢将`Do`方法看作是序列的[电话窃听](http://en.wikipedia.org/wiki/Telephone_tapping)。它让你有能力在不修改序列的情况下监听序列。

与`Subscribe`一样，而不是传递回调，有重载允许你为OnNext, OnError, 和OnCompleted通知提供回调，或者你可以传递`Do`一个`IObserver<T>`。

## 封装与AsObservable

糟糕的封装是开发人员留下引发错误的大门。这里有几个场景，其中粗心导致了抽象泄露。我们的第一个例子乍一看可能看起来无害，但有很多问题。

```csharp
public class UltraLeakyLetterRepo
{
    public ReplaySubject<string> Letters { get; }

    public UltraLeakyLetterRepo()
    {
        Letters = new ReplaySubject<string>();
        Letters.OnNext("A");
        Letters.OnNext("B");
        Letters.OnNext("C");
    }
}
```

在这个例子中，我们将我们的可观察序列作为一个属性公开。我们使用了`ReplaySubject<string>`，因此每个订阅者在订阅时都会收到所有的值。然而，在`Letters`属性的公共类型中透露这一实现选择是糟糕的封装，因为消费者可以调用`OnNext`/`OnError`/`OnCompleted`。为了关闭这个漏洞，我们可以简单地将公共可见的属性类型设为`IObservable<string>`。

```csharp
public class ObscuredLeakinessLetterRepo
{
    public IObservable<string> Letters { get; }

    public ObscuredLeakinessLetterRepo()
    {
        var letters = new ReplaySubject<string>();
        letters.OnNext("A");
        letters.OnNext("B");
        letters.OnNext("C");
        this.Letters = letters;
    }
}
```

这是一个重大改进：编译器不会让使用此源实例的某人写`source.Letters.OnNext("1")`。因此，API表面积适当地封装了实现细节，但如果我们过于谨慎，可以注意到没有什么能阻止消费者将结果转换回`ISubject<string>`，然后调用他们喜欢的任何方法。在这个例子中，我们看到外部代码将它们的值推入序列。

```csharp
var repo = new ObscuredLeakinessLetterRepo();
IObservable<string> good = repo.GetLetters();
    
good.Subscribe(Console.WriteLine);

// 捣乱
if (good is ISubject<string> evil)
{
    // 非常捣乱，1不是一个字母!
    evil.OnNext("1");
}
else
{
    Console.WriteLine("could not sabotage");
}
```

输出：

```
A
B
C
1
```

可以说，做这种事的代码是在自找麻烦，但如果我们想积极阻止这种情况，解决这个问题的方法很简单。通过应用`AsObservable`扩展方法，我们可以修改构造函数中设置`this.Letters`的行，用只实现`IObservable<T>`的类型包装主题。

```csharp
this.Letters = letters.AsObservable();
```

输出：

```
A
B
C
could not sabotage
```

虽然我在这些示例中使用了“邪恶”和“破坏”这样的词，但这些问题通常不是出于恶意而是由于疏忽造成的。问题首先出在设计泄漏类的程序员身上。设计接口是困难的，但我们应该尽力帮助我们的代码消费者落入[成功的陷阱](https://learn.microsoft.com/en-gb/archive/blogs/brada/the-pit-of-success)，通过提供可发现性和一致性的类型。如果我们减少类型的表面积，只暴露出我们打算让消费者使用的功能，类型会变得更容易被发现。在这个例子中，我们通过选择一个合适的公开面向类型的属性，并通过`AsObservable`方法阻止访问底层类型，减少了类型的表面积。

我们在本章中讨论的方法集完成了在[创建序列章节](03_CreatingObservableSequences.md)中开始的循环。现在我们有了进入和离开Rx世界的方法。在选择进入和退出`IObservable<T>`时要小心。最好不要来回转换——在一些基于Rx的处理、一些更常规的代码之间进行切换，然后将那些结果再接入Rx，可能很快就会使你的代码库变得混乱，并且可能表明存在设计缺陷。通常最好将你的Rx逻辑保持在一起，这样你只需要两次与外部世界集成：一次用于输入，一次用于输出。
