# ���� Rx.NET

�ִ�������֤��׼Ҫ��㷺���Զ������ԣ��԰���������Ԥ��ȱ�ݡ�����һ�ײ����׼�����֤��ȷ����Ϊ��������Ϊ�������̵�һ�������У��Ա㼰�緢�ֻع���һ����ϰ�ߡ�

`System.Reactive` Դ���������һ��ȫ��Ĳ����׼������Ի��� Rx �Ĵ��������һЩ��ս���ر����漰ʱ�����в�����ʱ��Rx.NET �Ĳ����׼��������������������ֵı�Ե����Ĳ��ԣ���ȷ���ڸ����µĿ�Ԥ����Ϊ����ֻ����Ϊ Rx.NET �����Ϊ�ɲ��Եġ�

�ڱ����У����ǽ�չʾ������� Rx �Ŀɲ����������Լ��Ĵ����С�

## ����ʱ��

�� Rx �д���ʱ���ǳ����ġ����������������ṩ�˼�������ʱ��Ĳ������������������һ����ս�����ǲ����������ٲ��ԣ���Ϊ����ܻ�ʹ�����׼�����̫��ʱ��ִ�У���������β���һ��Ӧ�ó���ȴ��û�ֹͣ����������ύ��ѯ������أ���ȷ���Բ���Ҳ������һ�����⣺�����ھ�̬����ʱ���ɿ������´�����Щ������ܷǳ����ѡ�

[Scheduling and Threading](11_SchedulingAndThreading.md) �½������˵��������ʹ������ʱ��ı��֡������ʹ�����ܹ���֤��ʱ����ص���Ϊ������Ҫ���������ǿ��Կ��� Rx ��ʱ���չ�ĸ�֪��ʹ�����ܹ���д�߼�����Ҫ�����ӣ���ʵ������΢����ִ�еĲ��ԡ�

����������ӣ����Ǵ���һ��ÿ�뷢��ֵ������������С�

```csharp
IObservable<long> interval = Observable
    .Interval(TimeSpan.FromSeconds(1))
    .Take(5);
```

һ���򵥵Ĳ�����ȷ��������ÿ���������ֵ����Ҫ�����������С��ǿɲ��У�������Ҫ����������ǧ�������������������С���һ���ǳ��ձ�������ǲ��Գ�ʱ����������ǳ��Բ���һ���ӵĳ�ʱ��

```csharp
var never = Observable.Never<int>();
var exceptionThrown = false;

never.Timeout(TimeSpan.FromMinutes(1))
     .Subscribe(
        i => Console.WriteLine("This will never run."),
        ex => exceptionThrown = true);

Assert.IsTrue(exceptionThrown);
```

���������Ǳ���ѡ��ֻ�������ǵĲ��Եȴ�һ�����ٽ��ж��ԡ�ʵ���ϣ����ǿ�����ȴ�һ���Ӷ�һ�㣬��Ϊ������в��Եļ����æµ�������Ժ󴥷���ʱ���������������������ʱ��ʹ����û�����������⣬����Ҳ��ż��ʧ�ܡ�

û������Ҫ���١���һ�µĲ��ԡ����ԣ������ǿ��� Rx ��ΰ������Ǳ�����Щ���⡣

## TestScheduler

[Scheduling and Threading](11_SchedulingAndThreading.md) �½ڽ����˵�����������ʱ�Լ����ִ�д��룬�Լ�������θ���ʱ�䡣��������һ���п����Ĵ������������������߳����⣬�����ڼ�ʱ���棬���Ƕ���ͼ�������ʱ�����й������� Rx �ṩ�� `TestScheduler`������ȫ��ͬ�ش���ʱ�䡣�����õ���������������ʱ����ص���Ϊ����ʵ���������ܹ�ģ��Ϳ���ʱ�䡣

**ע�⣺** `TestScheduler` ������ `System.Reactive` ���С�����Ҫ��Ӷ� `Microsoft.Reactive.Testing` �����ò���ʹ������

