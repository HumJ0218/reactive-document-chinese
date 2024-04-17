# 附录 B: 可释放对象

Rx使用现有的`IDisposable`接口来表示订阅关系。这种设计选择意味着我们可以使用已知如何与此接口协作的现有语言特性。Rx还提供了`IDisposable`的几种公开实现。这些可以在`System.Reactive.Disposables`命名空间中找到。此附录简要描述了它们中的每一个。

除了[`ScheduledDisposable`](#scheduleddisposable)外，这些与Rx没有特别的联系，可在任何需要使用`IDisposable`的代码中使用。（这些代码都位于`System.Reactive`中，因此，即使您完全在非Rx的代码中使用这些功能，您仍将依赖于Rx.NET。）

## `Disposable.Empty`
这个静态属性暴露了一个实现了`IDisposable`的实例，当调用`Dispose`方法时不执行任何操作。当您需要提供一个`IDisposable`（使用`Observable.Create`时可能需要）但在释放时不需要做任何事情时，这会很有用。

## `Disposable.Create(Action)`

该静态方法提供了一个实现了`IDisposable`的实例，它在调用`Dispose`方法时会调用提供的方法。按照指南，实现应该是幂等的，动作只会在第一次调用`Dispose`方法时被调用。

## `BooleanDisposable`

这个类实现了`IDisposable.Dispose`方法，并定义了一个只读属性`IsDisposed`。`IsDisposed`在类构造时为<code>false</code>，在调用`Dispose`方法时设为<code>true</code>。

## `CancellationDisposable`

`CancellationDisposable`类提供了.NET[取消范式](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)（`CancellationTokenSource`）与资源管理范式（`IDisposable`）之间的整合点。您可以通过提供一个`CancellationTokenSource`给构造函数创建一个`CancellationDisposable`实例，或者通过无参数构造函数为您创建一个。调用`Dispose`将会调用`CancellationTokenSource`上的`Cancel`方法。`CancellationDisposable`暴露出两个属性（`Token`和`IsDisposed`），它们分别是`CancellationTokenSource`属性的包装器，分别是`Token`和`IsCancellationRequested`。

## `CompositeDisposable`

`CompositeDisposable`类型允许您将多个可释放资源视为一个整体。您可以通过传入一个<code>params</code>可释放资源数组来创建`CompositeDisposable`的实例。对`CompositeDisposable`调用`Dispose`会按提供的顺序释放这些资源。此外，`CompositeDisposable`类实现了`ICollection<IDisposable>`；这允许您向集合添加和移除资源。在`CompositeDisposable`被释放后，添加到此集合中的任何后续资源都将被立即释放。从集合中移除的任何项目也将被释放，无论集合本身是否已被释放。这包括使用`Remove`和`Clear`方法。

## `ContextDisposable`
`ContextDisposable`允许您强制在给定的`SynchronizationContext`上执行资源的释放。构造函数需要一个`SynchronizationContext`和一个`IDisposable`资源。当对`ContextDisposable`调用`Dispose`方法时，将在指定的上下文中释放提供的资源。

## `MultipleAssignmentDisposable`

`MultipleAssignmentDisposable`暴露了一个只读的`IsDisposed`属性和一个读/写的`Disposable`属性。在`MultipleAssignmentDisposable`上调用`Dispose`方法将会释放由`Disposable`属性持有的当前值。然后将该值设置为null。只要`MultipleAssignmentDisposable`尚未被释放，您就可以按预期设置`Disposable`属性的`IDisposable`值。一旦`MultipleAssignmentDisposable`被释放，试图设置`Disposable`属性将使该值立即被释放；同时，`Disposable`将保持为null。

## `RefCountDisposable`

`RefCountDisposable`提供了一种功能，防止底层资源被释放，直到所有依赖资源都被释放。您需要一个基础的`IDisposable`值来构造一个`RefCountDisposable`。然后您可以调用`RefCountDisposable`实例的`GetDisposable`方法来检索一个依赖资源。每次调用`GetDisposable`时，一个内部计数器都会增加。每次从`GetDisposable`获取的依赖性可处置资源被释放时，计数器就会递减。只有当计数器达到零时，才会释放底层资源。这允许您在计数为零前后调用`RefCountDisposable`本身的`Dispose`方法。

## `ScheduledDisposable`

与`ContextDisposable`类似，`ScheduledDisposable`类型允许您指定一个调度器，底层资源将在该调度器上被释放。您需要传递`IScheduler`实例和`IDisposable`实例给构造函数。当`ScheduledDisposable`实例被释放时，将通过提供的调度器执行底层资源的释放。

## `SerialDisposable`

`SerialDisposable`与`MultipleAssignmentDisposable`非常相似，因为它们都暴露了一个读/写的`Disposable`属性。它们之间的区别在于，每当在`SerialDisposable`上设置`Disposable`属性时，先前的值会被释放。与`MultipleAssignmentDisposable`一样，一旦`SerialDisposable`被释放，`Disposable`属性将被设置为null，任何进一步尝试设置它的操作都将使该值被释放。该值将保持为null。

## `SingleAssignmentDisposable`

`SingleAssignmentDisposable`类也暴露了`IsDisposed`和`Disposable`属性。像`MultipleAssignmentDisposable`和`SerialDisposable`一样，当`SingleAssignmentDisposable`被释放时，`Disposable`值会被设置为null。这里实现的不同之处在于，如果在值不为null且`SingleAssignmentDisposable`未被释放时试图设置`Disposable`属性，`SingleAssignmentDisposable`将抛出一个`InvalidOperationException`异常。

<!-- 
TODO: we recently made SingleAssignmentDisposableValue public after a request to do so. This also doesn't mention MultipleAssignmentDisposableValue, which has been around for a while.

TODO: ICancelable?

TODO: StableCompositeDisposable?

TODO: fit this in?

```csharp
namespace System.Reactive.Disposables
{
    public static class Disposable
    {
    // Gets the disposable that does nothing when disposed.
    public static IDisposable Empty { get {...} }

    // Creates the disposable that invokes the specified action when disposed.
    public static IDisposable Create(Action dispose)
    {...}
    }
}
```

As you can see it exposes two members: `Empty` and `Create`. The `Empty` method allows you get a stub instance of an `IDisposable` that does nothing when `Dispose()` is called. This is useful for when you need to fulfil an interface requirement that returns an `IDisposable` but you have no specific implementation that is relevant.

The other overload is the `Create` factory method which allows you to pass an `Action` to be invoked when the instance is disposed. The `Create` method will ensure the standard Dispose semantics, so calling `Dispose()` multiple times will only invoke the delegate you provide once:

```csharp
var disposable = Disposable.Create(() => Console.WriteLine("Being disposed."));
Console.WriteLine("Calling dispose...");
disposable.Dispose();
Console.WriteLine("Calling again...");
disposable.Dispose();
```

Output:

```
Calling dispose...
Being disposed.
Calling again...
```

Note that "Being disposed." is only printed once. -->