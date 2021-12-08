// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using Vulkan;

using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {

	/// <summary>
	/// Managed activable for one Frame buffer
	/// </summary>
	public class FrameBuffer : Activable {
		internal VkFramebuffer handle;
		RenderPass renderPass;

		public List<Image> attachments = new List<Image> ();
		VkFramebufferCreateInfo createInfo;
		/// <summary>Framebuffer width.</summary>
		public uint Width => createInfo.width;
		/// <summary>Framebuffer height.</summary>
		public uint Height => createInfo.height;
		/// <summary>Framebuffer layers count.</summary>
		public uint Layers => createInfo.layers;

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Framebuffer, handle.Handle);
		#region CTORS
		public FrameBuffer (RenderPass _renderPass, uint _width, uint _height, uint _layers = 1) : base (_renderPass.Dev) {
			renderPass = _renderPass;
			createInfo.width = _width;
			createInfo.height = _height;
			createInfo.layers = _layers;
			createInfo.renderPass = renderPass.handle;
		}
		/// <summary>
		/// Create and Activate a new frabuffer for the supplied RenderPass.
		/// </summary>
		/// <param name="_renderPass">Render pass.</param>
		/// <param name="_width">Width.</param>
		/// <param name="_height">Height.</param>
		/// <param name="views">Array of image views. If null and not in unused state, attachment image and view will be automatically created from the
		/// supplied renderpass configuration.</param>
		public FrameBuffer (RenderPass _renderPass, uint _width, uint _height, params Image[] views)
			: this (_renderPass, _width, _height, 1, views) {}
        /// <summary>
        /// Create and Activate a new frabuffer for the supplied RenderPass.
        /// </summary>
        /// <param name="_renderPass">Render pass.</param>
        /// <param name="_width">Width.</param>
        /// <param name="_height">Height.</param>
        /// <param name="layers">layers count</param>
        /// <param name="views">Array of image views. If null and not in unused state, attachment image and view will be automatically created from the
        /// supplied renderpass configuration.</param>
        public FrameBuffer (RenderPass _renderPass, uint _width, uint _height, uint layers, params Image[] views)
		: this (_renderPass, _width, _height, layers) {
			for (int i = 0; i < views.Length; i++) {
				Image v = views[i];
				if (v == null) {
					//automatically create attachment if not in unused state in the renderpass
					VkAttachmentDescription ad = renderPass.Attachments[i];
					VkImageUsageFlags usage = 0;
					VkImageAspectFlags aspectFlags = 0;

					Helpers.QueryLayoutRequirements (ad.initialLayout, ref usage, ref aspectFlags);
					Helpers.QueryLayoutRequirements (ad.finalLayout, ref usage, ref aspectFlags);
					foreach (SubPass sp in renderPass.SubPasses) {
						//TODO:check subpass usage
					}

					v = new Image (renderPass.Dev, ad.format, usage, VkMemoryPropertyFlags.DeviceLocal,
						_width, _height, VkImageType.Image2D, ad.samples, VkImageTiling.Optimal, 1, createInfo.layers);
					v.SetName ($"fbImg{i}");
					v.CreateView (VkImageViewType.ImageView2D, aspectFlags);
				} else
					v.Activate ();//increase ref and create handle if not already activated

				attachments.Add (v);
			}
			Activate ();
		}
		#endregion

		public sealed override void Activate () {
			if (state != ActivableState.Activated) {
				VkImageView[] views = attachments.Select (a => a.Descriptor.imageView).ToArray ();
				createInfo.pAttachments = views;

				if (PNext != null)
					createInfo.pNext = PNext.GetPointer();

				CheckResult (vkCreateFramebuffer (renderPass.Dev.Handle, ref createInfo, IntPtr.Zero, out handle));

				createInfo.Dispose();

				if (PNext != null)
					PNext.ReleasePointer ();

			}
			base.Activate ();
		}


		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString ("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated)
				Dev.DestroyFramebuffer (handle);
			if (disposing) {
				foreach (Image img in attachments)
					img.Dispose ();
			} else
				System.Diagnostics.Debug.WriteLine ("VKE Activable object disposed by finalizer");

			base.Dispose (disposing);
		}
		#endregion

	}
}
