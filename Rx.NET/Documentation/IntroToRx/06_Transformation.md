# 序列的转换

我们消费的序列中的值并不总是我们需要的格式。有时候，信息量过多，我们需要挑选出我们感兴趣的值。有时候，每个值都需要扩展成一个更丰富的对象或更多的值。

到目前为止，我们已经查看了序列的创建、序列的过渡，以及通过过滤来减少序列。在本章中，我们将研究**转换**序列。

## Select

最直接的转换方法是 `Select`。它允许你提供一个函数，这个函数接受一个 `TSource` 的值，并返回一个 `TResult` 的值。`Select` 的签名反映了它将序列元素从一种类型转换为另一种类型的能力，即从 `IObservable<TSource>` 到 `IObservable<TResult>`。

```csharp
IObservable<TResult> Select<TSource, TResult>(
    this IObservable<TSource> source, 
    Func<TSource, TResult> selector)
```

你不必更改类型——如果你愿意，`TSource` 和 `TResult` 可以是相同的。这个第一个示例通过添加3来转换整数序列，结果产生另一个整数序列。

```csharp
IObservable<int> source = Observable.Range(0, 5);
source.Select(i => i + 3)
      .Dump("+3")
```

这使用了我们在[过滤章节](05_Filtering.md)开头定义的 `Dump` 扩展方法。它产生以下输出：

```
+3 --> 3
+3 --> 4
+3 --> 5
+3 --> 6
+3 --> 7
+3 completed
```

下一个示例以改变其类型的方式转换值。它将整数值转换为字符。

```csharp
Observable.Range(1, 5);
          .Select(i => (char)(i + 64))
          .Dump("char");
```

输出：

```
char --> A
char --> B
char --> C
char --> D
char --> E
char completed
```

这个例子将我们的整数序列转换为一个元素具有匿名类型的序列：

```csharp
Observable.Range(1, 5)
          .Select(i => new { Number = i, Character = (char)(i + 64) })
          .Dump("anon");
```

输出：

```
anon --> { Number = 1, Character = A }
anon --> { Number = 2, Character = B }
anon --> { Number = 3, Character = C }
anon --> { Number = 4, Character = D }
anon --> { Number = 5, Character = E }
anon completed
```

`Select` 是由 C# 的查询表达式语法支持的标准 LINQ 操作符之一，因此我们可以这样写最后一个示例：

```csharp
var query = from i in Observable.Range(1, 5)
            select new {Number = i, Character = (char) (i + 64)};

query.Dump("anon");
```

在 Rx 中，`Select` 还有另一个重载，在这个重载中，`selector` 函数接受两个值。额外的参数是元素在序列中的索引。如果序列中元素的索引对你的选择器函数很重要，就使用这种方法。

## SelectMany

虽然 `Select` 为每个输入生成一个输出，`SelectMany` 允许每个输入元素转换为任意数量的输出。为了看到这是如何工作的，让我们先看一个仅使用 `Select` 的示例：

```csharp
Observable
    .Range(1, 5)
    .Select(i => new string((char)(i+64), i))
    .Dump("strings");
```

这产生了以下输出：

```
strings-->A
strings-->BB
strings-->CCC
strings-->DDDD
strings-->EEEEE
strings completed
```

如你所见，对于 `Range` 产生的每个数字，我们的输出包含一个字符串，其长度就是那么多字符。如果我们不是将每个数字转换为一个字符串，而是将其转换为一个 `IObservable<char>` 呢？我们可以通过在构造字符串后添加 `.ToObservable()` 来做到这一点：

```csharp
Observable
    .Range(1, 5)
    .Select(i => new string((char)(i+64), i).ToObservable())
    .Dump("sequences");
```

(或者，我们可以将选择表达式替换为 `i => Observable.Repeat((char)(i+64), i)`。二者的效果完全相同。) 输出不是很有用：

```
strings-->System.Reactive.Linq.ObservableImpl.ToObservableRecursive`1[System.Char]
strings-->System.Reactive.Linq.ObservableImpl.ToObservableRecursive`1[System.Char]
strings-->System.Reactive.Linq.ObservableImpl.ToObservableRecursive`1[System.Char]
strings-->System.Reactive.Linq.ObservableImpl.ToObservableRecursive`1[System.Char]
strings-->System.Reactive.Linq.ObservableImpl.ToObservableRecursive`1[System.Char]
strings completed
```

我们有一个可观察序列的可观察序列。但看看如果我们现在将 `Select` 替换为 `SelectMany` 会发生什么：

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => new string((char)(i+64), i).ToObservable())
    .Dump("chars");
```

