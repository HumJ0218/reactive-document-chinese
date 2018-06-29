﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Microsoft.Reactive.Testing;
using Xunit;
#if STRESS
using ReactiveTests.Stress.Schedulers;
#endif

namespace ReactiveTests.Tests
{

    public class EventLoopSchedulerTest
    {
        [Fact]
        public void EventLoop_ArgumentChecking()
        {
            var el = new EventLoopScheduler();

#if !NO_THREAD
            ReactiveAssert.Throws<ArgumentNullException>(() => new EventLoopScheduler(null));
#endif
            ReactiveAssert.Throws<ArgumentNullException>(() => el.Schedule<int>(42, default));
            ReactiveAssert.Throws<ArgumentNullException>(() => el.Schedule<int>(42, DateTimeOffset.Now, default));
            ReactiveAssert.Throws<ArgumentNullException>(() => el.Schedule<int>(42, TimeSpan.Zero, default));
            ReactiveAssert.Throws<ArgumentNullException>(() => el.SchedulePeriodic(42, TimeSpan.FromSeconds(1), default));
            ReactiveAssert.Throws<ArgumentOutOfRangeException>(() => el.SchedulePeriodic(42, TimeSpan.FromSeconds(-1), _ => _));
        }

        [Fact]
        public void EventLoop_Now()
        {
            var res = new EventLoopScheduler().Now - DateTime.Now;
            Assert.True(res.Seconds < 1);
        }

        [Fact]
        public void EventLoop_ScheduleAction()
        {
            var ran = false;
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();
            el.Schedule(() =>
            {
                ran = true;
                gate.Release();
            });
            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.True(ran);
        }

#if !NO_THREAD
        [Fact]
        public void EventLoop_DifferentThread()
        {
            var id = default(int);
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();
            el.Schedule(() =>
            {
                id = Thread.CurrentThread.ManagedThreadId;
                gate.Release();
            });
            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, id);
        }
#endif

        [Fact]
        public void EventLoop_ScheduleOrderedActions()
        {
            var results = new List<int>();
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();
            el.Schedule(() => results.Add(0));
            el.Schedule(() =>
            {
                results.Add(1);
                gate.Release();
            });
            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            results.AssertEqual(0, 1);
        }

        [Fact]
        public void EventLoop_SchedulerDisposed()
        {
            var d = 0;
            var e = new ManualResetEvent(false);
            var f = new ManualResetEvent(false);

            var results = new List<int>();
            var el = new EventLoopScheduler();
            el.Schedule(() => results.Add(0));
            el.Schedule(() =>
            {
                el.Dispose();
                e.Set();

                results.Add(1);

                try
                {
                    el.Schedule(() => { throw new Exception("Should be disposed!"); });
                    f.Set();
                }
                catch (ObjectDisposedException)
                {
                    // BREAKING CHANGE v2 > v1.x - New exception behavior.
                    Interlocked.Increment(ref d);
                    f.Set();
                }
            });

            e.WaitOne();

            try
            {
                el.Schedule(() => results.Add(2));
            }
            catch (ObjectDisposedException)
            {
                // BREAKING CHANGE v2 > v1.x - New exception behavior.
                Interlocked.Increment(ref d);
            }

            f.WaitOne();

            results.AssertEqual(0, 1);

            Assert.Equal(2, d);
        }

        [Fact]
        public void EventLoop_ScheduleTimeOrderedActions()
        {
            var results = new List<int>();
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();
            el.Schedule(TimeSpan.FromMilliseconds(50), () => results.Add(1));
            el.Schedule(TimeSpan.FromMilliseconds(100), () =>
                        {
                            results.Add(0);
                            gate.Release();
                        });

            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            results.AssertEqual(1, 0);
        }

        [Fact]
        public void EventLoop_ScheduleOrderedAndTimedActions()
        {
            var results = new List<int>();
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();
            el.Schedule(() => results.Add(1));
            el.Schedule(TimeSpan.FromMilliseconds(100), () =>
            {
                results.Add(0);
                gate.Release();
            });

            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            results.AssertEqual(1, 0);
        }

        [Fact]
        public void EventLoop_ScheduleTimeOrderedInFlightActions()
        {
            var results = new List<int>();
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();

            el.Schedule(TimeSpan.FromMilliseconds(100), () =>
                        {
                            results.Add(0);
                            el.Schedule(TimeSpan.FromMilliseconds(50), () => results.Add(1));
                            el.Schedule(TimeSpan.FromMilliseconds(100), () =>
                            {
                                results.Add(2);
                                gate.Release();
                            });
                        });

            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            results.AssertEqual(0, 1, 2);
        }

        [Fact]
        public void EventLoop_ScheduleTimeAndOrderedInFlightActions()
        {
            var results = new List<int>();
            var gate = new Semaphore(0, 1);
            var el = new EventLoopScheduler();

            el.Schedule(TimeSpan.FromMilliseconds(100), () =>
            {
                results.Add(0);
                el.Schedule(() => results.Add(4));
                el.Schedule(TimeSpan.FromMilliseconds(50), () => results.Add(1));
                el.Schedule(TimeSpan.FromMilliseconds(100), () =>
                {
                    results.Add(2);
                    gate.Release();
                });
            });

            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            results.AssertEqual(0, 4, 1, 2);
        }