�κε�������ά��һ��Ҫִ�еĶ������С�ÿ����������ָ��һ��ִ��ʱ��ʱ��㡣����ʱ���ʱ���ǡ����족��������ʱ��Ĳ�����ͨ���ᰲ�Ź����ڽ�����ĳ������ʱ�����С����������ʹ�� `TestScheduler`������ʵ���ϱ��ֵú���ʱ�侲ֱֹ�����Ǹ�������������ʱ�����ǰ����

����������У�����ʹ����򵥵� `Schedule` ����������һ���������е����񡣾�������Ч��Ҫ�����������У�`TestScheduler` ���ǵ����Ǹ���������׼�����˲Ŵ������ŶӵĹ��������ǽ�����ʱ����ǰ�ƽ�һ��ʱ�̣���ʱ����ִ�и��Ŷӹ�������ÿ�������ƽ�����ʱ��ʱ�����������������Ŷӵġ����족��������������ƽ�ʱ���㹻Զ����ζ����ǰ���߼�����δ���Ĺ������ڿ������У���Ҳ��������Щ��������

```csharp
var scheduler = new TestScheduler();
var wasExecuted = false;
scheduler.Schedule(() => wasExecuted = true);
Assert.IsFalse(wasExecuted);
scheduler.AdvanceBy(1); // ִ�� 1 ��ʱ�̵��ŶӶ���
Assert.IsTrue(wasExecuted);
```

`TestScheduler` ʵ���� `IScheduler` �ӿڣ����������������ǿ��ƺͼ�������ʱ��ķ���������ʾ����Щ����ķ�����

```csharp
public class TestScheduler : // ...
{
    public bool IsEnabled { get; private set; }
    public TAbsolute Clock { get; protected set; }
    public void Start()
    public void Stop()
    public void AdvanceTo(long time)
    public void AdvanceBy(long time)
    
    ...
}
```

`TestScheduler` �� [`TimeSpan.Ticks`](https://learn.microsoft.com/en-us/dotnet/api/system.timespan.ticks) Ϊ��λ������������뽫ʱ����ǰ�ƽ� 1 �룬����Ե��� `scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks)`��һ�̶�Ӧ�� 100 ���룬��� 1 ���� 10,000,000 �̡�

### AdvanceTo

`AdvanceTo(long)` ����������ʱ������Ϊָ���Ŀ������⽫ִ�������Ѱ��ŵ��þ���ʱ��Ķ�����`TestScheduler` ʹ�ÿ���Ϊ��ʱ�������λ������������У����ǰ��Ŷ�������ִ�У��Լ��� 10 �̺� 20 �̣��ֱ�Ϊ 1 ΢��� 2 ΢�룩ִ�С�

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // ��������
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.AdvanceTo(1);");
scheduler.AdvanceTo(1);

Console.WriteLine("scheduler.AdvanceTo(10);");
scheduler.AdvanceTo(10);

Console.WriteLine("scheduler.AdvanceTo(15);");
scheduler.AdvanceTo(15);

Console.WriteLine("scheduler.AdvanceTo(20);");
scheduler.AdvanceTo(20);
```

�����

```
scheduler.AdvanceTo(1);
A
scheduler.AdvanceTo(10);
B
scheduler.AdvanceTo(15);
scheduler.AdvanceTo(20);
C
```

ע�⵱�����ƽ��� 15 ��ʱû�з����κ����顣������ 15 ��֮ǰ���ŵĹ����Ѿ���ɣ����ǻ�û���ƽ�������ִ����һ�����Ŷ�����ʱ�䡣

### AdvanceBy

`AdvanceBy(long)` �����������ǽ�ʱ����ǰ�ƽ�һ��ʱ�������� `AdvanceTo` ��ͬ������Ĳ���������ڵ�ǰ����ʱ��ġ��ٴΣ�������λ�ǿ̡����ǿ��Բ����������Ӳ������޸�Ϊʹ�� `AdvanceBy(long)`��

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // ��������
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.AdvanceBy(1);");
scheduler.AdvanceBy(1);

Console.WriteLine("scheduler.AdvanceBy(9);");
scheduler.AdvanceBy(9);

Console.WriteLine("scheduler.AdvanceBy(5);");
scheduler.AdvanceBy(5);

Console.WriteLine("scheduler.AdvanceBy(5);");
scheduler.AdvanceBy(5);
```

�����

```
scheduler.AdvanceBy(1);
A
scheduler.AdvanceBy(9);
B
scheduler.AdvanceBy(5);
scheduler.AdvanceBy(5);
C
```

### Start

`TestScheduler` �� `Start()` ���������Ѿ����ŵ��������񣬱�Ҫʱ�ƽ�����ʱ����ִ�б����ŵ��ض�ʱ��Ĺ��������ʹ����ͬ�����ӣ��� `AdvanceBy(long)` ���û��ɵ��� `Start()` ���á�

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A")); // ��������
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();

Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

�����

```
scheduler.Start();
A
B
C
scheduler.Clock:20
```

ע�⣬�����а��ŵĶ�������ִ�к�����ʱ������������ŵ���Ŀ��20 �̣�ƥ�䡣

���ǽ�һ����չ���ǵ����ӣ�����һ���� `Start()` �Ѿ������ú�ŷ������¶�����

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();

Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);

