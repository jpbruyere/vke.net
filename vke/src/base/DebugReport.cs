// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	[Obsolete("Use the new VK_EXT_debug_utils extension")]
    public class DebugReport : IDisposable {
        VkDebugReportCallbackEXT handle;
		Instance inst;

        PFN_vkDebugReportCallbackEXT debugCallbackDelegate = new PFN_vkDebugReportCallbackEXT (debugCallback);

        static VkBool32 debugCallback (VkDebugReportFlagsEXT flags, VkDebugReportObjectTypeEXT objectType, ulong obj,
            UIntPtr location, int messageCode, IntPtr pLayerPrefix, IntPtr pMessage, IntPtr pUserData) {
            string prefix = "";
            switch (flags) {
                case 0:
                    prefix = "?";
                    break;
                case VkDebugReportFlagsEXT.InformationEXT:
					Console.ForegroundColor = ConsoleColor.Gray;
					prefix = "INFO";
                    break;
                case VkDebugReportFlagsEXT.WarningEXT:
					Console.ForegroundColor = ConsoleColor.DarkYellow;
					prefix = "WARN";
                    break;
                case VkDebugReportFlagsEXT.PerformanceWarningEXT:
					Console.ForegroundColor = ConsoleColor.Yellow;
					prefix = "PERF";
                    break;
                case VkDebugReportFlagsEXT.ErrorEXT:
					Console.ForegroundColor = ConsoleColor.DarkRed;
					prefix = "EROR";
					break;
                case VkDebugReportFlagsEXT.DebugEXT:
					Console.ForegroundColor = ConsoleColor.Red;
					prefix = "DBUG";
                    break;
            }
			try {
				string msg = Marshal.PtrToStringAnsi (pMessage);
				string[] tmp = msg.Split ('|');
				Console.WriteLine ($"{prefix}:{tmp[1]} |{Marshal.PtrToStringAnsi (pLayerPrefix)}({messageCode}){objectType}:{tmp[0]}");
			} catch (Exception ex) {
				Console.WriteLine ("error parsing debug message: " + ex);
			}
			Console.ForegroundColor = ConsoleColor.White;
            return VkBool32.False;
        }

        public DebugReport (Instance instance, VkDebugReportFlagsEXT flags = VkDebugReportFlagsEXT.ErrorEXT | VkDebugReportFlagsEXT.WarningEXT) {
			inst = instance;

            VkDebugReportCallbackCreateInfoEXT dbgInfo = new VkDebugReportCallbackCreateInfoEXT {
                sType = VkStructureType.DebugReportCallbackCreateInfoEXT,
                flags = flags,
                pfnCallback = Marshal.GetFunctionPointerForDelegate (debugCallbackDelegate)
            };

            CheckResult (vkCreateDebugReportCallbackEXT (inst.Handle, ref dbgInfo, IntPtr.Zero, out handle));
        }

		#region IDisposable Support
		private bool disposedValue = false;

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// TODO: supprimer l'état managé (objets managés).
				}

				vkDestroyDebugReportCallbackEXT (inst.Handle, handle, IntPtr.Zero);

				disposedValue = true;
			}
		}

		~DebugReport () {
			Dispose (false);
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}
		#endregion
	}
}
