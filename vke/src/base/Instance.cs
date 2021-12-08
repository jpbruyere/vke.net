// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Vulkan Instance disposable class
	/// </summary>
	public class Instance : IDisposable {
		/// <summary>If true, the VK_LAYER_KHRONOS_validation layer is loaded at startup; </summary>
		[Obsolete("use constructor with layers as 1st argument, vkWindow implement overridable `EnabledLayers`")]
		public static bool VALIDATION;
		/// <summary>If true, the VK_LAYER_RENDERDOC_Capture layer is loaded at startup; </summary>
		[Obsolete("use constructor with layers as 1st argument, vkWindow implement overridable `EnabledLayers`")]
		public static bool RENDER_DOC_CAPTURE;

		public static uint VK_MAJOR = 1;
		public static uint VK_MINOR = 1;

		public static string ENGINE_NAME = "vke.net";
		public static string APPLICATION_NAME = "vke.net";

		VkInstance inst;

		public IntPtr Handle => inst.Handle;
		public VkInstance VkInstance => inst;

		static class Strings {

			public static FixedUtf8String main = "main";
		}
		const string strValidationLayer = "VK_LAYER_KHRONOS_validation";
		const string strRenderDocLayer = "VK_LAYER_RENDERDOC_Capture";

		internal bool debugUtilsEnabled;

		/// <summary>
		/// Create a new vulkan instance with enabled extensions given as argument.
		/// </summary>
		/// <param name="extensions">List of extension to enable if supported</param>
		public Instance (params string[] extensions) : this (null, extensions) {}

		/// <summary>
		/// Create a new vulkan instance with enabled layers and extensions given as arguments.
		/// </summary>
		/// <param name="layers">if not null, load layers in order, else use `VALIDATION` and `RENDER_DOC_CAPTURE`
		/// static variables to enable corresponding extensions</param>
		/// <param name="extensions">List of extension to enable if supported</param>
		public Instance (string[] layers, params string[] extensions) {
			List<IntPtr> instanceExtensions = new List<IntPtr> ();
			List<IntPtr> enabledLayerNames = new List<IntPtr> ();

			string[] supportedExts = SupportedExtensions (IntPtr.Zero);

			using (PinnedObjects pctx = new PinnedObjects ()) {
				for (int i = 0; i < extensions.Length; i++) {
					if (supportedExts.Contains (extensions [i])) {
						instanceExtensions.Add (extensions [i].Pin (pctx));
						if (extensions [i] == Ext.I.VK_EXT_debug_utils)
							debugUtilsEnabled = true;
					} else
						Console.WriteLine ($"Vulkan initialisation: Unsupported extension: {extensions [i]}");
				}

				if (layers != null) {
					for (int i = 0; i < layers.Length; i++)
						enabledLayerNames.Add (layers[i].Pin (pctx));
				} else {
					if (VALIDATION)
						enabledLayerNames.Add (strValidationLayer.Pin (pctx));
					if (RENDER_DOC_CAPTURE)
						enabledLayerNames.Add (strRenderDocLayer.Pin (pctx));
				}

				VkApplicationInfo appInfo = new VkApplicationInfo () {
					sType = VkStructureType.ApplicationInfo,
					apiVersion = new Vulkan.Version (VK_MAJOR, VK_MINOR, 0),
					pApplicationName = ENGINE_NAME,
					pEngineName = APPLICATION_NAME,
				};

				VkInstanceCreateInfo instanceCreateInfo = default;
				instanceCreateInfo.pApplicationInfo = appInfo;

				if (instanceExtensions.Count > 0) {
					instanceCreateInfo.enabledExtensionCount = (uint)instanceExtensions.Count;
					instanceCreateInfo.ppEnabledExtensionNames = instanceExtensions.Pin (pctx);
				}
				if (enabledLayerNames.Count > 0) {
					instanceCreateInfo.enabledLayerCount = (uint)enabledLayerNames.Count;
					instanceCreateInfo.ppEnabledLayerNames = enabledLayerNames.Pin (pctx);
				}

				VkResult result = vkCreateInstance (ref instanceCreateInfo, IntPtr.Zero, out inst);
				if (result != VkResult.Success)
					throw new InvalidOperationException ("Could not create Vulkan instance. Error: " + result);

				instanceCreateInfo.Dispose();
				appInfo.Dispose();

				Vk.LoadInstanceFunctionPointers (inst);
			}
		}
		public static string[] SupportedExtensions () => SupportedExtensions (IntPtr.Zero);
		public static string[] SupportedExtensions (IntPtr layer) {
			CheckResult (vkEnumerateInstanceExtensionProperties (layer, out uint count, IntPtr.Zero));

			int sizeStruct = Marshal.SizeOf<VkExtensionProperties> ();
			IntPtr ptrSupExts = Marshal.AllocHGlobal (sizeStruct * (int)count);
			CheckResult (vkEnumerateInstanceExtensionProperties (layer, out count, ptrSupExts));

			string[] result = new string[count];
			IntPtr tmp = ptrSupExts;
			for (int i = 0; i < count; i++) {
				result[i] = Marshal.PtrToStringAnsi (tmp);
				tmp += sizeStruct;
			}

			Marshal.FreeHGlobal (ptrSupExts);
			return result;
		}

		public PhysicalDeviceCollection GetAvailablePhysicalDevice () => new PhysicalDeviceCollection (inst);
		/// <summary>
		/// Create a new vulkan surface from native window pointer
		/// </summary>
		public VkSurfaceKHR CreateSurface (IntPtr hWindow) {
			ulong surf;
			CheckResult ((VkResult)Glfw.Glfw3.CreateWindowSurface (inst.Handle, hWindow, IntPtr.Zero, out surf), "Create Surface Failed.");
			return surf;
		}
		public void GetDelegate<T> (string name, out T del) {
			using (FixedUtf8String n = new FixedUtf8String (name)) {
				del = Marshal.GetDelegateForFunctionPointer<T> (vkGetInstanceProcAddr (Handle, (IntPtr)n));
			}
		}

		#region IDisposable Support
		private bool disposedValue = false;

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// TODO: supprimer l'état managé (objets managés).
				} else
					System.Diagnostics.Debug.WriteLine ("Instance disposed by Finalizer");

				vkDestroyInstance (inst, IntPtr.Zero);

				disposedValue = true;
			}
		}

		~Instance () {
			Dispose (false);
		}

		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}
		#endregion
	}
}
