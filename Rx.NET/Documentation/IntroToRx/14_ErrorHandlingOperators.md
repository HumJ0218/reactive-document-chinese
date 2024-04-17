# 错误处理操作符

异常会发生。有些异常是不可避免的，仅仅是因为我们代码中的错误。例如，如果我们将CLR置于必须引发`DivideByZeroException`的情况下，那我们做错了些什么。但是有很多异常是无法通过防御性编码防止的。例如，与I/O或网络故障相关的异常，如`FileNotFoundException`或`TimeoutException`，可能是由我们代码控制之外的环境因素引起的。在这些情况下，我们需要优雅地处理异常。具体的处理方式将取决于上下文。有时可能适合向用户提供某种错误消息；在某些情况下，记录错误可能是更合适的响应。如果故障可能是短暂的，我们可以尝试通过重试失败的操作来恢复。

`IObserver<T>`接口定义了`OnError`方法，从而源可以报告错误，但由于这终止了序列，它不直接提供了解决接下来要做什么的方法。然而，Rx提供了提供各种错误处理机制的操作符。

## Catch

Rx定义了一个`Catch`操作符。之所以特意让人想起C#的`try`/`catch`语法，是因为它让你可以以类似于从正常代码执行中产生的异常的方式来处理Rx源的错误。它有两种不同的工作方式。你可以只提供一个函数，Rx会将错误传递给这个函数，这个函数可以返回一个`IObservable<T>`，`Catch`现在将从该函数而不是原始源转发项目。或者，你可以不传递函数，而只是提供一个或多个额外的序列，catch将在当前一个失败时转移到下一个。

### 检查异常

`Catch`有一个重载，使您可以提供一个处理程序，如果源产生错误时将被调用：

```csharp
public static IObservable<TSource> Catch<TSource, TException>(
    this IObservable<TSource> source, 
    Func<TException, IObservable<TSource>> handler) 
    where TException : Exception
```

这在概念上非常类似于C#的`catch`块：我们可以编写查看异常然后决定如何继续的代码。就像`catch`块一样，我们可以决定我们感兴趣的异常类型。例如，我们可能知道源有时会产生`TimeoutException`，我们可能只想在这种情况下返回一个空序列，而不是错误：

```csharp
IObservable<int> result = source.Catch<int, TimeoutException>(_ => Observable.Empty<int>());
```

`Catch`只有在异常是指定的类型（或派生自该类型）时才调用我们的函数。如果序列以无法转换为`TimeoutException`的`Exception`终止，则错误不会被捕获并且会传递给订阅者。

这个例子返回`Observable.Empty<int>()`。这在概念上类似于C#中的“吞没”异常，即选择不采取任何行动。这对于您预期的异常来说可能是一个合理的响应，但通常不建议对基本`Exception`类型这样做。

由于这个示例忽略了它的输入，因为它只关注异常类型。 然而，我们可以检查异常，并对`Catch`应该发出什么做出更细致的决定：

```csharp
IObservable<string> result = source.Catch(
    (FileNotFoundException x) => x.FileName == "settings.txt"
        ? Observable.Return(DefaultSettings) : Observable.Throw<string>(x));
```

这为特定文件提供特殊处理，但其他情况重新抛出异常。 在这里返回`Observable.Throw<T>(x)`（其中`x`是原始异常）在概念上类似于在catch块中写`throw`。（在C#中，`throw;`和`throw x;`之间有一个重要区别，因为它改变了异常上下文的捕获方式，但在Rx中，`OnError`不捕获堆栈跟踪，因此没有等效的区别。）

当然，你也可以抛出一个完全不同的异常。你可以返回任何你喜欢的`IObservable<T>`，只要它的元素类型与源的相同。

### 回退

`Catch`的其他重载提供了不太挑剔的行为：你可以提供一个或多个额外的序列，每次当前源失败时，异常将被忽略，`Catch`将简单地转移到下一个序列。由于你永远不会知道异常是什么，这种机制不会让你知道发生的异常是你预期的，还是一个完全意外的，因此你通常会希望避免这种形式。但为了完整起见，这里是如何使用它的：

```csharp
IObservable<string> settings = settingsSource1.Catch(settingsSource2);
```

这种形式只提供了一个单一的备选方案。还有一个静态的`Observable.Catch`方法，它接受一个`params`数组，因此您可以传递任意数量的源。这与前面的示例完全相同：

```csharp
IObservable<string> settings = Observable.Catch(settingsSource1, settingsSource2);
```