scheduler.Schedule(() => Console.WriteLine("D"));
```

�����

```
scheduler.Start();
A
B
C
scheduler.Clock:20
```

ע�������ȫ��ͬ�����������Ҫִ�����ǵĵ��ĸ����������ǽ����ò��ٴε��� `Start()`���� `AdvanceTo` �� `AdvanceBy`����

### Stop

��һ����Ϊ `Stop()` �ķ������������ƺ���ʾ���� `Start()` ��ĳ�ֶԳ��ԡ���Ὣ�������� `IsEnabled` ��������Ϊ false��������� `Start` ��ǰ�������У�����ζ������ֹͣ�������еĽ�һ�����������ڵ�ǰ���ڴ���Ĺ�������ɺ��������ء�

����������У�����չʾ�����ʹ�� `Stop()` ��ͣ���Ŷ����Ĵ���

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(() => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(15), scheduler.Stop);
scheduler.Schedule(TimeSpan.FromTicks(20), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();
Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

�����

```
scheduler.Start();
A
B
scheduler.Clock:15
```

ע�⣬"C" û�б���ӡ��������Ϊ������ 15 ��ֹͣ��ʱ�ӡ�

���� `Start` �Զ�ֹͣ�������ľ���������ʱ���㲢��һ��Ҫ���� `Stop`�������ڵ�ֻ��Ϊ���ڲ��Թ����в������ʱ��ͣ `Start` �Ĵ���

### ���ų�ͻ

�����Ŷ���ʱ���ܿ��ܻ�����ද����������ͬһʱ�̡���ͨ���ᷢ����Ϊ _����_ ���Ŷ������ʱ����Ҳ���ܷ�����Ԥ��δ��ͬһʱ�̵Ķ������ʱ��`TestScheduler` ��һ���򵥵ķ������������������������������ʱ�����ǻᱻ���Ϊ���Ǳ����ŵ�ʱ��ʱ�䡣��������Ŀ��������ͬһʱ�̣����ǻᰴ�����Ǳ����ŵ�˳���Ŷӣ���ʱ���ƽ�ʱ�����и�ʱ�̵���Ŀ���ᰴ�����Ǳ����ŵ�˳��ִ�С�

```csharp
var scheduler = new TestScheduler();
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("A"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("B"));
scheduler.Schedule(TimeSpan.FromTicks(10), () => Console.WriteLine("C"));

