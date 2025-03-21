﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Azure.Messaging.EventHubs.Core;
using Microsoft.Azure.Amqp;

namespace Azure.Messaging.EventHubs.Amqp
{
    /// <summary>
    ///   A set of events with known size constraints, based on messages to be sent
    ///   using an AMQP-based transport.
    /// </summary>
    ///
    internal class AmqpEventBatch : TransportEventBatch
    {
        /// <summary>The amount of bytes to reserve as overhead for a small message.</summary>
        private const byte OverheadBytesSmallMessage = 5;

        /// <summary>The amount of bytes to reserve as overhead for a large message.</summary>
        private const byte OverheadBytesLargeMessage = 8;

        /// <summary>The maximum number of bytes that a message may be to be considered small.</summary>
        private const byte MaximumBytesSmallMessage = 255;

        /// <summary>A flag that indicates whether or not the instance has been disposed.</summary>
        private bool _disposed = false;

        /// <summary>The size of the batch, in bytes, as it will be sent via the AMQP transport.</summary>
        private long _sizeBytes = 0;

        /// <summary>
        ///   The maximum size allowed for the batch, in bytes.  This includes the events in the batch as
        ///   well as any overhead for the batch itself when sent to the Event Hubs service.
        /// </summary>
        ///
        public override long MaximumSizeInBytes { get; }

        /// <summary>
        ///   The size of the batch, in bytes, as it will be sent to the Event Hubs
        ///   service.
        /// </summary>
        ///
        public override long SizeInBytes => _sizeBytes;

        /// <summary>
        ///   The count of events contained in the batch.
        /// </summary>
        ///
        public override int Count => BatchMessages.Count;

        /// <summary>
        ///   The converter to use for translating <see cref="EventData" /> into the corresponding AMQP message.
        /// </summary>
        ///
        private AmqpMessageConverter MessageConverter { get; }

        /// <summary>
        ///   The set of options to apply to the batch.
        /// </summary>
        ///
        private BatchOptions Options { get; }

        /// <summary>
        ///   The set of messages that have been added to the batch.
        /// </summary>
        ///
        private List<AmqpMessage> BatchMessages { get; } = new List<AmqpMessage>();

        /// <summary>
        ///   Initializes a new instance of the <see cref="AmqpEventBatch"/> class.
        /// </summary>
        ///
        /// <param name="messageConverter">The converter to use for translating <see cref="EventData" /> into the corresponding AMQP message.</param>
        /// <param name="options">The set of options to apply to the batch.</param>
        ///
        public AmqpEventBatch(AmqpMessageConverter messageConverter,
                              BatchOptions options)
        {
            Guard.ArgumentNotNull(nameof(messageConverter), messageConverter);
            Guard.ArgumentNotNull(nameof(options), options);
            Guard.ArgumentNotNull(nameof(options.MaximumizeInBytes), options.MaximumizeInBytes);

            MessageConverter = messageConverter;
            Options = options;
            MaximumSizeInBytes = options.MaximumizeInBytes.Value;

            // Initialize the size by reserving space for the batch envelope.

            using var envelope = messageConverter.CreateBatchFromEvents(Enumerable.Empty<EventData>(), options.PartitionKey);
            _sizeBytes = envelope.SerializedMessageSize;
        }

        /// <summary>
        ///   Attempts to add an event to the batch, ensuring that the size
        ///   of the batch does not exceed its maximum.
        /// </summary>
        ///
        /// <param name="eventData">The event to attempt to add to the batch.</param>
        ///
        /// <returns><c>true</c> if the event was added; otherwise, <c>false</c>.</returns>
        ///
        public override bool TryAdd(EventData eventData)
        {
            Guard.ArgumentNotNull(nameof(eventData), eventData);
            GuardDisposed();

            var eventMessage = MessageConverter.CreateMessageFromEvent(eventData, Options.PartitionKey);

            try
            {
                // Calculate the size for the event, based on the AMQP message size and accounting for a
                // bit of reserved overhead size.

                var size = _sizeBytes
                    + eventMessage.SerializedMessageSize
                    + (eventMessage.SerializedMessageSize <= MaximumBytesSmallMessage
                        ? OverheadBytesSmallMessage
                        : OverheadBytesLargeMessage);

                if (size > MaximumSizeInBytes)
                {
                    eventMessage.Dispose();
                    return false;
                }

                _sizeBytes = size;
                BatchMessages.Add(eventMessage);
                return true;
            }
            catch
            {
                eventMessage?.Dispose();
                throw;
            }
        }

        /// <summary>
        ///   Represents the batch as an enumerable set of transport-specific
        ///   representations of an event.
        /// </summary>
        ///
        /// <typeparam name="T">The transport-specific event representation being requested.</typeparam>
        ///
        /// <returns>The set of events as an enumerable of the requested type.</returns>
        ///
        public override IEnumerable<T> AsEnumerable<T>()
        {
            if (typeof(T) != typeof(AmqpMessage))
            {
                throw new FormatException(String.Format(CultureInfo.CurrentCulture, Resources.UnsupportedTransportEventType, typeof(T).Name));
            }

            return (IEnumerable<T>)BatchMessages;
        }

        /// <summary>
        ///   Performs the task needed to clean up resources used by the <see cref="AmqpEventBatch" />.
        /// </summary>
        ///
        public override void Dispose()
        {
            _disposed = true;

            foreach (var message in BatchMessages)
            {
                message.Dispose();
            }

            BatchMessages.Clear();
            _sizeBytes = 0;
        }

        /// <summary>
        ///   Ensures that the batch has not already been disposed, throwing an exception
        ///   if it has.
        /// </summary>
        ///
        private void GuardDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AmqpEventBatch));
            }
        }
    }
}
