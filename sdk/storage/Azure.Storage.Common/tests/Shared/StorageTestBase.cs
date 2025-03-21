﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Testing;
using Azure.Identity;
using Azure.Storage.Common;
using NUnit.Framework;

namespace Azure.Storage.Test.Shared
{
    public abstract class StorageTestBase : RecordedTestBase
    {
        public StorageTestBase(bool async, RecordedTestMode? mode = null)
            : base(async, mode ?? GetModeFromEnvironment())
        {
            this.Sanitizer = new StorageRecordedTestSanitizer();
            this.Matcher = new StorageRecordMatcher(this.Sanitizer);
        }

        /// <summary>
        /// Gets the tenant to use by default for our tests.
        /// </summary>
        public TenantConfiguration TestConfigDefault => this.GetTestConfig(
                "Storage_TestConfigDefault",
                () => TestConfigurations.DefaultTargetTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require Read Access
        /// Geo-Redundant Storage to be setup.
        /// </summary>
        public TenantConfiguration TestConfigSecondary => this.GetTestConfig(
                "Storage_TestConfigSecondary",
                () => TestConfigurations.DefaultSecondaryTargetTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require Premium SSDs.
        /// </summary>
        public TenantConfiguration TestConfigPremiumBlob => this.GetTestConfig(
                "Storage_TestConfigPremiumBlob",
                () => TestConfigurations.DefaultTargetPremiumBlobTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require preview features.
        /// </summary>
        public TenantConfiguration TestConfigPreviewBlob => this.GetTestConfig(
                "Storage_TestConfigPreviewBlob",
                () => TestConfigurations.DefaultTargetPreviewBlobTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require authentication
        /// with Azure AD.
        /// </summary>
        public TenantConfiguration TestConfigOAuth => this.GetTestConfig(
                "Storage_TestConfigOAuth",
                () => TestConfigurations.DefaultTargetOAuthTenant);

        /// <summary>
        /// Gets a cache used for storing serialized tenant configurations.  Do
        /// not get values from this directly; use GetTestConfig.
        /// </summary>
        private readonly Dictionary<string, string> _recordingConfigCache =
            new Dictionary<string, string>();

        /// <summary>
        /// Gets a cache used for storing deserialized tenant configurations.
        /// Do not get values from this directly; use GetTestConfig.
        private readonly Dictionary<string, TenantConfiguration> _playbackConfigCache =
            new Dictionary<string, TenantConfiguration>();

        /// <summary>
        /// We need to clear the playback cache before every test because
        /// different recordings might have used different tenant
        /// configurations.
        /// </summary>
        [SetUp]
        public virtual void ClearCaches() =>
            this._playbackConfigCache.Clear();

        /// <summary>
        /// Get or create a test configuration tenant to use with our tests.
        ///
        /// If we're recording, we'll save a sanitized version of the test
        /// configuarion.  If we're playing recorded tests, we'll use the
        /// serialized test configuration.  If we're running the tests live,
        /// we'll just return the value.
        ///
        /// While we cache things internally, DO NOT cache them elsewhere
        /// because we need each test to have its configuration recorded.
        /// </summary>
        /// <param name="name">The name of the session record variable.</param>
        /// <param name="getTenant">
        /// A function to get the tenant.  This is wrapped in a Func becuase
        /// we'll throw Assert.Inconclusive if you try to access a tenant with
        /// an invalid config file.
        /// </param>
        /// <returns>A test tenant to use with our tests.</returns>
        private TenantConfiguration GetTestConfig(string name, Func<TenantConfiguration> getTenant)
        {
            TenantConfiguration config = null;
            string text = null;
            switch (this.Mode)
            {
                case RecordedTestMode.Playback:
                    if (!this._playbackConfigCache.TryGetValue(name, out config))
                    {
                        text = this.Recording.GetVariable(name, null);
                        config = TenantConfiguration.Parse(text);
                        this._playbackConfigCache[name] = config;
                    }
                    break;
                case RecordedTestMode.Record:
                    config = getTenant();
                    if (!this._recordingConfigCache.TryGetValue(name, out text))
                    {
                        text = TenantConfiguration.Serialize(config, true);
                        this._recordingConfigCache[name] = text;
                    }
                    this.Recording.GetVariable(name, text);
                    break;
                case RecordedTestMode.Live:
                default:
                    config = getTenant();
                    break;
            }
            return config;
        }

        public DateTimeOffset GetUtcNow() => this.Recording.UtcNow;

        public byte[] GetRandomBuffer(long size)
            => TestHelper.GetRandomBuffer(size, this.Recording.Random);

        public string GetNewString(int length = 20)
        {
            var buffer = new char[length];
            for (var i = 0; i < length; i++)
            {
                buffer[i] = (char)('a' + this.Recording.Random.Next(0, 25));
            }
            return new string(buffer);
        }

        public string GetNewMetadataName() => $"test_metadata_{this.Recording.Random.NewGuid().ToString().Replace("-", "_")}";

        public IDictionary<string, string> BuildMetadata()
            =>  new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "foo", "bar" },
                    { "meta", "data" }
                };

        public IPAddress GetIPAddress()
        {
            var a = this.Recording.Random.Next(0, 256);
            var b = this.Recording.Random.Next(0, 256);
            var c = this.Recording.Random.Next(0, 256);
            var d = this.Recording.Random.Next(0, 256);
            var ipString = $"{a}.{b}.{c}.{d}";
            return IPAddress.Parse(ipString);
        }

        public TokenCredential GetOAuthCredential() =>
            this.GetOAuthCredential(this.TestConfigOAuth);

        public TokenCredential GetOAuthCredential(TenantConfiguration config) =>
            this.GetOAuthCredential(
                config.ActiveDirectoryTenantId,
                config.ActiveDirectoryApplicationId,
                config.ActiveDirectoryApplicationSecret,
                new Uri(config.ActiveDirectoryAuthEndpoint));

        public TokenCredential GetOAuthCredential(string tenantId, string appId, string secret, Uri authorityHost) =>
            new ClientSecretCredential(
                tenantId,
                appId,
                secret,
                this.Recording.InstrumentClientOptions(
                    new IdentityClientOptions() { AuthorityHost = authorityHost }));

        public void AssertMetadataEquality(IDictionary<string, string> expected, IDictionary<string, string> actual)
        {
            Assert.IsNotNull(expected, "Expected metadata is null");
            Assert.IsNotNull(actual, "Actual metadata is null");

            Assert.AreEqual(expected.Count, actual.Count, "Metadata counts are not equal");

            foreach (var kvp in expected)
            {
                if (!actual.TryGetValue(kvp.Key, out var value) ||
                    String.Compare(kvp.Value, value, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    Assert.Fail($"Expected key <{kvp.Key}> with value <{kvp.Value}> not found");
                }
            }
        }

        /// <summary>
        /// To prevent test flakiness, we simply warn when certain timing sensitive
        /// tests don't appear to work as expected.  However, we will ask you to run
        /// it again if you're recording a test because it should work correctly at
        /// least then.
        /// </summary>
        public void WarnCopyCompletedTooQuickly()
        {
            if (this.Mode == RecordedTestMode.Record)
            {
                Assert.Fail("Copy may have completed too quickly to abort.  Please record again.");
            }
            else
            {
                Assert.Inconclusive("Copy may have completed too quickly to abort.");
            }
        }

        /// <summary>
        /// A number of our tests have built in delays while we wait an expected
        /// amount of time for a service operation to complete and this method
        /// allows us to wait (unless we're playing back recordings, which can
        /// complete immediately).
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to wait.</param>
        /// <param name="playbackDelayMilliseconds">
        /// An optional number of milliseconds to wait if we're playing back a
        /// recorded test.  This is useful for allowing client side events to
        /// get processed.
        /// </param>
        /// <returns>A task that will (optionally) delay.</returns>
        public async Task Delay(int milliseconds = 1000, int? playbackDelayMilliseconds = null)
        {
            if (this.Mode != RecordedTestMode.Playback)
            {
                await Task.Delay(milliseconds);
            }
            else if (playbackDelayMilliseconds != null)
            {
                await Task.Delay(playbackDelayMilliseconds.Value);
            }
        }

        /// <summary>
        /// Wait for the progress notifications to complete.
        /// </summary>
        /// <param name="progressList">
        /// The list of progress notifications being updated by the Progress handler.
        /// </param>
        /// <param name="totalSize">The total size we should eventually see.</param>
        /// <returns>A task that will (optionally) delay.</returns>
        protected async Task WaitForProgressAsync(List<StorageProgress> progressList, long totalSize)
        {
            for (var attempts = 0; attempts < 10; attempts++)
            {
                if (progressList.LastOrDefault()?.BytesTransferred >= totalSize)
                {
                    return;
                }

                // Wait for lingering progress events
                await this.Delay(500, 100).ConfigureAwait(false);
            }

            Assert.Fail("Progress notifications never completed!");
        }
    }
}
