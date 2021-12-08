// Copyright (c) 2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;
using System.Linq;
using System.Collections.Generic;
using Glfw;

namespace vke {
	public class Context : IDisposable {
		/** GLFW callback may return a custom pointer, this list makes the link between the GLFW window pointer and the
			manage VkWindow instance. */
		static Dictionary<IntPtr,Context> windows = new Dictionary<IntPtr, Context>();
		/** GLFW window native pointer. */
		IntPtr hWin;
		/**Vulkan Surface */
		protected VkSurfaceKHR hSurf;
		VkInstance inst;
		VkPhysicalDevice phy;
		VkDevice dev;
		VkQueue queue;
		VkSwapchainCreateInfoKHR createInfos;
		VkSwapchainKHR swapchainHandle;

		public VkInstance Inst => inst;
		public VkPhysicalDevice Phy => phy;
		public VkDevice Dev=> dev;
		public VkQueue Queue => queue;
		public uint Width { get; private set; }
		public uint Height { get; private set; }
		public bool VSync { get; private set; }

		VkPresentModeKHR presentMode => VSync ? VkPresentModeKHR.FifoKHR : VkPresentModeKHR.MailboxKHR;

		public Context (uint width = 800, uint height = 600, VkFormat format = VkFormat.B8g8r8a8Unorm,
			VkImageUsageFlags IMAGES_USAGE = VkImageUsageFlags.ColorAttachment,
			string windowName = "vulkan") {

			Width = width;
			Height = height;

			Glfw3.Init ();

			Glfw3.WindowHint (WindowAttribute.ClientApi, 0);
			Glfw3.WindowHint (WindowAttribute.Resizable, 1);

			hWin = Glfw3.CreateWindow ((int)Width, (int)Height, windowName, MonitorHandle.Zero, IntPtr.Zero);
			windows.Add (hWin, this);

			if (hWin == IntPtr.Zero)
				throw new Exception ("[GLFW3] Unable to create vulkan Window");

			Glfw3.SetKeyCallback (hWin, HandleKeyDelegate);
			Glfw3.SetMouseButtonPosCallback (hWin, HandleMouseButtonDelegate);
			Glfw3.SetCursorPosCallback (hWin, HandleCursorPosDelegate);
			Glfw3.SetScrollCallback (hWin, HandleScrollDelegate);
			Glfw3.SetCharCallback (hWin, HandleCharDelegate);

			string[] exts = Glfw3.GetRequiredInstanceExtensions ();

			IntPtr[] extensions = new IntPtr[exts.Length];
			for (int i = 0; i < exts.Length; i++)
				extensions[i] = exts[i].PinPointer();

			using (VkApplicationInfo ai = new VkApplicationInfo ()) {
				using (VkInstanceCreateInfo ci = new VkInstanceCreateInfo {
						pApplicationInfo = ai,
						enabledExtensionCount = (uint)extensions.Length,
						ppEnabledExtensionNames = extensions.Pin()	}){
					CheckResult (vkCreateInstance (ci, IntPtr.Zero, out inst));
				}
			}
			extensions.Unpin();
			for (int i = 0; i < extensions.Length; i++)
				extensions[i].Unpin();

			Vk.LoadInstanceFunctionPointers (inst);

			hSurf = inst.CreateSurface (hWin);

			if (!inst.TryGetPhysicalDevice (VkPhysicalDeviceType.DiscreteGpu, out phy))
				if (!inst.TryGetPhysicalDevice (VkPhysicalDeviceType.IntegratedGpu, out phy))
					if (!inst.TryGetPhysicalDevice (VkPhysicalDeviceType.Cpu, out phy))
						throw new Exception ("no suitable physical device found");

			VkQueueFamilyProperties[] qFamProps = phy.GetQueueFamilyProperties ();

			uint qFamIndex = (uint)qFamProps.Select((qFam, index) => (qFam, index))
				.First (qfp=>qfp.qFam.queueFlags.HasFlag (VkQueueFlags.Graphics)).index;

			float[] priorities = {0};

			exts = new string[] { Ext.D.VK_KHR_swapchain };
			extensions = new IntPtr[exts.Length];
			for (int i = 0; i < exts.Length; i++)
				extensions[i] = exts[i].PinPointer();

			using (VkDeviceQueueCreateInfo qInfo = new VkDeviceQueueCreateInfo (qFamIndex,1,priorities)) {
				using (VkDeviceCreateInfo deviceCreateInfo = new VkDeviceCreateInfo () {
							pQueueCreateInfos = qInfo,
							enabledExtensionCount = (uint)extensions.Length,
							ppEnabledExtensionNames = extensions.Pin()	}) {

					CheckResult (vkCreateDevice (phy, deviceCreateInfo, IntPtr.Zero, out dev));

				}
			}
			Vk.LoadDeviceFunctionPointers (dev);
			vkGetDeviceQueue (dev, qFamIndex, 0, out VkQueue gQ);

			VkSurfaceFormatKHR[] formats = phy.GetSurfaceFormats (hSurf);
			for (int i = 0; i < formats.Length; i++) {
				if (formats[i].format == format) {
					createInfos.imageFormat = format;
					createInfos.imageColorSpace = formats[i].colorSpace;
					break;
				}
			}
			if (createInfos.imageFormat == VkFormat.Undefined)
				throw new Exception ("Invalid format for swapchain: " + format);

			VkPresentModeKHR[] presentModes = phy.GetSurfacePresentModes (hSurf);
			for (int i = 0; i < presentModes.Length; i++) {
				if (presentModes[i] == presentMode) {
					createInfos.presentMode = presentMode;
					break;
				}
			}
			if (createInfos.presentMode != presentMode)
				throw new Exception ("Invalid presentMode for swapchain: " + presentMode);

			createInfos.surface = hSurf;
			createInfos.imageExtent = new VkExtent2D (Width, Height);
			createInfos.imageArrayLayers = 1;
			createInfos.imageUsage = IMAGES_USAGE;
			createInfos.imageSharingMode = VkSharingMode.Exclusive;
			createInfos.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR;
			createInfos.presentMode = presentMode;
			createInfos.clipped = 1;
			vkGetPhysicalDeviceSurfaceCapabilitiesKHR (phy, hSurf, out VkSurfaceCapabilitiesKHR capabilities);
			createInfos.minImageCount = capabilities.minImageCount;
			createInfos.preTransform = capabilities.currentTransform;

			CreateSwapChain();
		}

