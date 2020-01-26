﻿// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using vke.glTF;
using Vulkan;

namespace ModelSample
{
	class Program : VkWindow {
		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
			Instance.RENDER_DOC_CAPTURE = false;
#endif
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
		}

		Matrices matrices;

		HostBuffer uboMats;

		DescriptorPool descriptorPool;
		DescriptorSet dsMatrices, dsTextures;
		DescriptorSetLayout descLayoutMatrix;
		DescriptorSetLayout descLayoutTextures;

		FrameBuffers frameBuffers;

		GraphicPipeline pipeline;

		VkSampleCountFlags NUM_SAMPLES = VkSampleCountFlags.SampleCount1;

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 2f;

		SimpleModel helmet;

		protected override void initVulkan () {
			base.initVulkan ();

			cmds = cmdPool.AllocateCommandBuffer(swapChain.ImageCount);

			descriptorPool = new DescriptorPool (dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 3));

			descLayoutMatrix = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer));

			descLayoutTextures = new DescriptorSetLayout (dev, 
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
			);
				
			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, NUM_SAMPLES);
			cfg.rasterizationState.cullMode = VkCullModeFlags.Back;
			if (NUM_SAMPLES != VkSampleCountFlags.SampleCount1) {
				cfg.multisampleState.sampleShadingEnable = true;
				cfg.multisampleState.minSampleShading = 0.5f;
			}

			cfg.Layout = new PipelineLayout (dev, new VkPushConstantRange(VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Matrix4x4> ()), descLayoutMatrix, descLayoutTextures);
			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), cfg.Samples);
			cfg.AddVertexBinding<Model.Vertex> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);

			cfg.AddShader (VkShaderStageFlags.Vertex, "#shaders.model.vert.spv");
			cfg.AddShader (VkShaderStageFlags.Fragment, "#shaders.model.frag.spv");

			pipeline = new GraphicPipeline (cfg);

			helmet = new SimpleModel (presentQueue, Utils.DataDirectory + "models/DamagedHelmet/glTF/DamagedHelmet.gltf");

			dsMatrices = descriptorPool.Allocate (descLayoutMatrix);
			dsTextures = descriptorPool.Allocate (descLayoutTextures);

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsMatrices, descLayoutMatrix);
			uboUpdate.Write (dev, uboMats.Descriptor);

			DescriptorSetWrites texturesUpdate = new DescriptorSetWrites (dsTextures, descLayoutTextures);
			texturesUpdate.Write (dev,
				helmet.textures[0].Descriptor,
				helmet.textures[1].Descriptor,
				helmet.textures[2].Descriptor);

			uboMats.Map ();//permanent map
		}

		public override void UpdateView () {
			matrices.projection = Utils.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (45f),
				(float)swapChain.Width / (float)swapChain.Height, 0.1f, 16.0f);
			matrices.view =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom);
			matrices.model = Matrix4x4.Identity;
			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			updateViewRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (MouseButton[1]) {
				zoom += zoomSpeed * (float)diffY;
			}
			updateViewRequested = true;
		}
		void buildCommandBuffers () {
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers[i];
				cmds[i].Start ();

				pipeline.RenderPass.Begin (cmds[i], fb);

				cmds[i].SetViewport (swapChain.Width, swapChain.Height);
				cmds[i].SetScissor (swapChain.Width, swapChain.Height);

				cmds[i].BindDescriptorSet (pipeline.Layout, dsMatrices);
				cmds[i].BindDescriptorSet (pipeline.Layout, dsTextures, 1);

				Matrix4x4 matrix = Matrix4x4.Identity;
				cmds[i].PushConstant (pipeline.Layout, VkShaderStageFlags.Vertex, matrix);

				cmds[i].BindPipeline (pipeline);

				helmet.DrawAll (cmds[i], pipeline.Layout);

				pipeline.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		protected override void OnResize () {
			base.OnResize();

			frameBuffers?.Dispose();
			frameBuffers = pipeline.RenderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers ();
		}

		class SimpleModel : PbrModel {
			public new struct Vertex {
				[VertexAttribute (VertexAttributeType.Position, VkFormat.R32g32b32Sfloat)]
				public Vector3 pos;
				[VertexAttribute (VertexAttributeType.Normal, VkFormat.R32g32b32Sfloat)]
				public Vector3 normal;
				[VertexAttribute (VertexAttributeType.UVs, VkFormat.R32g32Sfloat)]
				public Vector2 uv;
				public override string ToString () {
					return pos.ToString () + ";" + normal.ToString () + ";" + uv.ToString ();
				}
			};
			public Image[] textures;

			public SimpleModel (Queue transferQ, string path) {
				dev = transferQ.Dev;

				using (CommandPool cmdPool = new CommandPool (dev, transferQ.index)) {
					using (vke.glTF.glTFLoader ctx = new vke.glTF.glTFLoader(path, transferQ, cmdPool)) {
						loadSolids<Vertex> (ctx);
						textures = ctx.LoadImages ();
					}
				}
			}

			public void DrawAll (CommandBuffer cmd, PipelineLayout pipelineLayout) {
				//helmet.Meshes
				cmd.BindVertexBuffer (vbo);
				cmd.BindIndexBuffer (ibo, IndexBufferType);
				foreach (Mesh m in Meshes) {
					foreach (var p in m.Primitives) {
						cmd.DrawIndexed (p.indexCount,1,p.indexBase,p.vertexBase);
					}
				}

				//foreach (Scene sc in Scenes) {
				//	foreach (Node node in sc.Root.Children)
				//		RenderNode (cmd, pipelineLayout, node, sc.Root.localMatrix, shadowPass);
				//}
			}

			public override void RenderNode (CommandBuffer cmd, PipelineLayout pipelineLayout, Node node, Matrix4x4 currentTransform, bool shadowPass = false) {
				throw new System.NotImplementedException ();
			}
			protected override void Dispose (bool disposing)
			{
				foreach (var t in textures)
					t.Dispose ();
				base.Dispose (disposing);
			}
		}

		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					helmet.Dispose ();
					pipeline.Dispose ();
					descLayoutMatrix.Dispose ();
					descLayoutTextures.Dispose ();
					frameBuffers?.Dispose();
					descriptorPool.Dispose ();
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
