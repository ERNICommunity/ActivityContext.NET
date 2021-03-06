﻿using System;
using System.Threading;
using Xunit;

namespace ActivityContext.Tests
{
    public class ActivityContextThreadingTests
    {
        /// <summary>
        /// Main activity covers execution of two child concurrent threads.
        /// Threads executes code, which enforces overlap of their execution.
        /// First child thread creates activity scope. Tests verifies that this activity is not visible in second child thread.
        /// </summary>
        [Fact]
        public void ConcurrentThreadsDoNotAffectEachOtherContext()
        {
            const string activityName = "Test";
            var activityId1 = Guid.NewGuid();
            var activityId2 = Guid.NewGuid();

            var gate1 = new ManualResetEvent(false);
            var gate2 = new ManualResetEvent(false);

            using (new Activity(activityName, activityId1))
            {
                var t1 = new Thread(() =>
                {
                    using (new Activity(activityName, activityId2))
                    {
                        // Flag activity scope is created
                        gate1.Set();

                        // Wait for asserts in second thread
                        gate2.WaitOne();
                    }
                });

                var t2 = new Thread(() =>
                {
                    // Wait for activity scope creation in first thread
                    gate1.WaitOne();

                    // Assert only the parent activity is visible here.
                    var activities = Activity.GetCurrentActivities();
                    Assert.Equal(1, activities.Count);
                    Assert.Equal(activityId1, activities[0].Id);

                    // Flag assertion are completed
                    gate2.Set();
                });

                // Start both threads
                t1.Start();
                t2.Start();

                // Wait for both threads.
                t1.Join();
                t2.Join();
            }
        }

        [Fact]
        public void DisposedActivityIsPreservedInChildContext()
        {
            var gate1 = new ManualResetEvent(false);
            var gate2 = new ManualResetEvent(false);

            Thread t;

            using (new Activity("Main"))
            {
                t = new Thread(() =>
                {
                    using (new Activity("Child"))
                    {
                        // Flag Child activity is created.
                        gate1.Set();

                        // Wait until main activity is disposed.
                        gate2.WaitOne();

                        // Main activity is already disposed, however child activity is still active.
                        // Therefore current context should contain both activities.
                        Assert.Equal(2, Activity.GetCurrentActivities().Count);
                    }
                });

                t.Start();

                // Wait until child activity is created.
                gate1.WaitOne();
            }

            // Main activity is already disposed.
            Assert.Equal(0, Activity.GetCurrentActivities().Count);

            gate2.Set();
            t.Join();
        }
    }
}
