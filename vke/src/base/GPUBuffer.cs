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
	/// Device local Buffer for an array of elements of type T.
	/// </summary>
	public class GPUBuffer<T> : GPUBuffer {
		/// <summary>
		/// return the current element count of this buffer.
		/// </summary>
		public int ElementCount { get; private set; }

		/// <summary>
		/// Create an empty new device local buffer with the size needed to store the specified item count of type T.
		/// </summary>
		/// <param name="device">the logical device that will create this buffer.</param>
		/// <param name="usage">bitmask of the intended usages for this buffer</param>
		/// <param name="elementCount">Element count of type T to reserve the space for.</param>
		public GPUBuffer (Device device, VkBufferUsageFlags usage, int elementCount)
            : base (device, usage, (ulong)(Marshal.SizeOf<T> () * elementCount)) {
			ElementCount = elementCount;
        }
		/// <summary>
		/// Create and polulate by copy a new device local buffer.
		/// </summary>
		/// <param name="staggingQ">The managed queue that will be used for the copy of the elements from a temporary stagging buffer to the final device buffer.</param>
		/// <param name="staggingCmdPool">A command pool for the supplied queue.</param>
		/// <param name="usage">bitmask of the intended usages for this buffer</param>
		/// <param name="elements">an array of elements of type T to populate the new device buffer with.</param>
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