Console.WriteLine("scheduler.Start();");
scheduler.Start();
Console.WriteLine("scheduler.Clock:{0}", scheduler.Clock);
```

�����

```
scheduler.AdvanceTo(10);
A
B
C
scheduler.Clock:10
```

ע������ʱ������ 10 �̣������ƽ�����ʱ�䡣

## ���� Rx ����

���������Ѿ��˽���һЩ���� `TestScheduler` ��֪ʶ�������ǿ���������ο���ʹ�����������������������ʹ�� `Interval` �� `Timeout` �Ĵ���Ƭ�Ρ�����ϣ�������ܿ��ִ�в��ԣ�����Ȼ����ʱ������塣����������У������������ǵ����ֵ��ÿ��ֵ���һ�룬���������ǵ� `TestScheduler` �� `Interval` ������ʹ�ö�����Ĭ�ϵĵ�������

```csharp
[TestMethod]
public void Testing_with_test_scheduler()
{
    var expectedValues = new long[] {0, 1, 2, 3, 4};
    var actualValues = new List<long>();
    var scheduler = new TestScheduler();

    var interval = Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(5);
    
    interval.Subscribe(actualValues.Add);

    scheduler.Start();
    CollectionAssert.AreEqual(expectedValues, actualValues);
    // ���ҵĻ�����ִ��ʱ������ 0.01 ��
}
```

��Ȼ��������е���Ȥ��������Ϊ����Ҫ�������ǽ���β���һ����ʵ�Ĵ���Ƭ�Ρ�����һ�£�һ�� ViewModel ������һ���۸������۸񷢲�ʱ�����Ὣ������ӵ�һ�������С���������һ�� WPF ʵ�֣�������Ȩǿ�ƶ����� `ThreadPool` ����ɣ����ҹ۲����� `Dispatcher` ��ִ�еġ�

```csharp
public class MyViewModel : IMyViewModel
{
    private readonly IMyModel _myModel;
    private readonly ObservableCollection<decimal> _prices;

    public MyViewModel(IMyModel myModel)
    {
        _myModel = myModel;
        _prices = new ObservableCollection<decimal>();
    }

    public void Show(string symbol)
    {
        // TODO: resource mgt, exception handling etc...
        _myModel.PriceStream(symbol)
                .SubscribeOn(Scheduler.ThreadPool)
                .ObserveOn(Scheduler.Dispatcher)
                .Timeout(TimeSpan.FromSeconds(10), Scheduler.ThreadPool)
                .Subscribe(
                    Prices.Add,
                    ex =>
                        {
                            if(ex is TimeoutException)
                                IsConnected = false;
                        });
        IsConnected = true;
    }

    public ObservableCollection<decimal> Prices
    {
        get { return _prices; }
    }

    public bool IsConnected { get; private set; }
}
```

### ע�����������

��Ȼ����Ĵ���Ƭ�ο��������������Ҫ�Ĺ��ܣ��������Ѳ��ԣ���Ϊ����ͨ����̬���Է��ʵ������ġ�������ҪһЩ�����ڲ����ڼ��ṩ��ͬ�ĵ�����������������У����ǽ�Ϊ��Ŀ�Ķ���һ���ӿڣ�

```csharp
public interface ISchedulerProvider
{
    IScheduler CurrentThread { get; }
    IScheduler Dispatcher { get; }
    IScheduler Immediate { get; }
    IScheduler NewThread { get; }
    IScheduler ThreadPool { get; }
    IScheduler TaskPool { get; } 
}
```

���������������е�Ĭ��ʵ��������ʾ��

```csharp
public sealed class SchedulerProvider : ISchedulerProvider
{
    public IScheduler CurrentThread => Scheduler.CurrentThread;
    public IScheduler Dispatcher => DispatcherScheduler.Instance;
    public IScheduler Immediate => Scheduler.Immediate;
    public IScheduler NewThread => Scheduler.NewThread;
    public IScheduler ThreadPool => Scheduler.ThreadPool;
    public IScheduler TaskPool => Scheduler.TaskPool;
}
```

���ǿ����ڲ������滻 `ISchedulerProvider` ��ʵ�֡����磺

```csharp
public sealed class TestSchedulers : ISchedulerProvider
{
    // �� TestScheduler �����ṩ������
    public TestScheduler CurrentThread { get; }  = new TestScheduler();
    public TestScheduler Dispatcher { get; }  = new TestScheduler();
    public TestScheduler Immediate { get; }  = new TestScheduler();
    public TestScheduler NewThread { get; }  = new TestScheduler();
    public TestScheduler ThreadPool { get; }  = new TestScheduler();
    
