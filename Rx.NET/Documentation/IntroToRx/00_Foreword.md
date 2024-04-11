# Rx简介
作者 Ian Griffiths 和 Lee Campbell
   
---

反应性编程并不是一个新概念。任何类型的用户界面开发都必然涉及到响应事件的代码。像 [Smalltalk](https://en.wikipedia.org/wiki/Smalltalk)、[Delphi](https://en.wikipedia.org/wiki/Delphi_(software)) 和 .NET 语言已经普及了反应式或事件驱动的编程范例。像 [CEP (复杂事件处理)](https://en.wikipedia.org/wiki/Complex_event_processing) 和 [CQRS (命令查询责任隔离)](https://en.wikipedia.org/wiki/Command_Query_Responsibility_Segregation) 这样的架构模式将事件作为其基本组成部分。反应性编程是任何需要处理发生事情的程序中的一个有用概念。

> 反应性编程是任何需要处理发生事情的程序中的一个有用概念。

事件驱动范式允许代码被调用，而无需打破封装或应用昂贵的轮询技术。实现这一点的常见方式包括 [观察者模式](https://en.wikipedia.org/wiki/Observer_pattern)、[直接在语言中公开的事件（例如 C#）](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/) 或通过代理注册的其他回调形式。反应式扩展通过 LINQ 扩展了回调隐喻，以启用事件序列的查询和并发管理。

.NET 运行时库已经包含了表示反应性编程核心概念的 `IObservable<T>` 和 `IObserver<T>` 接口超过十多年了。.NET的反应式扩展 (Rx.NET) 实质上是这些接口的实现库。Rx.NET 最早出现在 2010 年，但从那时起，Rx 库已经可以用于其他语言，并且这种编程方式在 JavaScript 中变得尤其流行。

本书将通过 C# 介绍 Rx。这些概念是普遍的，所以其他 .NET 语言的用户，如 VB.NET 和 F#，将能够提取概念并将它们转化为自己的特定语言。

Rx.NET 只是一个库，最初由微软创建，但现在是一个完全通过社区努力支持的开源项目。 (Rx 的当前主要维护人员 [Ian Griffiths](https://endjin.com/who-we-are/our-people/ian-griffiths/) 也是本书最新修订版的作者，事实上也是这句话的作者。)

如果你以前从未使用过 Rx，它 _将_ 改变你设计和构建软件的方式。它为计算中的一个基本重要概念提供了一个深思熟虑的抽象：事件序列。这些和列表或数组一样重要，但在 Rx 之前，库或语言中很少有直接支持，存在的支持往往是相当偶然的，并且建立在薄弱的理论基础上。Rx 改变了这一点。这个微软发明被某些传统上不是特别亲微软的开发者社区全心全意地采纳，足以证明其基本设计的优异质量。

本书旨在教你：

  * Rx 定义的类型
  * Rx 提供的扩展方法以及如何使用它们
  * 如何管理对事件源的订阅
  * 如何可视化“数据序列”并在编码前草拟你的解决方案
  * 如何利用并发为你的优势而避免常见陷阱
  * 如何组合、聚合和转换流
  * 如何测试你的 Rx 代码
  * 使用 Rx 时的一些常见最佳实践
    
学习 Rx 最好的方法是使用它。仅从本书中阅读理论将帮助你熟悉 Rx，但要完全理解它，你应该用它来构建东西。因此，我们热切鼓励你根据本书中的示例进行构建。

# 致谢

首先，我 ([Ian Griffiths](https://endjin.com/who-we-are/our-people/ian-griffiths/)) 应该明确这个修订版是在原作者 [Lee Campbell](https://github.com/LeeCampbell) 出色的工作基础上建立的。我很感激他慷慨地允许 Rx.NET 项目利用他的内容，使这个新版本得以问世。

我还想认可那些使这本书成为可能的人们。

感谢 [endjin](https://endjin.com) 的每个人，特别是 [Howard van Rooijen](https://endjin.com/who-we-are/our-people/howard-van-rooijen/) 和 [Matthew Adams](https://endjin.com/who-we-are/our-people/matthew-adams/)
不仅为本书的更新提供资金，而且还为 Rx.NET 本身的持续开发提供资金。 (还要感谢他们聘请我！)。同时感谢 [Felix Corke](https://www.blackspike.com/) 为[本书网络版](https://introtorx.com)的设计元素所做的工作。

在最初一版本的书中，除了作者[Lee Campbell](https://leecampbell.com/)，还有James Miles, Matt Barrett, [John Marks](https://johnhmarks.wordpress.com/), Duncan Mole, Cathal Golden, Keith Woods, Ray Booysen, Olivier DeHeurles, [Matt Davey](https://mdavey.wordpress.com), [Joe Albahari](https://www.albahari.com/) 和 Gregory Andrien也发挥了重要作用。

特别感谢微软团队的辛勤工作带来了Rx；[Jeffrey Van Gogh](https://www.linkedin.com/in/jeffrey-van-gogh-145673/), [Wes Dyer](https://www.linkedin.com/in/wesdyer/), [Erik Meijer](https://en.wikipedia.org/wiki/Erik_Meijer_%28computer_scientist%29) 和 [Bart De Smet](https://www.linkedin.com/in/bartdesmet/)。

同时感谢那些在Rx.NET停止由微软直接支持后，继续在开源项目中工作的人们。涉及的人很多，在这里不可能列出每一位贡献者，但我想特别感谢[Bart De Smet](https://github.com/bartdesmet)（再次感谢，因为他在微软内部转向其他事物后，仍继续在开源Rx上工作）以及[Claire Novotny](https://github.com/clairernovotny), [Daniel Weber](https://github.com/danielcweber), [David Karnok](https://github.com/akarnokd), [Brendan Forster](https://github.com/shiftkey), [Ani Betts](https://github.com/anaisbetts) 和 [Chris Pulman](https://www.linkedin.com/in/chrispulman/)。我们还要感谢[Richard Lander](https://www.linkedin.com/in/richardlander/) 和 [.NET Foundation](https://dotnetfoundation.org/) 帮助我们[endjin](https://endjin.com)成为[Rx.NET 项目](https://github.com/dotnet/reactive)的新管理人，使它能够继续繁荣。

如果你对Rx的起源感兴趣，你可能会觉得《A Little History of Reaqtor》这本电子书很有启发性。

本书依据的版本是 `System.Reactive` 版本 6.0。本书的源码可以在[https://github.com/dotnet/reactive/tree/main/Rx.NET/Documentation/IntroToRx](https://github.com/dotnet/reactive/tree/main/Rx.NET/Documentation/IntroToRx)找到。如果你在这本书中发现任何错误或其他问题，请在 https://github.com/dotnet/reactive/ [创建一个问题](https://github.com/dotnet/reactive/issues) 。如果你开始认真使用Rx.NET，你可能会发现[Reactive X slack](reactivex.slack.com)是一个有用的资源。

那么，启动Visual Studio，让我们开始吧。

---
