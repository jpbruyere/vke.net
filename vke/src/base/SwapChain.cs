﻿//
// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	public class SwapChain : Activable {
		/// <summary>
		/// Set the default swapchain image format.
		/// </summary>
		public static VkFormat PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;
		/// <summary>
		/// Set additional usage flags for the swapchain images such as TransferDst.
		/// </summary>
		public static VkImageUsageFlags IMAGES_USAGE = VkImageUsageFlags.ColorAttachment;

		public VkSwapchainKHR Handle { get; private set; }

		internal uint currentImageIndex;
		VkSwapchainCreateInfoKHR createInfos;
		PresentQueue presentQueue;

		public VkSemaphore presentComplete;
		public Image[] images;

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.SwapchainKHR, Handle.Handle);

		/// <summary>Swapchain images count.</summary>
		public uint ImageCount => (uint)images?.Length;
		public uint Width => createInfos.imageExtent.width;
		public uint Height => createInfos.imageExtent.height;
		public VkFormat ColorFormat => createInfos.imageFormat;
		public VkImageUsageFlags ImageUsage => createInfos.imageUsage;

		public SwapChain (PresentQueue _presentableQueue, uint width = 800, uint height = 600, VkFormat format = VkFormat.B8g8r8a8Unorm,
			VkPresentModeKHR presentMode = VkPresentModeKHR.FifoKHR)
		: base (_presentableQueue.dev) {

			presentQueue = _presentableQueue;
			createInfos = VkSwapchainCreateInfoKHR.New ();

			VkSurfaceFormatKHR[] formats = Dev.phy.GetSurfaceFormats (presentQueue.Surface);
			for (int i = 0; i < formats.Length; i++) {
				if (formats[i].format == format) {
					createInfos.imageFormat = format;
					createInfos.imageColorSpace = formats[i].colorSpace;
					break;
				}
			}
			if (createInfos.imageFormat == VkFormat.Undefined)
				throw new Exception ("Invalid format for swapchain: " + format);

			VkPresentModeKHR[] presentModes = Dev.phy.GetSurfacePresentModes (presentQueue.Surface);
			for (int i = 0; i < presentModes.Length; i++) {
				if (presentModes[i] == presentMode) {
					createInfos.presentMode = presentMode;
					break;
				}
			}
			if (createInfos.presentMode != presentMode)
				throw new Exception ("Invalid presentMode for swapchain: " + presentMode);

			createInfos.surface = presentQueue.Surface;
			createInfos.imageExtent = new VkExtent2D (width, height);
			createInfos.imageArrayLayers = 1;
			createInfos.imageUsage = IMAGES_USAGE;
			createInfos.imageSharingMode = VkSharingMode.Exclusive;
			createInfos.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR;
			createInfos.presentMode = presentMode;
			createInfos.clipped = 1;
		}
		public override void Activate () {
			if (state != ActivableState.Activated) {
				presentComplete = Dev.CreateSemaphore ();
				presentComplete.SetDebugMarkerName (Dev, "Semaphore PresentComplete");
			}
			base.Activate ();
		}
		/// <summary>
		/// Create swapchain and populate images array
		/// </summary>
		public void Create () {

			Dev.WaitIdle ();

			VkSurfaceCapabilitiesKHR capabilities = Dev.phy.GetSurfaceCapabilities (presentQueue.Surface);

			createInfos.minImageCount = capabilities.minImageCount;
			createInfos.preTransform = capabilities.currentTransform;
			createInfos.oldSwapchain = Handle;

			if (capabilities.currentExtent.width == 0xFFFFFFFF) {
				if (createInfos.imageExtent.width < capabilities.minImageExtent.width)
					createInfos.imageExtent.width = capabilities.minImageExtent.width;
				else if (createInfos.imageExtent.width > capabilities.maxImageExtent.width)
					createInfos.imageExtent.width = capabilities.maxImageExtent.width;

				if (createInfos.imageExtent.height < capabilities.minImageExtent.height)
					createInfos.imageExtent.height = capabilities.minImageExtent.height;
				else if (createInfos.imageExtent.height > capabilities.maxImageExtent.height)
					createInfos.imageExtent.height = capabilities.maxImageExtent.height;
			} else
				createInfos.imageExtent = capabilities.currentExtent;

			Utils.CheckResult (vkCreateSwapchainKHR (Dev.VkDev, ref createInfos, IntPtr.Zero, out VkSwapchainKHR newSwapChain));

			if (Handle.Handle != 0)
				_destroy ();
			Handle = newSwapChain;

			if (state != ActivableState.Activated)
				Activate ();
				
			Utils.CheckResult (vkGetSwapchainImagesKHR (Dev.VkDev, Handle, out uint imageCount, IntPtr.Zero));
			if (imageCount == 0)
				throw new Exception ("Swapchain image count is 0.");
			VkImage[] imgs = new VkImage[imageCount];
			Utils.CheckResult (vkGetSwapchainImagesKHR (Dev.VkDev, Handle, out imageCount, imgs.Pin ()));
			imgs.Unpin ();

			images = new Image[imgs.Length];
			for (int i = 0; i < imgs.Length; i++) {
				images[i] = new Image (Dev, imgs[i], ColorFormat, ImageUsage, Width, Height);
				images[i].CreateView ();
				images[i].SetName ("SwapChain Img" + i);
				images[i].Descriptor.imageView.SetDebugMarkerName (Dev, "SwapChain Img" + i + " view");
			}
		}
		/// <summary>
		/// Acquire next image, recreate swapchain if out of date or suboptimal error.
		/// </summary>
		/// <returns>Swapchain image index or -1 if failed</returns>
		/// <param name="fence">Fence param of 'vkAcquireNextImageKHR'</param>
		public int GetNextImage (VkFence fence = default (VkFence)) {
			VkResult res = vkAcquireNextImageKHR (Dev.VkDev, Handle, UInt64.MaxValue, presentComplete, fence, out currentImageIndex);
			if (res == VkResult.ErrorOutOfDateKHR || res == VkResult.SuboptimalKHR) {
				Create ();
				return -1;
			}
			Utils.CheckResult (res);
			return (int)currentImageIndex;
		}

		void _destroy () {
			for (int i = 0; i < ImageCount; i++)
				images[i].Dispose ();
			vkDestroySwapchainKHR (Dev.VkDev, Handle, IntPtr.Zero);
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{Handle.Handle.ToString ("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (disposing) {
				} else
					System.Diagnostics.Debug.WriteLine ("VKE Swapchain disposed by finalizer");

				Dev.DestroySemaphore (presentComplete);
				_destroy ();

			} else if (disposing)
				System.Diagnostics.Debug.WriteLine ("Calling dispose on unactive Swapchain");

			base.Dispose (disposing);
		}
		#endregion
	}
}
