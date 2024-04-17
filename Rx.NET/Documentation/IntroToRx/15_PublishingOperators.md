# ����������

��Դ��Ҫ�ܹ����¼����ݸ���������ߡ���Ȼ���ǿ����Լ�ʵ�ֶ����߸��٣�����дһ��ֻ�����ڵ��������ߵĹ��ڼ򻯵�Դ���ܻ�����ס���Ȼ�ⲻ����`IObservable<T>`������ʵ�֣�������������ʹ��Rx��һ��_�ಥ_���������䷢��Ϊ�ඩ������Դ���Ǿ��޹ؽ�Ҫ�ˡ���["��ʾ�ļ�ϵͳ�¼���Rx"](03_CreatingObservableSequences.md#representing-filesystem-events-in-rx)��ʹ����������ɣ����������ڱ����н������ģ����������һЩ���塣

## �ಥ

Rx�ṩ��������������ʹ�����ܹ���ʹ�ö�ĳ���ײ�Դ�ĵ�һ������֧�ֶ�������ߣ�[`Publish`](#publish)��[`PublishLast`](#publishlast) �� [`Replay`](#replay)������������Χ��Rx��`Multicast`�������İ�װ�������ṩ���������еĹ�ͬ���ơ�

`Multicast`���κ�`IObservable<T>`��Ϊһ��`IConnectableObservable<T>`����������������ֻ��������һ��`Connect`������

```csharp
public interface IConnectableObservable<out T> : IObservable<T>
{
    IDisposable Connect();
}
```

������������`IObservable<T>`���������`IConnectableObservable<T>`�ϵ���`Subscribe`����`Multicast`���ص�ʵ������������ʱ������õײ�Դ��`Subscribe`����ֻ�������`Connect`ʱ�ŵ��õײ�Դ��`Subscribe`��Ϊ���������ܿ�����һ�㣬�����Ƕ���һ��ÿ�ε���`Subscribe`ʱ�����ӡ����Ϣ��Դ��

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

������ֻ�ᱻ����һ�Σ������ж��ٹ۲��߶��ģ�`Multicast`�޷����ݸ����Լ���`Subscribe`������`IObserver<T>`����Ϊ���ǿ�����������������ʹ��[Subject](03_CreatingObservableSequences.md#subjectt)��Ϊ���ݸ��ײ�Դ�ĵ�һ`IObserver<T>`�����subject������������ж����ߡ��������ֱ�ӵ���`Multicast`��������Ҫ����������Ҫʹ�õ�subject��

```csharp
IConnectableObservable<int> m = src.Multicast(new Subject<int>());
```

�������ڿ��Զ�ζ��������

```csharp
m.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
m.Subscribe(x => Console.WriteLine($"Sub2: {x}"));
m.Subscribe(x => Console.WriteLine($"Sub3: {x}"));
```

�������ǵ���`Connect`��������Щ�����߶������յ��κζ�����

```csharp
m.Connect();
```

**ע��**��`Connect`����һ��`IDisposable`�����øö����`Dispose`��ȡ�����ĵײ���Դ��

��ε���`Connect`���������������

```csharp
Create callback called
Sub1: 1
Sub2: 1
Sub3: 1
Sub1: 2
Sub2: 2
Sub3: 2
```

�������������ģ����Ǵ��ݸ�`Create`�ķ���ֻ������һ�Σ�ȷ��`Multicast`ȷʵֻ������һ�Σ����������Ѿ�������`Subscribe`���Ρ�����ÿ����Ŀ�����ݸ��������������ġ�

`Multicast`�Ĺ�����ʽ�൱�򵥣�����subject��ɴ󲿷ֹ�����ÿ������`Multicast`���صĿɹ۲�����ϵ���`Subscribe`ʱ����ֻ�ǵ���subject��`Subscribe`���������`Connect`ʱ����ֻ�ǽ�subject���ݸ��ײ�Դ��`Subscribe`��������δ���������ͬ��Ч����

```csharp
var s = new Subject<int>();

s.Subscribe(x => Console.WriteLine($"Sub1: {x}"));
s.Subscribe(x => Console.WriteLine($"Sub2: {x}"));
s.Subscribe(x => Console.WriteLine($"Sub3: {x}"));

src.Subscribe(s);
```

Ȼ����`Multicast`��һ���ŵ���������`IConnectableObservable<T>`�����������Ժ󽫿����ģ�Rx��һЩ��������֪�����������ӿ�һ������

`Multicast`�ṩ��һ������ȫ��ͬ��ʽ���������ذ汾��������������Ҫ��дһ����ѯ���ò�ѯʹ����Դ`observable`���εĳ��������磬���ǿ�����ʹ��`Zip`������ڵ���Ŀ�ԣ�

```csharp
IObservable<(int, int)> ps = src.Zip(src.Skip(1));
ps.Subscribe(ps => Console.WriteLine(ps));
```

(��Ȼ[`Buffer`](08_Partitioning.md#buffer)����������һ�������Եķ�����������£�������`Zip`������һ���ŵ�������Զ���������һ��Ķԡ�������Ҫ��`Buffer`�����Ƕ�ʱ�����������ǵ��ﾡͷʱ������һ������������������Ҫ����Ĵ�����������⡣)

ʹ�����ַ����������ǣ�Դ�������������ģ�һ��ֱ������`Zip`��Ȼ��ͨ��`Skip`�ĵڶ��������������������Ĵ��룬���ǻῴ����������

```
Create callback called
Create callback called
(1, 2)
```

���ǵ�`Create`�ص����������Ρ��ڶ���`Multicast`���������Ǳ�����������⣺

```csharp
IObservable<(int, int)> ps = src.Multicast(() => new Subject<int>(), s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

�������ʾ��������˶�����ģ�

```csharp
Create callback called
(1, 2)
```

���`Multicast`���ط���һ����ͨ��`IObservable<T>`������ζ�����ǲ���Ҫ����`Connect`������Ҳ��ζ�ţ��Խ��`IObservable<T>`��ÿ�����Ķ��ᵼ�¶Եײ���Դ��һ�����ġ�����������Ƶĳ�����˵���ǿ��Եģ�����ֻ����ͼ����Եײ���Դ���������Ķ��ġ�

����һ���ж���������������`Publish`��`PublishLast`��`Replay`������Χ��`Multicast`�İ�װ����ÿһ����Ϊ���ṩ��һ���ض����͵�subject��

### Publish

`Publish`������ʹ��[`Subject<T>`](03_CreatingObservableSequences.md#subjectt)����`Multicast`����������Ч���ǣ�һ�����ڽ���ϵ�����`Connect`��Դ�������κ���Ŀ���ᱻ���ݸ����ж����ߡ���ʹ���ܹ��������������ӣ�

```csharp
IConnectableObservable<int> m = src.Multicast(new Subject<int>());
```

�滻Ϊ�����

```csharp
IConnectableObservable<int> m = src.Publish();
```

��������ȫ��Ч��

��Ϊ`Subject<T>`���������д����`OnNext`����ת��������ÿ�������ߣ�������Ϊ�����洢֮ǰ�������κε��ã����Խ����һ����Դ��������ڵ���`Connect`֮ǰ������һЩ�����ߣ�Ȼ���ڵ���`Connect`֮�󸽼��˸��ඩ���ߣ���ô��Щ����Ķ�����ֻ���յ������Ƕ��ĺ������¼������������ʾ����һ�㣺

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

���������ʾ������ֻ������Sub3��Sub4���ĵ���������¼��������

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

��[`Multicast`](#multicast)һ����`Publish`�ṩ��һ���ṩÿ�����㶩�Ķಥ�����ء����������ܹ��򻯸ý�ĩβ��ʾ�����������

```csharp
IObservable<(int, int)> ps = src.Multicast(() => new Subject<int>(), s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

��Ϊ�����

```csharp
IObservable<(int, int)> ps = src.Publish(s => s.Zip(s.Skip(1)));
ps.Subscribe(ps => Console.WriteLine(ps));
```

`Publish`�ṩ��������ָ����ʼֵ�����ء���Щʹ��[`BehaviorSubject<T>`](03_CreatingObservableSequences.md#behaviorsubjectt)������`Subject<T>`������������ǣ����ж����߽����������Ƕ���ʱ�յ�һ��ֵ������ײ���Դ��δ����һ����Ŀ���������û�е���`Connect`����ζ������������û�ж�����Դ�������ǽ��յ���ʼֵ����������յ���������Դ��һ����Ŀ���κ��µĶ����߽������յ���Դ����������ֵ��Ȼ������յ��κν�һ������ֵ��

### PublishLast

`PublishLast`������ʹ��[`AsyncSubject<T>`](03_CreatingObservableSequences.md#asyncsubjectt)����`Multicast`����������Ч���ǣ�Դ���������һ����Ŀ�������ݸ����ж����ߡ�����Ȼ��Ҫ����`Connect`���������ʱ���ĵײ���Դ�������ж����߶����յ������¼����������Ǻ�ʱ���ģ���Ϊ`AsyncSubject<T>`��ס�����ս�������ǿ���ͨ������ʾ��������һ�㣺

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

�ⴴ����һ����0.4���ڲ���4��ֵ��Դ��������һ�Զ����ߵ���`PublishLast`���ص�`IConnectableObservable<T>`��Ȼ����������`Connect`��Ȼ����˯��1�룬�����Դʱ������ɡ�����ζ����ǰ���������߽��յ����ǽ��յ���Ψһֵ�������е����ֵ�����Ǵε���`Thread.Sleep`����֮ǰ������������ָ�������������Ķ����ߡ��������ʾ����ЩҲ���յ���ͬһ�����¼���

```
Sub1: 3 (11/14/2023 9:15:46 AM)
Sub2: 3 (11/14/2023 9:15:46 AM)

Adding more subscribers

Sub3: 3 (11/14/2023 9:15:49 AM)
Sub4: 3 (11/14/2023 9:15:49 AM)
```

�����������������Ϊ���Ƕ����������Խ��յ�ֵ���ˣ�������`PublishLast`������`AsyncSubject<T>`ֻ������Щ���Ķ��������ط������ѽ��յ�������ֵ��

### Replay

`Replay`������ʹ��[`ReplaySubject<T>`](03_CreatingObservableSequences.md#replaysubjectt)����`Multicast`����������Ч���ǣ��ڵ���`Connect`֮ǰ���ӵ��κζ�����ֻ�յ��ײ�Դ�����������¼������κ��Ժ󸽼ӵĶ�����ʵ���Ͽ���'����'����Ϊ`ReplaySubject<T>`��ס�����Ѿ��������¼���������Щ�¼��طŸ��¶����ߡ�

���ʾ��������`Publish`�ķǳ����ƣ�

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

�ⴴ����һ������4���ڶ��ڲ�����Ŀ��Դ�����ڵ���`Connect`֮ǰ���������������ߡ�Ȼ�����ȴ��㹻����ʱ���Ա��һ�͵ڶ��¼�����֮ǰ����������������Ķ����ߡ�����`Publish`��ͬ����Щ�����Ķ����߽��������Ƕ���֮ǰ�������¼���

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

���ǵ�Ȼ�����յ���Щ�¼��ģ���Ϊ�������ĵġ��������ǿ���`Sub3`��`Sub4`�ڸ���ʱѸ�ٱ�����һ�������¼�����һ�����Ǹ����ˣ����Ǿͻ������յ����н�һ�����¼���

ʹ������Ϊ��Ϊ���ܵ�`ReplaySubject<T>`�������ڴ����洢�¼�����������ܼǵõģ�����subject���Ϳ�������Ϊ���洢�����������¼������߲���������ĳ��ָ��ʱ�����Ƶ��¼���`Replay`�������ṩ��������������Щ�������Ƶ����ء�

`Replay`Ҳ֧����Ϊ�˲�������������`Multicast`�Ĳ�����չʾ��ÿ�����Ķಥģ�͡�

## RefCount

������ǰһ���п�����`Multicast`���Լ�����ְ�װ����֧������ʹ��ģ�ͣ�

* ����һ��`IConnectableObservable<T>`������������ƺ�ʱ�����Եײ���Դ�Ķ���
* ����һ����ͨ��`IObservable<T>`��ʹ�����ܹ�������ʹ��Դ�Ķ���ط�������`s.Zip(s.Take(1))`���Ĳ�ѯ�н��в���Ҫ�Ķ�����ģ�����Ȼ��ÿ������`Subscribe`����һ�εײ���Դ��`Subscribe`����

`RefCount`�ṩ��һ�����в�ͬ��ģ�͡���ʹ���ĵ��ײ���Դ����ͨ����ͨ��`Subscribe`����������Ȼֻ����һ�ζԵײ���Դ�ĵ��á�����������ʹ�õ�AISʾ���У�����ܻ�����á������ϣ������������߸��ӵ����洬ֻ�����������㲥�Ķ�λ��Ϣ�Ŀɹ۲�Դ������ͨ��ϣ��Ϊ���ṩRx-based API�Ŀ�ֻ����һ�ε��ṩ��Щ��Ϣ���κεײ���񡣶�����ܿ���ϣ����ֻ��������һ���������ڼ���ʱ�����ӡ�`RefCount`�ǳ��ʺ��ڴˣ���Ϊ��ʹ������Դ�ܹ�֧�ֶ�������ߣ���ʹ�ײ���Դ֪�����Ǻ�ʱ�ӡ�û�ж����ߡ�״̬ת��Ϊ��������һ�������ߡ�״̬��

Ϊ���ܹ��۲쵽`RefCount`�Ĳ������ҽ�ʹ��һ���޸İ汾��Դ����Դ�����ʱ�������ģ�

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

������ʾ����ͬ����ʹ����`async`����ÿ��`OnNext`֮���ӳ٣���ȷ�����߳���ʱ����������Ŀ����֮ǰ���ö�����ġ�Ȼ�����ǿ�����`RefCount`��װ�����

```csharp
IObservable<int> rc = src
    .Publish()
    .RefCount();
```

ע�������ȱ������`Publish`��������Ϊ`RefCount`����һ��`IConnectableObservable<T>`����ϣ���ڵ�һ���ж�������ʱ����Դ��������������һ��������ʱ����`Connect`�������ǳ���һ�£�

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

�����������

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

ע��ֻ��`Sub1`�յ��˵�һ���¼���������Ϊ���ݸ�`Create`�Ļص���������������ֻ�е������õ�һ��`await`ʱ�����ŷ��ظ������ߣ�ʹ�����ܹ����ӵڶ��������ߡ����Ѿ�����˵�һ���¼�������������Կ����ģ������յ��˵ڶ��͵������¼�������ȴ��㹻����ʱ����ǰ�����¼������ڸ�����������Ķ�����֮ǰ������Կ��������ĸ������߶����յ��������¼���

����������ʾ��`RefCount`�����Ծ�����ߵ�������������������������0����������`Connect`���صĶ����`Dispose`���رն��ġ���������и���Ķ����߼��룬�����������������ʾ����ʾ����һ�㣺

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

���ǵõ�����������

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

��һ�Σ�`Create`�ص����������Ρ�������Ϊ��Ծ�����ߵ���������0������`RefCount`������`Dispose`���ر����񡣵��µĶ����߳���ʱ�����ٴε���`Connect`����������������һЩ����������ָ��һ��`disconnectDelay`�����������ڶ����������������ȴ�ָ����ʱ���ٶϿ����ӣ��Բ鿴�Ƿ����µĶ��׶����߳��֡������ָ����ʱ���ȥ�ˣ������ǻ�Ͽ����ӡ�����ⲻ������Ҫ�ģ���һ�������������ʺ��㡣

## AutoConnect

`AutoConnect`����������Ϊ��`RefCount`�ǳ����ƣ����ڵ�һ�������߶���ʱ������ײ��`IConnectableObservable<T>`�ϵ���`Connect`����֮ͬ������������ͼ����Ծ�����ߵ������Ƿ��ѽ����㣺һ�������ӣ����������ڵر������ӣ���ʹ��û�ж����ߡ�

����`AutoConnect`���ܷܺ��㣬����ҪС��һ�㣬��Ϊ�����Ե���й©������Զ�����Զ��Ͽ����ӡ������������������Ȼ�ǿ��ܵģ�`AutoConnect`����һ����ѡ��`Action<IDisposable>`���Ͳ����������״����ӵ�Դʱ����������������Դ��`Connect`�������ص�`IDisposable`���ݸ��㡣�����ͨ������`Dispose`���ر�����

�����еĲ�����������һ�����ʺϴ����������ߵ�Դʱ���ܺ����á����ṩ�˸��ַ�ʽ�����Ӷ�������ߣ�ͬʱֻ�����Եײ���Դ�ĵ�һ`Subscribe`��