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

namespace vke
{
	/// <summary>
	/// Collection of FrameBuffers, useful to handle multiple framebuffers for a swapchain.
	/// </summary>
	public class LinkedShaderObjects : Activable
	{
		List<ShaderObject> shaders;
		internal IEnumerable<VkShaderEXT> shaderHandles = null;
		internal IEnumerable<uint> shaderStages = null;

		public IEnumerable<VkShaderEXT> ShaderHandles => shaderHandles;
		public IEnumerable<uint> Stages => shaderStages;
		public uint Count => (uint)shaderHandles?.Count();
		//public IEnumerable<DescriptorSetLayout> Layouts => shaders?.Count > 0 ? shaders[0].DescriptorSetLayouts.ToArray() : null;

		#region CTORS
		public LinkedShaderObjects(params ShaderObject[] shaderObjects) : base(shaderObjects[0].Dev)
		{
			shaders = new List<ShaderObject>(shaderObjects);
		}
		#endregion		
		
		public override void Activate()
		{
			if (state != ActivableState.Activated)
			{
				List<VkShaderCreateInfoEXT> infos = new List<VkShaderCreateInfoEXT>();
				foreach (ShaderObject lso in shaders)
				{
					VkShaderCreateInfoEXT info = lso.getCreateInfo();
					info.flags |= VkShaderCreateFlagsEXT.LinkStageEXT;
					infos.Add(info);
				}

				//shaderHandles = new VkShaderEXT[shaders.Count];

				IntPtr handles = Marshal.AllocHGlobal (Marshal.SizeOf<VkShaderEXT> () * (int)shaders.Count);

				//CheckResult(vkCreateShadersEXT(Dev.Handle, (uint)infos.Count, infos.PinPointer(), IntPtr.Zero, shaderHandles.Pin()));
				VkResult res = vkCreateShadersEXT(Dev.Handle, (uint)infos.Count, infos.PinPointer(), IntPtr.Zero, handles);
				//shaderHandles.Unpin();

				VkShaderEXT[] hnds = new VkShaderEXT [shaders.Count];

				for (int i = 0; i < shaders.Count; i++)
					hnds [i] = (ulong) Marshal.ReadIntPtr (handles + i * Marshal.SizeOf<IntPtr> ());

				Marshal.FreeHGlobal (handles);


				shaderHandles = hnds;
				shaderStages = infos.Select(i => (uint)i.stage).ToArray();

				foreach (VkShaderCreateInfoEXT lsoci in infos)
				{
					lsoci.pCode.Unpin();
					lsoci.Dispose();
				}
			}

			base.Activate();
		}
        protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo => new VkDebugUtilsObjectNameInfoEXT (VkObjectType.ShaderEXT, (ulong)shaderHandles?.FirstOrDefault().Handle);

		#region IDisposable Support
		protected override void Dispose(bool disposing)
		{
			if (state == ActivableState.Activated)
			{
				if (disposing)
				{
					foreach (ShaderObject lso in shaders)
					{
						foreach (DescriptorSetLayout dsl in lso.DescriptorSetLayouts)
							dsl.Dispose();
					}
				}
				else
					System.Diagnostics.Debug.WriteLine("VKE Activable ShaderObject disposed by finalizer");

				foreach (VkShaderEXT hnd in shaderHandles)
					vkDestroyShaderEXT(Dev.Handle, hnd, IntPtr.Zero);

				shaderHandles = null;
				shaderStages = null;
			}
			else if (disposing)
				System.Diagnostics.Debug.WriteLine("Calling dispose on unactive ShaderObject");

			base.Dispose(disposing);
		}
		#endregion		
	}	

	public sealed class ShaderObject : Activable
	{
		internal VkShaderEXT handle;
		public VkShaderEXT Handle => handle;
		public string FileName;
		public string EntryPoint = "main";
		public VkShaderStageFlags Stage;
		public VkShaderStageFlags NextStage;
		public VkShaderCreateFlagsEXT Flags = VkShaderCreateFlagsEXT.LinkStageEXT;

