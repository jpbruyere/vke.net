// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Command pools are opaque objects that command buffer memory is allocated from, and which allow the implementation
	/// to amortize the cost of resource creation across multiple command buffers.
	/// <para>
	/// See <see href="https://www.khronos.org/registry/vulkan/specs/1.2-extensions/man/html/VkCommandPool.html"/> for more information.
	/// </para>
	/// </summary>
	public sealed class CommandPool : Activable {
        public readonly uint QFamIndex;
		public readonly VkCommandPoolCreateFlags Flags;
        VkCommandPool handle;

		#region CTORS
		/// <summary>
		/// Create and activate a new Command Pool.
		/// </summary>
		/// <param name="device">Vulkan Device.</param>
		/// <param name="qFamIdx">Queue family index.</param>
		/// <param name="flags">Command pool <see cref="VkCommandPoolCreateFlags">creation flags</see>.</param>
		public CommandPool (Device device, uint qFamIdx, VkCommandPoolCreateFlags flags = 0) : base(device)
        {
            QFamIndex = qFamIdx;
			Flags = flags;

			Activate ();
        }
		/// <summary>
		/// Initializes a new instance of the <see cref="CommandPool"/>" class.
		/// </summary>
		/// <param name="queue">Device <see cref="Queue"/> of the queue family to create the pool for.</param>
		/// <param name="flags">Command pool <see cref="VkCommandPoolCreateFlags">creation flags</see>.</param>
		public CommandPool (Queue queue, VkCommandPoolCreateFlags flags = 0) : this(queue.dev, queue.qFamIndex, flags) {}
		#endregion

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.CommandPool, handle.Handle);

		public override void Activate () {
			if (state != ActivableState.Activated) {
        	    VkCommandPoolCreateInfo infos = default;
    	        infos.queueFamilyIndex = QFamIndex;
				infos.flags = Flags;
	            CheckResult (vkCreateCommandPool (Dev.Handle, ref infos, IntPtr.Zero, out handle));
			}
			base.Activate ();
		}
		/// <summary>
		/// Allocates single primary command buffer.
		/// When command buffers are first allocated, they are in the initial state.
		/// </summary>
		/// <returns>The command buffer in the Init state.</returns>
		public PrimaryCommandBuffer AllocateCommandBuffer () {
            VkCommandBuffer buff;
            VkCommandBufferAllocateInfo infos = default;
            infos.commandPool = handle;
            infos.level = VkCommandBufferLevel.Primary;
            infos.commandBufferCount = 1;

            CheckResult (vkAllocateCommandBuffers (Dev.Handle, ref infos, out buff));

            return new PrimaryCommandBuffer (Dev.Handle, this, buff);
        }
		/// <summary>
		/// Allocates single primary command buffer.
		/// When command buffers are first allocated, they are in the initial state.
		/// </summary>
		/// <returns>The command buffer in the Init state.</returns>
		public SecondaryCommandBuffer AllocateSecondaryCommandBuffer () {
			VkCommandBuffer buff;
			VkCommandBufferAllocateInfo infos = default;
			infos.commandPool = handle;
			infos.level = VkCommandBufferLevel.Secondary;
			infos.commandBufferCount = 1;

			CheckResult (vkAllocateCommandBuffers (Dev.Handle, ref infos, out buff));

			return new SecondaryCommandBuffer (Dev.Handle, this, buff);
		}

		/// <summary>
		/// Allocates multiple command buffer.
		/// </summary>
		/// <returns>An array of command buffers alloocated from this pool.</returns>
		/// <param name="count">Buffer count to create.</param>
		public PrimaryCommandBuffer[] AllocateCommandBuffer (uint count) {
			VkCommandBufferAllocateInfo infos = default;
			infos.commandPool = handle;
			infos.level = VkCommandBufferLevel.Primary;
			infos.commandBufferCount = count;
			VkCommandBuffer[] buffs = new VkCommandBuffer[count];
			CheckResult (vkAllocateCommandBuffers (Dev.Handle, ref infos, buffs.Pin()));
			buffs.Unpin ();
			PrimaryCommandBuffer[] cmds = new PrimaryCommandBuffer[count];
			for (int i = 0; i < count; i++)
				cmds[i] = new PrimaryCommandBuffer (Dev.Handle, this, buffs[i]);

			return cmds;
		}
		/// <summary>
		/// Resetting a command pool recycles all of the resources from all of the command buffers allocated from the command
		/// pool back to the command pool. All command buffers that have been allocated from the command pool are put in the initial state.
		/// Any primary command buffer allocated from another VkCommandPool that is in the recording or executable state and has a secondary
		/// command buffer allocated from commandPool recorded into it, becomes invalid.
		/// </summary>
		/// <param name="flags">Set `ReleaseResources` flag to recycles all of the resources from the command pool back to the system.</param>
		public void Reset (VkCommandPoolResetFlags flags = 0) {
			Vk.vkResetCommandPool (Dev.Handle, handle, flags);
		}
		/// <summary>
		/// Allocates a new command buffer and automatically start it.
		/// </summary>
		/// <returns>New command buffer in the recording state.</returns>
		/// <param name="usage">Usage.</param>
		public PrimaryCommandBuffer AllocateAndStart (VkCommandBufferUsageFlags usage = 0) {
			PrimaryCommandBuffer cmd = AllocateCommandBuffer ();
			cmd.Start (usage);
			return cmd;
		}
		/// <summary>
		/// Any primary command buffer that is in the recording or executable state and has any element of the command buffer list recorded into it, becomes invalid.
		/// </summary>
		/// <param name="cmds">Command buffer list to free.</param>
		public void FreeCommandBuffers (params CommandBuffer[] cmds) {
            if (cmds.Length == 1) {
                VkCommandBuffer hnd = cmds[0].Handle;
                vkFreeCommandBuffers (Dev.Handle, handle, 1, ref hnd);
                return;
            }
			int sizeElt = Marshal.SizeOf<IntPtr> ();
			IntPtr cmdsPtr = Marshal.AllocHGlobal (cmds.Length * sizeElt);
			int count = 0;
			for (int i = 0; i < cmds.Length; i++) {
				if (cmds[i] == null)
					continue;
				Marshal.WriteIntPtr (cmdsPtr + count * sizeElt, cmds[i].Handle.Handle);
				count++;
			}
			if (count > 0)
				vkFreeCommandBuffers (Dev.Handle, handle, (uint)count, cmdsPtr);

			Marshal.FreeHGlobal (cmdsPtr);
        }

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (!disposing)
				System.Diagnostics.Debug.WriteLine ("VKE CommandPool disposed by finalizer");
			if (state == ActivableState.Activated)
				vkDestroyCommandPool (Dev.Handle, handle, IntPtr.Zero);
			base.Dispose (disposing);
		}
		#endregion

	}
}