这给了我们一个 `IObservable<char>`，输出如下：

```
chars-->A
chars-->B
chars-->B
chars-->C
chars-->C
chars-->D
chars-->C
chars-->D
chars-->E
chars-->D
chars-->E
chars-->D
chars-->E
chars-->E
chars-->E
chars completed
```

顺序有些打乱，但仔细观察，你会看到每个字母出现的次数与我们发出字符串时相同。例如，只有一个 `A`，但是 `C` 出现了三次，`E` 出现了五次。

`SelectMany` 期望建议转换函数返回一个 `IObservable<T>` 输入，并将这些结果合并回一个单一结果中。`Enumerable.SelectMany` 转换功能与对象不一样混乱若干，如果使用对象：

```csharp
Enumerable
    .Range(1, 5)
    .SelectMany(i => new string((char)(i+64), i))
    .ToList()
```

它会产生一个包含以下元素的列表：

```
[ A, B, B, C, C, C, D, D, D, D, E, E, E, E, E ]
```

顺序更加规整。这在[调度和线程章节](11_SchedulingAndThreading.md)中有更详细的解释。

### `IEnumerable<T>` 与 `IObservable<T>` 的 `SelectMany`

`IEnumerable<T>` 是基于拉取的——序列仅在被询问时产生元素。`Enumerable.SelectMany` 以一种非常特别的顺序从其源拉取项目。它首先询问其源 `IEnumerable<int>`（上述示例中由 `Range` 返回的）获取第一个值。然后 `SelectMany` 调用我们的回调，传递这第一个项目，然后枚举我们的回调返回的 `IEnumerable<char>` 中的所有内容。只有当它耗尽这个时，它才会向源 (`Range`) 请求第二个项目。再次，它将第二个项目传递给我们的回调，然后完全枚举我们返回的 `IEnumerable<char>`，依此类推。所以我们首先得到第一个嵌套序列中的所有内容，然后是第二个，等等。

`Enumerable.SelectMany` 能够以此方式进行的原因有两个。首先，`IEnumerable<T>` 的基于拉取的特性使它能够决定处理事物的顺序。其次，对于 `IEnumerable<T>` 来说，阻塞操作是正常的，即，它们不返回任何东西，直到它们有东西为我们提供。当前面的示例调用 `ToList` 时，它不会返回，直到它用所有结果完全填充了一个 `List<T>`。

Rx 的情况不是这样的。首先，消费者无法告诉源何时产生每个项目——源在准备好时发出项目。其次，Rx 通常模型的是持续进行的过程，所以我们通常不期望方法调用会阻塞直到它们完成。有些情况下 Rx 序列会自然地非常迅速地产生它们的所有项目，并尽快完成，但是我们倾向于用 Rx 建模的信息源通常不会以这种方式表现。所以 Rx 中的大多数操作都不会阻塞——它们会立即返回一些东西（例如 `IObservable<T>` 或代表订阅的 `IDisposable`），然后稍后产生值。

我们当前正在检查的 Rx 示例实际上是这些不寻常情况之一，其中每个序列尽快发出项目。从逻辑上讲，所有嵌套的 `IObservable<char>` 序列同时进行。结果是一团糟，因为这里的每个可观察源都试图尽可能快地生产每个元素，因为源可以消耗它们。它们最终被交错的原因与这些类型的可观察源使用 Rx 的调度系统有关，我们将在[调度和线程章节](11_SchedulingAndThreading.md)中描述。调度器确保即使在我们以逻辑上并发的过程建模时，Rx 的规则也被维护，并且 `SelectMany` 的输出观察者一次只会得到一个项目。下面的弹珠图展示了导致我们看到的混乱输出的事件：

