# 发布操作符

热源需要能够将事件传递给多个订阅者。虽然我们可以自己实现订阅者跟踪，但编写一个只适用于单个订阅者的过于简化的源可能会更容易。虽然这不会是`IObservable<T>`的完整实现，但如果我们随后使用Rx的一个_多播_操作符将其发布为多订阅者热源，那就无关紧要了。在["表示文件系统事件的Rx"](03_CreatingObservableSequences.md#representing-filesystem-events-in-rx)中使用了这个技巧，但正如你在本章中将看到的，这个主题有一些变体。

## 多播

Rx提供了三个操作符，使我们能够仅使用对某个底层源的单一订阅来支持多个订阅者：[`Publish`](#publish)、[`PublishLast`](#publishlast) 和 [`Replay`](#replay)。这三个都是围绕Rx的`Multicast`操作符的包装器，它提供了它们所有的共同机制。

`Multicast`将任何`IObservable<T>`变为一个`IConnectableObservable<T>`，正如你所见，它只是增加了一个`Connect`方法：

```csharp
public interface IConnectableObservable<out T> : IObservable<T>
{
    IDisposable Connect();
}
```

由于它派生自`IObservable<T>`，你可以在`IConnectableObservable<T>`上调用`Subscribe`，但`Multicast`返回的实现在你这样做时不会调用底层源的`Subscribe`。它只在你调用`Connect`时才调用底层源的`Subscribe`。为了让我们能看到这一点，让我们定义一个每次调用`Subscribe`时都会打印出消息的源：

```csharp
IObservable<int> src = Observable.Create<int>(obs =>
{
    Console.WriteLine("Create callback called");
    obs.OnNext(1);
    obs.OnNext(2);
    obs.OnCompleted();
    return Disposable.Empty;
});
```

由于这只会被调用一次，不管有多少观察者订阅，`Multicast`无法传递给它自己的`Subscribe`方法的`IObserver<T>`，因为它们可能有任意数量。它使用[Subject](03_CreatingObservableSequences.md#subjectt)作为传递给底层源的单一`IObserver<T>`，这个subject还负责跟踪所有订阅者。如果我们直接调用`Multicast`，我们需要传入我们想要使用的subject：

```csharp
IConnectableObservable<int> m = src.Multicast(new Subject<int>());
```

我们现在可以多次订阅这个：

```csharp
m.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
m.Subscribe(x => Console.WriteLine($"Sub2: {x}"));
m.Subscribe(x => Console.WriteLine($"Sub3: {x}"));
```

除非我们调用`Connect`，否则这些订阅者都不会收到任何东西：

```csharp
m.Connect();
```

**注意**：`Connect`返回一个`IDisposable`。调用该对象的`Dispose`会取消订阅底层来源。

这次调用`Connect`产生了以下输出：

```csharp
Create callback called
Sub1: 1
Sub2: 1
Sub3: 1
Sub1: 2
Sub2: 2
Sub3: 2
```

正如你所看到的，我们传递给`Create`的方法只运行了一次，确认`Multicast`确实只订阅了一次，尽管我们已经调用了`Subscribe`三次。但是每个项目都传递给了所有三个订阅。

`Multicast`的工作方式相当简单：它让subject完成大部分工作。每当你在`Multicast`返回的可观察对象上调用`Subscribe`时，它只是调用subject的`Subscribe`。当你调用`Connect`时，它只是将subject传递给底层源的`Subscribe`。所以这段代码会产生相同的效果：

```csharp
var s = new Subject<int>();

s.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
s.Subscribe(x => Console.WriteLine($"Sub2: {x}"));
s.Subscribe(x => Console.WriteLine($"Sub3: {x}"));

src.Subscribe(s);
```

然而，`Multicast`的一个优点是它返回`IConnectableObservable<T>`，正如我们稍后将看到的，Rx的一些其他部分知道如何与这个接口一起工作。

`Multicast`提供了一个以完全不同方式工作的重载版本：它适用于你想要编写一个查询，该查询使用其源`observable`两次的场景。例如，我们可能想使用`Zip`获得相邻的项目对：

```csharp
IObservable<(int, int)> ps = src.Zip(src.Skip(1));
ps.Subscribe(ps => Console.WriteLine(ps));
```

(虽然[`Buffer`](08_Partitioning.md#buffer)看起来像是一个更明显的方法来做这件事，但这种`Zip`方法的一个优点是它永远不会给我们一半的对。当我们要求`Buffer`给我们对时，它会在我们到达尽头时给我们一个单项缓冲区，这可能需要额外的代码来解决问题。)

使用这种方法的问题是，源将看到两个订阅：一个直接来自`Zip`，然后通过`Skip`的第二个。如果我们运行上面的代码，我们会看到这个输出：

```
Create callback called
Create callback called
(1, 2)
```

我们的`Create`回调运行了两次。第二个`Multicast`重载让我们避免了这个问题：

```csharp
IObservable<(int, int)> ps = src.Multicast(() => new Subject<int>(), s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

如输出所示，这避免了多个订阅：

```csharp
Create callback called
(1, 2)
```

这个`Multicast`重载返回一个普通的`IObservable<T>`。这意味着我们不需要调用`Connect`。但这也意味着，对结果`IObservable<T>`的每个订阅都会导致对底层来源的一个订阅。但对于它设计的场景来说这是可以的：我们只是试图避免对底层来源进行两倍的订阅。

在这一节中定义的其余操作符，`Publish`、`PublishLast`和`Replay`，都是围绕`Multicast`的包装器，每一个都为你提供了一种特定类型的subject。

### Publish

`Publish`操作符使用[`Subject<T>`](03_CreatingObservableSequences.md#subjectt)调用`Multicast`。这样做的效果是，一旦你在结果上调用了`Connect`，源产生的任何项目都会被传递给所有订阅者。这使我能够将这个较早的例子：

```csharp
IConnectableObservable<int> m = src.Multicast(new Subject<int>());
```

替换为这个：

```csharp
IConnectableObservable<int> m = src.Publish();
```

这两者完全等效。

因为`Subject<T>`立即将所有传入的`OnNext`调用转发给它的每个订阅者，并且因为它不存储之前做出的任何调用，所以结果是一个热源。如果你在调用`Connect`之前附加了一些订阅者，然后在调用`Connect`之后附加了更多订阅者，那么那些较晚的订阅者只会收到在他们订阅后发生的事件。这个例子演示了这一点：

```csharp
IConnectableObservable<long> publishedTicks = Observable
    .Interval(TimeSpan.FromSeconds(1))
    .Take(4)
    .Publish();

publishedTicks.Subscribe(x => Console.WriteLine($"Sub1: {x} ({DateTime.Now})"));
publishedTicks.Subscribe(x => Console.WriteLine($"Sub2: {x} ({DateTime.Now})"));

publishedTicks.Connect();
Thread.Sleep(2500);
Console.WriteLine();
Console.WriteLine("Adding more subscribers");
Console.WriteLine();

publishedTicks.Subscribe(x => Console.WriteLine($"Sub3: {x} ({DateTime.Now})"));
publishedTicks.Subscribe(x => Console.WriteLine($"Sub4: {x} ({DateTime.Now})"));
```

以下输出显示，我们只看到了Sub3和Sub4订阅的最后两个事件的输出：

```
Sub1: 0 (10/08/2023 16:04:02)
Sub2: 0 (10/08/2023 16:04:02)
Sub1: 1 (10/08/2023 16:04:03)
Sub2: 1 (10/08/2023 16:04:03)

Adding more subscribers

Sub1: 2 (10/08/2023 16:04:04)
Sub2: 2 (10/08/2023 16:04:04)
Sub3: 2 (10/08/2023 16:04:04)
Sub4: 2 (10/08/2023 16:04:04)
Sub1: 3 (10/08/2023 16:04:05)
Sub2: 3 (10/08/2023 16:04:05)
Sub3: 3 (10/08/2023 16:04:05)
Sub4: 3 (10/08/2023 16:04:05)
```

与[`Multicast`](#multicast)一样，`Publish`提供了一个提供每个顶层订阅多播的重载。这让我们能够简化该节末尾的示例，从这个：

```csharp
IObservable<(int, int)> ps = src.Multicast(() => new Subject<int>(), s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

变为这个：

```csharp
IObservable<(int, int)> ps = src.Publish(s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

`Publish`提供了允许你指定初始值的重载。这些使用[`BehaviorSubject<T>`](03_CreatingObservableSequences.md#behaviorsubjectt)而不是`Subject<T>`。这里的区别是，所有订阅者将立即在他们订阅时收到一个值。如果底层来源尚未产生一个项目（或者如果没有调用`Connect`，意味着我们甚至还没有订阅来源），他们将收到初始值。如果至少收到了来自来源的一个项目，任何新的订阅者将立即收到来源产生的最新值，然后继续收到任何进一步的新值。

### PublishLast

`PublishLast`操作符使用[`AsyncSubject<T>`](03_CreatingObservableSequences.md#asyncsubjectt)调用`Multicast`。这样做的效果是，源产生的最后一个项目将被传递给所有订阅者。你仍然需要调用`Connect`。这决定何时订阅底层来源。但所有订阅者都将收到最终事件，无论他们何时订阅，因为`AsyncSubject<T>`记住了最终结果。我们可以通过以下示例看到这一点：

```csharp
IConnectableObservable<long> pticks = Observable
    .Interval(TimeSpan.FromSeconds(0.1))
    .Take(4)
    .PublishLast();

pticks.Subscribe(x => Console.WriteLine($"Sub1: {x} ({DateTime.Now})"));
pticks.Subscribe(x => Console.WriteLine($"Sub2: {x} ({DateTime.Now})"));

pticks.Connect();
Thread.Sleep(3000);
Console.WriteLine();
Console.WriteLine("Adding more subscribers");
Console.WriteLine();

pticks.Subscribe(x => Console.WriteLine($"Sub3: {x} ({DateTime.Now})"));
pticks.Subscribe(x => Console.WriteLine($"Sub4: {x} ({DateTime.Now})"));
```

这创建了一个在0.4秒内产生4个值的源。它附上一对订阅者到由`PublishLast`返回的`IConnectableObservable<T>`，然后立即调用`Connect`。然后它睡眠1秒，这给了源时间来完成。这意味着那前两个订阅者将收到他们将收到的唯一值（序列中的最后值）在那次调用`Thread.Sleep`返回之前。但我们随后又附加了两个更多的订阅者。如输出所示，这些也接收到了同一最终事件：

```
Sub1: 3 (11/14/2023 9:15:46 AM)
Sub2: 3 (11/14/2023 9:15:46 AM)

Adding more subscribers

Sub3: 3 (11/14/2023 9:15:49 AM)
Sub4: 3 (11/14/2023 9:15:49 AM)
```

这最后两个订阅者因为他们订阅晚了所以接收到值晚了，但是由`PublishLast`创建的`AsyncSubject<T>`只是在这些晚到的订阅者中重放了其已接收到的最终值。

### Replay

`Replay`操作符使用[`ReplaySubject<T>`](03_CreatingObservableSequences.md#replaysubjectt)调用`Multicast`。这样做的效果是，在调用`Connect`之前附加的任何订阅者只收到底层源产生的所有事件，但任何稍后附加的订阅者实际上可以'赶上'，因为`ReplaySubject<T>`记住了它已经看到的事件，并将这些事件重放给新订阅者。

这个示例与用于`Publish`的非常相似：

```csharp
IConnectableObservable<long> pticks = Observable
    .Interval(TimeSpan.FromSeconds(1))
    .Take(4)
    .Replay();

pticks.Subscribe(x => Console.WriteLine($"Sub1: {x} ({DateTime.Now})"));
pticks.Subscribe(x => Console.WriteLine($"Sub2: {x} ({DateTime.Now})"));

pticks.Connect();
Thread.Sleep(2500);
Console.WriteLine();
Console.WriteLine("Adding more subscribers");
Console.WriteLine();

pticks.Subscribe(x => Console.WriteLine($"Sub3: {x} ({DateTime.Now})"));
pticks.Subscribe(x => Console.WriteLine($"Sub4: {x} ({DateTime.Now})"));
```

这创建了一个将在4秒内定期产生项目的源。它在调用`Connect`之前附加了两个订阅者。然后它等待足够长的时间以便第一和第二事件发生之前继续附加两个更多的订阅者。但与`Publish`不同，那些晚来的订阅者将看到他们订阅之前发生的事件：

```
Sub1: 0 (10/08/2023 16:18:22)
Sub2: 0 (10/08/2023 16:18:22)
Sub1: 1 (10/08/2023 16:18:23)
Sub2: 1 (10/08/2023 16:18:23)

Adding more subscribers

Sub3: 0 (10/08/2023 16:18:24)
Sub3: 1 (10/08/2023 16:18:24)
Sub4: 0 (10/08/2023 16:18:24)
Sub4: 1 (10/08/2023 16:18:24)
Sub1: 2 (10/08/2023 16:18:24)
Sub2: 2 (10/08/2023 16:18:24)
Sub3: 2 (10/08/2023 16:18:24)
Sub4: 2 (10/08/2023 16:18:24)
Sub1: 3 (10/08/2023 16:18:25)
Sub2: 3 (10/08/2023 16:18:25)
Sub3: 3 (10/08/2023 16:18:25)
Sub4: 3 (10/08/2023 16:18:25)
```

他们当然是晚收到这些事件的，因为他们晚订阅的。所以我们看到`Sub3`和`Sub4`在赶上时迅速报告了一连串的事件，但一旦他们赶上了，他们就会立即收到所有进一步的事件。

使这种行为成为可能的`ReplaySubject<T>`会消耗内存来存储事件。正如你可能记得的，这种subject类型可以配置为仅存储有限数量的事件，或者不保留早于某个指定时间限制的事件。`Replay`操作符提供了允许你配置这些类型限制的重载。

`Replay`也支持我为此部分中其他基于`Multicast`的操作符展示的每个订阅多播模型。

## RefCount

我们在前一节中看到了`Multicast`（以及其各种包装器）支持两种使用模型：

* 返回一个`IConnectableObservable<T>`，以允许顶层控制何时发生对底层来源的订阅
* 返回一个普通的`IObservable<T>`，使我们能够避免在使用源的多个地方（例如`s.Zip(s.Take(1))`）的查询中进行不必要的多个订阅，但仍然对每个顶层`Subscribe`进行一次底层来源的`Subscribe`调用

`RefCount`提供了一个略有不同的模型。它使订阅到底层来源可以通过普通的`Subscribe`触发，但仍然只进行一次对底层来源的调用。在整本书中使用的AIS示例中，这可能会很有用。你可能希望将多个订阅者附加到报告船只和其他船舶广播的定位消息的可观察源，但你通常希望为此提供Rx-based API的库只连接一次到提供这些消息的任何底层服务。而且你很可能希望它只在至少有一个订阅者在监听时才连接。`RefCount`非常适合于此，因为它使单个来源能够支持多个订阅者，并使底层来源知道我们何时从“没有订阅者”状态转变为“至少有一个订阅者”状态。

为了能够观察到`RefCount`的操作，我将使用一个修改版本的源，该源报告何时发生订阅：

```csharp
IObservable<int> src = Observable.Create<int>(async obs =>
{
    Console.WriteLine("Create callback called");
    obs.OnNext(1);
    await Task.Delay(250).ConfigureAwait(false);
    obs.OnNext(2);
    await Task.Delay(250).ConfigureAwait(false);
    obs.OnNext(3);
    await Task.Delay(250).ConfigureAwait(false);
    obs.OnNext(4);
    await Task.Delay(100).ConfigureAwait(false);
    obs.OnCompleted();
});
```

与早期示例不同，这使用了`async`并在每个`OnNext`之间延迟，以确保主线程有时间在所有项目生成之前设置多个订阅。然后我们可以用`RefCount`封装这个：

```csharp
IObservable<int> rc = src
    .Publish()
    .RefCount();
```

注意我首先必须调用`Publish`。这是因为`RefCount`期望一个`IConnectableObservable<T>`。它希望在第一次有东西订阅时启动源。它会在至少有一个订阅者时调用`Connect`。让我们尝试一下：

```csharp
rc.Subscribe(x => Console.WriteLine($"Sub1: {x} ({DateTime.Now})"));
rc.Subscribe(x => Console.WriteLine($"Sub2: {x} ({DateTime.Now})"));
Thread.Sleep(600);
Console.WriteLine();
Console.WriteLine("Adding more subscribers");
Console.WriteLine();
rc.Subscribe(x => Console.WriteLine($"Sub3: {x} ({DateTime.Now})"));
rc.Subscribe(x => Console.WriteLine($"Sub4: {x} ({DateTime.Now})"));
```

这里是输出：

```
Create callback called
Sub1: 1 (10/08/2023 16:36:44)
Sub1: 2 (10/08/2023 16:36:45)
Sub2: 2 (10/08/2023 16:36:45)
Sub1: 3 (10/08/2023 16:36:45)
Sub2: 3 (10/08/2023 16:36:45)

Adding more subscribers

Sub1: 4 (10/08/2023 16:36:45)
Sub2: 4 (10/08/2023 16:36:45)
Sub3: 4 (10/08/2023 16:36:45)
Sub4: 4 (10/08/2023 16:36:45)
```

注意只有`Sub1`收到了第一个事件。那是因为传递给`Create`的回调立即产生了它。只有当它调用第一个`await`时，它才返回给调用者，使我们能够附加第二个订阅者。那已经错过了第一个事件，但正如你可以看到的，它接收到了第二和第三个事件。代码等待足够长的时间让前三个事件发生在附加两个更多的订阅者之前，你可以看到所有四个订阅者都接收到了最后的事件。

正如名称所示，`RefCount`计算活跃订阅者的数量。如果这个数字曾经降至0，它将调用`Connect`返回的对象的`Dispose`，关闭订阅。如果后来有更多的订阅者加入，它将重新启动。这个示例显示了这一点：

```csharp
IDisposable s1 = rc.Subscribe(x => Console.WriteLine($"Sub1: {x} ({DateTime.Now})"));
IDisposable s2 = rc.Subscribe(x => Console.WriteLine($"Sub2: {x} ({DateTime.Now})"));
Thread.Sleep(600);

Console.WriteLine();
Console.WriteLine("Removing subscribers");
s1.Dispose();
s2.Dispose();
Thread.Sleep(600);
Console.WriteLine();

Console.WriteLine();
Console.WriteLine("Adding more subscribers");
Console.WriteLine();
rc.Subscribe(x => Console.WriteLine($"Sub3: {x} ({DateTime.Now})"));
rc.Subscribe(x => Console.WriteLine($"Sub4: {x} ({DateTime.Now})"));
```

我们得到了这个输出：

```
Create callback called
Sub1: 1 (10/08/2023 16:40:39)
Sub1: 2 (10/08/2023 16:40:39)
Sub2: 2 (10/08/2023 16:40:39)
Sub1: 3 (10/08/2023 16:40:39)
Sub2: 3 (10/08/2023 16:40:39)

Removing subscribers


Adding more subscribers

Create callback called
Sub3: 1 (10/08/2023 16:40:40)
Sub3: 2 (10/08/2023 16:40:40)
Sub4: 2 (10/08/2023 16:40:40)
Sub3: 3 (10/08/2023 16:40:41)
Sub4: 3 (10/08/2023 16:40:41)
Sub3: 4 (10/08/2023 16:40:41)
Sub4: 4 (10/08/2023 16:40:41)
```

这一次，`Create`回调运行了两次。那是因为活跃订阅者的数量降至0，所以`RefCount`调用了`Dispose`来关闭事务。当新的订阅者出现时，它再次调用`Connect`来重新启动事务。有一些重载允许你指定一个`disconnectDelay`。这会告诉它在订阅者数量降至零后等待指定的时间再断开连接，以查看是否有新的订阶订阅者出现。但如果指定的时间过去了，它还是会断开连接。如果这不是你想要的，下一个操作符可能适合你。

## AutoConnect

`AutoConnect`操作符的行为与`RefCount`非常相似，即在第一个订阅者订阅时就在其底层的`IConnectableObservable<T>`上调用`Connect`。不同之处在于它不试图检测活跃订阅者的数量是否已降至零：一旦它连接，它将无限期地保持连接，即使它没有订阅者。

尽管`AutoConnect`可能很方便，你需要小心一点，因为它可以导致泄漏：它永远不会自动断开连接。拆除它创建的连接仍然是可能的：`AutoConnect`接受一个可选的`Action<IDisposable>`类型参数。当它首次连接到源时，它会调用这个，将源的`Connect`方法返回的`IDisposable`传递给你。你可以通过调用`Dispose`来关闭它。

本章中的操作符在你有一个不适合处理多个订阅者的源时可能很有用。它提供了各种方式来附加多个订阅者，同时只触发对底层来源的单一`Subscribe`。