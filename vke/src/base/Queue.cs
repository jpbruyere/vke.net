// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;

using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {

	public class PresentQueue : Queue {
		public readonly VkSurfaceKHR Surface;

		public PresentQueue (Device _dev, VkQueueFlags requestedFlags, VkSurfaceKHR _surface, float _priority = 0.0f) {
			dev = _dev;
			priority = _priority;
			Surface = _surface;

			qFamIndex = searchQFamily (requestedFlags);
			dev.queues.Add (this);
		}

		uint searchQFamily (VkQueueFlags requestedFlags) {
			//search for dedicated Q
			for (uint i = 0; i < dev.phy.QueueFamilies.Length; i++) {
				if (dev.phy.QueueFamilies[i].queueFlags == requestedFlags && dev.phy.GetPresentIsSupported (i, Surface))
					return i;
			}
			//search Q having flags
			for (uint i = 0; i < dev.phy.QueueFamilies.Length; i++) {
				if ((dev.phy.QueueFamilies[i].queueFlags & requestedFlags) == requestedFlags && dev.phy.GetPresentIsSupported (i, Surface))
					return i;
			}

			throw new Exception (string.Format ("No Queue with flags {0} found", requestedFlags));
		}

		public void Present (VkPresentInfoKHR present) {
			CheckResult (vkQueuePresentKHR (handle, ref present));
		}
		public void Present (SwapChain swapChain, VkSemaphore wait) {
			VkPresentInfoKHR present = default;

			uint idx = swapChain.currentImageIndex;
			VkSwapchainKHR sc = swapChain.Handle;
			present.pSwapchains = sc;
			present.pImageIndices = idx;
			present.pWaitSemaphores = wait;

			vkQueuePresentKHR (handle, ref present);

			present.Dispose();
		}
	}

	public class Queue {

		protected VkQueue handle;
		internal Device dev;
		public Device Dev => dev;

		VkQueueFlags flags => dev.phy.QueueFamilies[qFamIndex].queueFlags;
		public uint qFamIndex;
		public uint index;//index in queue family
		public float priority;
		public VkQueue Handle => handle;

		protected Queue () { }
		public Queue (Device _dev, VkQueueFlags requestedFlags, float _priority = 0.0f) {
			dev = _dev;
			priority = _priority;

			qFamIndex = searchQFamily (requestedFlags);
			dev.queues.Add (this);
		}
		public CommandPool CreateCommandPool (VkCommandPoolCreateFlags flags = 0)
			=> new CommandPool (dev, qFamIndex, flags);
		/// <summary>
		/// End command recording, submit, and wait queue idle
		/// </summary>
		public void EndSubmitAndWait (PrimaryCommandBuffer cmd, bool freeCommandBuffer = false) {
			cmd.End ();
			Submit (cmd);
			WaitIdle ();
			if (freeCommandBuffer)
				cmd.Free ();
		}
		public void Submit (PrimaryCommandBuffer cmd, VkSemaphore wait = default, VkSemaphore signal = default, Fence fence = null) {
			cmd.Submit (handle, wait, signal, fence);
		}
		public void WaitIdle () {
			CheckResult (vkQueueWaitIdle (handle));
		}

		uint searchQFamily (VkQueueFlags requestedFlags) {
			//search for dedicated Q
			for (uint i = 0; i < dev.phy.QueueFamilies.Length; i++) {
				if (dev.phy.QueueFamilies[i].queueFlags == requestedFlags)
					return i;
			}
			//search Q having flags
			for (uint i = 0; i < dev.phy.QueueFamilies.Length; i++) {
				if ((dev.phy.QueueFamilies[i].queueFlags & requestedFlags) == requestedFlags)
					return i;
			}

			throw new Exception (string.Format ("No Queue with flags {0} found", requestedFlags));
		}

		internal void updateHandle () {
			vkGetDeviceQueue (Dev.Handle, qFamIndex, index, out handle);
		}
	}
}
