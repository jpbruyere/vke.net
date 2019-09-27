// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using Vulkan;

namespace vke {

    /// <summary>
    /// Device local Buffer
    /// </summary>
    public class GPUBuffer : Buffer {
        public GPUBuffer (Device device, VkBufferUsageFlags usage, UInt64 size) 
        : base (device, usage, VkMemoryPropertyFlags.DeviceLocal, size){ 
        }
    }
	/// <summary>
	/// Device local Buffer
	/// </summary>
	public class GPUBuffer<T> : GPUBuffer {

		public int ElementCount { get; private set; }

		public GPUBuffer (Device device, VkBufferUsageFlags usage, int elementCount)
            : base (device, usage, (ulong)(Marshal.SizeOf<T> () * elementCount)) {
			ElementCount = elementCount;
        }
        public GPUBuffer (Queue staggingQ, CommandPool staggingCmdPool, VkBufferUsageFlags usage, T[] elements)
            : base (staggingQ.Dev, usage | VkBufferUsageFlags.TransferDst, (ulong)(Marshal.SizeOf<T> () * elements.Length)) {
			using (HostBuffer<T> stagging = new HostBuffer<T> (Dev, VkBufferUsageFlags.TransferSrc, elements)) { 
				CommandBuffer cmd = staggingCmdPool.AllocateCommandBuffer ();
				cmd.Start (VkCommandBufferUsageFlags.OneTimeSubmit);

				stagging.CopyTo (cmd, this);

				cmd.End ();

				staggingQ.Submit (cmd);
				staggingQ.WaitIdle ();

				cmd.Free ();
			}
        }
    }
}