    // ISchedulerService ��Ҫ���Ƿ��� IScheduler��������ϣ������
    // ���� TestScheduler �Է�����Դ����ʹ�ã����������ṩ
    // �������Ե���ʽʵ����ƥ�� ISchedulerService��
    IScheduler ISchedulerProvider.CurrentThread => CurrentThread;
    IScheduler ISchedulerProvider.Dispatcher => Dispatcher;
    IScheduler ISchedulerProvider.Immediate => Immediate;
    IScheduler ISchedulerProvider.NewThread => NewThread;
    IScheduler ISchedulerProvider.ThreadPool => ThreadPool;
}
```

ע�� `ISchedulerProvider` ����ʽʵ�ֵģ���Ϊ�ýӿ�Ҫ��ÿ�����Է���һ�� `IScheduler`�������ǵĲ��Խ���Ҫֱ�ӷ��� `TestScheduler` ʵ���������ڿ���Ϊ�ҵ� ViewModel ��дһЩ�����ˡ����棬���ǲ�����һ���޸Ĺ��� `MyViewModel` �࣬������һ�� `ISchedulerProvider` ��ʹ���������� `Scheduler` ��ľ�̬�����������ǻ�ʹ�����е� [Moq](https://github.com/Moq) ����ṩ�ʵ��ļ�ʵ��ģ�͡�

```csharp
[TestInitialize]
public void SetUp()
{
    _myModelMock = new Mock<IMyModel>();
    _schedulerProvider = new TestSchedulers();
    _viewModel = new MyViewModel(_myModelMock.Object, _schedulerProvider);
}

[TestMethod]
public void Should_add_to_Prices_when_Model_publishes_price()
{
    decimal expected = 1.23m;
    var priceStream = new Subject<decimal>();
    _myModelMock.Setup(svc => svc.PriceStream(It.IsAny<string>())).Returns(priceStream);

    _viewModel.Show("SomeSymbol");
    
    // ���� OnNext
    _schedulerProvider.ThreadPool.Schedule(() => priceStream.OnNext(expected));  

    Assert.AreEqual(0, _viewModel.Prices.Count);

    // ִ�� OnNext ����
    _schedulerProvider.ThreadPool.AdvanceBy(1);  
    Assert.AreEqual(0, _viewModel.Prices.Count);
    
    // ִ�� OnNext �������
    _schedulerProvider.Dispatcher.AdvanceBy(1);  
    Assert.AreEqual(1, _viewModel.Prices.Count);
    Assert.AreEqual(expected, _viewModel.Prices.First());
}

