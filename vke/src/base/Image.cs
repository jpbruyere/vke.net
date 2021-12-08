// Copyright (c) 2019-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Buffers;
using System.Diagnostics;
using Vulkan;

using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Combined Image/Descriptor class. Optional Sampler and View are disposed with the vkImage. If multiple view/sampler have to be
	/// created for the same vkImage, you may call the constructor accepting a vkImage as parameter to import an existing one. vkImage handle of
	/// such imported image will not be disposed with the sampler and the view.
	/// </summary>
	public class Image : Resource {
#if STB_SHARP
		public static bool USE_STB_SHARP = true;
#else
		public static bool USE_STB_SHARP = false;
#endif
		/// <summary>Default format to use if not defined by constructor parameters.</summary>
		public static VkFormat DefaultTextureFormat = VkFormat.R8g8b8a8Unorm;

		internal VkImage handle;
		VkImageCreateInfo info;
		uint[] queuesFamillies;

		/// <summary>
		/// if true, the vkImage handle will not be destroyed on dispose, useful to create image for swapchain
		/// </summary>
		bool imported;

		public VkDescriptorImageInfo Descriptor;
		/// <summary>Get the create info structure used for creating this image.</summary>
		public VkImageCreateInfo CreateInfo => info;
		/// <summary>Get the dimensions in pixel for this image</summary>
		public VkExtent3D Extent => info.extent;
		/// <summary>Get image format</summary>
		public VkFormat Format => info.format;
		/// <summary>Native vulkan handle for the image.</summary>
		public VkImage Handle => handle;
		/// <summary>Width in pixel of the image.</summary>
		public uint Width => CreateInfo.extent.width;
		/// <summary>Height in pixel of the image.</summary>
		public uint Height => CreateInfo.extent.height;
		/// <summary>Boolean indicating if memory allocated for this image has linear or optimal tiling.</summary>
		public override bool IsLinar => CreateInfo.tiling == VkImageTiling.Linear;
		/// <summary>
		/// May be used to query the last known layout. It is set when commands explicitly secify the final layout of the image.
		/// </summary>
		/// <remarks>Due to the automatic layout trasitions handled by several vulkan commands, use this property with caution, it's value
		/// may not be the actual layout of the image.</remarks>
		public VkImageLayout lastKnownLayout { get; private set; }

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Image, handle.Handle);

		#region CTORS
		/// <summary>
		/// Create a new Image.
		/// </summary>
		/// <remarks>Initial layout will be automatically set to Undefined if tiling is optimal and Preinitialized if tiling is linear.</remarks>
		/// <param name="device">The logical device that create the image.</param>
		/// <param name="format">format and type of the texel blocks that will be contained in the image</param>
		/// <param name="usage">bitmask describing the intended usage of the image.</param>
		/// <param name="_memoryPropertyFlags">Memory property flags.</param>
		/// <param name="width">number of data in the X dimension of the image.</param>
		/// <param name="height">number of data in the Y dimension of the image.</param>
		/// <param name="type">value specifying the basic dimensionality of the image. Layers in array textures do not count as a dimension for the purposes of the image type.</param>
		/// <param name="samples">number of sample per texel.</param>
		/// <param name="tiling">tiling arrangement of the texel blocks in memory.</param>
		/// <param name="mipsLevels">describes the number of levels of detail available for minified sampling of the image.</param>
		/// <param name="layers">number of layers in the image.</param>
		/// <param name="depth">number of data in the Z dimension of the image</param>
		/// <param name="createFlags">bitmask describing additional parameters of the image.</param>
		/// <param name="sharingMode">value specifying the sharing mode of the image when it will be accessed by multiple queue families.</param>
		/// <param name="queuesFamillies">list of queue families that will access this image (ignored if sharingMode is not CONCURRENT).</param>
		public Image (Device device, VkFormat format, VkImageUsageFlags usage, VkMemoryPropertyFlags _memoryPropertyFlags,
			uint width, uint height,
			VkImageType type = VkImageType.Image2D, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1,
			VkImageTiling tiling = VkImageTiling.Optimal, uint mipsLevels = 1, uint layers = 1, uint depth = 1,
			VkImageCreateFlags createFlags = 0, VkSharingMode sharingMode = VkSharingMode.Exclusive, params uint[] queuesFamillies)
			: this (device, format, usage, _memoryPropertyFlags, width, height, 0, type, samples, tiling,
					mipsLevels, layers, depth, createFlags, sharingMode, queuesFamillies) {	}
		/// <summary>
		/// Create a new exportable Image, see VK_KHR_external_memory for more information.
		/// </summary>
		/// <remarks>
		/// if exportHandleType is not 0, image will be exportable depending on the HandleType bitmask provided.
		/// Initial layout will be automatically set to Undefined if tiling is optimal and Preinitialized if tiling is linear.
		/// </remarks>
		/// <param name="device">The logical device that create the image.</param>
		/// <param name="format">format and type of the texel blocks that will be contained in the image</param>
		/// <param name="usage">bitmask describing the intended usage of the image.</param>
		/// <param name="_memoryPropertyFlags">Memory property flags.</param>
		/// <param name="width">number of data in the X dimension of the image.</param>
		/// <param name="height">number of data in the Y dimension of the image.</param>
		/// <param name="exportHandleType">zero, or a bitmask of @ref VkExternalMemoryHandleTypeFlags specifying one or more external memory handle types.</param>
		/// <param name="type">value specifying the basic dimensionality of the image. Layers in array textures do not count as a dimension for the purposes of the image type.</param>
		/// <param name="samples">number of sample per texel.</param>
		/// <param name="tiling">tiling arrangement of the texel blocks in memory.</param>
		/// <param name="mipsLevels">describes the number of levels of detail available for minified sampling of the image.</param>
		/// <param name="layers">number of layers in the image.</param>
		/// <param name="depth">number of data in the Z dimension of the image</param>
		/// <param name="createFlags">bitmask describing additional parameters of the image.</param>
		/// <param name="sharingMode">value specifying the sharing mode of the image when it will be accessed by multiple queue families.</param>
		/// <param name="queuesFamillies">list of queue families that will access this image (ignored if sharingMode is not CONCURRENT).</param>
		public Image (Device device, VkFormat format, VkImageUsageFlags usage, VkMemoryPropertyFlags _memoryPropertyFlags,
			uint width, uint height, VkExternalMemoryHandleTypeFlags exportHandleType,
			VkImageType type = VkImageType.Image2D, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1,
			VkImageTiling tiling = VkImageTiling.Optimal, uint mipsLevels = 1, uint layers = 1, uint depth = 1,
			VkImageCreateFlags createFlags = 0, VkSharingMode sharingMode = VkSharingMode.Exclusive, params uint[] queuesFamillies)
			: base (device, _memoryPropertyFlags) {

			info.imageType = type;
			info.format = format;
			info.extent.width = width;
			info.extent.height = height;
			info.extent.depth = depth;
			info.mipLevels = mipsLevels;
			info.arrayLayers = layers;
			info.samples = samples;
			info.tiling = tiling;
			info.usage = usage;
			info.initialLayout = (tiling == VkImageTiling.Optimal) ? VkImageLayout.Undefined : VkImageLayout.Preinitialized;
			info.sharingMode = sharingMode;
			info.flags = createFlags;

			this.queuesFamillies = queuesFamillies;
			lastKnownLayout = info.initialLayout;
			this.importExportHandleTypes = exportHandleType;

			Activate ();
		}
		public Image (Device device, VkMemoryPropertyFlags memoryProperties, VkImageCreateInfo info, params uint[] queuesFamillies) :
		base (device, memoryProperties) {
			this.info = info;
		}
		/// <summary>
		/// Import vkImage handle into a new Image class, native handle will be preserve on destruction.
		/// </summary>
		public Image (Device device, VkImage vkHandle, VkFormat format, VkImageUsageFlags usage, uint width, uint height)
		: base (device, VkMemoryPropertyFlags.DeviceLocal) {
			info.imageType = VkImageType.Image2D;
			info.format = format;
			info.extent.width = width;
			info.extent.height = height;
			info.extent.depth = 1;
			info.mipLevels = 1;
			info.arrayLayers = 1;
			info.samples = VkSampleCountFlags.SampleCount1;
			info.tiling = VkImageTiling.Optimal;
			info.usage = usage;

			handle = vkHandle;
			imported = true;

			state = ActivableState.Activated;
			references++;
		}
		#endregion

		public static uint ComputeMipLevels (uint size) => (uint)Math.Floor (Math.Log (size)) + 1;
		public static uint ComputeMipLevels (int width, int height) => (uint)Math.Floor (Math.Log (Math.Max (width, height))) + 1;

		/// <summary>
		/// Check if specified image usage is supported for the given physical image format.
		/// </summary>
		/// <returns><c>true</c>, if usage is supported</returns>
		/// <param name="usage">bitmask of intend usage for an image.</param>
		/// <param name="phyFormatSupport">Physical format feature bitmask as returned by `GetFormatProperties` of PhysicalDevice</param>
		public static bool CheckFormatIsSupported (VkImageUsageFlags usage, VkFormatFeatureFlags phyFormatSupport) {
			if (usage.HasFlag (VkImageUsageFlags.TransferSrc) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.TransferSrc))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.TransferDst) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.TransferSrc))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.Sampled) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.SampledImage))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.Storage) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.StorageImage))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.ColorAttachment) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.ColorAttachment))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.DepthStencilAttachment) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.DepthStencilAttachment))
				return false;
			/*if (usage.HasFlag (VkImageUsageFlags.TransientAttachment) ^ phyFormatSupport.HasFlag (VkFormatFeatureFlags.))
				return false;*/
			if (usage.HasFlag (VkImageUsageFlags.InputAttachment) & !phyFormatSupport.HasFlag (VkFormatFeatureFlags.SampledImage))
				return false;
			/*if (usage.HasFlag (VkImageUsageFlags.ShadingRateImageNV) ^ phyFormatSupport.HasFlag (VkFormatFeatureFlags.TransferSrc))
				return false;
			if (usage.HasFlag (VkImageUsageFlags.FragmentDensityMapEXT) ^ phyFormatSupport.HasFlag (VkFormatFeatureFlags.TransferSrc))
				return false;*/
			return true;
		}
		/// <summary>
		/// Load image from byte array containing full image file (jpg, png,...)
		/// </summary>
		public static Image Load (Device dev, Queue staggingQ, CommandPool staggingCmdPool,
			Memory<byte> bitmap, VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.DeviceLocal,
			VkImageTiling tiling = VkImageTiling.Optimal, bool generateMipmaps = true,
			VkImageType imageType = VkImageType.Image2D,
			VkImageUsageFlags usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst) {

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;
			if (tiling == VkImageTiling.Optimal)
				usage |= VkImageUsageFlags.TransferDst;
			if (generateMipmaps)
				usage |= (VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

			using (StbImage stbi = new StbImage (bitmap)) {
				uint mipLevels = generateMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.load (staggingQ, staggingCmdPool, stbi.Handle, generateMipmaps);

				return img;
			}
		}
		/// <summary>
		/// create host visible linear image without command from data pointed by IntPtr pointer containing full image file (jpg, png,...)
		/// </summary>
		public static Image Load (Device dev,
			Memory<byte> bitmap, ulong bitmapByteCount, VkImageUsageFlags usage = VkImageUsageFlags.TransferSrc,
			VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
			VkImageTiling tiling = VkImageTiling.Linear, bool generateMipmaps = false,
			VkImageType imageType = VkImageType.Image2D) {

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;
			if (generateMipmaps)
				usage |= (VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

			using (StbImage stbi = new StbImage (bitmap)) {
				uint mipLevels = generateMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.Map ();
				stbi.CoptyTo (img.MappedData);
				img.Unmap ();

				return img;
			}
		}

		/// <summary>
		/// Load image from byte array containing full image file (jpg, png,...)
		/// </summary>
		public static Image Load (Device dev, Queue staggingQ, CommandPool staggingCmdPool,
			byte[] bitmap, VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.DeviceLocal,
			VkImageTiling tiling = VkImageTiling.Optimal, bool generateMipmaps = true,
			VkImageType imageType = VkImageType.Image2D,
			VkImageUsageFlags usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst) {

			Image img = Load (dev, staggingQ, staggingCmdPool, bitmap.Pin (), (ulong)bitmap.Length, format, memoryProps, tiling, generateMipmaps,
				imageType, usage);
			bitmap.Unpin ();
			return img;
		}

		#region bitmap loading
		/// <summary>
		/// Load image from data pointed by IntPtr pointer containing full image file (jpg, png,...)
		/// </summary>
		public static Image Load (Device dev, Queue staggingQ, CommandPool staggingCmdPool,
			IntPtr bitmap, ulong bitmapByteCount, VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.DeviceLocal,
			VkImageTiling tiling = VkImageTiling.Optimal, bool generateMipmaps = true,
			VkImageType imageType = VkImageType.Image2D,
			VkImageUsageFlags usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst) {

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;
			if (tiling == VkImageTiling.Optimal)
				usage |= VkImageUsageFlags.TransferDst;
			if (generateMipmaps)
				usage |= (VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

			using (StbImage stbi = new StbImage (bitmap, bitmapByteCount)) {
				uint mipLevels = generateMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.load (staggingQ, staggingCmdPool, stbi.Handle, generateMipmaps);

				return img;
			}
		}

		/// <summary>
		/// Load bitmap into Image with stagging and mipmap generation if necessary
		/// and usage.
		/// </summary>
		public static Image Load (Queue staggingQ, CommandPool staggingCmdPool,
			string path, VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.DeviceLocal,
			VkImageTiling tiling = VkImageTiling.Optimal, bool generateMipmaps = true,
			VkImageType imageType = VkImageType.Image2D,
			VkImageUsageFlags usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst) {

			Device dev = staggingQ.dev;

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;
			if (tiling == VkImageTiling.Optimal)
				usage |= VkImageUsageFlags.TransferDst;
			if (generateMipmaps)
				usage |= (VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

			using (StbImage stbi = new StbImage (path)) {
				uint mipLevels = generateMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.load (staggingQ, staggingCmdPool, stbi.Handle, generateMipmaps);

				return img;
			}
		}

		/// <summary>
		/// create host visible linear image without command from path
		/// </summary>
		public static Image Load (Device dev,
			string path, VkImageUsageFlags usage = VkImageUsageFlags.Sampled, bool reserveSpaceForMipmaps = true, VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
			VkImageTiling tiling = VkImageTiling.Linear, VkImageType imageType = VkImageType.Image2D) {

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;

			using (StbImage stbi = new StbImage (path)) {
				uint mipLevels = reserveSpaceForMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.Map ();
				stbi.CoptyTo (img.MappedData);
				img.Unmap ();

				return img;
			}
		}
		/// <summary>
		/// create host visible linear image without command from byte array
		/// </summary>
		public static Image Load (Device dev,
			byte[] bitmap, VkImageUsageFlags usage = VkImageUsageFlags.TransferSrc,
			VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
			VkImageTiling tiling = VkImageTiling.Linear, bool generateMipmaps = false,
			VkImageType imageType = VkImageType.Image2D) {

			Image img = Load (dev, bitmap.Pin (), (ulong)bitmap.Length, usage, format, memoryProps, tiling, generateMipmaps,
				imageType);
			bitmap.Unpin ();
			return img;
		}
		/// <summary>
		/// create host visible linear image without command from data pointed by IntPtr pointer containing full image file (jpg, png,...)
		/// </summary>
		public static Image Load (Device dev,
			IntPtr bitmap, ulong bitmapByteCount, VkImageUsageFlags usage = VkImageUsageFlags.TransferSrc,
			VkFormat format = VkFormat.Undefined,
			VkMemoryPropertyFlags memoryProps = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
			VkImageTiling tiling = VkImageTiling.Linear, bool generateMipmaps = false,
			VkImageType imageType = VkImageType.Image2D) {

			if (format == VkFormat.Undefined)
				format = DefaultTextureFormat;
			if (generateMipmaps)
				usage |= (VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

			using (StbImage stbi = new StbImage (bitmap, bitmapByteCount)) {
				uint mipLevels = generateMipmaps ? ComputeMipLevels (stbi.Width, stbi.Height) : 1;

				Image img = new Image (dev, format, usage, memoryProps, (uint)stbi.Width, (uint)stbi.Height, imageType,
					VkSampleCountFlags.SampleCount1, tiling, mipLevels);

				img.Map ();
				stbi.CoptyTo (img.MappedData);
				img.Unmap ();

				return img;
			}
		}

		/// <summary>
		/// load bitmap from pointer
		/// </summary>
		void load (Queue staggingQ, CommandPool staggingCmdPool, IntPtr bitmap, bool generateMipmaps = true) {
			long size = info.extent.width * info.extent.height * 4 * info.extent.depth;

			if (MemoryFlags.HasFlag (VkMemoryPropertyFlags.HostVisible)) {
				Map ();
				unsafe {
					System.Buffer.MemoryCopy (bitmap.ToPointer (), MappedData.ToPointer (), size, size);
				}
				Unmap ();

				if (generateMipmaps)
					BuildMipmaps (staggingQ, staggingCmdPool);
			} else {
				using (HostBuffer stagging = new HostBuffer (Dev, VkBufferUsageFlags.TransferSrc, (UInt64)size, bitmap)) {

					PrimaryCommandBuffer cmd = staggingCmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);

					stagging.CopyTo (cmd, this);
					if (generateMipmaps)
						BuildMipmaps (cmd);

					cmd.End ();
					staggingQ.Submit (cmd);
					staggingQ.WaitIdle ();
					cmd.Free ();
				}
			}
		}

		#endregion

		internal override void updateMemoryRequirements () {
			vkGetImageMemoryRequirements (Dev.Handle, handle, out memReqs);
		}
		internal override void bindMemory () {
#if MEMORY_POOLS
			CheckResult (vkBindImageMemory (Dev.Handle, handle, memoryPool.vkMemory, poolOffset));
#else
			CheckResult (vkBindImageMemory (Dev.Handle, handle, vkMemory, 0));
#endif
		}
		public sealed override void Activate () {
			if (state != ActivableState.Activated) {
				VkExternalMemoryImageCreateInfo externalImgInfo = default;
				if (importExportHandleTypes > 0 && importedHandle != IntPtr.Zero) {
					externalImgInfo.handleTypes = importExportHandleTypes;
					info.pNext = externalImgInfo.Pin ();
				}

				if (info.sharingMode == VkSharingMode.Concurrent && queuesFamillies?.Length > 0) {
					info.queueFamilyIndexCount = (uint)queuesFamillies.Length;
					info.pQueueFamilyIndices = queuesFamillies;
					CheckResult (vkCreateImage (Dev.Handle, ref info, IntPtr.Zero, out handle));
				} else
					CheckResult (vkCreateImage (Dev.Handle, ref info, IntPtr.Zero, out handle));

				if (importExportHandleTypes > 0 && importedHandle != IntPtr.Zero)
					externalImgInfo.Unpin ();

				info.pNext = IntPtr.Zero;
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

		public Image ExportTo (Device targetdev, VkExternalMemoryHandleTypeFlags handleTypes) {
			VkMemoryHostPointerPropertiesEXT hostPointerProps = default;
			VkResult res = vkGetMemoryHostPointerPropertiesEXT (Dev.Handle, handleTypes, importedHandle, out hostPointerProps);
			if (res != VkResult.Success)
				return null;
			Image img = new Image (targetdev, memoryFlags, this.info, queuesFamillies);
			img.importExportHandleTypes = handleTypes;
			img.importedHandle = (IntPtr)Memory.Handle;
			img.importedMemoryTypeBits = hostPointerProps.memoryTypeBits;
			img.Activate ();
			return img;
		}

		public void CreateView (VkImageViewType type = VkImageViewType.ImageView2D, VkImageAspectFlags aspectFlags = VkImageAspectFlags.Color,
			int layerCount = -1, uint baseMipLevel = 0, int levelCount = -1, uint baseArrayLayer = 0,
			VkComponentSwizzle r = VkComponentSwizzle.R,
			VkComponentSwizzle g = VkComponentSwizzle.G,
			VkComponentSwizzle b = VkComponentSwizzle.B,
			VkComponentSwizzle a = VkComponentSwizzle.A) {

			if (type == VkImageViewType.ImageView2D)
				layerCount = 1;

			VkImageView view = default (VkImageView);
			VkImageViewCreateInfo viewInfo = default;
			viewInfo.image = handle;
			viewInfo.viewType = type;
			viewInfo.format = Format;
			viewInfo.components.r = r;
			viewInfo.components.g = g;
			viewInfo.components.b = b;
			viewInfo.components.a = a;
			viewInfo.subresourceRange.aspectMask = aspectFlags;
			viewInfo.subresourceRange.baseMipLevel = baseMipLevel;
			viewInfo.subresourceRange.levelCount = levelCount < 0 ? info.mipLevels : (uint)levelCount;
			viewInfo.subresourceRange.baseArrayLayer = baseArrayLayer;
			viewInfo.subresourceRange.layerCount = layerCount < 0 ? info.arrayLayers : (uint)layerCount;

			CheckResult (vkCreateImageView (Dev.Handle, ref viewInfo, IntPtr.Zero, out view));

			if (Descriptor.imageView.Handle != 0)
				Dev.DestroyImageView (Descriptor.imageView);
			Descriptor.imageView = view;
		}

		public void CreateSampler (VkSamplerAddressMode addressMode, VkFilter minFilter = VkFilter.Linear,
				VkFilter magFilter = VkFilter.Linear, VkSamplerMipmapMode mipmapMode = VkSamplerMipmapMode.Linear,
				float maxAnisotropy = 1.0f, float minLod = 0.0f, float maxLod = -1f) {
			CreateSampler (minFilter, magFilter, mipmapMode, addressMode, maxAnisotropy, minLod, maxLod);
		}
		/// <summary>
		/// Create a Sampler and store it into the Descriptor structure of this image.
		/// </summary>
		/// <param name="minFilter">Minimum filter.</param>
		/// <param name="magFilter">Mag filter.</param>
		/// <param name="mipmapMode">Mipmap mode.</param>
		/// <param name="addressMode">Address mode.</param>
		/// <param name="maxAnisotropy">Max anisotropy.</param>
		/// <param name="minLod">Minimum lod.</param>
		/// <param name="maxLod">Max lod.</param>
		public void CreateSampler (VkFilter minFilter = VkFilter.Linear, VkFilter magFilter = VkFilter.Linear,
							   VkSamplerMipmapMode mipmapMode = VkSamplerMipmapMode.Linear, VkSamplerAddressMode addressMode = VkSamplerAddressMode.Repeat,
			float maxAnisotropy = 1.0f, float minLod = 0.0f, float maxLod = -1f) {
			VkSampler sampler;
			VkSamplerCreateInfo sampInfo = default;
			sampInfo.maxAnisotropy = maxAnisotropy;
			sampInfo.maxAnisotropy = 1.0f;// device->enabledFeatures.samplerAnisotropy ? device->properties.limits.maxSamplerAnisotropy : 1.0f;
										  //samplerInfo.anisotropyEnable = device->enabledFeatures.samplerAnisotropy;
			sampInfo.addressModeU = addressMode;
			sampInfo.addressModeV = addressMode;
			sampInfo.addressModeW = addressMode;
			sampInfo.magFilter = magFilter;
			sampInfo.minFilter = minFilter;
			sampInfo.mipmapMode = mipmapMode;
			sampInfo.minLod = minLod;
			sampInfo.maxLod = maxLod < 0f ? info.mipLevels > 1 ? info.mipLevels : 0 : maxLod;
			sampInfo.compareOp = VkCompareOp.Never;
			sampInfo.borderColor = VkBorderColor.FloatOpaqueWhite;

			CheckResult (vkCreateSampler (Dev.Handle, ref sampInfo, IntPtr.Zero, out sampler));

			if (Descriptor.sampler.Handle != 0)
				Dev.DestroySampler (Descriptor.sampler);
			Descriptor.sampler = sampler;
		}

		public void SetLayout (CommandBuffer cmdbuffer,
			VkImageAspectFlags aspectMask,
			VkImageLayout newImageLayout) {
			SetLayout (cmdbuffer, aspectMask, lastKnownLayout, newImageLayout, lastKnownLayout.GetDefaultStage (), newImageLayout.GetDefaultStage ());
		}
		public void SetLayout (CommandBuffer cmdbuffer,
			VkImageAspectFlags aspectMask,
			VkImageLayout oldImageLayout,
			VkImageLayout newImageLayout) {
			SetLayout (cmdbuffer, aspectMask, oldImageLayout, newImageLayout, oldImageLayout.GetDefaultStage (), newImageLayout.GetDefaultStage ());
		}
		public void SetLayout (
			CommandBuffer cmdbuffer,
			VkImageAspectFlags aspectMask,
			VkImageLayout oldImageLayout,
			VkImageLayout newImageLayout,
			VkPipelineStageFlags srcStageMask,
			VkPipelineStageFlags dstStageMask) {
			VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange {
				aspectMask = aspectMask,
				baseMipLevel = 0,
				levelCount = CreateInfo.mipLevels,
				layerCount = CreateInfo.arrayLayers,
			};
			SetLayout (cmdbuffer, oldImageLayout, newImageLayout, subresourceRange, srcStageMask, dstStageMask);
		}
		public void SetLayout (
			CommandBuffer cmdbuffer,
			VkImageAspectFlags aspectMask,
			VkAccessFlags srcAccessMask,
			VkAccessFlags dstAccessMask,
			VkImageLayout oldImageLayout,
			VkImageLayout newImageLayout,
			VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands,
			VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands,
			uint srcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			uint dstQueueFamilyIndex = Vk.QueueFamilyIgnored) {
			VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange {
				aspectMask = aspectMask,
				baseMipLevel = 0,
				levelCount = CreateInfo.mipLevels,
				layerCount = CreateInfo.arrayLayers,
			};
			SetLayout (cmdbuffer, srcAccessMask, dstAccessMask, oldImageLayout, newImageLayout, subresourceRange, srcStageMask, dstStageMask,
				srcQueueFamilyIndex, dstQueueFamilyIndex);
		}
		public void SetLayout (
			CommandBuffer cmdbuffer,
			VkAccessFlags srcAccessMask,
			VkAccessFlags dstAccessMask,
			VkImageLayout oldImageLayout,
			VkImageLayout newImageLayout,
			VkImageSubresourceRange subresourceRange,
			VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands,
			VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands,
			uint srcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			uint dstQueueFamilyIndex = Vk.QueueFamilyIgnored) {

			VkImageMemoryBarrier imageMemoryBarrier = default;
			imageMemoryBarrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
			imageMemoryBarrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
			imageMemoryBarrier.oldLayout = oldImageLayout;
			imageMemoryBarrier.newLayout = newImageLayout;
			imageMemoryBarrier.image = handle;
			imageMemoryBarrier.subresourceRange = subresourceRange;
			imageMemoryBarrier.srcAccessMask = srcAccessMask;
			imageMemoryBarrier.dstAccessMask = dstAccessMask;

			Vk.vkCmdPipelineBarrier (
				cmdbuffer.Handle,
				srcStageMask,
				dstStageMask,
				0,
				0, IntPtr.Zero,
				0, IntPtr.Zero,
				1, ref imageMemoryBarrier);

		}
		// Create an image memory barrier for changing the layout of
		// an image and put it into an active command buffer
		// See chapter 11.4 "Image Layout" for details
		public void SetLayout (
			CommandBuffer cmdbuffer,
			VkImageLayout oldImageLayout,
			VkImageLayout newImageLayout,
			VkImageSubresourceRange subresourceRange,
			VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands,
			VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands,
			uint srcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			uint dstQueueFamilyIndex = Vk.QueueFamilyIgnored) {
			// Create an image barrier object
			VkImageMemoryBarrier imageMemoryBarrier = default;
			imageMemoryBarrier.srcQueueFamilyIndex = srcQueueFamilyIndex;
			imageMemoryBarrier.dstQueueFamilyIndex = dstQueueFamilyIndex;
			imageMemoryBarrier.oldLayout = oldImageLayout;
			imageMemoryBarrier.newLayout = newImageLayout;
			imageMemoryBarrier.image = handle;
			imageMemoryBarrier.subresourceRange = subresourceRange;

			// Source layouts (old)
			// Source access mask controls actions that have to be finished on the old layout
			// before it will be transitioned to the new layout
			switch (oldImageLayout) {
			case VkImageLayout.Undefined:
				// Image layout is undefined (or does not matter)
				// Only valid as initial layout
				// No flags required, listed only for completeness
				imageMemoryBarrier.srcAccessMask = 0;
				break;

			case VkImageLayout.Preinitialized:
				// Image is preinitialized
				// Only valid as initial layout for linear images, preserves memory contents
				// Make sure host writes have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite;
				break;

			case VkImageLayout.ColorAttachmentOptimal:
				// Image is a color attachment
				// Make sure any writes to the color buffer have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite;
				break;

			case VkImageLayout.DepthStencilAttachmentOptimal:
				// Image is a depth/stencil attachment
				// Make sure any writes to the depth/stencil buffer have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
				break;

			case VkImageLayout.TransferSrcOptimal:
				// Image is a transfer source 
				// Make sure any reads from the image have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
				break;

			case VkImageLayout.TransferDstOptimal:
				// Image is a transfer destination
				// Make sure any writes to the image have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite;
				break;

			case VkImageLayout.ShaderReadOnlyOptimal:
				// Image is read by a shader
				// Make sure any shader reads from the image have been finished
				imageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead;
				break;
			}

			// Target layouts (new)
			// Destination access mask controls the dependency for the new image layout
			switch (newImageLayout) {
			case VkImageLayout.TransferDstOptimal:
				// Image will be used as a transfer destination
				// Make sure any writes to the image have been finished
				imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite;
				break;

			case VkImageLayout.TransferSrcOptimal:
				// Image will be used as a transfer source
				// Make sure any reads from and writes to the image have been finished
				//imageMemoryBarrier.srcAccessMask |= VkAccessFlags.TransferRead;
				imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead;
				break;

			case VkImageLayout.ColorAttachmentOptimal:
				// Image will be used as a color attachment
				// Make sure any writes to the color buffer have been finished
				imageMemoryBarrier.srcAccessMask = oldImageLayout == VkImageLayout.ShaderReadOnlyOptimal ? VkAccessFlags.ShaderRead : VkAccessFlags.TransferRead;
				imageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite;
				break;

			case VkImageLayout.DepthStencilAttachmentOptimal:
				// Image layout will be used as a depth/stencil attachment
				// Make sure any writes to depth/stencil buffer have been finished
				imageMemoryBarrier.dstAccessMask = imageMemoryBarrier.dstAccessMask | VkAccessFlags.DepthStencilAttachmentWrite;
				break;

			case VkImageLayout.ShaderReadOnlyOptimal:
				// Image will be read in a shader (sampler, input attachment)
				// Make sure any writes to the image have been finished
				if (imageMemoryBarrier.srcAccessMask == 0) {
					imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
				}
				imageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;
				break;
			}

			// Put barrier inside setup command buffer
			Vk.vkCmdPipelineBarrier (
				cmdbuffer.Handle,
				srcStageMask,
				dstStageMask,
				0,
				0, IntPtr.Zero,
				0, IntPtr.Zero,
				1, ref imageMemoryBarrier);

			lastKnownLayout = newImageLayout;
		}

		public void BuildMipmaps (Queue copyQ, CommandPool copyCmdPool) {
			if (info.mipLevels == 1) {
				Debug.WriteLine ("Invoking BuildMipmaps on image that has only one mipLevel");
				return;
			}
			PrimaryCommandBuffer cmd = copyCmdPool.AllocateCommandBuffer ();

			cmd.Start (VkCommandBufferUsageFlags.OneTimeSubmit);
			BuildMipmaps (cmd);
			cmd.End ();

			copyQ.Submit (cmd);
			copyQ.WaitIdle ();

			cmd.Free ();
		}
		/// <summary>
		/// Build mipmap chain for this image. Final layout will be ShaderReadOnlyOptimal.
		/// </summary>
		/// <param name="cmd">a command buffer to handle the operation.</param>
		public void BuildMipmaps (CommandBuffer cmd) {

			VkImageSubresourceRange mipSubRange = new VkImageSubresourceRange (VkImageAspectFlags.Color, 0, 1, 0, info.arrayLayers);
			SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal);

			for (int i = 1; i < info.mipLevels; i++) {
				VkImageBlit imageBlit = new VkImageBlit {
					srcSubresource = new VkImageSubresourceLayers (VkImageAspectFlags.Color, info.arrayLayers, (uint)i - 1),
					srcOffsets_1 = new VkOffset3D ((int)info.extent.width >> (i - 1), (int)info.extent.height >> (i - 1), 1),
					dstSubresource = new VkImageSubresourceLayers (VkImageAspectFlags.Color, info.arrayLayers, (uint)i),
					dstOffsets_1 = new VkOffset3D ((int)info.extent.width >> i, (int)info.extent.height >> i, 1)
				};

				SetLayout (cmd, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal, mipSubRange,
					VkPipelineStageFlags.Transfer, VkPipelineStageFlags.Transfer);
				vkCmdBlitImage (cmd.Handle, handle, VkImageLayout.TransferSrcOptimal, handle, VkImageLayout.TransferDstOptimal, 1, ref imageBlit, VkFilter.Linear);
				mipSubRange.baseMipLevel = (uint)i;
			}
			SetLayout (cmd, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal, mipSubRange,
					VkPipelineStageFlags.Transfer, VkPipelineStageFlags.Transfer);
			SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
					VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
		}
		/// <summary>
		/// Blit this image into another.
		/// </summary>
		/// <param name="cmd">a command buffer to handle the blit operation.</param>
		/// <param name="dest">the destination image to blit to.</param>
		/// <param name="filter">filtering for the blit operation.</param>
		public void BlitTo (CommandBuffer cmd, Image dest, VkFilter filter = VkFilter.Linear) {
			VkImageBlit imageBlit = new VkImageBlit {
				srcSubresource = new VkImageSubresourceLayers (VkImageAspectFlags.Color, info.arrayLayers, 0),
				srcOffsets_1 = new VkOffset3D ((int)info.extent.width, (int)info.extent.height, 1),
				dstSubresource = new VkImageSubresourceLayers (VkImageAspectFlags.Color, info.arrayLayers, 0),
				dstOffsets_1 = new VkOffset3D ((int)dest.info.extent.width, (int)dest.info.extent.height, 1)
			};
			vkCmdBlitImage (cmd.Handle, handle, VkImageLayout.TransferSrcOptimal, dest.handle, VkImageLayout.TransferDstOptimal, 1, ref imageBlit, filter);
		}
		public VkSubresourceLayout GetSubresourceLayout (VkImageAspectFlags aspectMask = VkImageAspectFlags.Color, uint mipLevel = 0, uint arrayLayer = 0) {
			VkImageSubresource subresource = new VkImageSubresource {
				aspectMask = aspectMask,
				mipLevel = mipLevel,
				arrayLayer = arrayLayer
			};
			vkGetImageSubresourceLayout (Dev.Handle, this.handle, ref subresource, out VkSubresourceLayout result);
			return result;
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString ("x")}]");
		}
		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (Descriptor.sampler.Handle != 0)
					Dev.DestroySampler (Descriptor.sampler);
				if (Descriptor.imageView.Handle != 0)
					Dev.DestroyImageView (Descriptor.imageView);
				if (!imported) {
					base.Dispose (disposing);
					Dev.DestroyImage (handle);
				}
			}
			state = ActivableState.Disposed;
		}
		#endregion
	}
}
