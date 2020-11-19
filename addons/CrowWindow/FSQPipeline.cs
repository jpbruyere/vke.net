// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using vke;
using Vulkan;

namespace vke {

	public class FSQPipeline : GraphicPipeline {
		public static string FragPath = "#CrowWindow.simpletexture.frag.spv";
		public FSQPipeline (RenderPass renderPass, PipelineLayout pipelineLayout, int attachment = 0, PipelineCache pipelineCache = null)
		: base (renderPass, pipelineCache, "FSQ pipeline") {

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, this.RenderPass.Samples, false);
			cfg.RenderPass = RenderPass;
			cfg.Layout = pipelineLayout;
			cfg.AddShader (Dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
			cfg.AddShader (Dev, VkShaderStageFlags.Fragment, FragPath);
			cfg.multisampleState.rasterizationSamples = Samples;

			cfg.blendAttachments[attachment] = new VkPipelineColorBlendAttachmentState (true);

			layout = cfg.Layout;

			init (cfg);

			cfg.DisposeShaders ();
		}

		public virtual void RecordDraw (CommandBuffer cmd) {
			Bind (cmd);
			cmd.Draw (3, 1, 0, 0);
		}

		protected override void Dispose (bool disposing) {
			base.Dispose (disposing);
		}
	}

}
