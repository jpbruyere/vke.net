// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using Vulkan;
using static Vulkan.Vk;

namespace vke {
	public static class ExtensionMethods {
		/// <summary>
		/// Extensions method to check byte array equality.
		/// </summary>
		public static bool AreEquals (this byte[] b, byte[] other) {
			if (b.Length != other.Length)
				return false;
			for (int i = 0; i < b.Length; i++) {
				if (b[i] != other[i])
					return false;
			}
			return true;
		}

		#region DebugMarkers
		public static void SetDebugMarkerName (this VkCommandBuffer obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.CommandBuffer,
				(ulong)obj.Handle.ToInt64 ()) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkImageView obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.ImageView,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkSampler obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Sampler,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkPipeline obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Pipeline,
				obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkDescriptorSet obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.DescriptorSet,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkSemaphore obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Semaphore,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkFence obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Fence,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.Handle, ref dmo));
			name.Unpin ();
		}
		#endregion

		#region shaderc


		/*public static ShaderInfo CreateShaderInfo (this shaderc.Compiler comp, Device dev, string shaderPath, shaderc.ShaderKind shaderKind,
			SpecializationInfo specializationInfo = null, string entryPoint = "main") {

			using (shaderc.Result res = comp.Compile (shaderPath, shaderKind)) {
				if (res.Status != shaderc.Status.Success)
					throw new Exception ($"Shaderc compilation failure: {res.ErrorMessage}");
				VkShaderStageFlags stageFlags = Utils.ShaderKindToStageFlag (shaderKind);
				return new ShaderInfo (dev, stageFlags, res.CodePointer, (UIntPtr)res.CodeLength, specializationInfo, entryPoint);
			}
		}*/
		#endregion

		#region temp
		public static void Dump (this Memory<byte> mem) {
			Span<byte> s = mem.Span;
			for (int i = 0; i < s.Length; i++)
				Console.Write (s[i].ToString("X2") + (i % 32 == 0 ? "\n" : " "));
		}
		#endregion

	}
}
