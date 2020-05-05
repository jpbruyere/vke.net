// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using vke;
using Vulkan;

namespace vkeEditor {

	public class FSQPipeline : GraphicPipeline {
		public FSQPipeline (GraphicPipeline pipeline, int attachment = 0, PipelineCache pipelineCache = null)
		: base (pipeline.RenderPass, pipelineCache, "FSQ pipeline") {

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, this.RenderPass.Samples, false);
			cfg.RenderPass = RenderPass;
			cfg.Layout = pipeline.Layout;
			cfg.AddShader (Dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
			cfg.AddShader (Dev, VkShaderStageFlags.Fragment, "#vke.simpletexture.frag.spv");
			cfg.multisampleState.rasterizationSamples = Samples;

			cfg.blendAttachments[attachment] = new VkPipelineColorBlendAttachmentState (true);

			layout = cfg.Layout;

			init (cfg);

			cfg.DisposeShaders ();
		}

		public void RecordDraw (CommandBuffer cmd) {
			Bind (cmd);
			cmd.Draw (3, 1, 0, 0);
		}

		protected override void Dispose (bool disposing) {
			base.Dispose (disposing);
		}
	}

}
