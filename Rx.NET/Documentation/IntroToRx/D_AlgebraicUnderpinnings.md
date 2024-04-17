# 附录D：Rx的代数基础

Rx操作符可以以您能想象的几乎任何方式组合在一起，它们通常能够无问题地组合。这一点的实现不仅仅是一个幸运的偶然。一般来说，软件组件之间的集成往往是软件开发中最大的痛点之一，所以这种良好的集成是非常显著的。这在很大程度上归功于Rx依赖于一些底层理论。Rx的设计使得您不需要了解这些细节就可以使用它，但好奇的开发者通常想要了解这些事情。

本书的前几章已经讨论了Rx的一个形式方面：可观察源与其观察者之间的契约。对于`IObserver<T>`的可接受使用有一个明确定义的语法。这超出了.NET类型系统能够强制执行的范围，所以我们依赖于代码做正确的事情。然而，`System.Reactive`库总是遵守这一契约，并且还有一些防护类型的措施，用来检测应用程序代码是否没有完全按规则行事，以防止这种情况引发灾难。

`IObserver<T>`的语法非常重要。组件依赖它来确保正确操作。例如，考虑`Where`操作符。它提供了自己的`IObserver<T>`实现，通过它订阅底层源。这将从该源接收项目，然后决定将哪些项目转发给订阅`Where`所呈现的`IObservable<T>`的观察者。您可以将它想象成这样：

```csharp
public class OverSimplifiedWhereObserver<T> : IObserver<T>
{
    private IObserver<T> downstreamSubscriber;
    private readonly Func<T, bool> predicate;

    public OverSimplifiedWhereObserver(
        IObserver<T> downstreamSubscriber, Func<T, bool> predicate)
    {
        this.downstreamSubscriber = downstreamSubscriber;
        this.predicate = predicate;
    }

    public void OnNext(T value)
    {
        if (this.predicate(value))
        {
            this.downstreamSubscriber.OnNext(value);
        }
    }

    public void OnCompleted()
    {
        this.downstreamSubscriber.OnCompleted();
    }

    public void OnError(Exception x)
    {
        this.downstreamSubscriber.OnCompleted(x);
    }
}
```

这不需要采取任何明确的步骤来遵循`IObserver<T>`语法。如果它订阅的源也遵守这些规则，则它不需要这样做。由于这个类只在它自己的`OnNext`中调用它的订阅者的`OnNext`，`OnCompleted`和`OnError`也是如此，只要它所订阅的底层源遵守调用这三种方法的规则，这个类也会自动遵循这些规则。

事实上，`System.Reactive`并不是那么信任。它确实有一些代码可以检测到某些语法违规情况，但即使这些措施仅仅确保一旦执行进入Rx，就会遵守语法。系统的边界有一些检查，但Rx的内部严重依赖于上游源将遵守规则。

然而，`IObservable<T>`的语法并不是Rx依赖形式主义以确保正确操作的唯一地方。它还依赖于一组特定的数学概念：

* 单子(Monads)
* 合子(Catamorphisms)
* 展子(Anamorphisms)

标准LINQ操作符可以完全用这三个概念来表达。

