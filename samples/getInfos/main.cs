// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using System.Linq;
using Vulkan;
using vke;

//very simple compute example that just do an addition on every items of a random list of numbers.
namespace SimpleCompute {
	class Program  {
		static void Main (string[] args) {
			Instance instance = new Instance ();
			PhysicalDevice phy = instance.GetAvailablePhysicalDevice ().FirstOrDefault ();
			/*Device dev = new Device (phy);

			dev.Activate (default (VkPhysicalDeviceFeatures));

			dev.WaitIdle ();

			dev.Dispose ();*/
			foreach (VkFormat format in Enum.GetValues (typeof(VkFormat))) {
				if (phy.TryGetImageFormatProperties (format, VkImageTiling.Optimal,
					VkImageUsageFlags.DepthStencilAttachment, out VkImageFormatProperties props)) {
					Console.WriteLine ($"{format} : max samples {props.sampleCounts}");
				} else {
					Console.WriteLine ($"{format} : NOT SUPPORTED");
				}
			}

			instance.Dispose ();
		}
	}
}
