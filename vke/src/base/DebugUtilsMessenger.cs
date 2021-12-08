// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using System.Text;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke.DebugUtils {
	/// <summary>
	/// Dispoable vke class that encapsulate a VkDebugUtilsMessengerEXT object
	/// </summary>
	public class Messenger : IDisposable {
		Instance inst;
		readonly VkDebugUtilsMessengerEXT handle;
		readonly static PFN_vkDebugUtilsMessengerCallbackEXT onMessage = new PFN_vkDebugUtilsMessengerCallbackEXT(HandlePFN_vkDebugUtilsMessengerCallbackEXT);

		static VkBool32 HandlePFN_vkDebugUtilsMessengerCallbackEXT (VkDebugUtilsMessageSeverityFlagsEXT messageSeverity, VkDebugUtilsMessageTypeFlagsEXT messageTypes, IntPtr pCallbackData, IntPtr pUserData) {
			VkDebugUtilsMessengerCallbackDataEXT data = Marshal.PtrToStructure<VkDebugUtilsMessengerCallbackDataEXT> (pCallbackData);
			ConsoleColor curColor = Console.ForegroundColor;

			switch (messageSeverity) {
			case VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT:
				Console.ForegroundColor = ConsoleColor.White;
				break;
			case VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT:
				Console.ForegroundColor = ConsoleColor.DarkCyan;
				break;
			case VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT:
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				break;
			case VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT:
				Console.ForegroundColor = ConsoleColor.Red;
				break;
			}

			switch (messageTypes) {
			case VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT:
				Console.Write ("GEN:");
				break;
			case VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT:
				Console.Write ("PERF:");
				break;
			}

			Span<byte> tmp = stackalloc byte [2048];

			if (data.pMessage != IntPtr.Zero) {
				byte b = Marshal.ReadByte (data.pMessage);
				int i = 1;
				while (b != 0 && i<tmp.Length) {
					tmp [i] = b;
					b = Marshal.ReadByte (data.pMessage, i);
					i++;
				}
				Console.WriteLine (Encoding.UTF8.GetString(tmp));
				Console.ForegroundColor = curColor;
			}
			return false;
		}
		/// <summary>
		/// Create a new debug utils messenger providing a PFN_vkDebugUtilsMessengerCallbackEXT delegate.
		/// </summary>
		/// <param name="instance">Vulkan Instance.</param>
		/// <param name="onMessageDelegate">Message callback.</param>
		/// <param name="typeMask">Type mask.</param>
		/// <param name="severityMask">Severity mask.</param>
		public Messenger (Instance instance, PFN_vkDebugUtilsMessengerCallbackEXT onMessageDelegate,
			VkDebugUtilsMessageTypeFlagsEXT typeMask =
				VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT,
			VkDebugUtilsMessageSeverityFlagsEXT severityMask =
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT) {
			inst = instance;
			VkDebugUtilsMessengerCreateInfoEXT info = default;
			info.messageType = typeMask;
			info.messageSeverity = severityMask;
			info.pfnUserCallback = Marshal.GetFunctionPointerForDelegate (onMessageDelegate);
			info.pUserData = IntPtr.Zero;

			CheckResult (vkCreateDebugUtilsMessengerEXT (inst.VkInstance, ref info, IntPtr.Zero, out handle));
		}
		/// <summary>
		/// Create a new debug utils messenger with default message callback outputing to Console.
		/// </summary>
		/// <param name="instance">Vulkan Instance.</param>
		/// <param name="typeMask">Type mask.</param>
		/// <param name="severityMask">Severity mask.</param>
		public Messenger (Instance instance,
			VkDebugUtilsMessageTypeFlagsEXT typeMask =
				VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT,
			VkDebugUtilsMessageSeverityFlagsEXT severityMask =
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT) : this (instance, onMessage, typeMask, severityMask) {
        }

		#region IDisposable Support
		private bool disposedValue;

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// TODO: supprimer l'état managé (objets managés).
				}

				vkDestroyDebugUtilsMessengerEXT (inst.Handle, handle, IntPtr.Zero);

				disposedValue = true;
			}
		}

		~Messenger () {
			Dispose (false);
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}
		#endregion
	}
}