还有一个接受`IEnumerable<IObservable<T>>`的重载。

如果任何源在不报告异常的情况下达到其结尾，`Catch`也会立即报告完成，并不会订阅任何后续的源。如果最后一个源报告异常，`Catch`将没有更多的源可以回退，因此在这种情况下它不会捕获异常。它将向其订阅者转发最终的异常。

## Finally

类似于C#中的`finally`块，Rx使我们能够在序列完成时执行一些代码，无论它是自然完成还是失败。Rx添加了第三种完成模式，这在`catch`/`finally`中没有确切的等价物：订阅者可能在源有机会完成之前取消订阅。 (这在概念上类似于使用`break`提前终止`foreach`。) `Finally`扩展方法接受一个`Action`作为参数。无论调用`OnCompleted`还是`OnError`，这个`Action`都会在序列终止时被调用。如果在完成之前取消订阅，也会调用该动作。

```csharp
public static IObservable<TSource> Finally<TSource>(
    this IObservable<TSource> source, 
    Action finallyAction)
{
    ...
}
```

在这个例子中，我们有一个完成的序列。我们提供一个动作，并看到它在我们的`OnCompleted`处理程序之后被调用。

```csharp
var source = new Subject<int>();
IObservable<int> result = source.Finally(() => Console.WriteLine("Finally action ran"));
result.Dump("Finally");
source.OnNext(1);
source.OnNext(2);
source.OnNext(3);
source.OnCompleted();
```

输出：

```
Finally-->1
Finally-->2
Finally-->3
Finally completed
Finally action ran
```

源序列也可能以异常终止。在那种情况下，异常将被发送给订阅者的`OnError`（我们会在控制台输出中看到这一点），然后我们提供给`Finally`的委托将被执行。

另外，我们可能会丢弃我们的订阅。在下一个示例中，我们看到即使序列没有完成，`Finally`操作也会被调用。

```csharp
var source = new Subject<int>();
var result = source.Finally(() => Console.WriteLine("Finally"));
var subscription = result.Subscribe(
    Console.WriteLine,
    Console.WriteLine,
    () => Console.WriteLine("Completed"));
source.OnNext(1);
source.OnNext(2);
source.OnNext(3);
subscription.Dispose();
```

输出：

```
1
2
3
Finally
```

注意，如果订阅者的`OnError`抛出异常，并且如果源在没有`try`/`catch`块的情况下调用`OnNext`，CLR的未处理异常报告机制会介入，在某些情况下这可能导致应用程序关闭，而没有机会调用`Finally`操作。我们可以用以下代码创建这种情况：

```csharp
var source = new Subject<int>();
var result = source.Finally(() => Console.WriteLine("Finally"));
result.Subscribe(
    Console.WriteLine,
    // Console.WriteLine,
    () => Console.WriteLine("Completed"));
source.OnNext(1);
source.OnNext(2);
source.OnNext(3);

// 使应用崩溃。Finally操作可能不会被调用。
source.OnError(new Exception("Fail"));
```

如果你直接从程序的入口点运行这个，而不把它包在`try`/`catch`中，你可能会或可能不会看到`Finally`消息显示，因为当一个异常到达堆栈顶部而没有被捕获时，异常处理的工作方式略有不同。 （奇怪的是，它通常会运行，但如果你附加了一个调试器，程序通常会在运行`Finally`回调之前退出。）

这主要只是一个好奇心：像ASP.NET Core或WPF这样的应用程序框架通常会安装自己的顶部堆栈异常处理程序，无论如何，你不应该订阅一个你知道会调用`OnError`而没有提供错误回调的源。因为这个问题只是因为这里使用的基于委托的`Subscribe`重载提供了一个在`OnError`中抛出的`IObserver<T>`实现。然而，如果你在构建控制台应用程序来实验Rx的行为，你很可能会遇到这个问题。实际上，`Finally`在更正常的情况下会做正确的事情。（但无论如何，你不应该从`OnError`处理程序中抛出异常。）

## Using

`Using`工厂方法允许你将资源的生命周期绑定到可观测序列的生命周期。该方法接受两个回调：一个用于创建可处理资源，另一个用于提供序列。这使得一切都可以懒惰地计算。当代码调用此方法返回的`IObservable<T>`上的`Subscribe`时，这些回调将被调用。

```csharp
public static IObservable<TSource> Using<TSource, TResource>(
    Func<TResource> resourceFactory, 
    Func<TResource, IObservable<TSource>> observableFactory) 
    where TResource : IDisposable
{
    ...
}
```

