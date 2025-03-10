﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Messaging.EventHubs.Metadata
{
    /// <summary>
    ///   The set of properties that comprise a connection string from the
    ///   Azure portal.
    /// </summary>
    ///
    internal struct ConnectionStringProperties
    {
        /// <summary>
        ///   The endpoint to be used for connecting to the Event Hubs namespace.
        /// </summary>
        ///
        /// <value>The endpoint address, including protocol, from the connection string.</value>
        ///
        public Uri Endpoint { get; }

        /// <summary>
        ///   The path to the specific Event Hub under the namespace.
        /// </summary>
        ///
        public string EventHubPath { get; }

        /// <summary>
        ///   The name of the shared access key, either for the Event Hubs namespace
        ///   or the Event Hub.
        /// </summary>
        ///
        public string SharedAccessKeyName { get; }

        /// <summary>
        ///   The value of the shared access key, either for the Event Hubs namespace
        ///   or the Event Hub.
        /// </summary>
        ///
        public string SharedAccessKey { get; }

        /// <summary>
        ///   Initializes a new instance of the <see cref="ConnectionStringProperties"/> struct.
        /// </summary>
        ///
        /// <param name="endpoint">The endpoint of the Event Hubs namespace.</param>
        /// <param name="eventHubPath">The path to the specific Event Hub under the namespace.</param>
        /// <param name="sharedAccessKeyName">The name of the shared access key, to use authorization.</param>
        /// <param name="sharedAccessKey">The shared access key to use for authorization.</param>
        ///
        public ConnectionStringProperties(Uri endpoint,
                                          string eventHubPath,
                                          string sharedAccessKeyName,
                                          string sharedAccessKey)
        {
            Endpoint = endpoint;
            EventHubPath = eventHubPath;
            SharedAccessKeyName = sharedAccessKeyName;
            SharedAccessKey = sharedAccessKey;
        }
    }
}
