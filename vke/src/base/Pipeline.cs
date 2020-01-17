// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	public abstract class Pipeline : Activable {
        protected VkPipeline handle;
		protected PipelineLayout layout;
		protected PipelineCache Cache;

		public VkPipeline Handle => handle;
		public PipelineLayout Layout => layout;

		protected readonly VkPipelineBindPoint bindPoint;

		#region CTORS
		protected Pipeline (Device dev, PipelineCache cache = null, string name = "custom pipeline") : base(dev, name) {
			this.Cache = cache;
		}
		#endregion

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Pipeline, handle.Handle);

		public abstract void Bind (CommandBuffer cmd);
		public abstract void BindDescriptorSet (CommandBuffer cmd, DescriptorSet dset, uint firstSet = 0);
		public void PushConstant (CommandBuffer cmd, object obj, int rangeIndex = 0, uint offset = 0) {
			cmd.PushConstant (layout, layout.PushConstantRanges[rangeIndex].stageFlags, obj, offset);
		}


		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated) {
				if (disposing) {
					layout.Dispose ();
					Cache?.Dispose ();
				} else
					System.Diagnostics.Debug.WriteLine ($"Pipeline '{name}' disposed by finalizer");

				vkDestroyPipeline (Dev.VkDev, handle, IntPtr.Zero);
			} else if (disposing)
				System.Diagnostics.Debug.WriteLine ($"Calling dispose on unactive Pipeline: {name}");

			base.Dispose (disposing);
		}
	}
}
