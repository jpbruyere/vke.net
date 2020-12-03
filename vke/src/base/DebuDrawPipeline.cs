// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vulkan;

namespace vke {
	public class DebugDrawPipeline : GraphicPipeline {
		public HostBuffer Vertices;
		uint vertexCount;
        uint vboLength;

		public DebugDrawPipeline (RenderPass renderPass, uint maxVertices = 100,
            VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1, PipelineCache pipelineCache = null) :
			base (renderPass, pipelineCache, "Debug draw pipeline") {

            vboLength = maxVertices * 6 * sizeof(float);

			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.LineList, samples, false)) {
				cfg.rasterizationState.lineWidth = 1.0f;
				cfg.RenderPass = RenderPass;
				cfg.Layout = new PipelineLayout (Dev, new VkPushConstantRange (VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Matrix4x4> () * 2));
				cfg.AddVertexBinding (0, 6 * sizeof (float));
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);
				cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState (true);

				cfg.AddShaders (
					new ShaderInfo (Dev, VkShaderStageFlags.Vertex, "#vke.debug.vert.spv"),
					new ShaderInfo (Dev, VkShaderStageFlags.Fragment, "#vke.debug.frag.spv")
				);

				layout = cfg.Layout;

				init (cfg);
			}

			Vertices = new HostBuffer (Dev, VkBufferUsageFlags.VertexBuffer, vboLength);
			Vertices.Map ();
		}

		public void AddLine (Vector3 start, Vector3 end, float r, float g, float b) {
			float[] data = {
				start.X, start.Y, start.Z,
				r, g, b,
				end.X, end.Y, end.Z,
				r, g, b
			};
			Vertices.Update (data, 12 * sizeof (float), vertexCount * 6 * sizeof (float));
			vertexCount+=2;
		}
		public void AddStar (Vector3 position, float size, float r, float g, float b) {
			AddLine (position - new Vector3 (size, 0, 0), position + new Vector3 (size, 0, 0), r, g, b);
			AddLine (position - new Vector3 (0, size, 0), position + new Vector3 (0, size, 0), r, g, b);
			AddLine (position - new Vector3 (0, 0, size), position + new Vector3 (0, 0, size), r, g, b);
		}
		public void UpdateLine (uint lineNum, Vector3 start, Vector3 end, float r, float g, float b) {
			float[] data = {
				start.X, start.Y, start.Z,
				r, g, b,
				end.X, end.Y, end.Z,
				r, g, b
			};
			Vertices.Update (data, 12 * sizeof (float), (lineNum-1) * 2 * 6 * sizeof (float));
		}

		public void RecordDraw (CommandBuffer cmd, Matrix4x4 projection, Matrix4x4 view) {		
            Bind(cmd);

            cmd.PushConstant (layout, VkShaderStageFlags.Vertex, projection);
            cmd.PushConstant (layout, VkShaderStageFlags.Vertex, view, (uint)Marshal.SizeOf<Matrix4x4>());

			cmd.BindVertexBuffer (Vertices);
			cmd.Draw (vertexCount);
		}

		protected override void Dispose (bool disposing) {
			if (disposing) {
				Vertices.Unmap ();
				Vertices.Dispose ();
			}

			base.Dispose (disposing);
		}
	}

}
