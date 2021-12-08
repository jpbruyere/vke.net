// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Logical device encapsulating vulkan logical device handle. Implements only IDisposable an do not derive from
	/// Activable, so it may be activated only once and no reference counting on it is handled, and no reactivation is posible
	/// after being disposed.
	/// </summary>
	public class Device : IDisposable {
		public readonly PhysicalDevice phy;										/**Vulkan physical device class*/

		VkDevice dev;
		[Obsolete("Use Handle instead")]
		public VkDevice VkDev => dev;                                           /**Vulkan logical device handle*/
		public VkDevice Handle => dev;                                          /**Vulkan logical device handle*/


		internal List<Queue> queues = new List<Queue> ();
		internal bool debugUtilsEnabled;

#if MEMORY_POOLS
		public ResourceManager resourceManager;
#endif

		public Device (PhysicalDevice _phy) {
			phy = _phy;
		}

		public void Activate (VkPhysicalDeviceFeatures enabledFeatures, params string[] extensions) {
			List<VkDeviceQueueCreateInfo> qInfos = new List<VkDeviceQueueCreateInfo> ();

			foreach (IGrouping<uint, Queue> qfams in queues.GroupBy (q => q.qFamIndex)) {
				int qTot = qfams.Count ();
				uint qIndex = 0;
				List<float> priorities = new List<float> ();
				bool qCountReached = false;//true when queue count of that family is reached

				foreach (Queue q in qfams) {
					if (!qCountReached)
						priorities.Add (q.priority);
					q.index = qIndex++;
					if (qIndex == phy.QueueFamilies[qfams.Key].queueCount) {
						qIndex = 0;
						qCountReached = true;
					}
				}

				qInfos.Add (new VkDeviceQueueCreateInfo {
					sType = VkStructureType.DeviceQueueCreateInfo,
					queueCount = qCountReached ? phy.QueueFamilies[qfams.Key].queueCount : qIndex,
					queueFamilyIndex = qfams.Key,
					pQueuePriorities = priorities
				});
			}

			//enable only supported exceptions
			List<IntPtr> deviceExtensions = new List<IntPtr> ();
			for (int i = 0; i < extensions.Length; i++) {
				if (phy.GetDeviceExtensionSupported (extensions[i]))
					deviceExtensions.Add (new FixedUtf8String (extensions[i]));
				else
					Console.WriteLine ($"Unsupported device extension: {extensions[i]}");
			}

			VkDeviceCreateInfo deviceCreateInfo = new VkDeviceCreateInfo ();
			deviceCreateInfo.pQueueCreateInfos = qInfos;
			deviceCreateInfo.pEnabledFeatures = enabledFeatures;

			if (deviceExtensions.Count > 0) {
				deviceCreateInfo.enabledExtensionCount = (uint)deviceExtensions.Count;
				deviceCreateInfo.ppEnabledExtensionNames = deviceExtensions.Pin ();
			}

			CheckResult (vkCreateDevice (phy.Handle, ref deviceCreateInfo, IntPtr.Zero, out dev));

			deviceCreateInfo.Dispose();
			foreach (VkDeviceQueueCreateInfo qI in qInfos)
				qI.Dispose();

			if (deviceExtensions.Count > 0)
				deviceExtensions.Unpin ();

			//Vk.LoadDeviceFunctionPointers (dev);

			foreach (Queue q in queues)
				q.updateHandle ();

#if MEMORY_POOLS
			resourceManager = new ResourceManager (this);
#endif
		}
		/// <summary>
		/// Creates a new semaphore.
		/// </summary>
		/// <returns>The semaphore native handle</returns>
		public VkSemaphore CreateSemaphore () {
			VkSemaphore tmp;
			VkSemaphoreCreateInfo info = default;
			CheckResult (vkCreateSemaphore (dev, ref info, IntPtr.Zero, out tmp));
			return tmp;
		}
		public void DestroySemaphore (VkSemaphore semaphore) {
			vkDestroySemaphore (dev, semaphore, IntPtr.Zero);
			semaphore = 0;
		}

		public void DestroyShaderModule (VkShaderModule module) {
			vkDestroyShaderModule (VkDev, module, IntPtr.Zero);
			module = 0;
		}
		/// <summary>
		/// Wait for this logical device to enter the idle state.
		/// </summary>
		public void WaitIdle () {
			CheckResult (vkDeviceWaitIdle (dev));
		}
		public VkRenderPass CreateRenderPass (VkRenderPassCreateInfo info) {
			VkRenderPass renderPass;
			CheckResult (vkCreateRenderPass (dev, ref info, IntPtr.Zero, out renderPass));
			return renderPass;
		}

		public VkImageView CreateImageView (VkImage image, VkFormat format, VkImageViewType viewType = VkImageViewType.ImageView2D, VkImageAspectFlags aspectFlags = VkImageAspectFlags.Color) {
			VkImageView view;
			VkImageViewCreateInfo infos = default;
			infos.image = image;
			infos.viewType = viewType;
			infos.format = format;
			infos.components = new VkComponentMapping { r = VkComponentSwizzle.R, g = VkComponentSwizzle.G, b = VkComponentSwizzle.B, a = VkComponentSwizzle.A };
			infos.subresourceRange = new VkImageSubresourceRange (aspectFlags);

			CheckResult (vkCreateImageView (dev, ref infos, IntPtr.Zero, out view));
			return view;
		}
		public void DestroyImageView (VkImageView view) {
			vkDestroyImageView (dev, view, IntPtr.Zero);
		}
		public void DestroySampler (VkSampler sampler) {
			vkDestroySampler (dev, sampler, IntPtr.Zero);
		}
		public void DestroyImage (VkImage img) {
			vkDestroyImage (dev, img, IntPtr.Zero);
		}
		public void DestroyFramebuffer (VkFramebuffer fb) {
			vkDestroyFramebuffer (dev, fb, IntPtr.Zero);
		}
		public void DestroyRenderPass (VkRenderPass rp) {
			vkDestroyRenderPass (dev, rp, IntPtr.Zero);
		}
		// This function is used to request a Device memory type that supports all the property flags we request (e.g. Device local, host visibile)
		// Upon success it will return the index of the memory type that fits our requestes memory properties
		// This is necessary as implementations can offer an arbitrary number of memory types with different
		// memory properties.
		// You can check http://vulkan.gpuinfo.org/ for details on different memory configurations
		internal uint GetMemoryTypeIndex (uint typeBits, VkMemoryPropertyFlags properties) {
			// Iterate over all memory types available for the Device used in this example
			for (uint i = 0; i < phy.memoryProperties.memoryTypeCount; i++) {
				if ((typeBits & 1) == 1) {
					if ((phy.memoryProperties.memoryTypes.AsSpan[(int)i].propertyFlags & properties) == properties) {
						return i;
					}
				}
				typeBits >>= 1;
			}

			throw new InvalidOperationException ("Could not find a suitable memory type!");
		}
		public VkFormat GetSuitableDepthFormat () {
			VkFormat[] formats = new VkFormat[] {VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm };
			foreach (VkFormat f in formats) {
				Console.WriteLine ( (int)phy.GetFormatProperties (f).optimalTilingFeatures);
				if (phy.GetFormatProperties (f).optimalTilingFeatures.HasFlag(VkFormatFeatureFlags.DepthStencilAttachment))
					return f;
			}
			throw new InvalidOperationException ("No suitable depth format found.");
		}
		/// <summary>
		/// Load compiled SpirvShader.
		/// </summary>
		/// <returns>the vulkan shader module.</returns>
		/// <param name="filename">path of the spv shader.</param>
		public VkShaderModule CreateShaderModule (string filename) {
			using (Stream stream = Helpers.GetStreamFromPath (filename)) {
				using (BinaryReader br = new BinaryReader (stream)) {
					byte[] shaderCode = br.ReadBytes ((int)stream.Length);
					UIntPtr shaderSize = (UIntPtr)shaderCode.Length;
					Span<uint> tmp = MemoryMarshal.Cast<byte, uint> (shaderCode);
					VkShaderModule shaderModule = CreateShaderModule (tmp.ToArray(), shaderSize);
					return shaderModule;
				}
			}
		}/// <summary>
		/// Load spirv code from unmanaged native pointer.
		/// </summary>
		/// <returns>the vulkan shader module.</returns>
		/// <param name="code">unmanaged pointer holding the spirv code. Pointer must stay valid only during
		/// the call to this method.</param>
		/// <param name="codeSize">spirv code byte size.</param>
		public VkShaderModule CreateShaderModule (uint[] code, UIntPtr codeSize) {
			using (VkShaderModuleCreateInfo moduleCreateInfo = new VkShaderModuleCreateInfo (codeSize, code)) {
				CheckResult (vkCreateShaderModule (Handle, moduleCreateInfo, IntPtr.Zero, out VkShaderModule shaderModule));
				return shaderModule;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // Pour détecter les appels redondants

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (disposing) {
#if MEMORY_POOLS
					resourceManager.Dispose ();
#endif
				} else
					System.Diagnostics.Debug.WriteLine ("Device disposed by Finalizer.");

				vkDestroyDevice (dev, IntPtr.Zero);

				disposedValue = true;
			}
		}

		~Device() {
		   Dispose(false);
		}

		// Ce code est ajouté pour implémenter correctement le modèle supprimable.
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize(this);
		}
#endregion
	}
}
