﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Azure.Core.Extensions
{
    internal static class ClientFactory
    {
        public static object CreateClient(Type clientType, Type optionsType, object options, IConfiguration configuration, TokenCredential credential)
        {
            List<object> arguments = new List<object>();
            foreach (var constructor in clientType.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
            {
                if (!IsApplicableConstructor(constructor, optionsType))
                {
                    continue;
                }

                arguments.Clear();

                bool match = true;
                foreach (var parameter in constructor.GetParameters())
                {
                    if (IsCredentialParameter(parameter))
                    {
                        arguments.Add(credential);
                        continue;
                    }

                    if (IsOptionsParameter(parameter, optionsType))
                    {
                        break;
                    }

                    if (!TryConvertArgument(configuration, parameter.Name, parameter.ParameterType, out object argument))
                    {
                        match = false;
                        break;
                    }

                    arguments.Add(argument);
                }

                if (!match)
                {
                    continue;
                }

                arguments.Add(options);

                return constructor.Invoke(arguments.ToArray());
            }

            throw new InvalidOperationException(BuildErrorMessage(clientType, optionsType));
        }

        internal static TokenCredential CreateCredential(IConfiguration configuration, IdentityClientOptions identityClientOptions = null)
        {
            var clientId = configuration["clientId"];
            var tenantId = configuration["tenantId"];
            var clientSecret = configuration["clientSecret"];
            var certificate = configuration["clientCertificate"];
            var certificateStoreName = configuration["clientCertificateStoreName"];
            var certificateStoreLocation = configuration["clientCertificateStoreLocation"];

            if (!string.IsNullOrWhiteSpace(tenantId) &&
                !string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(clientSecret))
            {
                return new ClientSecretCredential(tenantId, clientId, clientSecret, identityClientOptions);
            }

            if (!string.IsNullOrWhiteSpace(tenantId) &&
                !string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(certificate))
            {
                StoreLocation storeLocation = StoreLocation.CurrentUser;

                if (!string.IsNullOrWhiteSpace(certificateStoreLocation))
                {
                    storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), certificateStoreLocation, true);
                }

                if (string.IsNullOrWhiteSpace(certificateStoreName))
                {
                    certificateStoreName = "MY"; // MY is the default used in X509Store
                }

                using var store = new X509Store(certificateStoreName, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, certificate, false);

                if (certs.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to find a certificate with thumbprint '{certificate}'");
                }

                var credential = new ClientCertificateCredential(tenantId, clientId, certs[0], identityClientOptions);
                store.Close();

                return credential;
            }

            // TODO: More logging
            return null;
        }

        private static bool IsCredentialParameter(ParameterInfo parameter)
        {
            return parameter.ParameterType == typeof(TokenCredential);
        }

        private static bool IsOptionsParameter(ParameterInfo parameter, Type optionsType)
        {
            return parameter.ParameterType.IsAssignableFrom(optionsType) &&
                   parameter.Position == ((ConstructorInfo)parameter.Member).GetParameters().Length - 1;
        }

        private static string BuildErrorMessage(Type clientType, Type optionsType)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Unable to find matching constructor. Define one of the follow sets of configuration parameters:");

            int counter = 1;

            foreach (var constructor in clientType.GetConstructors())
            {
                if (!IsApplicableConstructor(constructor, optionsType))
                {
                    continue;
                }

                builder.Append(counter).Append(". ");

                bool first = true;

                foreach (var parameter in constructor.GetParameters())
                {
                    if (IsOptionsParameter(parameter, optionsType))
                    {
                        break;
                    }

                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.Append(", ");
                    }

                    builder.Append(parameter.Name);
                }

                builder.AppendLine();
                counter++;
            }

            return builder.ToString();
        }

        private static bool IsApplicableConstructor(ConstructorInfo constructorInfo, Type optionsType)
        {
            var parameters = constructorInfo.GetParameters();

            return constructorInfo.IsPublic &&
                   parameters.Length > 0 &&
                   IsOptionsParameter(parameters[parameters.Length - 1], optionsType);
        }

        private static bool TryConvertArgument(IConfiguration configuration, string parameterName, Type parameterType, out object value)
        {
            if (parameterType == typeof(string))
            {
                return TryConvertFromString(configuration, parameterName, s => s, out value);
            }

            if (parameterType == typeof(Uri))
            {
                return TryConvertFromString(configuration, parameterName, s => new Uri(s), out value);
            }

            if (configuration[parameterName] != null)
            {
                throw new InvalidOperationException($"Unable to convert value '{configuration[parameterName]}' to parameter type {parameterType.FullName}");
            }

            return TryCreateObject(parameterType, configuration.GetSection(parameterName), out value);
        }

        private static bool TryConvertFromString(IConfiguration configuration, string parameterName, Func<string, object> func, out object value)
        {
            string stringValue = configuration[parameterName];
            if (stringValue == null)
            {
                value = null;
                return false;
            }

            value = func(stringValue);
            return true;
        }

        internal static bool TryCreateObject(Type type, IConfigurationSection configuration, out object value)
        {
            if (!configuration.GetChildren().Any())
            {
                value = null;
                return false;
            }

            List<object> arguments = new List<object>();
            foreach (var constructor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
            {
                arguments.Clear();

                bool match = true;
                foreach (var parameter in constructor.GetParameters())
                {
                    if (!TryConvertArgument(configuration, parameter.Name, parameter.ParameterType, out object argument))
                    {
                        match = false;
                        break;
                    }

                    arguments.Add(argument);
                }

                if (!match)
                {
                    continue;
                }

                value = constructor.Invoke(arguments.ToArray());
                return true;
            }

            throw new InvalidOperationException($"Unable to convert section '{configuration.Path}' to parameter type '{type}', unable to find matching constructor.");
        }

    }
}