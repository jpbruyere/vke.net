// Copyright (c) 2019 Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;

using static Vulkan.Vk;

namespace vke {
#if MEMORY_POOLS
	/// Direct how memory is allocated by a MemoryPool.
	public enum MemoryPoolType {
		/// Not yet implemented.
		Random,
		/// First free space next to the last added ressource will be choosen.
		Linear
	}
	/// <summary>
	/// A memory pool is a single chunck of memory of a kind shared among multiple resources.
	/// </summary>
	public class MemoryPool : IDisposable {
		Device dev;
		internal VkDeviceMemory vkMemory;
		VkMemoryAllocateInfo memInfo = VkMemoryAllocateInfo.New ();
		readonly ulong bufferImageGranularity;
		Resource lastResource;

		IntPtr mappedPointer;
		/// Allocated device size in byte.
		public ulong Size => memInfo.allocationSize;
		/// Return true if pool memory is currently mapped.
		public bool IsMapped => mappedPointer != IntPtr.Zero;
		/// Return mapped memory pointer or null if not mapped.
		public IntPtr MappedData => mappedPointer;
		/// Last added resource, this is the entry element for the double linked list of ressource.
		public Resource Last => lastResource;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:vke.MemoryPool"/> class.
		/// </summary>
		/// <param name="dev">The vulkan device instance associated with this memory pool.</param>
		/// <param name="memoryTypeIndex">Memory type index.</param>
		/// <param name="size">Size</param>
		public MemoryPool (Device dev, uint memoryTypeIndex, UInt64 size) {
			this.dev = dev;
			bufferImageGranularity = dev.phy.Limits.bufferImageGranularity;
			memInfo.allocationSize = size;
			memInfo.memoryTypeIndex = memoryTypeIndex;
			CheckResult (vkAllocateMemory (Dev.Handle, ref memInfo, IntPtr.Zero, out vkMemory));
		}
		/// <summary>
		/// Allocate memory for a new resource in this memory pool.
		/// </summary>
		/// <param name="resource">An <see cref="T:vke.Image"/> or a <see cref="T:vke.Buffer"/> ressource.</param>
		public void Add (Resource resource) {
			resource.memoryPool = this;

			ulong limit = Size;
			ulong offset = 0;
			Resource previous = lastResource;

			if (previous != null) {
				do {
					offset = previous.poolOffset + previous.AllocatedDeviceMemorySize;

					if (previous.IsLinar != resource.IsLinar && offset % bufferImageGranularity > 0)
						offset += bufferImageGranularity - (offset % bufferImageGranularity);
					if (offset % resource.MemoryAlignment > 0)
						offset += resource.MemoryAlignment - (offset % resource.MemoryAlignment);

					if (previous.next == null) {
						if (offset + resource.AllocatedDeviceMemorySize >= limit) {
							offset = 0;
							limit = previous.poolOffset;
						}
						break;
					}

					if (previous.next.poolOffset < previous.poolOffset) {
						limit = Size;
						if (offset + resource.AllocatedDeviceMemorySize < Size)
							break;
						offset = 0;
						limit = previous.next.poolOffset;
					}else
						limit = previous.next.poolOffset;

					if (offset + resource.AllocatedDeviceMemorySize < limit)
						break;

					previous = previous.next;

				} while (previous != lastResource);

			}

			if (offset + resource.AllocatedDeviceMemorySize >= limit)
				throw new Exception ($"Out of Memory pool: {memInfo.memoryTypeIndex}");

			resource.poolOffset = offset;
			resource.previous = previous;
			if (previous != null) {
				if (previous.next == null) {
					resource.next = resource.previous = previous;
					previous.next = previous.previous = resource;
				} else {
					resource.next = previous.next;
					previous.next = resource.next.previous = resource;
				}
			}
			lastResource = resource;

			resource.bindMemory ();
		}
		/// <summary>
		/// Try to reorganize ressources in this pool to optimize memory usage.
		/// </summary>
		public void Defrag () {
			throw new NotImplementedException ();
		}
		/// <summary>
		/// Remove the specified resource.
		/// </summary>
		/// <param name="resource">Resource.</param>
		public void Remove (Resource resource) {
			if (resource == lastResource)
				lastResource = resource.previous;
			if (lastResource != null) {
				if (resource.previous == resource.next)//only 1 resources remaining
					lastResource.next = lastResource.previous = null;
				else {
					resource.previous.next = resource.next;
					resource.next.previous = resource.previous;
				}
			}
 			resource.next = resource.previous = null;
		}/// <summary>
		/// Map the pool's memory at the specified offset.
		/// </summary>
		/// <param name="size">Size.</param>
		/// <param name="offset">Offset.</param>
		public void Map (ulong size = Vk.WholeSize, ulong offset = 0) {
			CheckResult (vkMapMemory (Dev.Handle, vkMemory, offset, size, 0, ref mappedPointer));
		}
		/// <summary>
		/// Unmap previously mapped memory of this pool.
		/// </summary>
		public void Unmap () {
			vkUnmapMemory (Dev.Handle, vkMemory);
			mappedPointer = IntPtr.Zero;
		}

		#region IDisposable Support
		private bool disposedValue;

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					//TODO:should automatically free resources here
				} else
					System.Diagnostics.Debug.WriteLine ("MemoryPool disposed by Finalizer.");

				vkFreeMemory (Dev.Handle, vkMemory, IntPtr.Zero);
				disposedValue = true;
			}
		}

		~MemoryPool() {	
			Dispose(false);
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
#endif
}