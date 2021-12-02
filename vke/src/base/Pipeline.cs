// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	/// <summary>
	/// Abstract base classe for vulkan pipelines.
	/// </summary>
	public abstract class Pipeline : Activable {
        protected VkPipeline handle;
		protected PipelineLayout layout;
		protected PipelineCache Cache;

		/// <summary>
		/// Vulkan handle of the pipeline.
		/// </summary>
		public VkPipeline Handle => handle;
		/// <summary>
		/// Pipeline layout, activated on pipeline activation.
		/// </summary>
		public PipelineLayout Layout => layout;

		protected readonly VkPipelineBindPoint bindPoint;

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Pipeline, handle.Handle);

		#region CTORS
		protected Pipeline (Device dev, PipelineCache cache = null, string name = "custom pipeline") : base(dev, name) {
			this.Cache = cache;
		}
        #endregion

		/*/// <summary>
		/// PipelineCache and PipelineLayout will also be activated if present.
		/// </summary>
        public override void Activate () {
			Cache?.Activate ();
			layout?.Activate ();
            base.Activate ();
        }*/
		/// <summary>
		/// Bind Pipeline command call
		/// </summary>
		/// <param name="cmd">Recording command buffer</param>
		public abstract void Bind (CommandBuffer cmd);
		/// <summary>
		/// 
		/// </summary>
		/// <param name="cmd">Recording command buffer</param>
		/// <param name="dset"></param>
		/// <param name="firstSet">first descriptor set to bind</param>
		public abstract void BindDescriptorSet (CommandBuffer cmd, DescriptorSet dset, uint firstSet = 0);
		/// <summary>
		/// 
		/// </summary>
		/// <param name="cmd">Recording command buffer</param>
		/// <param name="obj">a blittable object that contains the data to push</param>
		/// <param name="rangeIndex"></param>
		/// <param name="offset">byte offset</param>
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

				vkDestroyPipeline (Dev.Handle, handle, IntPtr.Zero);
			} else if (disposing)
				System.Diagnostics.Debug.WriteLine ($"Calling dispose on unactive Pipeline: {name}");

			base.Dispose (disposing);
		}
	}
}
