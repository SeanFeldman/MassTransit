﻿namespace MassTransit.RabbitMqTransport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using MassTransit.Testing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using RabbitMQ.Client;
    using RabbitMqTransport.Testing;
    using TestFramework;
    using Transports;
    using Util;


    public class RabbitMqTestFixture :
        BusTestFixture
    {
        TestExecutionContext _fixtureContext;

        public RabbitMqTestFixture(Uri logicalHostAddress = null, string inputQueueName = null)
            : this(new RabbitMqTestHarness(inputQueueName), logicalHostAddress)
        {
        }

        public RabbitMqTestFixture(RabbitMqTestHarness harness, Uri logicalHostAddress = null)
            : base(harness)
        {
            RabbitMqTestHarness = harness;

            if (logicalHostAddress != null)
            {
                RabbitMqTestHarness.NodeHostName = RabbitMqTestHarness.HostAddress.Host;
                RabbitMqTestHarness.HostAddress = logicalHostAddress;
            }

            RabbitMqTestHarness.OnConfigureRabbitMqHost += ConfigureRabbitMqHost;
            RabbitMqTestHarness.OnConfigureRabbitMqBus += ConfigureRabbitMqBus;
            RabbitMqTestHarness.OnConfigureRabbitMqReceiveEndpoint += ConfigureRabbitMqReceiveEndpoint;
            RabbitMqTestHarness.OnCleanupVirtualHost += CleanupVirtualHost;
        }

        protected RabbitMqTestHarness RabbitMqTestHarness { get; }

        /// <summary>
        /// The sending endpoint for the InputQueue
        /// </summary>
        protected ISendEndpoint InputQueueSendEndpoint => RabbitMqTestHarness.InputQueueSendEndpoint;

        protected Uri InputQueueAddress => RabbitMqTestHarness.InputQueueAddress;

        protected Uri HostAddress => RabbitMqTestHarness.HostAddress;

        /// <summary>
        /// The sending endpoint for the Bus
        /// </summary>
        protected ISendEndpoint BusSendEndpoint => RabbitMqTestHarness.BusSendEndpoint;

        protected ISentMessageList Sent => RabbitMqTestHarness.Sent;

        protected Uri BusAddress => RabbitMqTestHarness.BusAddress;

        protected IMessageNameFormatter NameFormatter => RabbitMqTestHarness.NameFormatter;

        protected RabbitMqHostSettings GetHostSettings()
        {
            return RabbitMqTestHarness.GetHostSettings();
        }

        [OneTimeSetUp]
        public async Task SetupRabbitMqTestFixture()
        {
            _fixtureContext = TestExecutionContext.CurrentContext;

            LoggerFactory.Current = _fixtureContext;

            await RabbitMqTestHarness.Start().ConfigureAwait(false);

            await Task.Delay(200);
        }

        [OneTimeTearDown]
        public async Task TearDownRabbitMqTestFixture()
        {
            LoggerFactory.Current = _fixtureContext;

            await RabbitMqTestHarness.Stop().ConfigureAwait(false);

            RabbitMqTestHarness.Dispose();
        }

        protected virtual void ConfigureRabbitMqHost(IRabbitMqHostConfigurator configurator)
        {
        }

        protected virtual void ConfigureRabbitMqBus(IRabbitMqBusFactoryConfigurator configurator)
        {
        }

        protected virtual void ConfigureRabbitMqReceiveEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
        {
        }

        void CleanupVirtualHost(IModel model)
        {
            var cleanVirtualHostEntirely = !bool.TryParse(Environment.GetEnvironmentVariable("CI"), out var isBuildServer) || !isBuildServer;
            if (cleanVirtualHostEntirely)
            {
                IList<string> exchanges = TaskUtil.Await(() => GetVirtualHostEntities("exchanges"));
                foreach (var exchange in exchanges)
                    model.ExchangeDelete(exchange);

                IList<string> queues = TaskUtil.Await(() => GetVirtualHostEntities("queues"));
                foreach (var queue in queues)
                    model.QueueDelete(queue);
            }

            OnCleanupVirtualHost(model);
        }

        protected virtual void OnCleanupVirtualHost(IModel model)
        {
        }

        async Task<IList<string>> GetVirtualHostEntities(string element)
        {
            try
            {
                using var client = new HttpClient();
                var byteArray = Encoding.ASCII.GetBytes($"{RabbitMqTestHarness.Username}:{RabbitMqTestHarness.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var requestUri = new UriBuilder("http", HostAddress.Host, 15672, $"api/{element}/{HostAddress.AbsolutePath.Trim('/')}").Uri;
                using var response = await client.GetStreamAsync(requestUri);
                using var reader = new StreamReader(response);
                using var jsonReader = new JsonTextReader(reader);

                var token = await JToken.ReadFromAsync(jsonReader);

                IEnumerable<string> entities = from elements in token.Children()
                    select elements["name"].ToString();

                return entities.Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("amq.")).ToList();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return new List<string>();
        }
    }
}
