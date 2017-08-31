﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Vostok.Clusterclient.Helpers;
using Vostok.Clusterclient.Model;
using Vostok.Clusterclient.Modules;
using Vostok.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Vostok.Clusterclient.Core.Modules
{
    public class ErrorCatchingModule_Tests
    {
        private readonly IRequestContext context;
        private readonly ILog log;
        private readonly ErrorCatchingModule module;

        public ErrorCatchingModule_Tests(ITestOutputHelper outputHelper)
        {
            log = Substitute.For<ILog>();
            log
                .When(l => l.Log(Arg.Any<LogEvent>()))
                .Do(info => new TestOutputLog(outputHelper).Log(info.Arg<LogEvent>()));

            context = Substitute.For<IRequestContext>();
            context.Log.Returns(log);
            context.Request.Returns(Request.Get("foo/bar"));
            module = new ErrorCatchingModule();
        }

        [Fact]
        public void Should_return_unexpected_exception_result_if_next_module_throws_an_error()
        {
            module.ExecuteAsync(context, _ => throw new Exception()).Result.Status.Should().Be(ClusterResultStatus.UnexpectedException);
        }

        [Fact]
        public void Should_return_canceled_result_if_next_module_throws_a_cancellation_exception()
        {
            module.ExecuteAsync(context, _ => throw new OperationCanceledException()).Result.Status.Should().Be(ClusterResultStatus.Canceled);
        }

        [Fact]
        public void Should_log_an_error_message_if_next_module_throws_an_error()
        {
            module.ExecuteAsync(context, _ => throw new Exception()).GetAwaiter().GetResult();

            log.Received(1).Log(Arg.Is<LogEvent>(evt => evt.Level == LogLevel.Error));
        }

        [Fact]
        public void Should_not_log_an_error_message_if_next_module_throws_a_cancellation_error()
        {
            module.ExecuteAsync(context, _ => throw new OperationCanceledException()).GetAwaiter().GetResult();

            log.ReceivedCalls().Should().BeEmpty();
        }

        [Fact]
        public void Should_delegate_to_next_module_if_no_exceptions_arise()
        {
            var task = Task.FromResult(ClusterResult.ReplicasNotFound(context.Request));

            module.ExecuteAsync(context, _ => task).Result.Should().BeSameAs(task.Result);
        }
    }
}