这些概念来自[范畴理论](https://en.wikipedia.org/wiki/Category_theory)，这是一个关于数学结构的非常抽象的数学分支。在1980年代末，一些计算机科学家正在探索这一数学领域，试图使用它们来模拟程序的行为。[Eugenio Moggi](https://en.wikipedia.org/wiki/Eugenio_Moggi)（当时在爱丁堡大学工作的意大利计算机科学家）通常被认为是意识到单子特别适合描述计算，正如他1991年的论文[Notions of computations and monads](https://person.dibris.unige.it/moggi-eugenio/ftp/ic91.pdf)所解释的那样。这些理论观点被纳入到Haskell编程语言中，主要通过Philip Wadler和Simon Peyton Jones的贡献，在1992年他们发布了关于[monadic handling of IO](https://www.microsoft.com/en-us/research/wp-content/uploads/1993/01/imperative.pdf)（IO的单态处理）的建议。到1996年，这已经完全被纳入Haskell v1.3版本，使得程序处理输入和输出（例如处理用户输入或向文件写入数据）的方式以强大的数学基础为支撑。这被广泛认为是比Haskell早期尝试以纯函数式语言建模IO的现实更好的改进。

这些数学基础是为何重要？这些数学基础正是LINQ操作符能够自由组合的原因。

范畴理论作为数学学科，对各种数学结构有了非常深入的理解，而对程序员最有用的是，它提供了一些规则，如果遵循这些规则，可以确保软件元素在组合在一起时的良好行为。这确实是一个相当含糊的解释。如果您想要详细了解如何将范畴理论应用于编程以及为什么这样做有用，我非常推荐[Bartosz Milewski的《程序员的范畴理论》](https://bartoszmilewski.com/2014/10/28/category-theory-for-programmers-the-preface/)。那里的信息量庞大，可以清楚地说明为什么我不打算在这个附录中尝试进行完整的解释。相反，我的目标只是概述基本概念，并解释它们如何与Rx的特性对应。

## 单子

单子是支撑LINQ（因此也是Rx）设计的最重要的数学概念之一。实际上，不必了解单子是什么就可以使用Rx。最重要的事实是，它们的数学特性（特别是它们对组合的支持）使Rx操作符能够自由组合在一起。从实际角度来看，最重要的是它能够正常工作，但如果你读到这里，这可能还不能让你满意。

描述精确的数学对象通常很难，因为它们本质上是抽象的。因此，在我解释单子的定义之前，了解LINQ如何使用这个概念可能会有所帮助。LINQ将单子视为一种通用的表示项的容器。作为开发者，我们知道有许多种类型的东西可以包含项。有数组，还有其他集合类型，如`IList<T>`。还有数据库，尽管数据库表与数组有很多不同之处，但在某些方面它们也是相似的。LINQ的基本见解是，存在一种数学抽象，可以捕捉容器共有的本质。如果我们确定某种.NET类型代表一个单子，那么多年来数学家们为了理解单子的特性和行为所做的所有工作都将适用于该.NET类型。

例如，`IEnumerable<T>`是一个单子，`IQueryable<T>`也是。关键的是对于Rx，`IObservable<T>`也是。LINQ的设计依赖于单子的属性，所以如果您可以确定某种.NET类型是单子，那么它就是LINQ实现的候选对象。（反之，如果你尝试为一个不是单子的类型创建LINQ提供者，你可能会遇到问题。）

那么，LINQ依赖的这些特性是什么呢？第一个直接关系到容纳：必须有可能取得某个值并将其放入你的单子中。您会注意到我到目前为止给出的所有例子都是泛型类型，这并非巧合：单子本质上是类型构造器，类型参数指示您希望单子包含的事物类型。所以给定某个类型`T`的值，必须有可能将它封装在该类型的单子中。给定一个`int`，我们可以得到一个`IEnumerable<int>`，如果做不到这一点，`IEnumerable<T>`就不是单子。第二个特性更难以界定，而不陷入高度抽象，但本质上归结为这样一个想法：如果我们有可以应用于单个包含项的函数，如果这些函数以有用的方式组合，我们可以创建新的函数来操作不是单个值而是容器，关键是，这些函数也可以以相同的方式组合。

这使我们能够像操作单个值一样自由地处理整个容器。

### 单子的操作：return 和 bind

我们刚刚看到了，单子不仅仅是一个类型。它们需要提供某些操作。这第一个操作，将一个值包装在单子内，有时在数学文本中称为 _unit_，但在计算环境中更常称为 _return_。这就是[`Observable.Return`](03_CreatingObservableSequences.md#observablereturn) 如何得名的。

并不一定需要一个实际的函数。只要有某种机制可以将值放入单子，就满足了单子的规律。例如，不像`Observable`，`Enumerable`类型没有定义一个`Return`方法，但这并不重要。你可以简单写成`new[] { value }`，这就足够了。

单子需要提供的另一个操作只有一个。数学文献称之为 _bind_，一些编程系统称其为`flatMap`，LINQ称之为`SelectMany`。这是一个最可能让人头疼的操作，因为尽管它有一个清晰的正式定义，比起_return_来说，很难说它究竟做了什么。然而，我们通过它们能够表示容器的能力来看待单子，这为我们提供了一种相当直接的方式来理解bind/`SelectMany`：它允许我们接受一个每个项都是嵌套容器的容器（例如，一个数组的数组，或一个`IEnumerable<IEnumerable<T>>`），并将其展平。例如，一个列表的列表将变为一个列表，包含每个列表中的每个项。我们很快会看到，这与bind的正式数学定义不明显相关，但它与之兼容，这是允许我们享用数学家劳动成果所需要的全部条件。

至关重要的是，作为单子，刚才描述的两个操作（return和bind）必须符合某些规则，或者经常在文献中描述的_laws（规律）_。这些法则有些抽象，因此不是很明显_为什么_它们能够实现这一点，但它们是不容置疑的。如果你的类型和操作不遵循这些法则，那么你就没有一个单子，因此你不能依靠单子所保证的特性。

bind实际上是什么样的？下面是`IEnumerable<T>`的bind看起来是什么样的：

```csharp
public static IEnumerable<TResult> SelectMany<TSource, TResult> (
    this IEnumerable<TSource> source,
    Func<TSource,IEnumerable<TResult>> selector);
```

所以它是一个函数，接受两个输入。第一个是`IEnumerable<TSource>`。第二个输入是一个函数，当提供一个`TSource`时产生一个`IEnumerable<TResult>`。当你用这两个参数调用`SelectMany`（也就是_bind_）时，你会得到一个`IEnumerable<TResult>`。虽然bind的正式定义需要它有这种形式，但它并没有规定任何特定的行为——任何符合法则的东西都是可以接受的。但在LINQ的上下文中，我们确实期望一个具体的行为：这将调用函数（第二个参数）一次，为源枚举（第一个参数）中的每个`TSource`，然后收集所有由函数返回的`IEnumerable<TResult>`集合中的所有`TResult`值，包装它们为一个大的`IEnumerable<TResult>`。在这个特定的`IEnumerable<T>`案例中，我们可以将`SelectMany`描述为为每个输入值获取一个输出集合，然后将所有这些输出集合连接在一起。

但我们现在已经变得有点太具体了。即使我们专门看LINQ如何使用单子来表示泛化的容器，`SelectMany`也并不一定需要连接。它只需要确保由`SelectMany`返回的容器包含由回调为个别输入所生成的每个值即可。连接是一种策略，但Rx做的是不同的事情。由于可观察对象倾向于根据它们的意愿产生值，由函数生成的个别`TSource`的`IObservable<TResult>`在该函数产生值时，由`Observable.SelectMany`返回的`IObservable<TResult>`就会产生一个值。（它执行了一些同步操作以确保它遵循Rx对`IObserver<T>`调用的规则，所以如果一个可观察对象在调用订阅者的`OnNext`时产生了一个值，它会等待它返回，然后再推送下一个值。但除此之外，它只是将所有值直接推送。）因此，源值在这里基本上是交错的，而不是连接的。但更广泛的原则——结果是一个容器，包含由回调为个别输入所产生的每个值——适用。

数学定义的单子bind具有相同的基本形状，它只是没有规定特定的行为。所以任何单子都会有一个bind操作，它接受两个输入：一个为某个输入值类型(`TSource`)构造的单子类型的实例，以及一个函数，该函数以`TSource`作为输入并产生某个输出值类型(`TResult`)的单子类型的实例。当你用这两个输入调用bind时，结果是一个为输出值类型构造的单子类型的实例。我们不能在C#的类型系统中精确地表示这个一般性的想法，但这可以给出一个大致的感觉：

```csharp
// 对单子bind的总体形式的印象派素描
public static M<TResult> SelectMany<TSource, TResult> (
    this M<TSource> source,
    Func<TSource, M<TResult>> selector);
```

用你选择的单子类型（`IObservable<T>`、`IEnumerable<T>`、`IQueryable<T>`或其他）替换`M<T>`，这会告诉你bind对于特定类型应该是什么样的。

但提供这两个函数，return和bind，还不够。不仅它们必须具有正确的形状，它们还必须遵守法则。

### 单子法则

因此，单子由类型构造器（例如`IObservable<T>`）和两个函数`Return`和`SelectMany`组成。（从现在开始我将只使用这些LINQ式的名字。）但要符合单子的标准，这些特征必须遵守三个“法则”（这里以非常简洁的形式给出，我将在以下部分解释）：

1. `Return` 是 `SelectMany` 的“左恒等式”
2. `Return` 是 `SelectMany` 的“右恒等式”
3. `SelectMany` 实际上应该是关联的

让我们更详细地看看这些：

#### 单子法则1：`Return` 是 `SelectMany` 的“左恒等式”

这个法则意味着，如果你把某个值`x`传递给`Return`，然后将结果作为一个输入传递给`SelectMany`，其中另一个输入是一个函数`SomeFunc`，那么结果应该与直接将`x`传递给`SomeFunc`相同。例如：

```csharp
// 假设有这样一个函数：
//  IObservable<bool> SomeFunc(int)
// 那么这两个应该是相同的。
IObservable<bool> o1 = Observable.Return(42).SelectMany(SomeFunc);
IObservable<bool> o2 = SomeFunc(42);
```

这里一个非正式的理解方式：`SelectMany` 将输入容器中的每个项目通过`SomeFunc`推送，并且每个这样的调用都会产生一个类型为`IObservable<bool>`的容器，并且它收集所有这些容器到一个大的`IObservable<bool>`中，该容器包含所有单个`IObservable<bool>`容器中的项目。但在这个例子中，我们提供给`SelectMany`的输入只包含一个项目，这意味着没有收集工作要做。`SelectMany`将只调用我们的函数一次，并使用那个唯一的输入，并且这将只产生一个输出`IObservable<bool>`。`SelectMany`有责任返回一个`IObservable<bool>`，它包含它从对`SomeFunc`的单个调用中得到的单个`IObservable<bool>`中的所有项目。在这种情况下，没有其他进一步处理要做的了。因为只有一个对`SomeFunc`的调用，所以它不需要在这种情况下合并多个容器中的项目：由单个调用产生的那个单个输出包含应该在`SelectMany`要返回的容器中的所有内容。因此，我们可以直接使用单个输入项调用 `SomeFunc`。

如果`SelectMany`做了别的事情，那会很奇怪。如果`o1`以某种方式不同，那意味着三种可能之一：

* `o1` 包含不在`o2`中的项目（意味着它以某种方式包含了未由`SomeFunc`产生的项目）
* `o2` 包含不在`o1`中的项目（意味着`SelectMany`遗漏了一些由`SomeFunc`产生的项目）
* `o1` 和 `o2` 包含相同的项目，但以某种依赖于使用的单子类型的特定方式可检测到的不同（例如，项目的顺序不同）

因此，这个法则实质上规定了`SelectMany`不应该添加或移除项目，或者不应该保留单子使用应该保留的特性，比如顺序。（注意，在.NET LINQ提供者中，这通常不要求这些对象完全相同。它们通常不会。它只意味着它们必须代表完全相同的事物。例如，在这种情况下 `o1` 和 `o2` 都是`IEnumerable<bool>`，这意味着它们应该生成完全相同的 `bool` 值序列。）

#### 单子定律2：`Return`是`SelectMany`的“左同一性”

这个法则意味着，如果将 `Return` 作为函数输入传递给 `SelectMany`，然后将某个已构建的单子类型值作为另一个参数传入，你应该得到相同的值作为输出。例如：

```csharp
// 这两个应该是相同的。
IObservable<int> o1 = GetAnySource();
IObservable<int> o2 = o1.SelectMany(Observable.Return);
```

通过使用 `Return` 作为传递给 `SelectMany` 的函数，我们本质上是在请求将输入容器中的每个项都包装在它自己的容器中（`Return` 包装单个项），然后将所有这些容器重新展平成一个容器。我们增加了一层包装然后又删除它，因此理解这应该没有任何效果是有意义的。

#### 单子法则3：`SelectMany` 应该是关联的

假设我们有两个函数，`Tx1` 和 `Tx2`，每个函数都是适合传递给 `SelectMany` 的形式。我们可以有两种方式应用这些函数：

```csharp
// 这两个应该是相同的。
IObservable<int> o1 = source.SelectMany(x => Tx1(x).SelectMany(Tx2));
IObservable<int> o2 = source.SelectMany(x => Tx1(x)).SelectMany(Tx2);
```

这里的区别只是括号的位置略有变化：改变的只是右侧的 `SelectMany` 调用是在传递给其他 `SelectMany` 的函数内部调用的，还是在其他 `SelectMany` 的结果上调用的。以下例子调整了布局，并且用完全等效的 `Tx1` 替换了 lambda `x => Tx1(x)`，这可能使结构上的区别更容易看出：

```csharp
IObservable<int> o1 = source
    .SelectMany(x => Tx1(x).SelectMany(Tx2));
IObservable<int> o2 = source
    .SelectMany(Tx1)
    .SelectMany(Tx2);
```

第三个法则说这两种方式应该具有相同的效果。无论第二个 `SelectMany` 调用（用于 `Tx2`）发生在第一个 `SelectMany` 调用的“内部”还是之后，都不应有区别。

一个非正式的方式来思考这一点是，`SelectMany` 实质上应用了两个操作：一个转换和一个展开。转换是由你传递给 `SelectMany` 的任何函数定义的，但因为该函数返回单子类型（在LINQ术语中返回一个可能包含任意数量项目的容器），`SelectMany` 在将项目传递给该函数时展开每个返回的容器，以便将所有项目收集到最终返回的单个容器中。当你嵌套这种操作时，展开的顺序无关紧要。例如，考虑这些函数：

```csharp
IObservable<int> Tx1(int i) => Observable.Range(1, i);
IObservable<string> Tx2(int i) => Observable.Return(i.ToString());
```

第一个将一个数字转换为同样长度的数字范围。`1` 变为 `[1]`，`3` 变为 `[1, 2, 3]` 等。在我们到达 `SelectMany` 之前，想象如果我们用 `Select` 在产生数字范围的可观察源上使用这个函数会发生什么：

```csharp
IObservable<int> input = Observable.Range(1, 3); // [1,2,3]
IObservable<IObservable<int>> expandTx1 = input.Select(Tx1);
```

我们得到了一系列的序列。`expand2` 实际上是这样的：

```
[
    [1],
    [1,2],
    [1,2,3],
]
```

如果我们曾经使用 `SelectMany`：

```csharp
IObservable<int> expandTx1Collect = input.SelectMany(Tx1);
```

它将应用相同的转换，但然后将结果展平回单个列表中：

```
[
    1,
    1,2,
    1,2,3,
]
```

我保留了换行符以强调这与前面输出的联系，但我也可以只写成 `[1, 1, 2, 1, 2, 3]`。

如果我们接着想应用第二次转换，我们可以使用 `Select`：

```csharp
IObservable<IObservable<string>> expandTx1CollectExpandTx2 = expandTx1Collect
    .SelectMany(Tx1)
    .Select(Tx2);
```

这将每个 `expandTx1Collect` 中的数字传递给 `Tx2`，将其转换为只包含单个字符串的序列：

```
[
    ["1"],
    ["1"],["2"],
    ["1"],["2"],["3"]
]
```

但如果我们也在最后的位置使用 `SelectMany`：

```csharp
IObservable<string> expandTx1CollectExpandTx2Collect = expandTx1Collect
    .SelectMany(Tx1)
    .SelectMany(Tx2);
```

它将这些展平回单个字符串中：

```
[
    "1",
    "1","2",
    "1","2","3"
]
```

关联性需求表示，如果我们在传递给第一个 `SelectMany` 的函数内部应用 `Tx1` 而不是将其应用于第一个 `SelectMany` 的结果，那么这不应有区别。因此，我们不是从这个开始：

```csharp
IObservable<IObservable<int>> expandTx1 = input.Select(Tx1);
```

我们可能会写出这个：

```csharp
IObservable<IObservable<IObservable<string>>> expandTx1ExpandTx2 =
    input.Select(x => Tx1(x).Select(Tx2));
```

这将产生：

```
[
    [["1"]],
    [["1"],["2"]],
    [["1"],["2"],["3"]]
]
```

如果我们将内部调用改为使用 `SelectMany`：

```csharp
IObservable<IObservable<string>> expandTx1ExpandTx2Collect =
    input.Select(x => Tx1(x).SelectMany(Tx2));
```

这将展平内部项目（但我们仍在外部使用 `Select`，所以我们仍然得到列表的列表）产生这个：

```
[
    ["1"],
    ["1","2"],
    ["1","2","3"]
]
```

然后，如果我们将第一个 `Select` 更改为 `SelectMany`：

```csharp
IObservable<string> expandTx1ExpandTx2CollectCollect =
    input.SelectMany(x => Tx1(x).SelectMany(Tx2));
```

它将展平最外层的列表，给我们带来：

```
[
    "1",
    "1","2",
    "1","2","3"
]
```

这与我们之前得到的最终结果相同，正如第三个单子法则所要求的。

总结一下，这里的两个过程是：

* 展开并转换 Tx1，展平，展开并转换 Tx2，展平
* 展开并转换 Tx1，展开并转换 Tx2，展平，展平

这两个过程都应用了两次转换，并展平了这些转换添加的额外的包含层，因此尽管中间步骤看起来不同，我们最终得到了相同的结果，因为无论你是在每次转换后都展开，还是在进行所有转换后再展开，都没有关系。

#### 为什么这些法则很重要

这三个法则直接反映了适用于数字上函数组合的法则。如果我们有两个函数，\(f\) 和 \(g\)，我们可以写一个新的函数 \(h\)，定义为 \(g(f(x))\)。这种组合函数的方式被称为 _组合_，通常写为 \(g \circ f\)。如果恒等函数称为 \(id\)，那么以下陈述是真实的：

* \(id \circ f\) 等同于只有 \(f\)
* \(f \circ id\) 等同于只有 \(f\)
* \((f \circ g) \circ s\) 等同于 \(f \circ (g \circ s)\)

这些直接对应于三个单子法则。非正式地说，这反映了单子的 bind 操作（`SelectMany`）在结构上与函数组合有深刻的相似性。这就是为什么我们可以自由组合 LINQ 操作符的原因。

### 使用 `SelectMany` 重建其他操作符

记住，LINQ 的核心是三个数学概念：单子、展子和合子。因此，尽管前面的讨论重点是 `SelectMany`，但它的意义要广泛得多，因为我们可以用这些原语表达其他标准的 LINQ 操作符。例如，这展示了我们如何仅使用 `Return` 和 `SelectMany` 实现 [`Where`](05_Filtering.md#where)：

```csharp
public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
{
    return source.SelectMany(item =>
        predicate(item)
            ? Observable.Return(item)
            : Observable.Empty<T>());
}
```

这实现了 `Select`：

```csharp
public static IObservable<TResult> Select<TSource, TResult>(
    this IObservable<TSource> source, Func<TSource, TResult> f)
{
    return source.SelectMany(item => Observable.Return(f(item)));
}
```

有些操作符需要展子或合子，所以现在让我们看一下这些。

## 合子（Catamorphisms）

合子本质上是对任何类型的处理的泛化，它考虑到了容器中的每个项目。在 LINQ 的实践中，这通常意味着检查所有的值，并产生一个结果值，如 [Observable.Sum](07_Aggregation.md#sum)。更广泛地说，任何形式的聚合都构成合子。合子的数学定义比这更泛化——例如，它不一定要将事物简化为单一值——但为了理解 LINQ，这种以容器为导向的观点是最直接的方法来考虑这个结构。

合子是 LINQ 的基本构建块之一，因为你不能仅用其他元素构造合子。但有许多 LINQ 操作符可以由 LINQ 最基本的合子，[`Aggregate`](07_Aggregation.md#aggregate) 操作符构建。例如，这是用 `Aggregate` 实现 `Count` 的一种方式：

```csharp
public static IObservable<int> MyCount<T>(this IObservable<T> items)
    => items.Aggregate(0, (total, _) => total + 1);
```

我们可以这样实现 `Sum`：

```csharp
public static IObservable<T> MySum<T>(this IObservable<T> items)
    where T : INumber<T>
    => items.Aggregate(T.Zero, (total, x) => x + total);
```

这比我在 [聚合章节](07_Aggregation.md) 中展示的类似 sum 示例更灵活，因为那个只适用于 `IObservable<int>`。在这里，我使用了 C# 11.0 和 .NET 7.0 中新增的 _泛型数学_ 功能，使 `MySum` 能够适用于任何类似数字的类型。但操作原理基本相同。

如果您是为了理论而来，仅仅看到各种聚合操作符都是 `Aggregate` 的特例可能不足以满足您。合子究竟是什么呢？其中一个定义是“从初始代数到某种其他代数的唯一同态”，但就像范畴理论通常的情况一样，这种解释最容易理解的前提是你已经理解了它试图描述的概念。如果你试图用学校数学形式的代数理解这个定义，其中我们写出一些值由字母表示的方程，很难理解这个定义。这是因为合子采用了更广泛的视角来看待“代数”的含义，基本上意味着某种形式的表达式可以被构建和评估。

更准确地说，合子是相对于某种称为 F-代数的东西来描述的。这是三个东西的组合：

1. 一个函数子（Functor）_F_，它在某个范畴 _C_ 上定义了某种结构
2. 范畴 _C_ 中的某个对象 _A_
3. 从 _F A_ 到 _A_ 的态射，有效地评估结构

但这开启了比它回答的更多的问题。所以让我们从一个明显的问题开始：什么是函数子？从 LINQ 的角度看，它本质上是任何实现了 `Select` 的东西。（一些编程系统称这为 `fmap`。）从我们以容器为导向的视角来看，它是两个东西：1) 一个类似容器的类型构造器（例如，像 `IEnumerable<T>` 或 `IObservable<T>` 这样的东西）和 2) 一种将函数应用于容器中的所有内容的方法。因此，如果你有一个从 `string` 转换为 `int` 的函数，一个函数子让你可以一次性地将其应用于它所包含的所有内容。

合子配合 `IEnumerable<T>` 和其 `Select` 扩展方法是一个函数子。你可以使用 `Select` 将一个 `IEnumerable<string>` 转换为一个 `IEnumerable<int>`。`IObservable<T>` 和它的 `Select` 形式构成了另一个函数子，我们可以利用这些从 `IObservable<string>` 到 `IObservable<int>`。关于 "在某个范畴 _C_ 上" 的部分呢？这提示了数学描述中函数子的范围更广。当开发者使用范畴理论时，我们通常坚持使用代表类型（如编程语言中的 `int` 类型）和函数的范畴。（严格来说，函数子从一个范畴映射到另一个范畴，所以在最一般的情况下，函数子将某个范畴 _C_ 中的对象和态射映射到某个范畴 _D_ 中的对象和态射。但出于编程的目的，我们总是使用代表类型的范畴，所以对于我们使用的函数子来说，_C_ 和 _D_ 将是相同的东西。严格来说，这意味着我们应该称它们为自函子（Endofunctors），但似乎没有人在意。实际上我们使用了更一般形式的名字，函数子（Functor），并默认我们指的是自函子在类型和函数的范畴上。）

那么，这就是函数子的部分。接下来是 2，“_C_ 范畴中的某个对象 _A_”。好，_C_ 是函数子的范畴，我们刚才确定该范畴中的对象是类型，所以 _A_ 可能是 `string` 类型。如果我们选择的函数子是 `IObservable<T>` 和它的 `Select` 方法的组合，那么 _F A_ 将是 `IObservable<string>`。

那么“态射”又是什么呢？再次，就我们的目的而言，我们只使用类型和函数的自函数子，因此在这种情况下，态射只是函数。因此，我们可以将 F-代数的定义重新表述为更熟悉的术语：

1. 一些类似容器的泛型类型，如 `IObservable<T>`
2. 一个项类型 `A`（例如，`string` 或 `int`）
3. 一个接受 `IObservable<A>` 并返回类型为 `A` 的值的函数（例如, `Observable.Aggregate<A>`）

这更具体一些。范畴理论通常关注于捕捉数学结构的最一般真理，并且这种改革将这种普遍性抛弃了。然而，从一个寻求依靠数学理论的程序员的角度来看，这是可以的。只要我们所做的事情符合 F-代数的模式，数学家们推导出的所有一般结果都将适用于我们对理论的更专业的应用。

不过，为了给你展示一般概念的 F-代数 可以实现的种类，有可能让函数子成为表示编程语言中表达式的类型，你可以创建一个 F-代数，对这些表达式进行评估。这与 LINQ 的 `Aggregate` 类似，因为它遍历 Functor 表示的整个结构（如果是 `IEnumerable<T>`，就是列表中的每个元素；如果你是表示表达式，就是每个子表达式）并将整个结构简化为单一值，但代替我们的函数子表示一系列事物，它具有更复杂的结构：编程语言中的表达式。

所以，这就是 F-代数。从理论角度看，重要的是第三部分不一定需要简化。理论上，类型可以是递归的，项目类型 _A_ 可以是 _F A_。这对于诸如表达式这样的固有递归结构很重要，并且通常有一个在函数中只处理结构，不实际执行任何简化的最一般 F-代数。这些想法的一般化就是合子概念，即存在同样函数子的其他不那么一般的 F-代数。

例如，对于 `IObservable<T>`，一般概念是每个由某个源产生的项目可以通过反复应用某个函数来处理，这个函数有两个参数，一个是容器中的类型 `T` 的值，另一个是某种累加器，代表到目前为止累积的所有信息。这个函数将返回更新的累加器，准备好与下一个 `T` 一起再次传递给该函数。然后有更具体的形式，其中应用了具体的积累逻辑（例如，求和或确定最大值）。从技术上讲，这里的合子是从一般形式到更具体形式的连接。但在实践中，常常将具体的特殊形式（如 [`Sum`](07_Aggregation.md#sum) 或 [`Average`](07_Aggregation.md#average)）称为合子。

### 保持在容器内

虽然合子一般可以脱离容器（例如，`IEnumerable<int>` 的 `Sum` 产生一个 `int`），但这并非绝对必要，并且在 Rx 中，大多数合子不这样做。正如在线程和调度章节的 [Lock-ups](11_SchedulingAndThreading.md#lock-ups) 部分所述，当某个 `IObservable<T>` 必须做某事（例如，如果你想计算项目的总和，你必须等到你看到所有项目）时，阻塞某个线程以等待结果是实际操作中的死锁风险。

因此，大多数合子执行某种简化，但继续产生包裹在 `IObservable<T>` 中的结果。

## 展子（Anamorphisms）

粗略地说，展子与合子相反。尽管合子基本上将某种结构简化为更简单的东西，展子则将某些输入扩展为更复杂的结构。例如，给定某个数字（例如，5），我们可以想象一种机制，将其转换为具有指定数量元素的序列（例如，[0,1,2,3,4]）。

实际上我们不必想象这样的事物：这正是 [`Observable.Range`](03_CreatingObservableSequences.md#observablerange) 所做的。

我们可以将单子的 `Return` 操作视为一个非常简单的展子。给定类型 `T` 的某个值，[`Observable.Return`](03_CreatingObservableSequences.md#observablereturn) 将其扩展为一个 `IObservable<T>`。展子本质上是此类想法的泛化。

展子的数学定义是“将一个余代数分配给其唯一的态射到一个自函子的最终余代数”。这是合子定义的“对偶”，从范畴理论的角度来看，基本上意味着所有态射的方向都相反。在我们并非完全泛化的范畴理论应用中，这里的态射是合子中将项目简化为某个输出的态射，因此在展子中，这变成了将某个值扩展为容器类型的某个实例（例如，从 `int` 到 `IObservable<int>`）。

我不打算像合子那样详细介绍，而是指出这个核心部分：一个函数子的最一般 F-代数体现了对函数子的本质结构的理解，合子利用 этто定义各种简化。类似地，一个函数子的最一般余代数也体现了对函数子的本质结构的理解，展子利用这一点来定义各种扩展。

[`Observable.Generate`](03_CreatingObservableSequences.md#observablegenerate) 代表了这种最一般的能力：它具有生成一个 `IObservable<T>` 的能力，但需要提供一些专门的扩展函数来生成任何特定的可观察对象。

## 理论的终结

现在我们已经回顾了 LINQ 背后的理论概念，让我们回过头来看看我们如何使用它们。我们有三种操作：

* 展子进入序列：`T1 --> IObservable<T2>`
* Bind 修改序列。`IObservable<T1> --> IObservable<T2>`
* 合子离开序列。逻辑上是 `IObservable<T1> --> T2`，但实际上通常是 `IObservable<T1> --> IObservable<T2>`，其中输出可观察对象只产生单一值

顺便说一下，bind 和 catamorphism 由谷歌的 [MapReduce](http://en.wikipedia.org/wiki/MapReduce) 框架使广为人知。在这里，谷歌引用了在某些函数式语言中更常用的名称，Map 和 Reduce。

大多数 Rx 操作符实际上都是更高阶函数概念的特例。给出一些例子：

- 展子（Anamorphisms）:
  - [`Generate`](03_CreatingObservableSequences.md#observablegenerate)
  - [`Range`](03_CreatingObservableSequences.md#observablerange)
  - [`Return`](03_CreatingObservableSequences.md#observablereturn)
- Bind:
  - [`SelectMany`](06_Transformation.md#selectmany)
  - [`Select`](06_Transformation.md#select)
  - [`Where`](05_Filtering.md)
- 合子（Catamorphism）:
  - [`Aggregate`](07_Aggregation.md#aggregate)
  - [`Sum`](07_Aggregation.md#sum)
  - [`Min` 和 `Max`](07_Aggregation.md#min-and-max)

## Amb

当我开始使用 Rx 时，`Amb` 方法是一个我不熟悉的新概念。这个函数最初由 [John McCarthy](https://en.wikipedia.org/wiki/John_McCarthy_(computer_scientist)) 在其 1961 年的论文 [《计算的数学理论基础》](https://www.cambridge.org/core/journals/journal-of-symbolic-logic/article/abs/john-mccarthy-a-basis-for-a-mathematical-theory-of-computation-preliminary-report-proceedings-of-the-western-joint-computer-conference-papers-presented-at-the-joint-ireaieeacm-computer-conference-los-angeles-calif-may-911-1961-western-joint-computer-conference-1961-pp-225238-john-mccarthy-a-basis-for-a-mathematical-theory-of-computation-computer-programming-and-formal-systems-edited-by-p-braffort-and-d-hirschberg-studies-in-logic-and-the-foundations-of-mathematics-northholland-publishing-company-amsterdam1963-pp-3370/D1AD4E0CDB7FBE099B04BB4DAF24AFFA) 中首次介绍。这是个缩写，取自单词 _Ambiguous_。Rx 在这里稍微偏离了正常的 .NET 类库命名约定，部分是因为 `amb` 是这个操作符的既定名称，但也作为对 McCarthy 工作的致敬，他的工作启发了 Rx 的设计。

但 `Amb` 究竟做什么呢？[_ambiguous function_](http://www-formal.stanford.edu/jmc/basis1/node7.html) 的基本思想是我们被允许定义多种产生结果的方式，这些方式在实际操作中可能无法产生结果。假设我们定义了一些名为 `equivocate` 的模糊函数，也许对于某个特定的输入值，`equivocate` 的所有组成部分——我们提供的所有不同的计算结果方式——都无法处理该值。（也许每个部分都通过输入来除以一个数。如果我们提供一个输入为 `0` 的值，那么这些组成部分都无法为此输入产生值，因为它们都将尝试除以 0。）在这些情况下，`equivocate` 本身无法产生结果。但假设我们提供了某个输入，其中 `equivocate` 的确切一个组成部分能够产生结果。在这种情况下，这个结果将成为 `equivocate` 对该输入的结果。

因此，从本质上讲，我们提供了许多不同的方式来处理输入，如果其中确切一个能够产生结果，我们选择那个结果。如果处理输入的方式都没有产生任何东西，那么我们的模糊函数也不产生任何东西。

在一种模糊函数的构成部分能产生多个结果的情况下略显怪异（在这里 Rx 与原始定义的 `amb` 有所偏离）是 McCarthy 的理论制定中，这种模糊函数实际上会产生所有可能的输出作为可能输出。（这在技术上被称为 _非确定性_ 计算，尽管这个名字可能会产生误导：它听起来像是结果将是不可预测的。但我们在谈论计算时说的 _非确定性_ 并不是指这个意思。就好像评估模糊函数的计算机克隆了自己，为每个可能的结果产生一个副本，继续执行每个副本。你可以想象这种系统的多线程实现，每当一个模糊函数产生多个可能结果时，我们创建那么多新线程以便能够评估所有可能的结果。这是一个非确定性计算的合理心理模型，但这不是 Rx 的 `Amb` 操作符实际发生的情况。) 在模糊函数被引入的那些理论工作中，这种模糊经常在最后消失。可能有无数计算方式可以进行，但它们可能最终都产生相同的结果。然而，这类理论问题将我们带离了 Rx 的 `Amb` 实际做什么以及我们如何在实际中使用它。

[Rx 的 `Amb`](09_CombiningSequences.md#amb) 提供了在输入都没有产生任何东西或确切一个输入产生了东西的情况下描述的行为。然而，它没有尝试支持非确定性计算，所以它处理多个构成部分能产生值的情况的方式过于简化，但 McCarthy 的 `amb` 首先是一个分析构造，所以任何实际的实现都无法达到。

## 保持在单子内

使用 Rx 时，可能会试图在编程风格之间切换。对于容易看出 Rx 应用的部分，我们自然会使用 Rx。但当事情变得棘手时，改变策略似乎是最简单的。可能看起来最简单的做法是 `await` 一个 observable，然后继续执行普通的顺序代码。或者也许似乎最简单的做法是让传递给像 `Select` 或 `Where` 这样的操作符的回调执行额外的操作——让它们具有实际效用的副作用。

虽然这有时可以工作，但应谨慎交换 Paradigms，因为这是并发问题（例如死锁和可扩展性问题）的常见根源。基本原因是只要你保持在 Rx 的做事方式中，你就会从数学基础的基本健全性中受益。但要使其发挥作用，你需要使用函数式风格。函数应该处理它们的输入并根据这些输入确定性地产生输出，它们既不应该依赖于外部状态也不应该改变它。这可能是一项艰巨的任务，并且不总是可能的，但如果你打破这些规则，大部分理论就会崩溃。组合不会像它可能的那样可靠地工作。因此，使用函数式风格并保持代码在 Rx 的习惯用法中将倾向于提高可靠性。

## 副作用的问题

如果程序运行的结果对世界没有任何影响，那么你可能一样不运行它——所以探索副作用的问题是有用的，这样我们就可以知道在必要时如何最好地处理它们。如果函数除了任何返回值之外还有其他可观察的效果，则认为该函数具有副作用。通常，‘可观察的效果’是状态的修改。这种可观察的效果可能是：

* 修改作用域比函数更广的变量（即全局、静态或可能是一个参数）
* I/O 操作，如读取或修改文件、发送或接收网络消息或更新显示
* 导致物理活动，如自动售货机分发物品或将硬币导入其硬币盒

函数式编程通常试图避免创建任何副作用。特别是那些修改状态的带有副作用的函数，需要程序员了解不仅仅是函数的输入和输出。完全理解函数的操作可能需要了解被修改状态的完整历史和上下文。这可能大大增加函数的复杂性，使其更难正确理解和维护。

副作用并不总是有意的。减少意外副作用的一种简单方法是减少变化的表面积。这里有两个程序员可以采取的简单行动：减少状态的可见性或范围，并使你能够不变的东西不变。你可以通过将变量的范围限制在代码块中，如方法（而不是字段或属性），来减少变量的可见性。你可以通过将类成员设为 private 或 protected 来减少可见性。从定义上讲，不可变数据不能被修改，因此它不能表现出副作用。这些是明智的封装规则，将大大提高你的 Rx 代码的可维护性。

为了提供一个查询中存在副作用的简单示例，我们将尝试通过更新一个变量（闭包）来输出订阅接收到的元素的索引和值。

```csharp
IObservable<char> letters = Observable
    .Range(0, 3)
    .Select(i => (char)(i + 65));

int index = -1;
IObservable<char> result = letters.Select(
    c =>
    {
        index++;
        return c;
    });

result.Subscribe(
    c => Console.WriteLine("Received {0} at index {1}", c, index),
    () => Console.WriteLine("completed"));
```

输出：

```
Received A at index 0
Received B at index 1
Received C at index 2
completed
```

虽然这似乎足够无害，但想象一下如果另一个人看到这段代码并理解这是团队使用的模式。他们反过来也采用这种风格。为了示例，我们将在之前的示例中添加一个重复订阅。

```csharp
var letters = Observable.Range(0, 3)
                        .Select(i => (char)(i + 65));

var index = -1;
var result = letters.Select(
    c =>
    {
        index++;
        return c;
    });

result.Subscribe(
    c => Console.WriteLine("Received {0} at index {1}", c, index),
    () => Console.WriteLine("completed"));

result.Subscribe(
    c => Console.WriteLine("Also received {0} at index {1}", c, index),
    () => Console.WriteLine("2nd completed"));
```

输出

```
Received A at index 0
Received B at index 1
Received C at index 2
completed
Also received A at index 3
Also received B at index 4
Also received C at index 5
2nd completed
```

现在第二个人的输出显然是无意义的。他们期望索引值为 0，1 和 2，但得到了 3，4 和 5。我在代码库中见过更狡猾的副作用版本。可怕的那些经常修改状态，比如 Boolean 值，例如 `hasValues`、`isStreaming` 等。

除了在现有软件中创造潜在不可预测的结果，展示副作用的程序在测试和维护方面也更加困难。未来的重构、增强或其他维护活动在展示副作用的程序上进行，可能更易碎。这在异步或并发软件中尤其如此。

## 在管道中组合数据

捕获状态的首选方式是作为构成您订阅的 Rx 操作符管道的信息的一部分。理想情况下，我们希望管道的每个部分都是独立和确定的。也就是说，构成管道的每个函数应该仅以其输入和输出作为其唯一的状态。要纠正我们的示例，我们可以丰富管道中的数据，以便没有共享状态。这将是我们可以使用暴露索引的 `Select` 重载的一个绝佳示例。

```csharp
IObservable<int> source = Observable.Range(0, 3);
IObservable<(int Index, char Letter)> result = source.Select(
    (idx, value) => (Index: idx, Letter: (char) (value + 65)));

result.Subscribe(
    x => Console.WriteLine($"Received {x.Letter} at index {x.Index}"),
    () => Console.WriteLine("completed"));

result.Subscribe(
    x => Console.WriteLine($"Also received {x.Letter} at index {x.Index}"),
    () => Console.WriteLine("2nd completed"));
```

输出：

```
Received A at index 0
Received B at index 1
Received C at index 2
completed
Also received A at index 0
Also received B at index 1
Also received C at index 2
2nd completed
```

跳出固定模式，我们还可以使用其他功能，如 `Scan`，来达到类似的结果。这是一个示例。

```csharp
var result = source.Scan(
                new
                {
                    Index = -1,
                    Letter = new char()
                },
                (acc, value) => new
                {
                    Index = acc.Index + 1,
                    Letter = (char)(value + 65)
                });
```

这里的关键是隔离状态，并减少或消除任何副作用，如改变状态。