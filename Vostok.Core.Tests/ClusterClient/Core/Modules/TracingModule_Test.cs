﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Vostok.Airlock;
using Vostok.Clusterclient.Model;
using Vostok.Clusterclient.Modules;
using Vostok.Clusterclient.Strategies;
using Vostok.Tracing;
using NUnit.Framework;

namespace Vostok.Clusterclient.Core.Modules
{
    public class TracingModule_Test
    {
        private TracingModule tracingModule;
        private IAirlockClient airlockClient;

        [SetUp]
        public void SetUp()
        {
            airlockClient = Substitute.For<IAirlockClient>();
            Trace.Configuration.AirlockClient = airlockClient;
            Trace.Configuration.AirlockRoutingKey = () => "routingKey";
            tracingModule = new TracingModule("serviceName");
        }

        [Test]
        public async Task ExecuteAsync_should_create_trace()
        {
            var requestContext = Substitute.For<IRequestContext>();
            var request = new Request("GET", new Uri("vostok/process?p1=p", UriKind.Relative));
            requestContext.Request.Returns(request);
            requestContext.Strategy.Returns(new ParallelRequestStrategy(2));
            var response = new Response(ResponseCode.Conflict);
            var clusterResult = new ClusterResult(ClusterResultStatus.Success, new List<ReplicaResult>(), response, request);
            var expectedAnnotations = new Dictionary<string, string>
            {
                [TracingAnnotationNames.Kind] = "cluster-client",
                [TracingAnnotationNames.Component] = "cluster-client",
                [TracingAnnotationNames.ClusterStrategy] = "Parallel-2",
                [TracingAnnotationNames.ClusterStatus] = "Success",
                [TracingAnnotationNames.HttpUrl] = "vostok/process",
                [TracingAnnotationNames.HttpMethod] = "GET",
                [TracingAnnotationNames.HttpRequestContentLength] = "0",
                [TracingAnnotationNames.HttpResponseContentLength] = "0",
                [TracingAnnotationNames.HttpCode] = "409",
                [TracingAnnotationNames.Service] = "serviceName"
            };
            airlockClient.Push(Arg.Any<string>(), Arg.Do<Span>(span =>
            {
                span.Annotations.ShouldBeEquivalentTo(expectedAnnotations);
            }));

            await tracingModule.ExecuteAsync(requestContext, x => Task.FromResult(clusterResult)).ConfigureAwait(false);

            airlockClient.Received().Push(Arg.Any<string>(), Arg.Any<Span>());
        }
    }
}