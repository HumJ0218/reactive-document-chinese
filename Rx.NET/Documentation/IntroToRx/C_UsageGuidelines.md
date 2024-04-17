# 附录C：使用指南

这是一份快速指南列表，旨在帮助你编写Rx查询。

- 返回序列的成员不应返回null。这适用于 `IEnumerable<T>` 和 `IObservable<T>` 序列。应返回一个空序列。
- 只有在需要提前取消订阅时才处理（Dispose）订阅。
- 始终提供一个 `OnError` 处理器。
- 避免使用阻塞操作符，如 `First`、`FirstOrDefault`、`Last`、`LastOrDefault`、`Single`、`SingleOrDefault` 和 `ForEach`；使用非阻塞的替代品，如 `FirstAsync`。
- 避免在 `IObservable<T>` 和 `IEnumerable<T>` 之间来回切换。
- 偏好使用懒惰求值而非急切求值。
- 将大型查询分解成部分。大型查询的关键指标包括：
    1. 嵌套
    2. 查询表达式语法超过10行
    3. 使用 `into` 关键字
- 命名你的可观察对象时要恰当，即避免使用像 `query`、`q`、`xs`、`ys`、`subject` 等变量名。
- 避免创建副作用。如果真的无法避免，也不要将副作用隐藏在设计为函数式使用的操作符的回调中，如 `Select` 或 `Where`。通过使用 `Do` 操作符明确指出。
- 尽可能使用 `Observable.Create` 而不是subjects来定义新的Rx源。
- 避免创建你自己的 `IObservable<T>` 接口实现。使用 `Observable.Create`（或者如果真的需要，使用subjects）。
- 避免创建你自己的 `IObserver<T>` 接口实现。偏好使用 `Subscribe` 扩展方法的重载。
- 应用程序应定义并发模型。
    - 如果需要安排延迟工作，使用调度器
    - `SubscribeOn` 和 `ObserveOn` 操作符应该总是在 `Subscribe` 方法之前使用。（所以不要把它夹在中间，例如 `source.SubscribeOn(s).Where(x => x.Foo)`。）