[TestMethod]
public void Should_disconnect_if_no_prices_for_10_seconds()
{
    var timeoutPeriod = TimeSpan.FromSeconds(10);
    var priceStream = Observable.Never<decimal>();
    _myModelMock.Setup(svc => svc.PriceStream(It.IsAny<string>())).Returns(priceStream);

    _viewModel.Show("SomeSymbol");

    _schedulerProvider.ThreadPool.AdvanceBy(timeoutPeriod.Ticks - 1);
    Assert.IsTrue(_viewModel.IsConnected);
    _schedulerProvider.ThreadPool.AdvanceBy(timeoutPeriod.Ticks);
    Assert.IsFalse(_viewModel.IsConnected);
}
```

�����

```
2 passed, 0 failed, 0 skipped, took 0.41 seconds (MSTest 10.0).
```

����������ȷ��������£�

* `Price` ��������ģ�Ͳ����ļ۸������
* �������� ThreadPool �ϱ�����
* `Price` ������ Dispatcher �ϸ��£��������� Dispatcher �ϱ��۲�
* �۸�֮��� 10 �볬ʱ�Ὣ ViewModel ����Ϊ�Ͽ�����
* ���������ٶȿ졣

�������в��Ե�ʱ�䲢����ô����ӡ����̣����󲿷�ʱ���ƺ���������������Թ����ϡ����ң��������������ӵ� 10 ֻ������ 0.03 �롣һ����˵���ִ� CPU Ӧ���ܹ�ÿ��ִ����ǧ����Ԫ���ԡ�

�ڵ�һ�������У����ǿ��Կ���ֻ���� `ThreadPool` �� `Dispatcher` �����������к����ǲŻ�õ�������ڵڶ��������У�����������֤��ʱ������ 10 �롣

��ĳЩ����£������ܲ�����Ȥ���ǵ���������ϣ��רע�ڲ����������ܡ�����������������ô������ϣ��������һ�� `ISchedulerProvider` �Ĳ���ʵ�֣���ʵ��Ϊ���Ա���� `ImmediateScheduler`������԰��������������еĸ��š�

```csharp
public sealed class ImmediateSchedulers : ISchedulerService
{
    public IScheduler CurrentThread => Scheduler.Immediate;
    public IScheduler Dispatcher => Scheduler.Immediate;
    public IScheduler Immediate => Scheduler.Immediate;
    public IScheduler NewThread => Scheduler.Immediate;
    public IScheduler ThreadPool => Scheduler.Immediate;
}
```

## �߼����� - ITestableObserver

`TestScheduler` �ṩ�˸���߼����ܡ����������õ�ĳЩ������Ҫ���ض�������ʱ������ʱ����Щ���ܿ��ܻ����á�

### `Start(Func<IObservable<T>>)` ����

`Start` ���������أ������ڸ���ʱ�俪ʼ�۲�һ���ɹ۲����У���¼��������֪ͨ�����ڸ���ʱ�䴦�ö��ġ�������ܻ�����������Ϊ�޲������ص� `Start` �����ȫ�޹ء����������ط���һ�� `ITestableObserver<T>`�����������¼�ɹ۲����е�֪ͨ������������ [Transformation chapter](06_Transformation.md#materialize-and-dematerialize) �п����� `Materialize` ������

```csharp
public interface ITestableObserver<T> : IObserver<T>
{
    // ��ȡ�۲��߽��յ��ļ�¼֪ͨ��
    IList<Recorded<Notification<T>>> Messages { get; }
}
```

��Ȼ���������أ����������ȿ�������һ����������ؽ����ĸ�������

* һ���ɹ۲����й���ί��
* ���ù�����ʱ���
* ���Ĵӹ������صĿɹ۲����е�ʱ���
* ���ö��ĵ�ʱ���

������������� _ʱ��_ ���Կ�Ϊ��λ���� `TestScheduler` �������Աһ�¡�

```csharp
public ITestableObserver<T> Start<T>(
    Func<IObservable<T>> create, 
    long created, 
    long subscribed, 
    long disposed)
{...}
```

���ǿ���ʹ��������������� `Observable.Interval` ����������������Ǵ���һ��ÿ�����һ��ֵ���� 4 ��Ŀɹ۲����С�����ʹ�� `TestScheduler.Start` ����������������������ͨ��Ϊ�ڶ��͵������������� 0���������� 5 ���ȡ�����ǵĶ��ġ�һ�� `Start` ����������ϣ�����������Ǽ�¼�����ݡ�

```csharp
var scheduler = new TestScheduler();
var source = Observable.Interval(TimeSpan.FromSeconds(1), scheduler)
    .Take(4);

var testObserver = scheduler.Start(
    () => source, 
    0, 
    0, 
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("{0} @ {1}", message.Value, message.Time);
}
```

�����

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000001
OnNext(1) @ 20000001
OnNext(2) @ 30000001
OnNext(3) @ 40000001
OnCompleted() @ 40000001
```

��ע�⣬`ITestObserver<T>` ��¼ `OnNext` �� `OnCompleted` ֪ͨ����������Դ�����ֹ��`ITestObserver<T>` ���¼ `OnError` ֪ͨ��

