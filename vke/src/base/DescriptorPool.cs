// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	[Serializable]
    public sealed class DescriptorPool : Activable {
        internal VkDescriptorPool handle;
		public uint MaxSets;
		public List<VkDescriptorPoolSize> PoolSizes { get; private set; } = new List<VkDescriptorPoolSize> ();

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.DescriptorPool, handle.Handle);

		#region CTORS
		DescriptorPool () : base (null) {}
		/// <summary>
		/// Create a new managed descriptor pool that will be manualy activated after the pool sizes had been populated.
		/// </summary>
		/// <param name="device">the logical device that create the pool.</param>
		/// <param name="maxSets">maximum number of descriptor sets that can be allocated from the pool</param>
		public DescriptorPool (Device device, uint maxSets = 1) : base (device) {
            MaxSets = maxSets;
        }
		/// <summary>
		/// Create and automatically activate a new Descriptor pool with the supplied pool sizes.
		/// </summary>
		/// <param name="device">the logical device that create the pool.</param>
		/// <param name="maxSets">maximum number of descriptor sets that can be allocated from the pool</param>
		/// <param name="poolSizes">an array of pool sizes describing descriptor types and counts</param>
        public DescriptorPool (Device device, uint maxSets = 1, params VkDescriptorPoolSize[] poolSizes)
            : this (device, maxSets) {

			PoolSizes.AddRange (poolSizes);

            Activate ();
        }
		#endregion

		public sealed override void Activate () {
			if (state != ActivableState.Activated) {
				VkDescriptorPoolCreateInfo info = default;
				info.pPoolSizes = PoolSizes;
				info.maxSets = MaxSets;

				CheckResult (vkCreateDescriptorPool (Dev.Handle, ref info, IntPtr.Zero, out handle));
				info.Dispose();
			}
			base.Activate ();
		}
		/// <summary>
		/// Allocate a new DescriptorSet from this pool.
		/// </summary>
		/// <returns>A managed descriptor set.</returns>
		/// <param name="layouts">a variable sized array of descriptor layout(s) to allocate the descriptor for.</param>
		public DescriptorSet Allocate (params DescriptorSetLayout[] layouts) {
            DescriptorSet ds = new DescriptorSet (this, layouts);
            Allocate (ds);
            return ds;
        }
        public void Allocate (DescriptorSet descriptorSet) {
            VkDescriptorSetAllocateInfo allocInfo = default;
            allocInfo.descriptorPool = handle;
            allocInfo.pSetLayouts = descriptorSet.descriptorSetLayouts;

            CheckResult (vkAllocateDescriptorSets (Dev.Handle, ref allocInfo, out descriptorSet.handle));

			allocInfo.Dispose();
        }
        public void FreeDescriptorSet (params DescriptorSet[] descriptorSets) {
            if (descriptorSets.Length == 1) {
                CheckResult (vkFreeDescriptorSets (Dev.Handle, handle, 1, ref descriptorSets[0].handle));
                return;
            }
            CheckResult (vkFreeDescriptorSets (Dev.Handle, handle, (uint)descriptorSets.Length, descriptorSets.Pin()));
			descriptorSets.Unpin ();
        }
        public void Reset () {
            CheckResult (vkResetDescriptorPool (Dev.Handle, handle, 0));
        }

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (!disposing)
				System.Diagnostics.Debug.WriteLine ($"CVKL DescriptorPool '{name}' disposed by finalizer");
			if (state == ActivableState.Activated)
				vkDestroyDescriptorPool (Dev.Handle, handle, IntPtr.Zero);
			base.Dispose (disposing);
		}
		#endregion
	}
}