        [Fact]
        public void EventLoop_ScheduleActionNested()
        {
            var ran = false;
            var el = new EventLoopScheduler();
            var gate = new Semaphore(0, 1);
            el.Schedule(() => el.Schedule(() =>
            {
                ran = true;
                gate.Release();
            }));
            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.True(ran);
        }

        [Fact(Skip = "")]
        public void EventLoop_ScheduleActionDue()
        {
            var ran = false;
            var el = new EventLoopScheduler();
            var sw = new Stopwatch();
            var gate = new Semaphore(0, 1);
            sw.Start();
            el.Schedule(TimeSpan.FromSeconds(0.2), () =>
            {
                ran = true;
                sw.Stop();
                gate.Release();
            });
            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.True(ran, "ran");
            Assert.True(sw.ElapsedMilliseconds > 180, "due " + sw.ElapsedMilliseconds);
        }

        [Fact(Skip = "")]
        public void EventLoop_ScheduleActionDueNested()
        {
            var ran = false;
            var el = new EventLoopScheduler();
            var gate = new Semaphore(0, 1);

            var sw = new Stopwatch();
            sw.Start();
            el.Schedule(TimeSpan.FromSeconds(0.2), () =>
            {
                sw.Stop();
                sw.Start();
                el.Schedule(TimeSpan.FromSeconds(0.2), () =>
                {
                    sw.Stop();
                    ran = true;
                    gate.Release();
                });
            });

            Assert.True(gate.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.True(ran, "ran");
            Assert.True(sw.ElapsedMilliseconds > 380, "due " + sw.ElapsedMilliseconds);
        }

#if !NO_PERF
        [Fact]
        public void Stopwatch()
        {
            StopwatchTest.Run(new EventLoopScheduler());
        }
#endif

        [Fact]
        public void EventLoop_Immediate()
        {
            var M = 1000;
            var N = 4;

            for (var i = 0; i < N; i++)
            {
                for (var j = 1; j <= M; j *= 10)
                {
                    using (var e = new EventLoopScheduler())
                    {
                        using (var d = new CompositeDisposable())
                        {
                            var cd = new CountdownEvent(j);

                            for (var k = 0; k < j; k++)
                            {
                                d.Add(e.Schedule(() => cd.Signal()));
                            }

                            if (!cd.Wait(10000))
                            {
                                Assert.True(false, "j = " + j);
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void EventLoop_TimeCollisions()
        {
            var M = 1000;
            var N = 4;

            for (var i = 0; i < N; i++)
            {
                for (var j = 1; j <= M; j *= 10)
                {
                    using (var e = new EventLoopScheduler())
                    {
                        using (var d = new CompositeDisposable())
                        {
                            var cd = new CountdownEvent(j);

                            for (var k = 0; k < j; k++)
                            {
                                d.Add(e.Schedule(TimeSpan.FromMilliseconds(100), () => cd.Signal()));
                            }

                            if (!cd.Wait(10000))
                            {
                                Assert.True(false, "j = " + j);
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void EventLoop_Spread()
        {
            var M = 1000;
            var N = 4;

            for (var i = 0; i < N; i++)
            {
                for (var j = 1; j <= M; j *= 10)
                {
                    using (var e = new EventLoopScheduler())
                    {
                        using (var d = new CompositeDisposable())
                        {
                            var cd = new CountdownEvent(j);

                            for (var k = 0; k < j; k++)
                            {
                                d.Add(e.Schedule(TimeSpan.FromMilliseconds(k), () => cd.Signal()));
                            }

                            if (!cd.Wait(10000))
                            {
                                Assert.True(false, "j = " + j);
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void EventLoop_Periodic()
        {
            var n = 0;

            using (var s = new EventLoopScheduler())
            {
                var e = new ManualResetEvent(false);

                var d = s.SchedulePeriodic(TimeSpan.FromMilliseconds(25), () =>
                {
                    if (Interlocked.Increment(ref n) == 10)
                    {
                        e.Set();
                    }
                });

                if (!e.WaitOne(10000))
                {
                    Assert.True(false);
                }

                d.Dispose();
            }
        }

#if STRESS
        [Fact]
        public void EventLoop_Stress()
        {
            EventLoop.NoSemaphoreFullException();
        }
#endif

#if DESKTOPCLR
        [Fact]
        public void EventLoop_CorrectWorkStealing()
        {
            const int workItemCount = 100;

            var failureCount = 0;
            var countdown = new CountdownEvent(workItemCount);
            var dueTime = DateTimeOffset.Now + TimeSpan.FromSeconds(1);

            using (var d = new CompositeDisposable())
            {
                for (var i = 0; i < workItemCount; i++)
                {
                    var scheduler = new EventLoopScheduler();

                    scheduler.Schedule(() =>
                    {
                        var schedulerThread = Thread.CurrentThread;

                        scheduler.Schedule(dueTime, () =>
                        {
                            if (Thread.CurrentThread != schedulerThread)
                            {
                                Interlocked.Increment(ref failureCount);
                            }
                            countdown.Signal();
                        });
                    });

                    d.Add(scheduler);
                }

                countdown.Wait();
            }

            Assert.Equal(0, failureCount);
        }
#endif
    }
}
