﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs.Core;
using Azure.Messaging.EventHubs.Errors;
using Moq;
using NUnit.Framework;

namespace Azure.Messaging.EventHubs.Tests
{
    /// <summary>
    ///   The suite of tests for the <see cref="BasicRetryPolicy" />
    ///   class.
    /// </summary>
    ///
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class BasicRetryPolicyTests
    {
        /// <summary>
        ///   The test cases for exception types known to be retriable.
        /// </summary>
        ///
        public static IEnumerable<object[]> RetriableExceptionTestCases()
        {
            yield return new object[] { new TimeoutException() };
            yield return new object[] { new OperationCanceledException() };
            yield return new object[] { new SocketException(500) };

            // Task Canceled should use the inner exception as the decision point.

            yield return new object[] { new TaskCanceledException("dummy", new EventHubsException(true, null)) };

            // Aggregate should use the first inner exception as the decision point.

            yield return new object[]
            {
                new AggregateException(new Exception[]
                {
                    new EventHubsException(true, null),
                    new ArgumentException()
                })
            };
        }

        /// <summary>
        ///   The test cases for exception types known to be non-retriable.
        /// </summary>
        ///
        public static IEnumerable<object[]> NonRetriableExceptionTestCases()
        {
            yield return new object[] { new ArgumentException() };
            yield return new object[] { new InvalidOperationException() };
            yield return new object[] { new NotSupportedException() };
            yield return new object[] { new NullReferenceException() };
            yield return new object[] { new OutOfMemoryException() };
            yield return new object[] { new ObjectDisposedException("dummy") };

            // Task Canceled should use the inner exception as the decision point.

            yield return new object[] { new TaskCanceledException("dummy", new EventHubsException(false, null)) };

            // Null is not retriable, even if it is a blessed type.

            yield return new object[] { (TimeoutException)null };

            // Aggregate should use the first inner exception as the decision point.

            yield return new object[]
            {
                new AggregateException(new Exception[]
                {
                    new ArgumentException(),
                    new EventHubsException(true, null),
                    new TimeoutException()
                })
            };
        }

        /// <summary>
        ///   Verifies functionality of the <see cref="BasicRetryPolicy.CalculateTryTimeout" />
        ///   method.
        /// </summary>
        ///
        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        public void CalulateTryTimeoutRespectsOptions(int attemptCount)
        {
            var timeout = TimeSpan.FromSeconds(5);
            var options = new RetryOptions { TryTimeout = timeout };
            var policy = new BasicRetryPolicy(options);

            Assert.That(policy.CalculateTryTimeout(attemptCount), Is.EqualTo(options.TryTimeout));
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        public void CalculateRetryDelayDoesNotRetryWhenThereIsNoMaximumRetries()
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 0,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.FromHours(1),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), -1), Is.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        public void CalculateRetryDelayDoesNotRetryWhenThereIsNoMaximumDelay()
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.Zero,
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), 88), Is.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCase(6)]
        [TestCase(9)]
        [TestCase(14)]
        [TestCase(200)]
        public void CalculateRetryDelayDoesNotRetryWhenAttemptsExceedTheMaximum(int retryAttempt)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 5,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.FromHours(1),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), retryAttempt), Is.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        public void CalculateRetryDelayAllowsRetryForTransientExceptions()
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.FromSeconds(100),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(new EventHubsException(true, null, null), 88), Is.Not.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCaseSource(nameof(RetriableExceptionTestCases))]
        public void CalculateRetryDelayAllowsRetryForKnownRetriableExceptions(Exception exception)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.FromSeconds(100),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(exception, 88), Is.Not.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCaseSource(nameof(NonRetriableExceptionTestCases))]
        public void CalculateRetryDelayDoesNotRetryForNotKnownRetriableExceptions(Exception exception)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(1),
                MaximumDelay = TimeSpan.FromSeconds(100),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(exception, 88), Is.Null);
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(240)]
        public void CalculateRetryDelayRespectsMaximumDuration(int delaySeconds)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(delaySeconds),
                MaximumDelay = TimeSpan.FromSeconds(1),
                Mode = RetryMode.Fixed
            });

            Assert.That(policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), 88), Is.EqualTo(policy.Options.MaximumDelay));
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void CalculateRetryDelayUsesFixedMode(int iterations)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromSeconds(iterations),
                MaximumDelay = TimeSpan.FromHours(72),
                Mode = RetryMode.Fixed
            });

            var variance = TimeSpan.FromSeconds(policy.Options.Delay.TotalSeconds * policy.JitterFactor);

            for (var index = 0; index < iterations; ++index)
            {
                Assert.That(policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), 88), Is.EqualTo(policy.Options.Delay).Within(variance), $"Iteration: { index } produced an unexpected delay.");
            }
        }

        /// <summary>
        ///  Verifies functionality of the <see cref="BasicRetryPolicy.CalculateRetryDelay" />
        ///  method.
        /// </summary>
        ///
        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(25)]
        public void CalculateRetryDelayUsesExponentialMode(int iterations)
        {
            var policy = new BasicRetryPolicy(new RetryOptions
            {
                MaximumRetries = 99,
                Delay = TimeSpan.FromMilliseconds(15),
                MaximumDelay = TimeSpan.FromHours(50000),
                Mode = RetryMode.Exponential
            });

            var previousDelay = TimeSpan.Zero;

            for (var index = 0; index < iterations; ++index)
            {
                var variance = TimeSpan.FromSeconds((policy.Options.Delay.TotalSeconds * index) * policy.JitterFactor);
                var delay = policy.CalculateRetryDelay(Mock.Of<TimeoutException>(), index);

                Assert.That(delay.HasValue, Is.True, $"Iteration: { index } did not have a value.");
                Assert.That(delay.Value, Is.GreaterThan(previousDelay.Add(variance)), $"Iteration: { index } produced an unexpected delay.");

                previousDelay = delay.Value;
            }
        }
    }
}
