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

		#region pinning
		/// <summary>
		/// list of pinned GCHandles used to pass value from managed to unmanaged code.
		/// </summary>
		public static Dictionary<object, GCHandle> handles = new Dictionary<object, GCHandle>();

		/// <summary>
		/// Unpin the specified object and free the GCHandle associated.
		/// </summary>
		public static void Unpin (this object obj) {
			if (!handles.ContainsKey (obj)) {
				Debug.WriteLine ("Trying to unpin unpinned object: {0}.", obj);
				return;
			}
			handles[obj].Free ();
			handles.Remove (obj);
		}

		/// <summary>
		/// Pin the specified object and return a pointer. MUST be Unpined as soon as possible.
		/// </summary>
		public static IntPtr Pin (this object obj) {
			if (handles.ContainsKey (obj)) {
				Debug.WriteLine ("Trying to pin already pinned object: {0}", obj);
				return handles[obj].AddrOfPinnedObject ();
			}
                
            GCHandle hnd = GCHandle.Alloc (obj, GCHandleType.Pinned);
            handles.Add (obj, hnd);
            return hnd.AddrOfPinnedObject ();
        }
        public static IntPtr Pin<T> (this List<T> obj) {
            if (handles.ContainsKey (obj)) 
                Debug.WriteLine ("Pinning already pinned object: {0}", obj);
                
            GCHandle hnd = GCHandle.Alloc (obj.ToArray(), GCHandleType.Pinned);
            handles.Add (obj, hnd);
            return hnd.AddrOfPinnedObject ();
        }
		public static IntPtr Pin<T> (this T[] obj) {
			if (handles.ContainsKey (obj))
				Debug.WriteLine ("Pinning already pinned object: {0}", obj);

			GCHandle hnd = GCHandle.Alloc (obj, GCHandleType.Pinned);
			handles.Add (obj, hnd);
			return hnd.AddrOfPinnedObject ();
		}
		public static IntPtr Pin (this string obj) {
			if (handles.ContainsKey (obj)) {
				Debug.WriteLine ("Trying to pin already pinned object: {0}", obj);
				return handles[obj].AddrOfPinnedObject ();
			}
            byte[] n = System.Text.Encoding.UTF8.GetBytes(obj +'\0');
			GCHandle hnd = GCHandle.Alloc (n, GCHandleType.Pinned);            
            handles.Add (obj, hnd);
            return hnd.AddrOfPinnedObject ();
        }

		//pin with pinning context
		public static IntPtr Pin (this object obj, PinnedObjects ctx) {
			GCHandle hnd = GCHandle.Alloc (obj, GCHandleType.Pinned);
			ctx.Handles.Add (hnd);
			return hnd.AddrOfPinnedObject ();
		}
		public static IntPtr Pin<T> (this List<T> obj, PinnedObjects ctx) {
			GCHandle hnd = GCHandle.Alloc (obj.ToArray (), GCHandleType.Pinned);
			ctx.Handles.Add (hnd);
			return hnd.AddrOfPinnedObject ();
		}
		public static IntPtr Pin<T> (this T[] obj, PinnedObjects ctx) {
			GCHandle hnd = GCHandle.Alloc (obj, GCHandleType.Pinned);
			ctx.Handles.Add (hnd);
			return hnd.AddrOfPinnedObject ();
		}
		public static IntPtr Pin (this string obj, PinnedObjects ctx) {
			byte[] n = System.Text.Encoding.UTF8.GetBytes (obj + '\0');
			GCHandle hnd = GCHandle.Alloc (n, GCHandleType.Pinned);
			ctx.Handles.Add (hnd);
			return hnd.AddrOfPinnedObject ();
		}

		#endregion

		#region DebugMarkers
		public static void SetDebugMarkerName (this VkCommandBuffer obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.CommandBuffer,
				(ulong)obj.Handle.ToInt64 ()) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkImageView obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.ImageView,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkSampler obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Sampler,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkPipeline obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Pipeline,
				obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkDescriptorSet obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.DescriptorSet,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkSemaphore obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Semaphore,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		public static void SetDebugMarkerName (this VkFence obj, Device dev, string name) {
			if (!dev.debugUtilsEnabled)
				return;
			VkDebugUtilsObjectNameInfoEXT dmo = new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Fence,
				(ulong)obj.Handle) { pObjectName = name.Pin () };
			Utils.CheckResult (vkSetDebugUtilsObjectNameEXT (dev.VkDev, ref dmo));
			name.Unpin ();
		}
		#endregion

		#region shaderc
		public static ShaderInfo CreateShaderInfo (this shaderc.Compiler comp, Device dev, string shaderPath, shaderc.ShaderKind shaderKind,
			SpecializationInfo specializationInfo = null, string entryPoint = "main") {

			using (shaderc.Result res = comp.Compile (shaderPath, shaderKind)) {
				Console.WriteLine ($"SpirV generation: {shaderPath} {res.Status}");
				if (res.Status != shaderc.Status.Success) 
					throw new Exception ($"Shaderc compilation failure: {res.ErrorMessage}");
				VkShaderStageFlags stageFlags;
				switch (shaderKind) {
				case shaderc.ShaderKind.VertexShader:
					stageFlags = VkShaderStageFlags.Vertex;
					break;
				case shaderc.ShaderKind.FragmentShader:
					stageFlags = VkShaderStageFlags.Fragment;
					break;
				case shaderc.ShaderKind.ComputeShader:
					stageFlags = VkShaderStageFlags.Compute;
					break;
				case shaderc.ShaderKind.GeometryShader:
					stageFlags = VkShaderStageFlags.Geometry;
					break;
				case shaderc.ShaderKind.TessControlShader:
					stageFlags = VkShaderStageFlags.TessellationControl;
					break;
				case shaderc.ShaderKind.TessEvaluationShader:
					stageFlags = VkShaderStageFlags.TessellationEvaluation;
					break;
				default:
					throw new NotSupportedException ($"shaderc compilation: shaderKind {shaderKind} not handled");
				}
				return new ShaderInfo (dev, stageFlags, res.CodePointer, (UIntPtr)res.CodeLength, specializationInfo, entryPoint);
			}
		}
		#endregion
	}
}
