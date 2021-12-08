// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace vke {
	/// <summary>
	/// Collection of physical devices returned by the vulkan instance.
	/// </summary>
	public class PhysicalDeviceCollection : IEnumerable<PhysicalDevice> {
		readonly VkInstance inst;
		readonly PhysicalDevice [] phys;

		/// <summary>
		/// Retrieve the physical devices available for the provided vulkan instance
		/// </summary>
		/// <param name="instance">The vulkan instance to retrieve the physical devices from.</param>
		public PhysicalDeviceCollection (VkInstance instance)
		{
			inst = instance;
			CheckResult (vkEnumeratePhysicalDevices (inst, out uint gpuCount, IntPtr.Zero));
			if (gpuCount <= 0)
				throw new Exception ("No GPU found");

			IntPtr gpus = Marshal.AllocHGlobal (Marshal.SizeOf<IntPtr> () * (int)gpuCount);
			CheckResult (vkEnumeratePhysicalDevices (inst, out gpuCount, gpus), "Could not enumerate physical devices.");

			phys = new PhysicalDevice [gpuCount];

			for (int i = 0; i < gpuCount; i++)
				phys [i] = new PhysicalDevice (Marshal.ReadIntPtr (gpus + i * Marshal.SizeOf<IntPtr> ()));

			Marshal.FreeHGlobal (gpus);
		}

		public PhysicalDevice this [int i] => phys [i];
		public IEnumerator<PhysicalDevice> GetEnumerator () => ((IEnumerable<PhysicalDevice>)phys).GetEnumerator ();
		IEnumerator IEnumerable.GetEnumerator () => ((IEnumerable<PhysicalDevice>)phys).GetEnumerator ();
	}
	/// <summary>
	/// Vke class that encapsulate a physical device.
	/// </summary>
	public class PhysicalDevice {
		readonly IntPtr phy;

		public VkPhysicalDeviceMemoryProperties memoryProperties { get; private set; }
		public VkQueueFamilyProperties [] QueueFamilies { get; private set; }
		public VkPhysicalDeviceProperties Properties {
			get {
				vkGetPhysicalDeviceProperties (phy, out VkPhysicalDeviceProperties pdp);
				return pdp;
			}
		}
		public VkPhysicalDeviceFeatures Features {
			get {
				vkGetPhysicalDeviceFeatures (phy, out VkPhysicalDeviceFeatures df);
				return df;
			}
		}
		public VkPhysicalDeviceLimits Limits => Properties.limits;

		public bool HasSwapChainSupport { get; private set; }
		public IntPtr Handle => phy;

		#region CTOR
		internal PhysicalDevice (IntPtr vkPhy)
		{
			phy = vkPhy;

			// Gather physical Device memory properties
			IntPtr tmp = Marshal.AllocHGlobal (Marshal.SizeOf<VkPhysicalDeviceMemoryProperties> ());
			vkGetPhysicalDeviceMemoryProperties (phy, tmp);

			memoryProperties = Marshal.PtrToStructure<VkPhysicalDeviceMemoryProperties> (tmp);
			Marshal.FreeHGlobal (tmp);

			vkGetPhysicalDeviceQueueFamilyProperties (phy, out uint queueFamilyCount, IntPtr.Zero);
			QueueFamilies = new VkQueueFamilyProperties [queueFamilyCount];

			if (queueFamilyCount <= 0)
				throw new Exception ("No queues found for physical device");

			vkGetPhysicalDeviceQueueFamilyProperties (phy, out queueFamilyCount, QueueFamilies.Pin ());
			QueueFamilies.Unpin ();

			HasSwapChainSupport = GetDeviceExtensionSupported (Ext.D.VK_KHR_swapchain);
		}
		#endregion

		public bool GetDeviceExtensionSupported (string extName) => SupportedExtensions ().Contains (extName);
		public string [] SupportedExtensions () => SupportedExtensions (IntPtr.Zero);
		public string [] SupportedExtensions (IntPtr layer)
		{
			CheckResult (vkEnumerateDeviceExtensionProperties (phy, layer, out uint count, IntPtr.Zero));

			int sizeStruct = Marshal.SizeOf<VkExtensionProperties> ();
			IntPtr ptrSupExts = Marshal.AllocHGlobal (sizeStruct * (int)count);
			CheckResult (vkEnumerateDeviceExtensionProperties (phy, layer, out count, ptrSupExts));

			string [] result = new string [count];
			IntPtr tmp = ptrSupExts;
			for (int i = 0; i < count; i++) {
				result [i] = Marshal.PtrToStringAnsi (tmp);
				tmp += sizeStruct;
			}

			Marshal.FreeHGlobal (ptrSupExts);
			return result;
		}

		public bool GetPresentIsSupported (uint qFamilyIndex, VkSurfaceKHR surf)
		{
			vkGetPhysicalDeviceSurfaceSupportKHR (phy, qFamilyIndex, surf, out VkBool32 isSupported);
			return isSupported;
		}

		public VkSurfaceCapabilitiesKHR GetSurfaceCapabilities (VkSurfaceKHR surf)
		{
			vkGetPhysicalDeviceSurfaceCapabilitiesKHR (phy, surf, out VkSurfaceCapabilitiesKHR caps);
			return caps;
		}

		public VkSurfaceFormatKHR [] GetSurfaceFormats (VkSurfaceKHR surf)
		{
			vkGetPhysicalDeviceSurfaceFormatsKHR (phy, surf, out uint count, IntPtr.Zero);
			VkSurfaceFormatKHR [] formats = new VkSurfaceFormatKHR [count];

			vkGetPhysicalDeviceSurfaceFormatsKHR (phy, surf, out count, formats.Pin ());
			formats.Unpin ();

			return formats;
		}
		public VkPresentModeKHR[] GetSurfacePresentModes (VkSurfaceKHR surf) {
			vkGetPhysicalDeviceSurfacePresentModesKHR (phy, surf, out uint count, IntPtr.Zero);
			if (Type.GetType ("Mono.Runtime") == null) {
				uint[] modes = new uint[count];//this cause an error on mono
				vkGetPhysicalDeviceSurfacePresentModesKHR (phy, surf, out count, modes.Pin ());
				modes.Unpin ();
				VkPresentModeKHR[] mds = new VkPresentModeKHR[count];
				for (int i = 0; i < count; i++)
					mds[i] = (VkPresentModeKHR)modes[i];
				return mds;
			} else {
				VkPresentModeKHR[] modes = new VkPresentModeKHR[count];//enums not blittable on ms.Net
				vkGetPhysicalDeviceSurfacePresentModesKHR (phy, surf, out count, modes.Pin ());
				modes.Unpin ();
				return modes;
			}
		}
		public VkFormatProperties GetFormatProperties (VkFormat format)
		{
			vkGetPhysicalDeviceFormatProperties (phy, format, out VkFormatProperties properties);
			return properties;
		}
		public bool TryGetImageFormatProperties (VkFormat format, VkImageTiling tiling,
			VkImageUsageFlags usage, out VkImageFormatProperties properties,
			VkImageType type = VkImageType.Image2D, VkImageCreateFlags flags = 0) {
			VkResult result = vkGetPhysicalDeviceImageFormatProperties (phy, format, type,
				tiling, usage, flags, out properties);
			return result == VkResult.Success;
		}
		public VkPhysicalDeviceToolPropertiesEXT[] GetToolProperties () {
			CheckResult (vkGetPhysicalDeviceToolPropertiesEXT (phy , out uint count, IntPtr.Zero));
			int sizeStruct = Marshal.SizeOf<VkPhysicalDeviceToolPropertiesEXT> ();
			IntPtr ptrTools = Marshal.AllocHGlobal (sizeStruct * (int)count);
			CheckResult (vkGetPhysicalDeviceToolPropertiesEXT (phy , out count, ptrTools));

			VkPhysicalDeviceToolPropertiesEXT[] result = new VkPhysicalDeviceToolPropertiesEXT[count];
			IntPtr tmp = ptrTools;
			for (int i = 0; i < count; i++) {
				result[i] = Marshal.PtrToStructure<VkPhysicalDeviceToolPropertiesEXT> (tmp);
				tmp += sizeStruct;
			}

			Marshal.FreeHGlobal (ptrTools);
			return result;
		}
	}
}
