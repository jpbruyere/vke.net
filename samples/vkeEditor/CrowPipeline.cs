// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Threading;
using Glfw;
using vke;
using Vulkan;
using Image = vke.Image;

namespace vkeEditor {

	public class CrowPipeline : FSQPipeline {
		public CrowPipeline (VkWindow window, PrimaryCommandBuffer cmdUpdateCrow, RenderPass renderPass, PipelineLayout pipelineLayout, int attachment = 0, PipelineCache pipelineCache = null)
		: base  (renderPass, pipelineLayout, attachment, pipelineCache) {

			this.cmdUpdateCrow = cmdUpdateCrow;
			this.window = window;

			startCrow ();
		}


		#region Crow interface
		PrimaryCommandBuffer cmdUpdateCrow;
		public Image crowImage;
		public HostBuffer crowBuffer;
		Crow.Interface iFace;
		volatile bool running;
		VkWindow window;

		void startCrow () {
			iFace = new Crow.Interface ((int)window.Width, (int)window.Height, window.WindowHandle);
			iFace.Init ();
			iFace.Load ("#ui.testImage.crow").DataSource = this;

			Thread ui = new Thread (crowThread);
			ui.IsBackground = true;
			ui.Start ();
		}
		void crowThread () {
			while (iFace.surf == null) {
				Thread.Sleep (10);
			}
			running = true;
			while (running) {
				iFace.Update ();
				Thread.Sleep (10);
			}
		}
		public void releaseCrowUpdateMutex () {
			if (Monitor.IsEntered (iFace.UpdateMutex))
				Monitor.Exit (iFace.UpdateMutex);
		}
		public void initCrowSurface (DescriptorSet descriptorSet, VkDescriptorSetLayoutBinding binding) {
			lock (iFace.UpdateMutex) {
				iFace.surf?.Dispose ();
				crowImage?.Dispose ();
				crowBuffer?.Dispose ();

				crowBuffer = new HostBuffer (Dev, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, window.Width * window.Height * 4, true);

				crowImage = new Image (Dev, VkFormat.B8g8r8a8Unorm, VkImageUsageFlags.Sampled,
					VkMemoryPropertyFlags.DeviceLocal, window.Width, window.Height, VkImageType.Image2D, VkSampleCountFlags.SampleCount1, VkImageTiling.Linear);
				crowImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
				crowImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);
				crowImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
				//uiImage.Map ();

//				NotifyValueChanged ("uiImage", crowImage);

				DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, binding);
				uboUpdate.Write (Dev, crowImage.Descriptor);

				iFace.surf = new Crow.Cairo.ImageSurface (crowBuffer.MappedData, Crow.Cairo.Format.ARGB32,
					(int)window.Width, (int)window.Height, (int)window.Width * 4);
			}
			iFace.ProcessResize (new Crow.Rectangle (0, 0, (int)window.Width, (int)window.Height));
		}

		public void updateCrow (Queue queue, Fence drawFence) {
			if (iFace.IsDirty) {
				drawFence.Wait ();
				drawFence.Reset ();
				Monitor.Enter (iFace.UpdateMutex);
				queue.Submit (cmdUpdateCrow, default, default, drawFence);
				iFace.IsDirty = false;
			}
		}
		/// <summary>
		/// command buffer must have been reseted
		/// </summary>
		public void recordUpdateCrowCmd () {
			cmdUpdateCrow.Start ();
			crowImage.SetLayout (cmdUpdateCrow, VkImageAspectFlags.Color,
				VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
				VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.Transfer);

			crowBuffer.CopyTo (cmdUpdateCrow, crowImage, VkImageLayout.ShaderReadOnlyOptimal);

			crowImage.SetLayout (cmdUpdateCrow, VkImageAspectFlags.Color,
				VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
				VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
			cmdUpdateCrow.End ();
		}
		#endregion

		public bool OnMouseMove (double xPos, double yPos) {
			iFace.OnMouseMove ((int)xPos, (int)yPos);
			return (iFace.HoverWidget != null);
		}
		public bool OnMouseButtonDown (MouseButton button) => iFace.OnMouseButtonDown (button);
		public bool OnMouseButtonUp (MouseButton button) => iFace.OnMouseButtonUp (button);
		public bool OnKeyUp (Key key) => iFace.OnKeyUp (key);
		public bool OnKeyDown (Key key) => iFace.OnKeyDown (key);
		public bool OnKeyPress (Char c) => iFace.OnKeyPress (c);


		protected override void Dispose (bool disposing) {
			running = false;
			crowImage?.Dispose ();
			crowBuffer?.Dispose ();
			iFace.Dispose ();

			base.Dispose (disposing);
		}
	}

}
