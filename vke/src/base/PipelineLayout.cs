// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	public sealed class PipelineLayout : Activable {
        internal VkPipelineLayout handle;
		public VkPipelineLayout Handle => handle;

		public List<DescriptorSetLayout> DescriptorSetLayouts = new List<DescriptorSetLayout> ();
		public List<VkPushConstantRange> PushConstantRanges = new List<VkPushConstantRange> ();

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.PipelineLayout, handle.Handle);

		#region CTORS
		public PipelineLayout (Device device) : base (device) {	}
		public PipelineLayout (Device device, VkPushConstantRange pushConstantRange, params DescriptorSetLayout[] descriptorSetLayouts)
		: this (device, descriptorSetLayouts) {
			PushConstantRanges.Add (pushConstantRange);
		}
		public PipelineLayout (Device device, VkPushConstantRange[] pushConstantRanges, params DescriptorSetLayout[] descriptorSetLayouts)
		: this (device, descriptorSetLayouts) {
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add (pcr);
		}
		public PipelineLayout (Device device, params DescriptorSetLayout[] descriptorSetLayouts)
			:this (device) {

			if (descriptorSetLayouts.Length > 0)
				DescriptorSetLayouts.AddRange (descriptorSetLayouts);
        }
		#endregion

		public void AddPushConstants (params VkPushConstantRange[] pushConstantRanges) {
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add (pcr);
		}

		public override void Activate () {
			if (state != ActivableState.Activated) {
				foreach (DescriptorSetLayout dsl in DescriptorSetLayouts)
					dsl.Activate ();
				VkPipelineLayoutCreateInfo info = default;
				VkDescriptorSetLayout[] dsls = DescriptorSetLayouts.Select (dsl => dsl.handle).ToArray ();

				if (dsls.Length > 0) {
					info.pSetLayouts = dsls;
				}
				if (PushConstantRanges.Count > 0) {
					info.pPushConstantRanges = PushConstantRanges;
				}
				CheckResult (vkCreatePipelineLayout (Dev.Handle, ref info, IntPtr.Zero, out handle));

				info.Dispose();

			}
			base.Activate ();
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (disposing) {
					foreach (DescriptorSetLayout dsl in DescriptorSetLayouts)
						dsl.Dispose ();
				} else
					System.Diagnostics.Debug.WriteLine ("VKE Activable PipelineLayout disposed by finalizer");

				vkDestroyPipelineLayout (Dev.Handle, handle, IntPtr.Zero);
			}else if (disposing)
				System.Diagnostics.Debug.WriteLine ("Calling dispose on unactive PipelineLayout");

			base.Dispose (disposing);
		}
		#endregion
	}
}
