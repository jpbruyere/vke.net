using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using vke;
using Vulkan;
using static Vulkan.Vk;
using Image = vke.Image;

namespace ExternalMemmories
{
	class VulkanContext : IDisposable {
		public Instance instance;
		public PhysicalDevice phy;
		public Device dev;

		Queue gQ;
		vke.DebugUtils.Messenger dbgmsg;
		public VulkanContext () {

			instance = new Instance (
				new string[] {"VK_LAYER_KHRONOS_validation"},
				new string[] {
					Ext.I.VK_EXT_debug_utils,
					Ext.I.VK_KHR_get_physical_device_properties2,
					Ext.I.VK_KHR_external_memory_capabilities
				}
			);
			dbgmsg = new vke.DebugUtils.Messenger (instance,
				VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT |
				VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT |
				VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				//VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT);

			phy = instance.GetAvailablePhysicalDevice ().FirstOrDefault (p => p.HasSwapChainSupport);

			dev = new Device (phy);
			gQ = new Queue (dev, Vulkan.VkQueueFlags.Graphics);
			VkPhysicalDeviceFeatures features = default;

			dev.Activate (features, new string[] {
				Ext.D.VK_KHR_external_memory,
				Ext.D.VK_EXT_external_memory_host,
				Ext.D.VK_EXT_external_memory_dma_buf,
				Ext.D.VK_KHR_external_memory_fd,
			});
		}
		public bool TryGetImageFormatProperties (VkFormat format, VkImageTiling tiling,
			VkImageUsageFlags usage, out VkImageFormatProperties properties,
			VkImageType type = VkImageType.Image2D, VkImageCreateFlags flags = 0) {
			VkResult result = vkGetPhysicalDeviceImageFormatProperties (phy.Handle, format, type,
				tiling, usage, flags, out properties);
			return result == VkResult.Success;
		}
		public static VkFormatFeatureFlags imageFormatFlags =
			VkFormatFeatureFlags.SampledImage|
			VkFormatFeatureFlags.ColorAttachment|
			VkFormatFeatureFlags.TransferSrc|
			VkFormatFeatureFlags.TransferDst;
		public static VkImageUsageFlags imageUsageFlags =
			//VkImageUsageFlags.Storage|
			VkImageUsageFlags.TransferSrc|
			VkImageUsageFlags.TransferDst;
		public void listFormat () {

			foreach (VkFormat format in Enum.GetValues(typeof(VkFormat))) {
				if (format == VkFormat.G16B16R163plane444UnormKHR)
					break;

				vkGetPhysicalDeviceFormatProperties (phy.Handle, format, out VkFormatProperties props);
				if (props.linearTilingFeatures == (VkFormatFeatureFlags)0 && props.optimalTilingFeatures == (VkFormatFeatureFlags)0 && props.bufferFeatures == (VkFormatFeatureFlags)0)
					continue;
				bool linSupported = (props.linearTilingFeatures & imageFormatFlags) == imageFormatFlags;
				bool optSupported = (props.optimalTilingFeatures & imageFormatFlags) == imageFormatFlags;

				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine ($"{format}");
				Console.ResetColor();


				VkPhysicalDeviceImageFormatInfo2 imgFormatInfo2 = default;
				VkImageFormatProperties2 imgProps2 = default;

				VkPhysicalDeviceExternalImageFormatInfo extImgFormatInfo = default;
				VkExternalImageFormatProperties extProps = default;

				imgFormatInfo2.format = format;
				imgFormatInfo2.tiling = VkImageTiling.Optimal;
				imgFormatInfo2.type = VkImageType.Image2D;
				imgFormatInfo2.usage = imageUsageFlags;
				extImgFormatInfo.handleType = VkExternalMemoryHandleTypeFlags.DmaBufEXT;

				using (PinnedObjects pinCtx = new PinnedObjects()) {
					imgFormatInfo2.pNext = extImgFormatInfo.Pin (pinCtx);
					imgProps2.pNext = extProps.Pin (pinCtx);

					foreach (VkExternalMemoryHandleTypeFlags handleType in Enum.GetValues (typeof(VkExternalMemoryHandleTypeFlags))) {
						extImgFormatInfo.handleType = handleType;

						VkResult res = vkGetPhysicalDeviceImageFormatProperties2 (phy.Handle, ref imgFormatInfo2, out imgProps2);
						if (res == VkResult.Success) {
							Console.WriteLine ($"\t{handleType}: f:{extProps.externalMemoryProperties.externalMemoryFeatures} c:{extProps.externalMemoryProperties.compatibleHandleTypes} e:{extProps.externalMemoryProperties.exportFromImportedHandleTypes}");
						}
					}
				}


				/*if (optSupported) {
					if (TryGetImageFormatProperties (format, VkImageTiling.Optimal, imageUsageFlags, out VkImageFormatProperties imgProps)) {
						Console.Write ($"{imgProps.maxExtent.width + "x"+ imgProps.maxExtent.height,13}");
					} else {
						Console.Write (new string(' ', 13));
					}
				} else
					Console.Write (new string(' ', 13));*/

				Console.Write (linSupported ? " LIN " : new string(' ',5));
				Console.Write (optSupported ? " OPT " : new string(' ',5));
				//Console.Write ((props.bufferFeatures & formatFlags) > 0 ? " BUFF" : new string(' ',5));
				Console.WriteLine (props.bufferFeatures > 0 ? " BUFF" : "");
				Console.WriteLine ();

			}
		}

		public void Dispose()
		{
			dev.Dispose();
			dbgmsg.Dispose();
			instance.Dispose();
		}

		public Image CreateExportableImage (uint width, uint height, VkExternalMemoryHandleTypeFlags handleTypes) {
			Image tmp = new Image (dev, VkFormat.R8g8b8a8Unorm, imageUsageFlags, VkMemoryPropertyFlags.DeviceLocal,
				width, height, handleTypes);
			using (Image stagging = Image.Load (dev, "/mnt/devel/vke.net/datas/textures/texture.jpg", imageUsageFlags)) {
				using (CommandPool cmdPool = gQ.CreateCommandPool ()) {
					PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart(VkCommandBufferUsageFlags.OneTimeSubmit);
					tmp.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal);
					stagging.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferSrcOptimal);
					stagging.BlitTo (cmd, tmp);
					tmp.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferSrcOptimal);
					gQ.EndSubmitAndWait(cmd, true);
				}
			}