![一个 Rx 弹珠图展示 7 个可观测变量。第一个展示了 Range 操作员产生 1 到 5 的值。这些颜色编码如下:绿色，蓝色，黄色，红色和粉红色。这些颜色对应于图中进一步向下的可观察变量，将在不久的将来进行描述。这个第一个可观察变量中的项目并不均匀分布。第 2 个值紧跟在第 1 个值之后，但在第 3、4、5 个项目之前有间隙。这些间隙与图中进一步显示的活动相对应。在第一个可观察变量的下方是调用 SelectMany 操作符的代码，传递此 lambda: "i => new string((char)(i+64),i).ToObservable()"。在此之下是另外 6 个可观测变量。前 5 个显示 lambda 为每个输入生成的单独的可观察变量。这些颜色编码显示了它们是如何对应的:第一个可观察变量的项目是绿色的，表明这个可观察变量对应于 Range 发出的第一个项目，第二个可观察变量的项目是蓝色的，显示它对应于 Range 发出的第二个项目，以此类推，颜色序列与之前为 Range 描述的相同。每个可观察变量的第一个项目在水平方向上与来自 Range 的相应项目对齐，表明每个这样的可观察变量在 Range 发出一个值时开始。这 5 个可观察变量显示 lambda 为 Range 的 5 个值返回的可观察变量生成的值。这些子可观察变量中的第一个显示一个值为 'A' 的单个项目，垂直对齐 Range 可观察变量的值 1，表明这个项目是在 Range 产生其第一个值时立即产生的。这个子可观测变量然后立即结束，表明只生产了一个项目。第二个子可观察变量包含两个 'B' 值，第三个三个 'C' 值，第四个四个 'D' 值，第五个五个 'E' 值。这些项目的水平位置表明图中的前 6 个可观测变量（Range 可观察变量和 lambda 产生的 5 个可观察变量）在某种程度上重叠。最初，这种重叠是最小的:lambda 产生的第一个可观察变量在 Range 产生其第一个值的同时开始，因此这两个可观测变量重叠，但由于这第一个子项立即完成，因此它与其他任何内容都不重叠。第二个子项在 Range 产生其第二个值时开始，并设法在发生其他任何事情之前产生两个值，然后完成，因此到目前为止，lambda 产生的子可观测范围仅与 Range 重叠，而不是彼此重叠。但是，当 Range 产生其第三个值时，由此产生的子可观察变量产生了两个 'C' 值，但接下来发生的事情（由项目的水平位置表示）是，Range 管理产生其第 4 个值，以及其对应的子可观察变量产生第一个 'D' 值。接着，第三个子可观察变量产生了其第三个和最后一个 'C'，因此这第三个子不仅与 Range 可观察变量重叠，而且还与第四个子重叠。然后第四个可观察变量产生了第二个'D'。然后 Range 产生了第五个也是最后一个值，相应的子可观察变量产生了第一个 'E'。然后第四个和第五个子可观测变量交替产生 'D'，'E' 和 'D'，此时第四个子可观察变量完成，现在第五个子可观察变量产生了其最后三个 'E' 值，因为此时它是唯一仍在运行的可观察变量。在图的底部是表示 SelectMany 输出的第 7 个可观察变量。这显示了来自每个 5 个子可观察变量的所有值，每个水平位置都完全相同（表明 SelectMany 返回的可观察变量在其任何子可观察变量产生值时产生一个值）。因此，我们可以看到这个输出可观察变量生成的序列 'ABBCCDCDEDEDEEE'，这正是我们在前面的示例输出中看到的。](GraphicsIntro/Ch06-Transformation-Marbles-Select-Many-Marbles.svg)

我们可以对代码稍作修改，防止子序列同时尝试运行。（这还使用了 `Observable.Repeat`，而不是之前示例中构造一个 `string` 然后调用 `ToObservable` 的相对间接的方法。我之前是这样做的，是为了强调与 LINQ to Objects 示例的相似性，但实际上在 Rx 中你不会真的那样做。）

```csharp
Observable
    .Range(1, 5)
    .SelectMany(i => 
        Observable.Repeat((char)(i+64), i)
                  .Delay(TimeSpan.FromMilliseconds(i * 100)))
    .Dump("chars");
```

现在我们得到了与 `IEnumerable<T>` 版本一致的输出：

```
chars-->A
chars-->B
chars-->B
chars-->C
chars-->C
chars-->C
chars-->D
chars-->D
chars-->D
chars-->D
chars-->E
chars-->E
chars-->E
chars-->E
chars-->E
chars completed
```

这表明 `SelectMany` 允许你为源产生的每一项生成一个序列，并将所有这些新序列中的所有项展开回一个包含所有内容的序列中。虽然这可能使其更容易理解，但实际上你并不希望仅仅为了让其更易于理解而引入这种延迟。这些延迟意味着所有元素出现大约需要一秒半的时间。这个弹珠图展示了上面的代码通过使每个子可观察对象产生一小堆项目，我们刚刚引入了停顿时间来产生合理的排序：

![一个 Rx 弹珠图，如前一个图表一样，展示 7 个可观察变量。第一个展示了 Range 操作员产生的 1 到 5 的值。这些颜色编码如下：绿色，蓝色，黄色，红色和粉红色。这些颜色将在不久的将来在图中进一步描述。](GraphicsIntro/Ch06-Transformation-Marbles-Select-Many-Marbles-Delay.svg)