���ǿ��Ըı���������Բ鿴��������Ӱ�졣����֪�� `Observable.Interval` ������һ����ɹ۲������˴���������ʱ�䲢����Ҫ���ı����ⶩ��ʱ����Ըı����ǵĽ����������ǽ����Ϊ 2 �룬���ǻ�ע�⵽����������ô���ʱ��ά���� 5 �룬���ǻ���һЩ��Ϣ��

```csharp
var testObserver = scheduler.Start(
    () => Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(4), 
    0,
    TimeSpan.FromSeconds(2).Ticks,
    TimeSpan.FromSeconds(5).Ticks);
```

�����

```
Time is 50000000 ticks
Received 2 notifications
OnNext(0) @ 30000000
OnNext(1) @ 40000000
```

�����ڵ� 2 �뿪ʼ���ģ�`Interval` ��ÿ������ֵ������ 3 ��͵� 4 �룩���ڵ� 5 ������ȡ���˶��ġ��������Ǵ������������ `OnNext` ��Ϣ�Լ� `OnCompleted` ��Ϣ��

�� `TestScheduler.Start` ������������������������ʾ��

```csharp
public ITestableObserver<T> Start<T>(Func<IObservable<T>> create, long disposed)
{
    if (create == null)
    {
        throw new ArgumentNullException("create");
    }
    else
    {
        return this.Start<T>(create, 100L, 200L, disposed);
    }
}

public ITestableObserver<T> Start<T>(Func<IObservable<T>> create)
{
    if (create == null)
    {
        throw new ArgumentNullException("create");
    }
    else
    {
        return this.Start<T>(create, 100L, 200L, 1000L);
    }
}
```

��������������Щ����ֻ�ǵ�������һֱ�ڿ��ı��壬��������һЩĬ��ֵ����ЩĬ��ֵ�ڴ����Ͷ���֮���Լ�ʵ�ʹ�������֮ǰ�ṩ���㹻�ļ�϶����ЩĬ��ֵû��ʲô�ر�����ģ�����������Ӽ���Գ�����ȫ���Ե���ʵ������Ը��������Լ��������Ч������ô����ܸ�ϲ�����ַ�ʽ��Rx Դ���뱾�������ǧ�����ԣ����д���ʹ����򵥵� `Start` ���أ��ո�һ���ڴ�����й����Ŀ�����Ա�ܿ��ϰ������ʱ�� 100 ��������ʱ�� 200 ���ģ��Լ���ʱ�� 1000 ֮ǰ��������Ҫ���Ե����ж������뷨��

### CreateColdObservable

�������ǿ��Լ�¼һ���ɹ۲�����һ��������Ҳ����ʹ�� `CreateColdObservable` ���ط�һ�� `Recorded<Notification<int>>`��`CreateColdObservable` ��ǩ��ֻ�����һ�� `params` ����ļ�¼֪ͨ��

```csharp
// ��һϵ��֪ͨ�д���һ����ɹ۲����
// ������ʾָ����Ϣ��Ϊ����ɹ۲����
public ITestableObservable<T> CreateColdObservable<T>(
    params Recorded<Notification<T>>[] messages)
{...}
```

`CreateColdObservable` ����һ�� `ITestableObservable<T>`������ӿ�ͨ����¶ "����" �б��������������Ϣ�б���չ�� `IObservable<T>`��

```csharp
public interface ITestableObservable<T> : IObservable<T>
{
    // ��ȡ�Կɹ۲����Ķ��ġ�
    IList<Subscription> Subscriptions { get; }

    // ��ȡ�ɹ۲�����͵ļ�¼֪ͨ��
    IList<Recorded<Notification<T>>> Messages { get; }
}
```