			return tmp;
		}

		public void SaveDeviceImage (Image img) {

			using (Image stagging = new Image (dev, VkFormat.R8g8b8a8Unorm, imageUsageFlags, VkMemoryPropertyFlags.HostVisible, img.Width, img.Height, VkImageType.Image2D, VkSampleCountFlags.SampleCount1, VkImageTiling.Linear)) {
				using (CommandPool cmdPool = gQ.CreateCommandPool ()) {
					PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart(VkCommandBufferUsageFlags.OneTimeSubmit);
					img.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferSrcOptimal);
					stagging.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal);
					img.BlitTo (cmd, stagging);
					stagging.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.TransferSrcOptimal);
					gQ.EndSubmitAndWait(cmd, true);
				}

				stagging.Map();

				using (Image<Rgba32> image = new Image<Rgba32>((int)img.Width, (int)img.Height)) {
					if(image.TryGetSinglePixelSpan(out var pixelSpan))					{
						byte[] rgbaBytes = MemoryMarshal.AsBytes(pixelSpan).ToArray();
						System.Runtime.InteropServices.Marshal.Copy (stagging.MappedData, rgbaBytes, 0, rgbaBytes.Length);
						using (var im = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(rgbaBytes, (int)img.Width, (int)img.Height)) {
							im.Save ("test.png");
						}
					}
				}
			}
		}
	}
	class Program
	{
		static void Main(string[] args)
		{

			VulkanContext ctx1 = new VulkanContext();
			VulkanContext ctx2 = new VulkanContext();
			ctx1.listFormat();

			VkExternalMemoryHandleTypeFlags handleType = VkExternalMemoryHandleTypeFlags.HostAllocationEXT;

			Image exportableImg = ctx1.CreateExportableImage (512, 512, handleType);
			ctx1.SaveDeviceImage (exportableImg);

			/*VkMemoryHostPointerPropertiesEXT hostPointerProps = VkMemoryHostPointerPropertiesEXT.New();
			IntPtr handle = (IntPtr)exportableImg.Memory.Handle;
			VkResult res = vkGetMemoryHostPointerPropertiesEXT (ctx1.dev.Handle, handleType, handle,
				out hostPointerProps);*/

			Image importedImg = exportableImg.ExportTo (ctx2.dev, handleType);


			//ctx2.SaveDeviceImage (importedImg);

			importedImg.Dispose();
			exportableImg.Dispose();

			ctx2.Dispose();
			ctx1.Dispose();

		}
	}
}
