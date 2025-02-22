// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using System;

namespace MQTTnet.Extensions.ManagedClient
{
    public static class MqttFactoryExtensions
    {
        public static IManagedMqttClient CreateManagedMqttClient(this MqttFactory factory,
            IServiceScopeFactory serviceScopeFactory, IMqttClient mqttClient = null)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (mqttClient == null)
            {
                return new ManagedMqttClient(factory.CreateMqttClient(), factory.DefaultLogger, serviceScopeFactory);
            }

            return new ManagedMqttClient(mqttClient, factory.DefaultLogger, serviceScopeFactory);
        }

        public static IManagedMqttClient CreateManagedMqttClient(this MqttFactory factory, IMqttNetLogger logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return new ManagedMqttClient(factory.CreateMqttClient(logger), logger, serviceScopeFactory);
        }

        public static ManagedMqttClientOptionsBuilder CreateManagedMqttClientOptionsBuilder(this MqttFactory _)
        {
            return new ManagedMqttClientOptionsBuilder();
        }
    }
}