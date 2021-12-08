// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {

	/// <summary>
	/// Base class for managed vulkan buffer objects
	/// </summary>
	public class Buffer : Resource {
		internal VkBuffer handle;
		protected VkBufferCreateInfo createInfo;
		/// <summary>Native handle of this vulkan buffer.</summary>
		/// <value>The handle.</value>
		public VkBuffer Handle => handle;
		public VkBufferCreateInfo Infos => createInfo;
		/// <summary>Buffer memory is always linear.</summary>
		public override bool IsLinar => true;

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Buffer, handle.Handle);
		#region CTORS
		/// <summary>
		/// Create a vulkan buffer and automatically activate it. Automatic activation on startup implies to explicitly dispose the buffer.
		/// </summary>
		/// <param name="device">Logical Device.</param>
		/// <param name="usage">a bitmask specifying allowed usages of the buffer</param>
		/// <param name="_memoryPropertyFlags">Memory property flags.</param>
		/// <param name="size">Desired size in byte of the buffer to be created.</param>
		/// <param name="sharingMode">value specifying the sharing mode of the buffer when it will be accessed by multiple queue familie</param>
		public Buffer (Device device, VkBufferUsageFlags usage, VkMemoryPropertyFlags _memoryPropertyFlags, UInt64 size, VkSharingMode sharingMode = VkSharingMode.Exclusive)
		: base (device, _memoryPropertyFlags) {

			createInfo.size = size;
			createInfo.usage = usage;
			createInfo.sharingMode = VkSharingMode.Exclusive;

			Activate ();
		}
		#endregion

		/// <summary>
		/// Activate this vulkan buffer. Note that buffers are automatically activated on creation.
		/// </summary>
		public sealed override void Activate () {
			if (state != ActivableState.Activated) {
				CheckResult (vkCreateBuffer (Dev.Handle, ref createInfo, IntPtr.Zero, out handle));
#if MEMORY_POOLS
				Dev.resourceManager.Add (this);
#else
				updateMemoryRequirements ();
				allocateMemory ();
				bindMemory ();
#endif
			}
			base.Activate ();
		}

		#region Implement abstract members of the Resource abstract class.
		internal override void updateMemoryRequirements () {
			vkGetBufferMemoryRequirements (Dev.Handle, handle, out memReqs);
		}

		internal override void bindMemory () {
#if MEMORY_POOLS
			CheckResult (vkBindBufferMemory (Dev.Handle, handle, memoryPool.vkMemory, poolOffset));
#else
			CheckResult (vkBindBufferMemory (Dev.Handle, handle, vkMemory, 0));
#endif
		}
		#endregion

		/// <summary>
		/// Get a default buffer descriptor for the full size with no offset.
		/// </summary>
		/// <value>the vulkan buffer descriptor</value>
		public VkDescriptorBufferInfo Descriptor { get => GetDescriptor (); }
		/// <summary>
		/// Get a buffer descriptor.
		/// </summary>
		/// <returns>a vulkan buffer descriptor</returns>
		/// <param name="size">Size in byte of the buffer view</param>
		/// <param name="offset">an offset in the buffer memory at which point this descriptor.</param>
		public VkDescriptorBufferInfo GetDescriptor (ulong size = WholeSize, ulong offset = 0) =>
			new VkDescriptorBufferInfo { buffer = handle, range = size, offset = offset };

		/// <summary>
		/// Copy a vulkan buffer to an Image.
		/// </summary>
		/// <param name="cmd">a command buffer to handle the operation.</param>
		/// <param name="img">The Image to copy the buffer to.</param>
		/// <param name="finalLayout">The final layout to setup for the destination image.</param>
		public void CopyTo (CommandBuffer cmd, Image img, VkImageLayout finalLayout = VkImageLayout.ShaderReadOnlyOptimal) {
			img.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal);

			VkBufferImageCopy bufferCopyRegion = new VkBufferImageCopy {
				imageExtent = img.CreateInfo.extent,
				imageSubresource = new VkImageSubresourceLayers (VkImageAspectFlags.Color)
			};

			vkCmdCopyBufferToImage (cmd.Handle, handle, img.handle, VkImageLayout.TransferDstOptimal, 1, ref bufferCopyRegion);

			img.SetLayout (cmd, VkImageAspectFlags.Color, finalLayout);
		}
		/// <summary>
		/// Copy a vulkan buffer to another buffer.
		/// </summary>
		/// <param name="cmd">a command buffer to handle the operation.</param>
		/// <param name="buff">the destination buffer.</param>
		/// <param name="size">size of the copy operation in byte.</param>
		/// <param name="srcOffset">a source offset for the copy operation.</param>
		/// <param name="dstOffset">an offset in the destination buffer for the copy operation.</param>
		public void CopyTo (CommandBuffer cmd, Buffer buff, ulong size = 0, ulong srcOffset = 0, ulong dstOffset = 0) {
			VkBufferCopy bufferCopy = new VkBufferCopy {
				size = (size == 0) ? createInfo.size : size,
				srcOffset = srcOffset,
				dstOffset = dstOffset
			};
			vkCmdCopyBuffer (cmd.Handle, handle, buff.handle, 1, ref bufferCopy);
		}
		/// <summary>
		/// Fill a vulkan buffer memory with an unsinged integer value.
		/// </summary>
		/// <param name="cmd">a command buffer to handle the operation.</param>
		/// <param name="data">an unsigned integer to fill the buffer with.</param>
		/// <param name="size">size in byte to fill.</param>
		/// <param name="offset">an offset in byte in the buffer for the fill operation.</param>
		public void Fill (CommandBuffer cmd, uint data, ulong size = 0, ulong offset = 0) {
			vkCmdFillBuffer (cmd.Handle, handle, offset, (size == 0) ? AllocatedDeviceMemorySize : size, data);
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString ("x")}]");
		}

		#region IDisposable Support
		/// <summary>
		/// Destroy the native handle and set the Activable's state to `Disposed`.
		/// </summary>
		/// <remarks>Note that Buffers have to be always explicitly disposed on cleanup.</remarks>
		/// <param name="disposing">If set to <c>true</c>, this object is currently disposed by the user, not the finalizer.</param>
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				base.Dispose (disposing);
				vkDestroyBuffer (Dev.Handle, handle, IntPtr.Zero);
			}
			state = ActivableState.Disposed;
		}
		#endregion
	}
}
