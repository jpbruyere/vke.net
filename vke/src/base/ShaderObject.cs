// Copyright (c) 2023  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;
using System.IO;
using System.Runtime.InteropServices;

namespace vke {
	/// <summary>
	/// Collection of FrameBuffers, useful to handle multiple framebuffers for a swapchain.
	/// </summary>
	public class ShaderObjects : Collection<ShaderObject>, IDisposable
	{
		//public Framebuffer this[int index] => Items[index];

		public void Dispose()
		{
			foreach (ShaderObject fb in Items)
				fb.Dispose();
			ClearItems();
		}
	}	
	public sealed class ShaderObject : Activable {
        internal VkShaderEXT handle;
		public VkShaderEXT Handle => handle;
		public string FileName;
		public VkShaderStageFlags Stage;
		public VkShaderStageFlags NextStage;
		public VkShaderCreateFlagsEXT Flags = VkShaderCreateFlagsEXT.LinkStageEXT;

		public List<DescriptorSetLayout> DescriptorSetLayouts = new List<DescriptorSetLayout> ();
		public List<VkPushConstantRange> PushConstantRanges = new List<VkPushConstantRange> ();

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.ShaderEXT, handle.Handle);

		#region CTORS
		public ShaderObject (Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage) : base (device) {
			Stage = stage;
			NextStage = nextStage;
		}
		public ShaderObject (Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, VkPushConstantRange pushConstantRange, params DescriptorSetLayout[] descriptorSetLayouts)
		: this (device, stage, nextStage, descriptorSetLayouts) {
			PushConstantRanges.Add (pushConstantRange);
		}
		public ShaderObject (Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, VkPushConstantRange[] pushConstantRanges, params DescriptorSetLayout[] descriptorSetLayouts)
		: this (device, stage, nextStage, descriptorSetLayouts) {
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add (pcr);
		}
		public ShaderObject (Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, params DescriptorSetLayout[] descriptorSetLayouts)
			:this (device, stage, nextStage) {

			if (descriptorSetLayouts.Length > 0)
				DescriptorSetLayouts.AddRange (descriptorSetLayouts);
        }
		#endregion

		public void AddPushConstants (params VkPushConstantRange[] pushConstantRanges) {
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add (pcr);
		}

		uint[] getCode(out UIntPtr shaderSize) {
			using (Stream stream = Helpers.GetStreamFromPath (FileName)) {
				using (BinaryReader br = new BinaryReader (stream)) {
					byte[] shaderCode = br.ReadBytes ((int)stream.Length);
					shaderSize = (UIntPtr)shaderCode.Length;
					ReadOnlySpan<uint> tmp = MemoryMarshal.Cast<byte, uint> (shaderCode);
					return tmp.ToArray();
				}
			}
		}

		public override void Activate () {
			if (state != ActivableState.Activated) {
				foreach (DescriptorSetLayout dsl in DescriptorSetLayouts)
					dsl.Activate ();

				
				VkShaderCreateInfoEXT info = default;
				info.flags = VkShaderCreateFlagsEXT.LinkStageEXT;
				info.stage = VkShaderStageFlags.Vertex;
				info.stage = VkShaderStageFlags.Fragment;
				info.codeType = VkShaderCodeTypeEXT.SpirvEXT;

				uint[] code = getCode(out UIntPtr codeSize);
				info.codeSize = codeSize;
				info.pCode = code.PinPointer();

				VkDescriptorSetLayout[] dsls = DescriptorSetLayouts.Select (dsl => dsl.handle).ToArray ();

				if (dsls.Length > 0) {
					info.pSetLayouts = dsls;
				}
				if (PushConstantRanges.Count > 0) {
					info.pPushConstantRanges = PushConstantRanges;
				}
				CheckResult (vkCreateShadersEXT (Dev.Handle, 1, ref info, IntPtr.Zero, out handle));

				code.Unpin();
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
					System.Diagnostics.Debug.WriteLine ("VKE Activable ShaderObject disposed by finalizer");

				vkDestroyShaderEXT (Dev.Handle, handle, IntPtr.Zero);
			}else if (disposing)
				System.Diagnostics.Debug.WriteLine ("Calling dispose on unactive ShaderObject");

			base.Dispose (disposing);
		}
		#endregion
	}
}
