//
// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
    public sealed class ComputePipeline : Pipeline {

		public string SpirVPath;
    
		#region CTORS
		public ComputePipeline (Device dev, PipelineCache cache = null, string name = "compute pipeline") : base (dev, cache, name) { 
		}
		/// <summary>
		/// Create a new Pipeline with supplied PipelineLayout
		/// </summary>
		public ComputePipeline (PipelineLayout layout, string spirvPath, PipelineCache cache = null, string name = "pipeline") : base(layout.Dev, cache, name)
		{
			SpirVPath = spirvPath;
			this.layout = layout;

			Activate ();
		}
		#endregion

		public override void Activate () {
			if (state != ActivableState.Activated) {
				layout.Activate ();
				Cache?.Activate ();

				using (ShaderInfo shader = new ShaderInfo (Dev, VkShaderStageFlags.Compute, SpirVPath)) {
					VkComputePipelineCreateInfo info = default;
					info.layout = layout.Handle;
					info.stage = shader.Info;
					info.basePipelineHandle = 0;
					info.basePipelineIndex = 0;

					CheckResult (Vk.vkCreateComputePipelines (Dev.Handle, Cache == null ? VkPipelineCache.Null : Cache.handle, 1, ref info, IntPtr.Zero, out handle));
				}
			}
			base.Activate ();
		}

		public override void Bind (CommandBuffer cmd) {
            vkCmdBindPipeline (cmd.Handle, VkPipelineBindPoint.Compute, handle);
        }
		public override void BindDescriptorSet (CommandBuffer cmd, DescriptorSet dset, uint firstSet = 0) {
			cmd.BindDescriptorSet (VkPipelineBindPoint.Compute, layout, dset, firstSet);
		}
		public void BindAndDispatch (CommandBuffer cmd, uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1) {
			vkCmdBindPipeline (cmd.Handle, VkPipelineBindPoint.Compute, handle);
			vkCmdDispatch (cmd.Handle, groupCountX, groupCountY, groupCountZ);
		}
	}
}
