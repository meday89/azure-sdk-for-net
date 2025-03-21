﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace Azure.Core.Extensions
{
    // Slightly adjusted copy of https://github.com/aspnet/Extensions/blob/master/src/Options/Options/src/OptionsFactory.cs
    internal class ClientOptionsFactory<TClient, TOptions> where TOptions : class
    {
        private readonly IEnumerable<IConfigureOptions<TOptions>> _setups;
        private readonly IEnumerable<IPostConfigureOptions<TOptions>> _postConfigures;

        private readonly IEnumerable<ClientRegistration<TClient, TOptions>> _clientRegistrations;

        public ClientOptionsFactory(IEnumerable<IConfigureOptions<TOptions>> setups, IEnumerable<IPostConfigureOptions<TOptions>> postConfigures, IEnumerable<ClientRegistration<TClient, TOptions>> clientRegistrations)
        {
            _setups = setups;
            _postConfigures = postConfigures;
            _clientRegistrations = clientRegistrations;
        }

        private TOptions CreateOptions(string name)
        {
            object version = null;

            foreach (var clientRegistration in _clientRegistrations)
            {
                if (clientRegistration.Name == name)
                {
                    version = clientRegistration.Version;
                }
            }

            ConstructorInfo parameterlessConstructor = null;
            ParameterInfo versionConstructor = null;

            foreach (var constructor in typeof(TOptions).GetConstructors())
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    parameterlessConstructor = constructor;
                }

                if (parameters.Length == 1)
                {
                    versionConstructor = parameters[0];
                }
            }

            if (version != null)
            {
                if (versionConstructor != null)
                {
                    return (TOptions)Activator.CreateInstance(typeof(TOptions), version);
                }

                throw new InvalidOperationException("Unable to find constructor that takes service version");
            }

            if (parameterlessConstructor != null)
            {
                return Activator.CreateInstance<TOptions>();
            }

            return (TOptions)Activator.CreateInstance(typeof(TOptions), versionConstructor.DefaultValue);
        }

        /// <summary>
        /// Returns a configured <typeparamref name="TOptions"/> instance with the given <paramref name="name"/>.
        /// </summary>
        public TOptions Create(string name)
        {
            var options = CreateOptions(name);
            foreach (var setup in _setups)
            {
                if (setup is IConfigureNamedOptions<TOptions> namedSetup)
                {
                    namedSetup.Configure(name, options);
                }
                else if (name == Options.DefaultName)
                {
                    setup.Configure(options);
                }
            }
            foreach (var post in _postConfigures)
            {
                post.PostConfigure(name, options);
            }

            return options;
        }
    }
}