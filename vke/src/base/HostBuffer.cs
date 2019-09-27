// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	
	public class HostBuffer<T> : HostBuffer {
        int TSize;

		public HostBuffer (Device device, VkBufferUsageFlags usage, uint arrayElementCount, bool keepMapped = false, bool coherentMem = true)
            : base (device, usage, (ulong)(Marshal.SizeOf<T> () * arrayElementCount), keepMapped, coherentMem) {
            TSize = Marshal.SizeOf<T>();
        }
		public HostBuffer (Device device, VkBufferUsageFlags usage, IList<T> data, bool keepMapped = false, bool coherentMem = true)
            : base (device, usage, (ulong)(Marshal.SizeOf<T> () * data.Count), keepMapped, coherentMem) {
            TSize = Marshal.SizeOf<T>();
            Map();
            Update (data, createInfo.size);
			if (!keepMapped)
            	Unmap ();
        }
        public HostBuffer (Device device, VkBufferUsageFlags usage, T[] data, bool keepMapped = false, bool coherentMem = true)
            : base (device, usage, (ulong)(Marshal.SizeOf<T> () * data.Length), keepMapped, coherentMem) {
            TSize = Marshal.SizeOf<T>();
            Map();
            Update (data, createInfo.size);
			if (!keepMapped)
            	Unmap ();
        }
		public void Update (T[] data) {
			Update (data, (ulong)(TSize * data.Length));
		}
        public void Update (uint index, T data) {
            GCHandle ptr = GCHandle.Alloc(data, GCHandleType.Pinned);
            unsafe {
                System.Buffer.MemoryCopy(ptr.AddrOfPinnedObject().ToPointer(), (mappedData + (int)(TSize*index)).ToPointer(), TSize, TSize);
            }
            ptr.Free();
        }
        public void Flush (uint startIndex, uint endIndex) {
            VkMappedMemoryRange mr = new VkMappedMemoryRange
            {
                sType = VkStructureType.MappedMemoryRange,
#if MEMORY_POOLS
				memory = memoryPool.vkMemory,
                offset = poolOffset + (ulong)(startIndex * TSize),
#else
				memory = vkMemory,
				offset = (ulong)(startIndex * TSize),
#endif
                size = (ulong)((endIndex - startIndex) * TSize)
            };
            vkFlushMappedMemoryRanges(Dev.VkDev, 1, ref mr);
        }
    }
	/// <summary>
	/// Mappable Buffer with HostVisble and HostCoherent memory flags
	/// </summary>
	public class HostBuffer : Buffer {
        public HostBuffer (Device device, VkBufferUsageFlags usage, UInt64 size, bool keepMapped = false, bool coherentMem = true)
                    : base (device, usage, coherentMem ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent : VkMemoryPropertyFlags.HostVisible, size) {
            if (keepMapped)
                Map();
        }
        public HostBuffer (Device device, VkBufferUsageFlags usage, object data, bool keepMapped = false, bool coherentMem = true)
            : base (device, usage, coherentMem ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent : VkMemoryPropertyFlags.HostVisible, (ulong)Marshal.SizeOf(data)) {
            Map ();
            Update (data, createInfo.size);
			if (!keepMapped)
            	Unmap ();
        }
        public HostBuffer (Device device, VkBufferUsageFlags usage, UInt64 size, IntPtr data, bool keepMapped = false, bool coherentMem = true)
            : base (device, usage, coherentMem ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent : VkMemoryPropertyFlags.HostVisible, size) {
            Map ();
            unsafe {
                System.Buffer.MemoryCopy (data.ToPointer (), mappedData.ToPointer (), size, size);
            }
			if (!keepMapped)
            	Unmap ();
        }
    }	
}
