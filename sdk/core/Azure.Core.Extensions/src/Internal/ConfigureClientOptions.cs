﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Options;

namespace Azure.Core.Extensions
{
    internal class ConfigureClientOptions<TClient, TOptions> : IConfigureNamedOptions<TOptions> where TOptions : class
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ClientRegistration<TClient, TOptions> _registration;
        private readonly Action<TOptions, IServiceProvider> _configureOptions;

        public ConfigureClientOptions(IServiceProvider serviceProvider, ClientRegistration<TClient, TOptions> registration, Action<TOptions, IServiceProvider> configureOptions)
        {
            _serviceProvider = serviceProvider;
            _registration = registration;
            _configureOptions = configureOptions;
        }

        public void Configure(TOptions options)
        {
        }

        public void Configure(string name, TOptions options)
        {
            if (name == _registration.Name)
            {
                _configureOptions(options, _serviceProvider);
            }
        }
    }
}