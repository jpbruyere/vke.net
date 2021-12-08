// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Vulkan;
using System.Linq;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	public class GraphicPipeline : Pipeline {

		public readonly RenderPass RenderPass;
		public VkSampleCountFlags Samples => RenderPass.Samples;

		#region CTORS
		/// <summary>
		/// Create and activate a new pipeline for supplied render pass.
		/// </summary>
		/// <param name="renderPass">a managed Render pass that will be activated (if not already) during the pipeline creation.</param>
		/// <param name="cache">an optional pipeline cache to speed up pipeline creation.</param>
		/// <param name="name">an optionnal name that will be used by the debug utils extension if enabled.</param>
		protected GraphicPipeline (RenderPass renderPass, PipelineCache cache = null, string name = "graphic pipeline") : base (renderPass.Dev, cache, name) {
			RenderPass = renderPass;
		}
		/// <summary>
		/// Create a new Pipeline with supplied configuration
		/// </summary>
		public GraphicPipeline (GraphicPipelineConfig cfg, string name = "graphic pipeline") : this (cfg.RenderPass, cfg.Cache, name) {
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

				bool enableTesselation = false;

				List<VkPipelineShaderStageCreateInfo> shaderStages = new List<VkPipelineShaderStageCreateInfo> ();
				foreach (ShaderInfo shader in cfg.Shaders) {
					if (shader.Stage == VkShaderStageFlags.TessellationControl || shader.Stage == VkShaderStageFlags.TessellationEvaluation)
						enableTesselation = true;
					shaderStages.Add (shader.Info);
				}

				using (PinnedObjects pctx = new PinnedObjects ()) {

					VkPipelineColorBlendStateCreateInfo colorBlendInfo = default;
					colorBlendInfo.logicOpEnable = cfg.ColorBlendLogicOpEnable;
					colorBlendInfo.logicOp = cfg.ColorBlendLogicOp;
					colorBlendInfo.blendConstants = cfg.ColorBlendConstants;
					colorBlendInfo.pAttachments = cfg.blendAttachments;

					VkPipelineDynamicStateCreateInfo dynStatesInfo = default;
					dynStatesInfo.pDynamicStates = cfg.dynamicStates;

					VkPipelineVertexInputStateCreateInfo vertInputInfo = default;
					vertInputInfo.pVertexBindingDescriptions = cfg.vertexBindings;
					vertInputInfo.pVertexAttributeDescriptions = cfg.vertexAttributes;

					VkPipelineViewportStateCreateInfo viewportState = default;
					if (cfg.Viewports.Count > 0) {
						viewportState.pViewports = cfg.Viewports;
					} else
						viewportState.viewportCount = 1;

					if (cfg.Scissors.Count > 0) {
						viewportState.pScissors = cfg.Scissors;
					} else
						viewportState.scissorCount = 1;

					VkGraphicsPipelineCreateInfo info = default;
					info.renderPass = RenderPass.handle;
					info.layout = Layout.handle;
					info.pVertexInputState = vertInputInfo;
					info.pInputAssemblyState = cfg.inputAssemblyState;
					info.pRasterizationState = cfg.rasterizationState;
					info.pColorBlendState = colorBlendInfo;
					info.pMultisampleState = cfg.multisampleState;
					info.pViewportState = viewportState;
					info.pDepthStencilState = cfg.depthStencilState;
					info.pDynamicState = dynStatesInfo;
					info.pStages = shaderStages;
					info.subpass = cfg.SubpassIndex;

					if (enableTesselation) {
						VkPipelineTessellationStateCreateInfo tessellationInfo = default;
						tessellationInfo.patchControlPoints = cfg.TessellationPatchControlPoints;
						info.pTessellationState = tessellationInfo;
					}

					CheckResult (vkCreateGraphicsPipelines (Dev.Handle, Cache == null ? VkPipelineCache.Null : Cache.handle, 1, ref info, IntPtr.Zero, out handle));

					vertInputInfo.Dispose();
					viewportState.Dispose();
					dynStatesInfo.Dispose();
					colorBlendInfo.Dispose();
					info.Dispose ();
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
			} else
				System.Diagnostics.Debug.WriteLine ("GraphicPipeline disposed by finalizer");

			base.Dispose (disposing);
		}
	}
}