		internal void CreateSwapChain () {
			vkDeviceWaitIdle (dev);
			vkGetPhysicalDeviceSurfaceCapabilitiesKHR (phy, hSurf, out VkSurfaceCapabilitiesKHR caps);
			createInfos.oldSwapchain = swapchainHandle;

			if (caps.currentExtent.width == 0xFFFFFFFF) {
				if (createInfos.imageExtent.width < caps.minImageExtent.width)
					createInfos.imageExtent.width = caps.minImageExtent.width;
				else if (createInfos.imageExtent.width > caps.maxImageExtent.width)
					createInfos.imageExtent.width = caps.maxImageExtent.width;

				if (createInfos.imageExtent.height < caps.minImageExtent.height)
					createInfos.imageExtent.height = caps.minImageExtent.height;
				else if (createInfos.imageExtent.height > caps.maxImageExtent.height)
					createInfos.imageExtent.height = caps.maxImageExtent.height;
			} else
				createInfos.imageExtent = caps.currentExtent;

			CheckResult (vkCreateSwapchainKHR (dev, ref createInfos, IntPtr.Zero, out VkSwapchainKHR newSwapChain));

			if (swapchainHandle != VkSwapchainKHR.Null)
				vkDestroySwapchainKHR (dev, swapchainHandle, IntPtr.Zero);

			swapchainHandle = newSwapChain;
		}

		public virtual void Run () {


			while (!Glfw3.WindowShouldClose (hWin)) {

				Glfw3.PollEvents ();
			}
		}

		#region events
		protected double lastMouseX { get; private set; }
		protected double lastMouseY { get; private set; }
		protected virtual void onScroll (double xOffset, double yOffset) { }
		protected virtual void onMouseMove (double xPos, double yPos) {
		}
		protected virtual void onMouseButtonDown (MouseButton button) { }
		protected virtual void onMouseButtonUp (MouseButton button) { }
		protected virtual void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.Escape:
					Glfw3.SetWindowShouldClose (hWin, 1);
					break;
			}
		}
		protected virtual void onKeyUp (Key key, int scanCode, Modifier modifiers) { }
		protected virtual void onChar (CodePoint cp) { }
		static CursorPosDelegate HandleCursorPosDelegate = (window, xPosition, yPosition) => {
			windows[window].onMouseMove (xPosition, yPosition);
			windows[window].lastMouseX = xPosition;
			windows[window].lastMouseY = yPosition;
		};
		static MouseButtonDelegate HandleMouseButtonDelegate = (IntPtr window, Glfw.MouseButton button, InputAction action, Modifier mods) => {
			if (action == InputAction.Press)
				windows[window].onMouseButtonDown (button);
			else
				windows[window].onMouseButtonUp (button);
		};
		static ScrollDelegate HandleScrollDelegate = (IntPtr window, double xOffset, double yOffset) => {
			windows[window].onScroll (xOffset, yOffset);
		};
		static KeyDelegate HandleKeyDelegate = (IntPtr window, Key key, int scanCode, InputAction action, Modifier modifiers) => {
			if (action == InputAction.Press || action == InputAction.Repeat) {
				windows[window].onKeyDown (key, scanCode, modifiers);
			} else {
				windows[window].onKeyUp (key, scanCode, modifiers);
			}
		};
		static CharDelegate HandleCharDelegate = (IntPtr window, CodePoint codepoint) => {
			windows[window].onChar (codepoint);
		};
		#endregion
		#region IDisposable implementation
		bool disposedValue;
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)
				}

				vkDestroyDevice (dev, IntPtr.Zero);
				vkDestroyInstance (inst, IntPtr.Zero);
				disposedValue = true;
			}
		}
		~Context() {
		     Dispose(disposing: false);
		}
		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
