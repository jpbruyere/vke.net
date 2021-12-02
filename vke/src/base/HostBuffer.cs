// Copyright (c) 2019-2022  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	/// <summary>
	/// Host visible mappable buffer to handle array of blittable type T
	/// </summary>
	public class HostBuffer<T> : HostBuffer {
		int TSize;
		int elementCount;
		/// <summary>
		/// Create an empty mappable vulkan buffer for elements of type T whith specified size.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="arrayElementCount">Array element count.</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
		public HostBuffer (Device device, VkBufferUsageFlags usage, int arrayElementCount, bool keepMapped = false, bool coherentMem = true)
			: base (device, usage, (ulong)(Marshal.SizeOf<T> () * arrayElementCount), keepMapped, coherentMem) {
			elementCount = arrayElementCount;
			TSize = Marshal.SizeOf<T> ();
		}
		/// <summary>
		/// Create and populate a mappable vulkan buffer.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="data">a list of T implementing the IList interface which will be used to populate the buffer.</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
		public HostBuffer (Device device, VkBufferUsageFlags usage, IList<T> data, bool keepMapped = false, bool coherentMem = true)
			: base (device, usage, (ulong)(Marshal.SizeOf<T> () * data.Count), keepMapped, coherentMem) {
			TSize = Marshal.SizeOf<T> ();
			elementCount = data.Count;
			Map ();
			Update (data, createInfo.size);
			if (!keepMapped)
				Unmap ();
		}
		public HostBuffer (Device device, VkBufferUsageFlags usage, T data, bool keepMapped = false, bool coherentMem = true)
			: base (device, usage, (ulong)(Marshal.SizeOf<T> ()), keepMapped, coherentMem) {
			TSize = Marshal.SizeOf<T> ();
			elementCount = 1;
			Map ();
			this.AsSpan()[0] = data;
			if (!keepMapped)
				Unmap ();
		}
		/// <summary>
		/// Create and populate a mappable vulkan buffer.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="data">an array  of T which will be used to populate the buffer.</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
		public HostBuffer (Device device, VkBufferUsageFlags usage, T[] data, bool keepMapped = false, bool coherentMem = true)
			: base (device, usage, (ulong)(Marshal.SizeOf<T> () * data.Length), keepMapped, coherentMem) {
			TSize = Marshal.SizeOf<T> ();
			Map ();
			Update (data, createInfo.size);
			if (!keepMapped)
				Unmap ();
		}
		/// <summary>
		/// Update content of the buffer with array of given length with no offset.
		/// </summary>
		/// <param name="data">Data.</param>
		public void Update (T[] data) {
			Update (data, (ulong)(TSize * data.Length));
		}
		/// <summary>
		/// Update a single T element in the buffer at specified index.
		/// </summary>
		/// <param name="index">0 based index of the T element in the array.</param>
		/// <param name="data">new value for T to set in the buffer.</param>
		public void Update (uint index, T data) {
			GCHandle ptr = GCHandle.Alloc (data, GCHandleType.Pinned);
			unsafe {
				System.Buffer.MemoryCopy (ptr.AddrOfPinnedObject ().ToPointer (), (mappedData + (int)(TSize * index)).ToPointer (), TSize, TSize);
			}
			ptr.Free ();
		}
		/// <summary>
		/// Flush memory, note that coherent memory desn't need it.
		/// </summary>
		/// <param name="startIndex">index of the first T element to flush</param>
		/// <param name="endIndex">index of the last T element to flush</param>
		public void Flush (uint startIndex, uint endIndex) {
			//TODO: vulkan has some alignement constrains on flushing!
			VkMappedMemoryRange mr = new VkMappedMemoryRange {
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
			vkFlushMappedMemoryRanges (Dev.Handle, 1, ref mr);
		}
		/// <summary>
		/// Retrieve a Span&lt;T&gt; on native memory of the buffer. Automatically Map it if not yet done.
		/// </summary>
		/// <returns>Span&lt;T&gt; on native memory valid as long as the buffer is mapped</returns>
		public Span<T> AsSpan () {
			if (!IsMapped)
				Map();
			unsafe {
				return new Span<T>(mappedData.ToPointer(), elementCount);
			}
		}
		/// <summary>
		/// Retrieve a Span&lt;T&gt; on native memory of the buffer. Automatically Map it if not yet done.
		/// </summary>
		/// <param name="startIndex">start index in the native memory</param>
		/// <returns>Span&lt;T&gt; on native memory valid as long as the buffer is mapped</returns>
		public Span<T> AsSpan (int startIndex) {
			if (!IsMapped)
				Map();
			unsafe {
				return new Span<T>(mappedData.ToPointer(), elementCount).Slice (startIndex);
			}
		}
		/// <summary>
		/// Retrieve a Span&lt;T&gt; on native memory of the buffer. Automatically Map it if not yet done.
		/// </summary>
		/// <param name="startIndex">start index in the native memory</param>
		/// <param name="lenght">leght of the span to return</param>
		/// <returns>Span&lt;T&gt; on native memory valid as long as the buffer is mapped</returns>
		public Span<T> AsSpan (int startIndex, int lenght) {
			if (!IsMapped)
				Map();
			unsafe {
				return new Span<T>(mappedData.ToPointer(), elementCount).Slice (startIndex, lenght);
			}
		}
	}
	/// <summary>
	/// Mappable Buffer with HostVisble and HostCoherent memory flags
	/// </summary>
	public class HostBuffer : Buffer {
		/// <summary>
		/// Create an empty mappable vulkan buffer whith specified size in byte.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="size">buffer memory size in byte</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
		public HostBuffer (Device device, VkBufferUsageFlags usage, UInt64 size, bool keepMapped = false, bool coherentMem = true)
					: base (device, usage, coherentMem ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent : VkMemoryPropertyFlags.HostVisible, size) {
			if (keepMapped)
				Map ();
		}
		/// <summary>
		/// Create a mappable vulkan buffer whith the supplied object.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="data">Object to set as content of the buffer. It must be blittable to be able to compute size automatically.</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
		public HostBuffer (Device device, VkBufferUsageFlags usage, object data, bool keepMapped = false, bool coherentMem = true)
			: base (device, usage, coherentMem ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent : VkMemoryPropertyFlags.HostVisible, (ulong)Marshal.SizeOf (data)) {
			Map ();
			Update (data, createInfo.size);
			if (!keepMapped)
				Unmap ();
		}
		/// <summary>
		/// Create a mappable vulkan buffer of specified size whith a data pointer.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">Buffer Usage.</param>
		/// <param name="size">Size in byte of the datas supplied with the pointer.</param>
		/// <param name="data">Managed pointer which point to the data to copy in the buffer at startup.</param>
		/// <param name="keepMapped">If set to <c>true</c>, buffer will stay mapped after the constructor.</param>
		/// <param name="coherentMem">If set to <c>true</c> vulkan memory with have the coherent flag.</param>
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