ʹ�� `CreateColdObservable`�����ǿ���ģ������֮ǰʹ�� `Observable.Interval` �Ĳ��ԡ�

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateColdObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
    new Recorded<Notification<long>>(20000000, Notification.CreateOnNext(1L)),
    new Recorded<Notification<long>>(30000000, Notification.CreateOnNext(2L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnNext(3L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnCompleted<long>())
    );

var testObserver = scheduler.Start(
    () => source,
    0,
    0,
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("  {0} @ {1}", message.Value, message.Time);
}
```

�����

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000001
OnNext(1) @ 20000001
OnNext(2) @ 30000001
OnNext(3) @ 40000001
OnCompleted() @ 40000001
```

��ע�⣬���ǵ������ `Observable.Interval` ��ǰһ��ʾ����ȫ��ͬ��

### CreateHotObservable

����Ҳ����ʹ�� `CreateHotObservable` ���������Ȳ��Կɹ۲����С��������� `CreateColdObservable` ��ͬ�Ĳ����ͷ���ֵ���������ڣ�ÿ����Ϣ������ʱ������������ڿɹ۲���󴴽�ʱ��ʱ�䣬�������� `CreateColdObservable` ����һ���Ķ���ʱ�䡣

���������������Ǹ����䡱��������������һ���ȿɹ۲����С�

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateHotObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
// ...    
```

�����

```
Time is 50000000 ticks
Received 5 notifications
OnNext(0) @ 10000000
OnNext(1) @ 20000000
OnNext(2) @ 30000000
OnNext(3) @ 40000000
OnCompleted() @ 40000000
```

ע�⣬��������������ơ����Ŵ����Ͷ��Ĳ���Ӱ���ȿɹ۲�������֪ͨ����ͬλ����ǰ 1 �̷�����

ͨ���޸����ⴴ��ʱ������ⶩ��ʱ��Ϊ��ֵͬ�����ǿ��Կ����ȿɹ۲�������Ҫ��֮ͬ����������ɹ۲������˵�����ⴴ��ʱ�䲢û��������Ӱ�죬��Ϊ�����������κζ����ġ�����ζ�����ǲ��ܴ����ɹ۲������κ�������Ϣ�������ȿɹ۲����������Ƕ���̫�����ǿ��ܻ�����Ϣ����������������������ȿɹ۲���󣬵�ֻ�� 1 �����������˴���˵�һ����Ϣ����

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateHotObservable(
    new Recorded<Notification<long>>(10000000, Notification.CreateOnNext(0L)),
    new Recorded<Notification<long>>(20000000, Notification.CreateOnNext(1L)),
    new Recorded<Notification<long>>(30000000, Notification.CreateOnNext(2L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnNext(3L)),
    new Recorded<Notification<long>>(40000000, Notification.CreateOnCompleted<long>())
    );

var testObserver = scheduler.Start(
    () => source,
    0,
    TimeSpan.FromSeconds(1).Ticks,
    TimeSpan.FromSeconds(5).Ticks);

Console.WriteLine("Time is {0} ticks", scheduler.Clock);
Console.WriteLine("Received {0} notifications", testObserver.Messages.Count);

foreach (Recorded<Notification<long>> message in testObserver.Messages)
{
    Console.WriteLine("  {0} @ {1}", message.Value, message.Time);
}
```

�����

```
Time is 50000000 ticks
Received 4 notifications
OnNext(1) @ 20000000
OnNext(2) @ 30000000
OnNext(3) @ 40000000
OnCompleted() @ 40000000
```

### CreateObserver 

������������ʹ�� `TestScheduler.Start` ����������Ҫ�����Ĺ۲��߽��и�ϸ���ȵĿ��ƣ�������ʹ�� `TestScheduler.CreateObserver()`���⽫����һ�� `ITestObserver`��������ʹ��������������Ŀɹ۲����еĶ��ġ����⣬����Ȼ���Կ�����¼����Ϣ���κζ����ߡ�

�ִ���ҵ��׼Ҫ��㷺�����Զ�����Ԫ����������������֤��׼��Ȼ�����������ͨ����һ���������õ���������Rx �ṩ��һ��������õĲ��Թ���ʵ�֣�����ȷ���Ժ͸��������Ĳ��ԡ�`TestScheduler` �ṩ�˿�������ʱ����������ڲ��ԵĿɹ۲����еķ������������ɿɿ��ز��Բ���ϵͳ������ʹ Rx �������������������