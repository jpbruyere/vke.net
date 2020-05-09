// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using vke;
using Vulkan;
using Crow;
using System.Threading;

namespace vke {
	/// <summary>
	/// Vulkan context with Crow enabled window.
	/// Crow vector drawing is handled with Cairo Image on an Host mapped vulkan image.
	/// This is an easy way to have GUI in my samples with low GPU cost. Most of the ui
	/// is cached on cpu memory images.
	/// </summary>
	public class CrowWindow : VkWindow, IValueChange {
		#region IValueChange implementation
		public event EventHandler<Crow.ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value)
		{
			ValueChanged?.Invoke (this, new ValueChangeEventArgs (MemberName, _value));
		}
		#endregion

		public bool MouseIsInInterface =>
			iFace.HoverWidget != null;

		FSQPipeline fsqPl;
		DescriptorPool dsPool;
		DescriptorSet descSet;
		CommandPool cmdPoolCrow;
		PrimaryCommandBuffer cmdUpdateCrow;
		Image crowImage;
		HostBuffer crowBuffer;
		Interface iFace;
		protected RenderPass renderPass;

		volatile bool running;


		VkDescriptorSetLayoutBinding dslBinding = new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler);

		protected override void initVulkan () {
			base.initVulkan ();

			iFace = new Interface ((int)Width, (int)Height, WindowHandle);
			iFace.Init ();

			CreateRenderPass ();

			fsqPl = new FSQPipeline (renderPass,
				new PipelineLayout (dev, new DescriptorSetLayout (dev, dslBinding)));

			cmdPoolCrow = new CommandPool (presentQueue, VkCommandPoolCreateFlags.ResetCommandBuffer);
			cmdUpdateCrow = cmdPoolCrow.AllocateCommandBuffer ();

			dsPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler));
			descSet = dsPool.Allocate (fsqPl.Layout.DescriptorSetLayouts[0]);

			Thread ui = new Thread (crowThread);
			ui.IsBackground = true;
			ui.Start ();
		}
		protected virtual void CreateRenderPass () {
			renderPass = new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), VkSampleCountFlags.SampleCount1);
		}


		protected override void onMouseMove (double xPos, double yPos)
		{
			if (iFace.OnMouseMove ((int)xPos, (int)yPos))
				return;
			base.onMouseMove (xPos, yPos);
		}
		protected override void onMouseButtonDown (MouseButton button) {
			if (iFace.OnMouseButtonDown (button))
				return;
			base.onMouseButtonDown (button);
		}
		protected override void onMouseButtonUp (MouseButton button)
		{
			if (iFace.OnMouseButtonUp (button))
				return;
			base.onMouseButtonUp (button);
		}
		protected override void onChar (CodePoint cp) {
			if (iFace.OnKeyPress (cp.ToChar()))
				return;
			base.onChar (cp);
		}
		protected override void onKeyUp (Key key, int scanCode, Modifier modifiers) {
			if (iFace.OnKeyUp (key))
				return;
			base.onKeyUp (key, scanCode, modifiers);
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			if (iFace.OnKeyDown (key))
				return;
			base.onKeyDown (key, scanCode, modifiers);
		}

		protected override void render () {

			int idx = swapChain.GetNextImage ();
			if (idx < 0) {
				OnResize ();
				return;
			}

			if (cmds[idx] == null)
				return;

			drawFence.Wait ();
			drawFence.Reset ();

			if (Monitor.IsEntered (iFace.UpdateMutex))
				Monitor.Exit (iFace.UpdateMutex);

			presentQueue.Submit (cmds[idx], swapChain.presentComplete, drawComplete[idx], drawFence);
			presentQueue.Present (swapChain, drawComplete[idx]);
		}

		public override void Update () {
			if (iFace.IsDirty) {
				drawFence.Wait ();
				drawFence.Reset ();
				Monitor.Enter (iFace.UpdateMutex);
				presentQueue.Submit (cmdUpdateCrow, default, default, drawFence);
				iFace.IsDirty = false;
			}

			NotifyValueChanged ("fps", fps);
		}

		protected override void OnResize ()
		{
			base.OnResize ();
			dev.WaitIdle ();
			initCrowSurface ();
			iFace.ProcessResize (new Crow.Rectangle (0, 0, (int)Width, (int)Height));
		}

		public void RecordDraw (CommandBuffer cmd) {
			//fsqPl.Bind (cmd);
			//cmd.BindDescriptorSet()
			fsqPl.BindDescriptorSet (cmd, descSet, 0);
			fsqPl.RecordDraw (cmd);
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
		void initCrowSurface () {
			lock (iFace.UpdateMutex) {
				iFace.surf?.Dispose ();
				crowImage?.Dispose ();
				crowBuffer?.Dispose ();

				crowBuffer = new HostBuffer (dev, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, Width * Height * 4, true);

				crowImage = new Image (dev, VkFormat.B8g8r8a8Unorm, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
					VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, VkSampleCountFlags.SampleCount1, VkImageTiling.Linear);
				crowImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
				crowImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);
				crowImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;

				DescriptorSetWrites dsw = new DescriptorSetWrites (descSet, dslBinding);
				dsw.Write (dev, crowImage.Descriptor);

				iFace.surf = new Crow.Cairo.ImageSurface (crowBuffer.MappedData, Crow.Cairo.Format.ARGB32,
					(int)Width, (int)Height, (int)Width * 4);

				PrimaryCommandBuffer cmd = cmdPoolCrow.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
				crowImage.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.Preinitialized, VkImageLayout.ShaderReadOnlyOptimal);
				presentQueue.EndSubmitAndWait (cmd, true);

				recordUpdateCrowCmd ();
			}
		}

		/// <summary>
		/// command buffer must have been reseted
		/// </summary>
		void recordUpdateCrowCmd () {
			cmdPoolCrow.Reset ();
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


		protected void loadWindow (string path, object dataSource = null) {
			try {
				Widget w = iFace.FindByName (path);
				if (w != null) {
					iFace.PutOnTop (w);
					return;
				}
				w = iFace.Load (path);
				w.Name = path;
				w.DataSource = dataSource;

			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine (ex.ToString ());
			}
		}
		protected void closeWindow (string path) {
			Widget g = iFace.FindByName (path);
			if (g != null)
				iFace.DeleteWidget (g);
		}


		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();

			running = false;
			fsqPl.Dispose ();
			dsPool.Dispose ();
			cmdPoolCrow.Dispose ();
			crowImage?.Dispose ();
			crowBuffer?.Dispose ();
			iFace.Dispose ();

			base.Dispose (disposing);
		}

	}
}
