// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
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

		SimpleModel helmet;

		DebugDrawPipeline dbgPipeline;



		protected override void initVulkan () {
			base.initVulkan ();

			camera = new Camera (Utils.DegreesToRadians (45f), 1f, 0.1f, 64f);
			camera.SetRotation (Utils.DegreesToRadians (90),0, 0);
			camera.SetPosition (0, 0, -3);

			cmds = cmdPool.AllocateCommandBuffer(swapChain.ImageCount);
			cmdDebug = cmdPool.AllocateSecondaryCommandBuffer ();

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

			dbgPipeline = new DebugDrawPipeline (pipeline.RenderPass);
			dbgPipeline.AddLine (Vector3.Zero, Vector3.UnitX, 1, 0, 0);
			dbgPipeline.AddLine (Vector3.Zero, Vector3.UnitY, 0, 1, 0);
			dbgPipeline.AddLine (Vector3.Zero, Vector3.UnitZ, 0, 0, 1);
			dbgPipeline.AddStar (Vector3.One*0.2f, 0.3f, 1, 0, 1);

			cmdPoolModel = new CommandPool (presentQueue, VkCommandPoolCreateFlags.ResetCommandBuffer);
			cmdModel = cmdPoolModel.AllocateSecondaryCommandBuffer ();
		}


		bool rebuildCmdBuffers, rebuildCmdModel = true;
		CommandPool cmdPoolModel;
		SecondaryCommandBuffer cmdModel, cmdDebug;

		void buildModelCmd () {
			cmdPoolModel.Reset ();
			cmdModel.Start (VkCommandBufferUsageFlags.RenderPassContinue | VkCommandBufferUsageFlags.SimultaneousUse, pipeline.RenderPass, 0);

			cmdModel.SetViewport (swapChain.Width, swapChain.Height);
			cmdModel.SetScissor (swapChain.Width, swapChain.Height);

			cmdModel.BindDescriptorSet (pipeline.Layout, dsMatrices);
			cmdModel.BindDescriptorSet (pipeline.Layout, dsTextures, 1);

			Matrix4x4 matrix = Matrix4x4.Identity;
			cmdModel.PushConstant (pipeline.Layout, VkShaderStageFlags.Vertex, matrix);

			cmdModel.BindPipeline (pipeline);

			helmet.DrawAll (cmdModel, pipeline.Layout);
			cmdModel.End ();
		}

		void buildDebugCmd () {
			cmdDebug.Start (VkCommandBufferUsageFlags.RenderPassContinue | VkCommandBufferUsageFlags.SimultaneousUse, pipeline.RenderPass);

			float d = 0.2f;
			uint dbgW = (uint)(swapChain.Width * d);
			uint dbgH = (uint)(swapChain.Height * d);
			cmdDebug.SetViewport (dbgW, dbgH, swapChain.Width - dbgW, swapChain.Height - dbgH);
			cmdDebug.SetScissor (dbgW, dbgH, (int)(swapChain.Width - dbgW), (int)(swapChain.Height - dbgH));

			dbgPipeline.RecordDraw (cmdDebug, camera.Projection,
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, camera.Rotation.Z) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, camera.Rotation.Y) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, camera.Rotation.X) *
				Matrix4x4.CreateTranslation (0, 0, -3));
			cmdDebug.End ();
		}

		void buildCommandBuffers () {
			dev.WaitIdle ();
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			if (rebuildCmdModel) {
				buildModelCmd ();
				rebuildCmdModel = false;
			}

			buildDebugCmd ();

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers[i];
				cmds[i].Start ();

				pipeline.RenderPass.Begin (cmds[i], fb, VkSubpassContents.SecondaryCommandBuffers);

				if (cmdModel != null)
					cmds[i].Execute (cmdModel);
					
				cmds[i].Execute (cmdDebug);

				pipeline.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}


		public override void Update () {
			if (rebuildCmdBuffers) {
				buildCommandBuffers ();
				rebuildCmdBuffers = false;
			}
		}
		public override void UpdateView () {
			camera.AspectRatio = (float)swapChain.Width / swapChain.Height;
			matrices.projection = camera.Projection;
			matrices.view = camera.View;
			matrices.model = camera.Model;

			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			updateViewRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				camera.Rotate ((float)-diffY,0, (float)diffX);
			} else if (MouseButton[1]) {
				camera.SetZoom ((float)diffY);
			} else
				return;
			rebuildCmdBuffers = true;
			updateViewRequested = true;
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
			dev.WaitIdle ();

			if (disposing) {
				if (!isDisposed) {
					helmet.Dispose ();
					pipeline.Dispose ();
					descLayoutMatrix.Dispose ();
					descLayoutTextures.Dispose ();
					frameBuffers?.Dispose();
					descriptorPool.Dispose ();
					uboMats.Dispose ();
					dbgPipeline?.Dispose ();
					cmdPoolModel?.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