当序列以`OnCompleted`或`OnError`终止，或者当订阅被处置时，资源将被处置。

## OnErrorResumeNext

只是这个章节的标题就会让老VB开发者感到不寒而栗！（对于那些不熟悉这个模糊语言功能的人来说，VB语言允许你指示它在执行期间忽略任何错误，并在任何失败后继续执行下一个语句。）在Rx中，有一个名为`OnErrorResumeNext`的扩展方法，其语义类似于具有相同名称的VB关键字/语句。这个扩展方法允许无论第一个序列是优雅完成还是由于错误完成，都与另一个序列继续序列。

这与`Catch`的第二种形式非常相似（如[Fallback](#fallback)中所述）。区别在于，如果任何源序列在不报告错误的情况下达到其结尾，`Catch`不会转移到下一个序列。`OnErrorResumeNext`将转发其所有输入产生的所有元素，因此它类似于[`Concat`](09_CombiningSequences.md#concat)，它只是忽略所有错误。

正如`OnErrorResumeNext`关键字在VB中最好用于除了临时代码之外的其他任何东西一样，它也应该在Rx中谨慎使用。它会安静地吞下异常，并可能使你的程序处于未知状态。通常，这将使你的代码更难维护和调试。（同样适用于`Catch`的fallback形式。）

## Retry

如果你预期你的序列会遇到可预测的失败，你可能只想重试。例如，如果你在云环境中运行，偶尔操作失败是很常见的，没有明显的原因。云平台通常会出于操作原因定期重新定位服务，这意味着操作失败并不罕见——你可能在云提供商决定将该服务移动到其他计算节点之前请求了服务——但如果你立即重试相同的操作（因为重试的请求被路由到新节点），则可能成功。Rx的`Retry`扩展方法提供了在失败时重试的能力，可以指定次数或直到成功。这通过在源报告错误时重新订阅源来工作。

这个示例使用简单的重载，它将始终在任何异常情况下重试。

```csharp
public static void RetrySample<T>(IObservable<T> source)
{
    source.Retry().Subscribe(t => Console.WriteLine(t)); // 将始终重试
    Console.ReadKey();
}
```

给定一个产生值0、1和2的源，然后调用`OnError`，输出将是数字0、1、2重复地无休止地重复。这个输出将永远继续，因为这个示例从未取消订阅，如果你不告诉它不这样做，`Retry`将永远重试。

我们可以指定重试的最大次数。在下一个示例中，我们只重试一次，因此在第二次订阅上发布的错误将被传递给最终订阅。注意我们告诉`Retry`最大尝试次数，所以如果我们想它重试一次，你传递的值是2——即初始尝试加上一次重试。

```csharp
source.Retry(2).Dump("Retry(2)"); 
```

输出：

```
Retry(2)-->0
Retry(2)-->1
Retry(2)-->2
Retry(2)-->0
Retry(2)-->1
Retry(2)-->2
Retry(2) failed-->Test Exception
```

在使用无限重复重载时应该谨慎。显然，如果你的底层序列存在持续问题，你可能会发现自己陷入无限循环。此外，请注意没有允许你指定要重试的异常类型的重载。

Rx还提供了一个`RetryWhen`方法。这与我们看到的第一个`Catch`重载类似：它不是无差别地处理所有异常，而是让你提供可以决定要做什么的代码。它的工作方式略有不同：它不是每个错误调用一次这个回调，而是调用一次通过`IObservable<Exception>`传递所有异常的回调，回调返回一个称为_signal_ observable的`IObservable<T>`。`T`可以是任何东西，因为这个observable可能返回的值将被忽略：重要的是调用三个`IObserver<T>`方法中的哪一个。

如果在接收到异常时，信号observable调用`OnError`，`RetryWhen`将不会重试，并将向其订阅者报告相同的错误。如果另一方面信号observable调用`OnCompleted`，同样`RetryWhen`将不会重试，并将在不报告错误的情况下完成。但如果信号observable调用`OnNext`，这将导致`RetryWhen`通过重新订阅源来重试。

<!--TODO: 与读者一起构建BackOffRetry-->

应用程序通常需要超出简单`OnError`处理程序的异常管理逻辑。Rx提供了类似于我们在C#中习惯的异常处理操作符，您可以使用这些操作符来组合复杂且健壮的查询。在本章中，我们讨论了高级错误处理和Rx的一些资源管理功能。