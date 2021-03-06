﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.RabbitMqTransport.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using MassTransit.Testing;
    using MassTransit.Testing.TestDecorators;
    using NUnit.Framework;
    using RabbitMQ.Client;
    using TestFramework;
    using Transports;


    [TestFixture]
    public class RabbitMqTestFixture :
        BusTestFixture
    {
        static readonly ILog _log = Logger.Get<RabbitMqTestFixture>();
        IBusControl _bus;
        Uri _inputQueueAddress;
        ISendEndpoint _inputQueueSendEndpoint;
        ISendEndpoint _busSendEndpoint;
        readonly TestSendObserver _sendObserver;
        Uri _hostAddress;
        IMessageNameFormatter _nameFormatter;
        BusHandle _busHandle;
        string _nodeHostName;
        IRabbitMqHost _host;

        public RabbitMqTestFixture()
        {
            _hostAddress = new Uri("rabbitmq://[::1]/test/");
            _inputQueueAddress = new Uri(_hostAddress, "input_queue");

            _sendObserver = new TestSendObserver(TestTimeout);
        }

        protected RabbitMqTestFixture(Uri logicalHostAddress)
            : this()
        {
            _nodeHostName = _hostAddress.Host;

            _hostAddress = logicalHostAddress;
            _inputQueueAddress = new Uri(_hostAddress, "input_queue");
        }

        protected override IBus Bus => _bus;

        /// <summary>
        /// The sending endpoint for the InputQueue
        /// </summary>
        protected ISendEndpoint InputQueueSendEndpoint => _inputQueueSendEndpoint;

        protected Uri InputQueueAddress
        {
            get { return _inputQueueAddress; }
            set
            {
                if (Bus != null)
                    throw new InvalidOperationException("The Bus has already been created, too late to change the URI");

                _inputQueueAddress = value;
            }
        }

        protected Uri HostAddress
        {
            get { return _hostAddress; }
            set
            {
                if (Bus != null)
                    throw new InvalidOperationException("The Bus has already been created, too late to change the URI");

                _hostAddress = value;
            }
        }

        /// <summary>
        /// The sending endpoint for the Bus 
        /// </summary>
        protected ISendEndpoint BusSendEndpoint => _busSendEndpoint;

        protected ISentMessageList Sent => _sendObserver.Messages;

        protected Uri BusAddress => _bus.Address;

        [OneTimeSetUp]
        public async Task SetupInMemoryTestFixture()
        {
            _bus = CreateBus();

            _busHandle = await _bus.StartAsync();
            try
            {
                _busSendEndpoint = await _bus.GetSendEndpoint(_bus.Address);
                _busSendEndpoint.ConnectSendObserver(_sendObserver);

                _inputQueueSendEndpoint = await _bus.GetSendEndpoint(_inputQueueAddress);
                _inputQueueSendEndpoint.ConnectSendObserver(_sendObserver);
            }
            catch (Exception)
            {
                try
                {
                    using (var tokenSource = new CancellationTokenSource(TestTimeout))
                    {
                        await _busHandle?.StopAsync(tokenSource.Token);
                    }
                }
                finally
                {
                    _busHandle = null;
                    _bus = null;
                }

                throw;
            }
        }

        [OneTimeTearDown]
        public async Task TearDownInMemoryTestFixture()
        {
            try
            {
                using (var tokenSource = new CancellationTokenSource(TestTimeout))
                {
                    await _bus.StopAsync(tokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Bus Stop Failed", ex);
            }
            finally
            {
                _busHandle = null;
                _bus = null;
            }
        }

        protected virtual void ConfigureBus(IRabbitMqBusFactoryConfigurator configurator)
        {
        }

        protected virtual void ConfigureBusHost(IRabbitMqBusFactoryConfigurator configurator, IRabbitMqHost host)
        {
        }

        protected virtual void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
        {
        }

        protected virtual void OnCleanupVirtualHost(IModel model)
        {
        }

        IBusControl CreateBus()
        {
            return MassTransit.Bus.Factory.CreateUsingRabbitMq(x =>
            {
                ConfigureBus(x);

                _host = ConfigureHost(x);

                CleanUpVirtualHost(_host);

                ConfigureBusHost(x, _host);

                x.ReceiveEndpoint(_host, "input_queue", e =>
                {
                    e.PrefetchCount = 16;
                    e.PurgeOnStartup = true;

                    ConfigureInputQueueEndpoint(e);
                });
            });
        }

        protected IRabbitMqHost Host =>_host;

        protected virtual IRabbitMqHost ConfigureHost(IRabbitMqBusFactoryConfigurator x)
        {
            return x.Host(_hostAddress, h =>
            {
                h.Username("guest");
                h.Password("guest");

                if (!string.IsNullOrWhiteSpace(_nodeHostName))
                    h.UseCluster(c => c.Node(_nodeHostName));
            });
        }

        protected IMessageNameFormatter NameFormatter => _nameFormatter;

        void CleanUpVirtualHost(IRabbitMqHost host)
        {
            try
            {
                _nameFormatter = new RabbitMqMessageNameFormatter();

                var connectionFactory = host.Settings.GetConnectionFactory();
                using (
                    var connection = host.Settings.ClusterMembers?.Any() ?? false
                        ? connectionFactory.CreateConnection(host.Settings.ClusterMembers, host.Settings.Host)
                        : connectionFactory.CreateConnection())
                using (var model = connection.CreateModel())
                {
                    model.ExchangeDelete("input_queue");
                    model.QueueDelete("input_queue");

                    model.ExchangeDelete("input_queue_skipped");
                    model.QueueDelete("input_queue_skipped");

                    model.ExchangeDelete("input_queue_error");
                    model.QueueDelete("input_queue_error");

                    model.ExchangeDelete("input_queue_delay");
                    model.QueueDelete("input_queue_delay");

                    OnCleanupVirtualHost(model);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}