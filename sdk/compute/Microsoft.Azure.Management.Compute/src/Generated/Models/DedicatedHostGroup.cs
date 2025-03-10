// <auto-generated>
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.
//
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Microsoft.Azure.Management.Compute.Models
{
    using Microsoft.Rest;
    using Microsoft.Rest.Serialization;
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Specifies information about the dedicated host group that the dedicated
    /// hosts should be assigned to. &lt;br&gt;&lt;br&gt; Currently, a
    /// dedicated host can only be added to a dedicated host group at creation
    /// time. An existing dedicated host cannot be added to another dedicated
    /// host group.
    /// </summary>
    [Rest.Serialization.JsonTransformation]
    public partial class DedicatedHostGroup : Resource
    {
        /// <summary>
        /// Initializes a new instance of the DedicatedHostGroup class.
        /// </summary>
        public DedicatedHostGroup()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the DedicatedHostGroup class.
        /// </summary>
        /// <param name="location">Resource location</param>
        /// <param name="platformFaultDomainCount">Number of fault domains that
        /// the host group can span.</param>
        /// <param name="id">Resource Id</param>
        /// <param name="name">Resource name</param>
        /// <param name="type">Resource type</param>
        /// <param name="tags">Resource tags</param>
        /// <param name="hosts">A list of references to all dedicated hosts in
        /// the dedicated host group.</param>
        /// <param name="zones">Availability Zone to use for this host group �
        /// only single zone is supported. The zone can be assigned only during
        /// creation. If not provided, the group supports all zones in the
        /// region. If provided, enforces each host in the group to be in the
        /// same zone.</param>
        public DedicatedHostGroup(string location, int platformFaultDomainCount, string id = default(string), string name = default(string), string type = default(string), IDictionary<string, string> tags = default(IDictionary<string, string>), IList<SubResourceReadOnly> hosts = default(IList<SubResourceReadOnly>), IList<string> zones = default(IList<string>))
            : base(location, id, name, type, tags)
        {
            PlatformFaultDomainCount = platformFaultDomainCount;
            Hosts = hosts;
            Zones = zones;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets number of fault domains that the host group can span.
        /// </summary>
        [JsonProperty(PropertyName = "properties.platformFaultDomainCount")]
        public int PlatformFaultDomainCount { get; set; }

        /// <summary>
        /// Gets a list of references to all dedicated hosts in the dedicated
        /// host group.
        /// </summary>
        [JsonProperty(PropertyName = "properties.hosts")]
        public IList<SubResourceReadOnly> Hosts { get; private set; }

        /// <summary>
        /// Gets or sets availability Zone to use for this host group � only
        /// single zone is supported. The zone can be assigned only during
        /// creation. If not provided, the group supports all zones in the
        /// region. If provided, enforces each host in the group to be in the
        /// same zone.
        /// </summary>
        [JsonProperty(PropertyName = "zones")]
        public IList<string> Zones { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public override void Validate()
        {
            base.Validate();
            if (PlatformFaultDomainCount > 3)
            {
                throw new ValidationException(ValidationRules.InclusiveMaximum, "PlatformFaultDomainCount", 3);
            }
            if (PlatformFaultDomainCount < 1)
            {
                throw new ValidationException(ValidationRules.InclusiveMinimum, "PlatformFaultDomainCount", 1);
            }
        }
    }
}
