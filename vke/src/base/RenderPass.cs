// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using Vulkan;

using static Vulkan.Vk;

namespace vke {
	public class RenderPass : Activable {
		internal VkRenderPass handle;

		public readonly VkSampleCountFlags Samples;

		internal List<VkAttachmentDescription> attachments = new List<VkAttachmentDescription> ();
		internal List<SubPass> subpasses = new List<SubPass> ();
		List<VkSubpassDependency> dependencies = new List<VkSubpassDependency> ();
		public List<VkClearValue> ClearValues = new List<VkClearValue> ();
		public VkAttachmentDescription [] Attachments => attachments.ToArray ();
		public SubPass [] SubPasses => subpasses.ToArray ();

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.RenderPass, handle.Handle);
		#region CTORS

		/// <summary>
		/// Create empty render pass with no attachment
		/// </summary>
		public RenderPass (Device device, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1) : base(device) {
			Samples = samples;
		}

		/// <summary>
		/// Create renderpass with a single color attachment and a resolve one if needed
		/// </summary>
		public RenderPass (Device device, VkFormat colorFormat, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear)
			: this (device) {
			Samples = samples;

			AddAttachment (colorFormat, (samples == VkSampleCountFlags.SampleCount1) ? VkImageLayout.PresentSrcKHR : VkImageLayout.ColorAttachmentOptimal, samples,
				loadOp, VkAttachmentStoreOp.Store);
			ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });

			SubPass subpass0 = new SubPass ();
			subpass0.AddColorReference (0, VkImageLayout.ColorAttachmentOptimal);

			if (samples != VkSampleCountFlags.SampleCount1) {
				AddAttachment (colorFormat, VkImageLayout.PresentSrcKHR, VkSampleCountFlags.SampleCount1);
				ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
				subpass0.AddResolveReference (1, VkImageLayout.ColorAttachmentOptimal);
			}

			AddSubpass (subpass0);

			AddDependency (Vk.SubpassExternal, 0,
				VkPipelineStageFlags.BottomOfPipe, VkPipelineStageFlags.ColorAttachmentOutput,
				VkAccessFlags.MemoryRead, VkAccessFlags.ColorAttachmentWrite);
			AddDependency (0, Vk.SubpassExternal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.BottomOfPipe,
				VkAccessFlags.ColorAttachmentWrite, VkAccessFlags.MemoryRead);

		}
		/// <summary>
		/// Create default renderpass with one color, one depth attachments, and a resolve one if needed.
		/// </summary>
		public RenderPass (Device device, VkFormat colorFormat, VkFormat depthFormat, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1)
			: this (device){

			Samples = samples;

			AddAttachment (colorFormat, (samples == VkSampleCountFlags.SampleCount1) ? VkImageLayout.PresentSrcKHR : VkImageLayout.ColorAttachmentOptimal, samples);
			AddAttachment (depthFormat, VkImageLayout.DepthStencilAttachmentOptimal, samples);

			ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
			ClearValues.Add (new VkClearValue { depthStencil = new VkClearDepthStencilValue (1.0f, 0) });

			SubPass subpass0 = new SubPass ();

			subpass0.AddColorReference (0, VkImageLayout.ColorAttachmentOptimal);
			subpass0.SetDepthReference (1, VkImageLayout.DepthStencilAttachmentOptimal);

			if (samples != VkSampleCountFlags.SampleCount1) {
				AddAttachment (colorFormat, VkImageLayout.PresentSrcKHR, VkSampleCountFlags.SampleCount1);
				ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.2f) });
				subpass0.AddResolveReference (2, VkImageLayout.ColorAttachmentOptimal);
			}

			AddSubpass (subpass0);

			AddDependency (Vk.SubpassExternal, 0,
				VkPipelineStageFlags.BottomOfPipe, VkPipelineStageFlags.ColorAttachmentOutput,
				VkAccessFlags.MemoryRead, VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite);
			AddDependency (0, Vk.SubpassExternal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.BottomOfPipe,
				VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite, VkAccessFlags.MemoryRead);
		}
		#endregion

		public override void Activate () {
			if (state != ActivableState.Activated) {
				List<VkSubpassDescription> spDescs = new List<VkSubpassDescription> ();
				foreach (SubPass sp in subpasses)
					spDescs.Add (sp.SubpassDescription);

				VkRenderPassCreateInfo renderPassInfo = default;
				renderPassInfo.pAttachments = attachments;
				renderPassInfo.pSubpasses = spDescs;
				renderPassInfo.pDependencies = dependencies;
				if (PNext != null)
					renderPassInfo.pNext = PNext.GetPointer ();

				handle = Dev.CreateRenderPass (renderPassInfo);

				if (PNext != null)
					PNext.ReleasePointer ();

				renderPassInfo.Dispose ();
				foreach (VkSubpassDescription spd in spDescs)
					spd.Dispose ();
			}
			base.Activate ();
		}


		public void AddAttachment (VkFormat format,
			VkImageLayout finalLayout, VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1,
			VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear,
			VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store,
			VkImageLayout initialLayout = VkImageLayout.Undefined) {
			attachments.Add (new VkAttachmentDescription {
				format = format,
				samples = samples,
				loadOp = loadOp,
				storeOp = storeOp,
				stencilLoadOp = VkAttachmentLoadOp.DontCare,
				stencilStoreOp = VkAttachmentStoreOp.DontCare,
				initialLayout = initialLayout,
				finalLayout = finalLayout,
			});
		}
		public void AddAttachment (VkFormat format, VkImageLayout finalLayout,
			VkAttachmentLoadOp stencilLoadOp,
			VkAttachmentStoreOp stencilStoreOp,
			VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.DontCare,
			VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.DontCare,
			VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1,
			VkImageLayout initialLayout = VkImageLayout.Undefined) {
			attachments.Add (new VkAttachmentDescription {
				format = format,
				samples = samples,
				loadOp = loadOp,
				storeOp = storeOp,
				stencilLoadOp = stencilLoadOp,
				stencilStoreOp = stencilStoreOp,
				initialLayout = initialLayout,
				finalLayout = finalLayout,
			});
		}
		//public void AddDependency (SubPass srcSubpass, SubPass dstSubpass,
		//	VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask,
		//	VkAccessFlags srcAccessMask, VkAccessFlags dstAccessMask,
		//	VkDependencyFlags dependencyFlags = VkDependencyFlags.ByRegion) {

		//	AddDependency (srcSubpass.Index, dstSubpass.Index, srcStageMask, dstStageMask,
		//		srcAccessMask, dstAccessMask, dependencyFlags);
		//}

		public void AddDependency (uint srcSubpass, uint dstSubpass,
			VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask,
			VkAccessFlags srcAccessMask, VkAccessFlags dstAccessMask,
			VkDependencyFlags dependencyFlags = VkDependencyFlags.ByRegion) {
			dependencies.Add (new VkSubpassDependency {
				srcSubpass = srcSubpass,
				dstSubpass = dstSubpass,
				srcStageMask = srcStageMask,
				dstStageMask = dstStageMask,
				srcAccessMask = srcAccessMask,
				dstAccessMask = dstAccessMask,
				dependencyFlags = dependencyFlags
			});
		}
		public void AddSubpass (params SubPass[] subPass) {
			for (uint i = 0; i < subPass.Length; i++) {
				subPass[i].Index = (uint)subpasses.Count + i;
				subpasses.Add (subPass[i]);
			}
		}
		/// <summary>
		/// Begin Render pass with framebuffer extent dimensions
		/// </summary>
		public void Begin (PrimaryCommandBuffer cmd, FrameBuffer frameBuffer, VkSubpassContents contents = VkSubpassContents.Inline) {
			Begin (cmd, frameBuffer, frameBuffer.Width, frameBuffer.Height, contents);
		}
		/// <summary>
		/// Begin Render pass with custom render area
		/// </summary>
		public void Begin (PrimaryCommandBuffer cmd, FrameBuffer frameBuffer, uint width, uint height, VkSubpassContents contents = VkSubpassContents.Inline) {

			using (VkRenderPassBeginInfo info = new VkRenderPassBeginInfo {
						renderPass = handle,
						renderArea = new VkRect2D (default, new VkExtent2D (width, height)),
						pClearValues = ClearValues,
						framebuffer = frameBuffer.handle
			}){

				vkCmdBeginRenderPass (cmd.Handle, info, contents);

			}
		}
		/// <summary>
		/// Switch to next subpass
		/// </summary>
		public void BeginSubPass (PrimaryCommandBuffer cmd, VkSubpassContents subpassContents = VkSubpassContents.Inline) {
			vkCmdNextSubpass (cmd.Handle, subpassContents);
		}
		public void End (PrimaryCommandBuffer cmd) {
			vkCmdEndRenderPass (cmd.Handle);
		}
		/// <summary>
		/// Create one framebuffer per swapchain images. The presentable attachment of this renderpass is found searching for its final layout that could be PresentSrcKHR or SharedPresentKHR.
		/// </summary>
		/// <returns>A collection of FrameBuffer</returns>
		/// <param name="swapChain">a managed SwapChain instance.</param>
		public FrameBuffers CreateFrameBuffers (SwapChain swapChain) {
			FrameBuffers fbs = new FrameBuffers();
			Image[] images = new Image[attachments.Count];

			int presentableImgIdx = attachments.IndexOf(attachments.FirstOrDefault(a => a.finalLayout == VkImageLayout.PresentSrcKHR || a.finalLayout == VkImageLayout.SharedPresentKHR));

			if (presentableImgIdx<0)
				throw new Exception("RenderPass used in Pipeline has no presentable attachment");

			for (int i = 0; i < swapChain.ImageCount; ++i)
			{
				images[presentableImgIdx] = swapChain.images[i];
				fbs.Add(new FrameBuffer(this, swapChain.Width, swapChain.Height, images));
			}
			return fbs;
		}


		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (disposing) {
				} else
					System.Diagnostics.Debug.WriteLine ("VKE Activable RenderPass disposed by finalizer");

				Dev.DestroyRenderPass (handle);
			}else if (disposing)
				System.Diagnostics.Debug.WriteLine ("Calling dispose on unactive RenderPass");

			base.Dispose (disposing);
		}
		#endregion
	}
}