我仅仅引入这些间隙是为了提供一个稍微不那么混乱的示例，但如果你真的需要这种严格按顺序处理，实际上你不会以这种方式使用 `SelectMany`。首先，它不能完全保证能起作用。（如果你尝试这个示例，但将时间间隔修改得越来越短，最终你会达到项目开始混乱的点。由于 .NET 不是一个实时编程系统，实际上没有你可以指定的安全时间跨度来保证排序。）如果你绝对需要在看到第二个子序列的任何项目前看到第一个子序列的所有项目，有一种健壮的方法可以请求这个：

```csharp
Observable
    .Range(1, 5)
    .Select(i => Observable.Repeat((char)(i+64), i))
    .Concat())
    .Dump("chars");
```

但是，这将不再使用 `SelectMany`。（它使用 `Concat`，将在[结合序列](09_CombiningSequences.md)章节中讨论。）我们使用 `SelectMany` 是当我们知道我们正在展开单值序列，或者当我们没有具体的排序要求，希望按照子可观察对象产生的元素来接受元素时。

### SelectMany 的重要性

如果你按顺序阅读了本书的章节，你之前已经在前几章中看到了两个 `SelectMany` 的示例。第一个示例出现在第 2 章的[**LINQ 操作符和组合**部分](02_KeyTypes.md#linq-operators-and-composition)。这里是相关代码：

```csharp
IObservable<int> onoffs =
    from _ in src
    from delta in 
       Observable.Return(1, scheduler)
                 .Concat(Observable.Return(-1, scheduler)
                                   .Delay(minimumInactivityPeriod, scheduler))
    select delta;
```

（如果你想知道哪里调用了 `SelectMany`，记住如果查询表达式包含两个 `from` 子句，C# 编译器会将这些转换为调用 `SelectMany`。）这展示了 Rx 中的一种常见模式，可以描述为扩散开然后再收回来。

如你所记得，这个例子通过为 `src` 产生的每个项目创建一个新的、短暂的 `IObservable<int>` 工作。（这些子序列，由示例中的 `delta` 范围变量表示，产生值 `1`，然后在指定的 `minimumActivityPeriod` 后，它们产生 `-1`。这使我们能够跟踪最近发出的事件的数量。）这是扩散开的部分，其中源序列中的项目产生新的可观察序列。`SelectMany` 在这些场景中至关重要，因为它使所有这些新序列都可以平展回单个输出序列。

我第二次使用 `SelectMany` 有些不同：它是第 3 章[**在 Rx 中表示文件系统事件**部分的最后一个示例](03_CreatingObservableSequences.md#representing-filesystem-events-in-rx)。尽管该示例也将多个可观察源结合成单个可观察对象，但这个可观察列表是固定的：有一个来自 `FileSystemWatcher` 的不同事件。它使用了一个不同的操作符 `Merge`（我们将在[结合序列](09_CombiningSequences.md)中讨论），在那种场景中使用更简单，因为你只需传递你想结合的所有可观察对象的列表。然而，由于这段代码想要做的其他几件事（包括延迟启动、自动处理和在多个订阅者活跃时共享单一源），用于实现这一点的操作符组合意味着我们的合并代码返回一个 `IObservable<FileSystemEventArgs>`，需要作为转换步骤进行调用。如果我们只使用 `Select`，结果将是一个 `IObservable<IObservable<FileSystemEventArgs>>`。代码结构意味着它只会产生单一 `IObservable<FileSystemEventArgs>`，因此双包装类型非常不方便。`SelectMany` 在这些场景中非常有用。如果操作符的组合引入了你不想要的额外可观察层，`SelectMany` 可以为你解包一层。

这两种情况——扩散开然后再收回来，以及删除或避免一层可观察对象的可观察层——经常出现，这使得 `SelectMany` 成为一种重要的方法。（鉴于我无法在前面的示例中避免使用它，这并不奇怪。）

事实上，`SelectMany` 还是 Rx 基于的数学理论中一种特别重要的操作符。它是一个基本操作符，从某种意义上说，可以用它来构建许多其他 Rx 操作符。[附录 D 中的 'Recreating other operators with `SelectMany`' 部分](D_AlgebraicUnderpinnings.md#recreating-other-operators-with-selectmany) 展示了如何使用 `SelectMany` 实现 `Select` 和 `Where`。

## Cast

C# 的类型系统并不无所不知。有时我们可能知道有关从可观察源中出现的值的类型的一些信息，这些信息不反映在该源的类型中。这可能基于特定域的知识。例如，对于由船只广播的 AIS 消息，我们可能知道如果消息类型是 3，它将包含导航信息。这意味着我们可以编写如下代码：

```csharp
IObservable<IVesselNavigation> type3 = 
   receiverHost.Messages.Where(v => v.MessageType == 3)
                        .Cast<IVesselNavigation>();
```

这使用了 `Cast`，这是一个标准 LINQ 操作符，我们可以在知道某个集合中的项目是某个特定类型而类型系统未能推断出来时使用它。

`Cast` 和 [第 5 章中显示的 `OfType` 操作符](05_Filtering.md#oftype) 的区别在于它们处理不是指定类型的项目的方式。`OfType` 是一个过滤操作符，因此它只是过滤掉不是指定类型的任何项目。但是使用 `Cast`（如同正常的 C# 类型转换表达式一样），我们断言我们期望源项目是指定类型的，因此如果其源产生的项目与指定类型不兼容，`Cast` 返回的可观察对象将调用其订阅者的 `OnError`。

如果我们使用其他更基本的操作符重建 `Cast` 和 `OfType` 的功能，这种区别可能更容易看到。

```csharp
// source.Cast<int>(); 等同于
source.Select(i => (int)i);

// source.OfType<int>();
source.Where(i => i is int).Select(i => (int)i);
```

## Materialize 和 Dematerialize

`Materialize` 操作符将类型为 `IObservable<T>` 的源转换为 `IObservable<Notification<T>>`。对于源产生的每个项目，它将提供一个 `Notification<T>`，如果源终止，它将产生一个最终的 `Notification<T>` 表示它是成功完成还是带错误结束。

这很有用，因为它生成描述整个序列的对象。如果你想要记录一个可观察对象的输出，以便以后可以重放...好吧，你可能会使用 `ReplaySubject<T>`，因为它专为此任务设计。但是如果你想要做的不仅仅是重播序列——例如检查项目或在回复前修改它们——你可能想编写自己的代码来存储项目。`Notification<T>` 可以帮助，因为它使你能够以统一的方式表示源的所有操作。你不需要单独存储有关序列如何或如何结束的信息——此信息只是最后的 `Notification<T>`。

你可以想象将这与单元测试中的 `ToArray` 结合使用。这将使你能够获取一个类型为 `Notification<T>[]` 的数组，包含源的完整描述，从而轻松编写询问序列中第三个项目是什么的测试。（Rx.NET 源代码本身在许多测试中使用了 `Notification<T>`。）

如果我们实现化一个序列，我们可以看到返回的封装值。

```csharp
Observable.Range(1, 3)
          .Materialize()
          .Dump("Materialize");
```

输出：

```
Materialize --> OnNext(1)
Materialize --> OnNext(2)
Materialize --> OnNext(3)
Materialize --> OnCompleted()
Materialize completed
```

注意当源序列完成时，物化的序列产生一个 'OnCompleted' 通知值然后完成。`Notification<T>` 是一个抽象类，有三个实现：

 * OnNextNotification
 * OnErrorNotification
 * OnCompletedNotification

`Notification<T>` 公开四个公共属性以帮助您检查它：`Kind`、`HasValue`、`Value` 和 `Exception`。显然，只有 `OnNextNotification` 将对 `HasValue` 返回 true 并有 `Value` 的有用实现。同样，`OnErrorNotification` 是唯一具有 `Exception` 值的实现。`Kind` 属性返回一个 `enum`，允许您知道哪些方法适合使用。

```csharp
public enum NotificationKind
{
    OnNext,
    OnError,
    OnCompleted,
}
```

在这个接下来的例子中，我们生成一个出错的序列。注意，物化序列的最终值是一个 `OnErrorNotification`。还要注意，物化的序列没有出错，它成功完成了。

```csharp
var source = new Subject<int>();
source.Materialize()
      .Dump("Materialize");

source.OnNext(1);
source.OnNext(2);
source.OnNext(3);

source.OnError(new Exception("Fail?"));
```

输出：

```
Materialize --> OnNext(1)
Materialize --> OnNext(2)
Materialize --> OnNext(3)
Materialize --> OnError(System.Exception)
Materialize completed
```

物化一个序列对于执行序列的分析或记录非常方便。你可以通过应用 `Dematerialize` 扩展方法来解包一个物化的序列。`Dematerialize` 只能用于 `IObservable<Notification<TSource>>`。

这完成了我们对转换操作符的介绍。它们的共同特征是它们为每个输入项产生一个输出（或在 `SelectMany` 的情况下，一组输出）。接下来我们将看看可以从其源中的多个项目中结合信息的操作符。