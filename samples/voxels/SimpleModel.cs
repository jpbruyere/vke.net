// Copyright (c) 2019  Jean-Philippe Bruyère jp_bruyere@hotmail.com
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using vke;
using vke.glTF;
using Vulkan;

namespace voxels {
	class SimpleModel : PbrModel {
		public new struct Vertex {
			[VertexAttribute (VertexAttributeType.Position, VkFormat.R32g32b32Sfloat)]
			public Vector3 pos;
			[VertexAttribute (VertexAttributeType.Normal, VkFormat.R32g32b32Sfloat)]
			public Vector3 normal;
			[VertexAttribute (VertexAttributeType.UVs, VkFormat.R32g32Sfloat)]
			public Vector2 uv;
			public override string ToString ()
			{
				return pos.ToString () + ";" + normal.ToString () + ";" + uv.ToString ();
			}
		};
		public Image [] textures;

		public SimpleModel (Queue transferQ, string path)
		{
			dev = transferQ.Dev;

			using (CommandPool cmdPool = new CommandPool (dev, transferQ.index)) {
				using (vke.glTF.glTFLoader ctx = new vke.glTF.glTFLoader (path, transferQ, cmdPool)) {
					loadSolids<Vertex> (ctx);
					textures = ctx.LoadImages ();
				}
			}
		}

		public void DrawAll (CommandBuffer cmd, PipelineLayout pipelineLayout)
		{
			//helmet.Meshes
			cmd.BindVertexBuffer (vbo);
			cmd.BindIndexBuffer (ibo, IndexBufferType);
			foreach (Mesh m in Meshes) {
				foreach (var p in m.Primitives) {
					cmd.DrawIndexed (p.indexCount, 1, p.indexBase, p.vertexBase);
				}
			}

			//foreach (Scene sc in Scenes) {
			//	foreach (Node node in sc.Root.Children)
			//		RenderNode (cmd, pipelineLayout, node, sc.Root.localMatrix, shadowPass);
			//}
		}

		public override void RenderNode (CommandBuffer cmd, PipelineLayout pipelineLayout, Node node, Matrix4x4 currentTransform, bool shadowPass = false)
		{
			throw new System.NotImplementedException ();
		}
		protected override void Dispose (bool disposing)
		{
			foreach (Image t in textures) 
				t.Dispose ();
			base.Dispose (disposing);
		}
	}
}
