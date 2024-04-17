# ��¼ B: ���ͷŶ���

Rxʹ�����е�`IDisposable`�ӿ�����ʾ���Ĺ�ϵ���������ѡ����ζ�����ǿ���ʹ����֪�����˽ӿ�Э���������������ԡ�Rx���ṩ��`IDisposable`�ļ��ֹ���ʵ�֡���Щ������`System.Reactive.Disposables`�����ռ����ҵ����˸�¼��Ҫ�����������е�ÿһ����

����[`ScheduledDisposable`](#scheduleddisposable)�⣬��Щ��Rxû���ر����ϵ�������κ���Ҫʹ��`IDisposable`�Ĵ�����ʹ�á�����Щ���붼λ��`System.Reactive`�У���ˣ���ʹ����ȫ�ڷ�Rx�Ĵ�����ʹ����Щ���ܣ����Խ�������Rx.NET����

## `Disposable.Empty`
�����̬���Ա�¶��һ��ʵ����`IDisposable`��ʵ����������`Dispose`����ʱ��ִ���κβ�����������Ҫ�ṩһ��`IDisposable`��ʹ��`Observable.Create`ʱ������Ҫ�������ͷ�ʱ����Ҫ���κ�����ʱ���������á�

## `Disposable.Create(Action)`

�þ�̬�����ṩ��һ��ʵ����`IDisposable`��ʵ�������ڵ���`Dispose`����ʱ������ṩ�ķ���������ָ�ϣ�ʵ��Ӧ�����ݵȵģ�����ֻ���ڵ�һ�ε���`Dispose`����ʱ�����á�

## `BooleanDisposable`

�����ʵ����`IDisposable.Dispose`��������������һ��ֻ������`IsDisposed`��`IsDisposed`���๹��ʱΪ<code>false</code>���ڵ���`Dispose`����ʱ��Ϊ<code>true</code>��

## `CancellationDisposable`

`CancellationDisposable`���ṩ��.NET[ȡ����ʽ](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)��`CancellationTokenSource`������Դ����ʽ��`IDisposable`��֮������ϵ㡣������ͨ���ṩһ��`CancellationTokenSource`�����캯������һ��`CancellationDisposable`ʵ��������ͨ���޲������캯��Ϊ������һ��������`Dispose`�������`CancellationTokenSource`�ϵ�`Cancel`������`CancellationDisposable`��¶���������ԣ�`Token`��`IsDisposed`�������Ƿֱ���`CancellationTokenSource`���Եİ�װ�����ֱ���`Token`��`IsCancellationRequested`��

## `CompositeDisposable`

`CompositeDisposable`������������������ͷ���Դ��Ϊһ�����塣������ͨ������һ��<code>params</code>���ͷ���Դ����������`CompositeDisposable`��ʵ������`CompositeDisposable`����`Dispose`�ᰴ�ṩ��˳���ͷ���Щ��Դ�����⣬`CompositeDisposable`��ʵ����`ICollection<IDisposable>`�����������򼯺���Ӻ��Ƴ���Դ����`CompositeDisposable`���ͷź���ӵ��˼����е��κκ�����Դ�����������ͷš��Ӽ������Ƴ����κ���ĿҲ�����ͷţ����ۼ��ϱ����Ƿ��ѱ��ͷš������ʹ��`Remove`��`Clear`������

## `ContextDisposable`
`ContextDisposable`������ǿ���ڸ�����`SynchronizationContext`��ִ����Դ���ͷš����캯����Ҫһ��`SynchronizationContext`��һ��`IDisposable`��Դ������`ContextDisposable`����`Dispose`����ʱ������ָ�������������ͷ��ṩ����Դ��

## `MultipleAssignmentDisposable`

`MultipleAssignmentDisposable`��¶��һ��ֻ����`IsDisposed`���Ժ�һ����/д��`Disposable`���ԡ���`MultipleAssignmentDisposable`�ϵ���`Dispose`���������ͷ���`Disposable`���Գ��еĵ�ǰֵ��Ȼ�󽫸�ֵ����Ϊnull��ֻҪ`MultipleAssignmentDisposable`��δ���ͷţ����Ϳ��԰�Ԥ������`Disposable`���Ե�`IDisposable`ֵ��һ��`MultipleAssignmentDisposable`���ͷţ���ͼ����`Disposable`���Խ�ʹ��ֵ�������ͷţ�ͬʱ��`Disposable`������Ϊnull��

## `RefCountDisposable`

`RefCountDisposable`�ṩ��һ�ֹ��ܣ���ֹ�ײ���Դ���ͷţ�ֱ������������Դ�����ͷš�����Ҫһ��������`IDisposable`ֵ������һ��`RefCountDisposable`��Ȼ�������Ե���`RefCountDisposable`ʵ����`GetDisposable`����������һ��������Դ��ÿ�ε���`GetDisposable`ʱ��һ���ڲ��������������ӡ�ÿ�δ�`GetDisposable`��ȡ�������Կɴ�����Դ���ͷ�ʱ���������ͻ�ݼ���ֻ�е��������ﵽ��ʱ���Ż��ͷŵײ���Դ�����������ڼ���Ϊ��ǰ�����`RefCountDisposable`�����`Dispose`������

## `ScheduledDisposable`

��`ContextDisposable`���ƣ�`ScheduledDisposable`����������ָ��һ�����������ײ���Դ���ڸõ������ϱ��ͷš�����Ҫ����`IScheduler`ʵ����`IDisposable`ʵ�������캯������`ScheduledDisposable`ʵ�����ͷ�ʱ����ͨ���ṩ�ĵ�����ִ�еײ���Դ���ͷš�

## `SerialDisposable`

`SerialDisposable`��`MultipleAssignmentDisposable`�ǳ����ƣ���Ϊ���Ƕ���¶��һ����/д��`Disposable`���ԡ�����֮����������ڣ�ÿ����`SerialDisposable`������`Disposable`����ʱ����ǰ��ֵ�ᱻ�ͷš���`MultipleAssignmentDisposable`һ����һ��`SerialDisposable`���ͷţ�`Disposable`���Խ�������Ϊnull���κν�һ�������������Ĳ�������ʹ��ֵ���ͷš���ֵ������Ϊnull��

## `SingleAssignmentDisposable`

`SingleAssignmentDisposable`��Ҳ��¶��`IsDisposed`��`Disposable`���ԡ���`MultipleAssignmentDisposable`��`SerialDisposable`һ������`SingleAssignmentDisposable`���ͷ�ʱ��`Disposable`ֵ�ᱻ����Ϊnull������ʵ�ֵĲ�֮ͬ�����ڣ������ֵ��Ϊnull��`SingleAssignmentDisposable`δ���ͷ�ʱ��ͼ����`Disposable`���ԣ�`SingleAssignmentDisposable`���׳�һ��`InvalidOperationException`�쳣��

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