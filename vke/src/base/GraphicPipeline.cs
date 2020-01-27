// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Vulkan;
using System.Linq;
using static Vulkan.Vk;

namespace vke {
    public class GraphicPipeline : Pipeline {

		public readonly RenderPass RenderPass;
		public VkSampleCountFlags Samples => RenderPass.Samples;

		#region CTORS
		protected GraphicPipeline (RenderPass renderPass, PipelineCache cache = null, string name = "graphic pipeline") : base(renderPass.Dev, cache, name) { 
			RenderPass = renderPass;
		}
		/// <summary>
		/// Create a new Pipeline with supplied RenderPass
		/// </summary>
		public GraphicPipeline (GraphicPipelineConfig cfg, string name = "graphic pipeline") : this (cfg.RenderPass, cfg.Cache, name)
		{
			layout = cfg.Layout;

			init (cfg);
		}

		#endregion

		public override void Activate () => throw new NotSupportedException ("Please initialize graphic pipeline through the init method");

		protected void init (GraphicPipelineConfig cfg) {
			if (state != ActivableState.Activated) {
				Layout.Activate ();
				RenderPass.Activate ();
				Cache?.Activate ();

				List<VkPipelineShaderStageCreateInfo> shaderStages = new List<VkPipelineShaderStageCreateInfo> ();
				foreach (ShaderInfo shader in cfg.shaders)
					shaderStages.Add (shader.GetStageCreateInfo (Dev));

				using (PinnedObjects pctx = new PinnedObjects ()) {

					VkPipelineColorBlendStateCreateInfo colorBlendInfo = VkPipelineColorBlendStateCreateInfo.New ();
					colorBlendInfo.logicOpEnable = cfg.ColorBlendLogicOpEnable;
					colorBlendInfo.logicOp = cfg.ColorBlendLogicOp;
					unsafe
					{
						colorBlendInfo.blendConstants[0] = cfg.ColorBlendConstants.X;
						colorBlendInfo.blendConstants[1] = cfg.ColorBlendConstants.Y;
						colorBlendInfo.blendConstants[2] = cfg.ColorBlendConstants.Z;
						colorBlendInfo.blendConstants[3] = cfg.ColorBlendConstants.W;
					}
					colorBlendInfo.attachmentCount = (uint)cfg.blendAttachments.Count;
					colorBlendInfo.pAttachments = cfg.blendAttachments.Pin (pctx);

					VkPipelineDynamicStateCreateInfo dynStatesInfo = VkPipelineDynamicStateCreateInfo.New ();
					dynStatesInfo.dynamicStateCount = (uint)cfg.dynamicStates.Count;
					dynStatesInfo.pDynamicStates = cfg.dynamicStates.Cast<int>().ToArray().Pin (pctx);

					VkPipelineVertexInputStateCreateInfo vertInputInfo = VkPipelineVertexInputStateCreateInfo.New ();
					vertInputInfo.vertexBindingDescriptionCount = (uint)cfg.vertexBindings.Count;
					vertInputInfo.pVertexBindingDescriptions = cfg.vertexBindings.Pin (pctx);
					vertInputInfo.vertexAttributeDescriptionCount = (uint)cfg.vertexAttributes.Count;
					vertInputInfo.pVertexAttributeDescriptions = cfg.vertexAttributes.Pin (pctx);

					VkPipelineViewportStateCreateInfo viewportState = VkPipelineViewportStateCreateInfo.New ();
					if (cfg.Viewports.Count > 0) {
						viewportState.viewportCount = (uint)cfg.Viewports.Count;
						viewportState.pViewports = cfg.Viewports.Pin (pctx);
					} else
						viewportState.viewportCount = 1;

					if (cfg.Scissors.Count > 0) {
						viewportState.scissorCount = (uint)cfg.Scissors.Count;
						viewportState.pScissors = cfg.Scissors.Pin (pctx);
					} else
						viewportState.scissorCount = 1;

					VkGraphicsPipelineCreateInfo info = VkGraphicsPipelineCreateInfo.New ();
					info.renderPass = RenderPass.handle;
					info.layout = Layout.handle;
					info.pVertexInputState = vertInputInfo.Pin (pctx);
					info.pInputAssemblyState = cfg.inputAssemblyState.Pin (pctx);
					info.pRasterizationState = cfg.rasterizationState.Pin (pctx);
					info.pColorBlendState = colorBlendInfo.Pin (pctx);
					info.pMultisampleState = cfg.multisampleState.Pin (pctx);
					info.pViewportState = viewportState.Pin (pctx);
					info.pDepthStencilState = cfg.depthStencilState.Pin (pctx);
					info.pDynamicState = dynStatesInfo.Pin (pctx);
					info.stageCount = (uint)cfg.shaders.Count;
					info.pStages = shaderStages.Pin (pctx);
					info.subpass = cfg.SubpassIndex;

					Utils.CheckResult (vkCreateGraphicsPipelines (Dev.VkDev, Cache == null ? VkPipelineCache.Null : Cache.handle, 1, ref info, IntPtr.Zero, out handle));

					for (int i = 0; i < cfg.shaders.Count; i++)
						Dev.DestroyShaderModule (shaderStages [i].module);
				}
			}
			base.Activate ();
		}

		public override void Bind (CommandBuffer cmd) {
			vkCmdBindPipeline (cmd.Handle, VkPipelineBindPoint.Graphics, handle);
		}
		public override void BindDescriptorSet (CommandBuffer cmd, DescriptorSet dset, uint firstSet = 0) {
			cmd.BindDescriptorSet (VkPipelineBindPoint.Graphics, layout, dset, firstSet);
		}

		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (state == ActivableState.Activated) 
					RenderPass.Dispose ();
			}else
				System.Diagnostics.Debug.WriteLine ("GraphicPipeline disposed by finalizer");

			base.Dispose (disposing);
		}
	}
}
