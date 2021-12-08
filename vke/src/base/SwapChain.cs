//
// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// A swapchain provides the ability to present rendering results to a surface.
	/// A swapchain is an abstraction for an array of presentable images that are associated with a surface.
	/// The presentable images are represented by `Image` objects created by the platform.
	/// One image (which can: be an array image for multiview/stereoscopic-3D surfaces) is displayed at a time, but multiple images can: be queued for presentation.
	/// An application renders to the image, and then queues the image for presentation to the surface.
	/// </summary>
	/// <remarks>
	/// A native window cannot: be associated with more than one non-retired swapchain at a time.
	/// Further, swapchains cannot: be created for native windows that have a non-Vulkan graphics API surface associated with them.
	/// </remarks>
	public class SwapChain : Activable {
		/// <summary> Set the default swapchain image format. </summary>
		public static VkFormat PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;
		/// <summary> Set additional usage flags for the swapchain images such as TransferDst. </summary>
		public static VkImageUsageFlags IMAGES_USAGE = VkImageUsageFlags.ColorAttachment;
		/// <summary> Opaque handle to a swapchain object. </summary>
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

		/// <summary>
		/// Create a new managed `SwapChain` object. Native object will only be created with a call to the 'Create` method.
		/// </summary>
		/// <param name="_presentableQueue">Presentable queue.</param>
		/// <param name="width">Swapchain x dimension.</param>
		/// <param name="height">Swapchain y dimension.</param>
		/// <param name="format">Swapchain's images format.</param>
		/// <param name="presentMode">a present mode supported by the engine as returned by the `GetSurfacePresentModes` method of the `PhysicalDevice`</param>
		public SwapChain (PresentQueue _presentableQueue, uint width = 800, uint height = 600, VkFormat format = VkFormat.B8g8r8a8Unorm,
			VkPresentModeKHR presentMode = VkPresentModeKHR.FifoKHR)
		: base (_presentableQueue.dev) {

			presentQueue = _presentableQueue;
			createInfos = default;

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
				Create ();
			}
			base.Activate ();
		}
		/// <summary>
		/// Create/recreate swapchain and populate images array
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

			CheckResult (vkCreateSwapchainKHR (Dev.Handle, ref createInfos, IntPtr.Zero, out VkSwapchainKHR newSwapChain));

			if (Handle.Handle != 0)
				_destroy ();

			presentComplete = Dev.CreateSemaphore ();
			presentComplete.SetDebugMarkerName (Dev, "Semaphore PresentComplete");
			Handle = newSwapChain;

			CheckResult (vkGetSwapchainImagesKHR (Dev.Handle, Handle, out uint imageCount, IntPtr.Zero));
			if (imageCount == 0)
				throw new Exception ("Swapchain image count is 0.");
			VkImage[] imgs = new VkImage[imageCount];
			CheckResult (vkGetSwapchainImagesKHR (Dev.Handle, Handle, out imageCount, imgs.Pin ()));
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
		/// Retrieve the index of the next available presentable image, recreate swapchain if out of date or suboptimal error.
		/// </summary>
		/// <returns>Swapchain image index or -1 if failed</returns>
		/// <param name="fence">an optional fence to signal.</param>
		public int GetNextImage (Fence fence = null) {
			VkResult res = vkAcquireNextImageKHR (Dev.Handle, Handle, UInt64.MaxValue, presentComplete, fence, out currentImageIndex);
			if (res == VkResult.ErrorOutOfDateKHR || res == VkResult.SuboptimalKHR) {
				Create ();
				return -1;
			}
			CheckResult (res);
			return (int)currentImageIndex;
		}

		void _destroy () {
			for (int i = 0; i < ImageCount; i++)
				images[i].Dispose ();
			vkDestroySwapchainKHR (Dev.Handle, Handle, IntPtr.Zero);
			Dev.DestroySemaphore (presentComplete);
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{Handle.Handle.ToString ("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (!disposing)
					System.Diagnostics.Debug.WriteLine ("VKE Swapchain disposed by finalizer");

				_destroy ();

			} else if (disposing)
				System.Diagnostics.Debug.WriteLine ("Calling dispose on unactive Swapchain");

			base.Dispose (disposing);
		}
		#endregion
	}
}