		public List<DescriptorSetLayout> DescriptorSetLayouts = new List<DescriptorSetLayout>();
		public List<VkPushConstantRange> PushConstantRanges = new List<VkPushConstantRange>();

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT(VkObjectType.ShaderEXT, handle.Handle);

		#region CTORS
		public ShaderObject(Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage = 0) : base(device)
		{
			Stage = stage;
			NextStage = nextStage;
		}
		public ShaderObject(Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, VkPushConstantRange pushConstantRange, params DescriptorSetLayout[] descriptorSetLayouts)
		: this(device, stage, nextStage, descriptorSetLayouts)
		{
			PushConstantRanges.Add(pushConstantRange);
		}
		public ShaderObject(Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, VkPushConstantRange[] pushConstantRanges, params DescriptorSetLayout[] descriptorSetLayouts)
		: this(device, stage, nextStage, descriptorSetLayouts)
		{
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add(pcr);
		}
		public ShaderObject(Device device, VkShaderStageFlags stage, VkShaderStageFlags nextStage, params DescriptorSetLayout[] descriptorSetLayouts)
			: this(device, stage, nextStage)
		{

			if (descriptorSetLayouts.Length > 0)
				DescriptorSetLayouts.AddRange(descriptorSetLayouts);
		}
		#endregion

		public void AddPushConstants(params VkPushConstantRange[] pushConstantRanges)
		{
			foreach (VkPushConstantRange pcr in pushConstantRanges)
				PushConstantRanges.Add(pcr);
		}

		uint[] getCode(out UIntPtr shaderSize)
		{
			using (Stream stream = Helpers.GetStreamFromPath(FileName))
			{
				using (BinaryReader br = new BinaryReader(stream))
				{
					byte[] shaderCode = br.ReadBytes((int)stream.Length);
					shaderSize = (UIntPtr)shaderCode.Length;
					ReadOnlySpan<uint> tmp = MemoryMarshal.Cast<byte, uint>(shaderCode);
					return tmp.ToArray();
				}
			}
		}

		internal VkShaderCreateInfoEXT getCreateInfo()
		{
			foreach (DescriptorSetLayout dsl in DescriptorSetLayouts)
				dsl.Activate();

			VkShaderCreateInfoEXT info = VkShaderCreateInfoEXT.New;
			info.flags = Flags;
			info.stage = Stage;
			info.nextStage = NextStage;
			info.codeType = VkShaderCodeTypeEXT.SpirvEXT;
			info.pName = EntryPoint;

			uint[] code = getCode(out UIntPtr codeSize);
			info.codeSize = codeSize;
			info.pCode = code.PinPointer();

			VkDescriptorSetLayout[] dsls = DescriptorSetLayouts.Select(dsl => dsl.handle).ToArray();

			if (dsls.Length > 0)
			{
				info.pSetLayouts = dsls;
			}
			if (PushConstantRanges.Count > 0)
			{
				info.pPushConstantRanges = PushConstantRanges;
			}
			return info;
		}

		public override void Activate()
		{
			if (state != ActivableState.Activated)
			{
				VkShaderCreateInfoEXT info = getCreateInfo();
				CheckResult(vkCreateShadersEXT(Dev.Handle, 1, ref info, IntPtr.Zero, out handle));
				info.pCode.Unpin();
				info.Dispose();

			}
			base.Activate();
		}

		public override string ToString()
		{
			return string.Format($"{base.ToString()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose(bool disposing)
		{
			if (state == ActivableState.Activated)
			{
				if (disposing)
				{
					foreach (DescriptorSetLayout dsl in DescriptorSetLayouts)
						dsl.Dispose();
				}
				else
					System.Diagnostics.Debug.WriteLine("VKE Activable ShaderObject disposed by finalizer");

				vkDestroyShaderEXT(Dev.Handle, handle, IntPtr.Zero);
			}
			else if (disposing)
				System.Diagnostics.Debug.WriteLine("Calling dispose on unactive ShaderObject");

			base.Dispose(disposing);
		}
		#endregion
	}
}
