// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using vke;
using Vulkan;
using Buffer = vke.Buffer;

namespace TextureCube {
	/// <summary>
	/// Simple textured cube sampled.
	/// </summary>
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

		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 1f;

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
		}

		Matrices matrices;

		HostBuffer 			uboMats;
		GPUBuffer<float> 	vbo;
		DescriptorPool 		descriptorPool;
		DescriptorSetLayout dsLayout;
		DescriptorSet 		descriptorSet, dsVkvg;
		GraphicPipeline 	pipeline;
		FrameBuffers 		frameBuffers;

		Image texture;
		Image nextTexture;

		static float[] g_vertex_buffer_data = {
			-1.0f,-1.0f,-1.0f,    0.0f, 1.0f,  // -X side
			-1.0f,-1.0f, 1.0f,    1.0f, 1.0f,
			-1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			-1.0f,-1.0f,-1.0f,    0.0f, 1.0f,
			                      
			-1.0f,-1.0f,-1.0f,    1.0f, 1.0f,  // -Z side
			 1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			 1.0f,-1.0f,-1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			                      
			-1.0f,-1.0f,-1.0f,    1.0f, 0.0f,  // -Y side
			 1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f, 1.0f,    0.0f, 0.0f,
			                      
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,  // +Y side
			-1.0f, 1.0f, 1.0f,    0.0f, 0.0f,
			 1.0f, 1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f, 1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f, 1.0f,-1.0f,    1.0f, 1.0f,
			                      
			 1.0f, 1.0f,-1.0f,    1.0f, 0.0f,  // +X side
			 1.0f, 1.0f, 1.0f,    0.0f, 0.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			 1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			                      
			-1.0f, 1.0f, 1.0f,    0.0f, 0.0f,  // +Z side
			-1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    1.0f, 1.0f,
			 1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
		};
		int currentImgIndex = 4;
		string[] imgPathes = {
			Utils.DataDirectory + "textures/uffizi_cube.ktx",
			Utils.DataDirectory + "textures/papermill.ktx",
			Utils.DataDirectory + "textures/cubemap_yokohama_bc3_unorm.ktx",
			Utils.DataDirectory + "textures/gcanyon_cube.ktx",
			Utils.DataDirectory + "textures/pisa_cube.ktx",
		};

		VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1;

#if WITH_VKVG
		VkvgPipeline.VkvgPipeline vkvgPipeline;

		void vkvgDraw () {
			using (vkvg.Context ctx = vkvgPipeline.CreateContext()) {
				ctx.Clear ();
				vkvgPipeline.DrawResources (ctx, (int)swapChain.Width, (int)swapChain.Height);
			}
		}


#endif

		protected override void initVulkan () {
			base.initVulkan ();

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);

			vbo = new GPUBuffer<float> (presentQueue, cmdPool, VkBufferUsageFlags.VertexBuffer, g_vertex_buffer_data);

			descriptorPool = new DescriptorPool (dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer, 2),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 2)
			);

			dsLayout = new DescriptorSetLayout (dev, 0,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, samples);

			cfg.Layout = new PipelineLayout (dev, dsLayout);
			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), cfg.Samples);

			cfg.AddVertexBinding (0, 5 * sizeof (float));
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);

			cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#shaders.skybox.vert.spv");
			cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#shaders.skybox.frag.spv");

			pipeline = new GraphicPipeline (cfg);

			cfg.DisposeShaders ();

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);
			uboMats.Map ();//permanent map

			descriptorSet = descriptorPool.Allocate (dsLayout);
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, dsLayout.Bindings[0]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			loadTexture (imgPathes[currentImgIndex]);
			if (nextTexture != null)
				updateTextureSet ();

#if WITH_VKVG
			dsVkvg = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);
			vkvgPipeline = new VkvgPipeline.VkvgPipeline (instance, dev, presentQueue, pipeline);
		}

		public override void Update () {
			vkvgDraw ();
#endif
		}

		void buildCommandBuffers () {
			cmdPool.Reset();
			for (int i = 0; i < swapChain.ImageCount; ++i) { 								
				cmds[i].Start ();
				recordDraw (cmds[i], frameBuffers[i]);
				cmds[i].End ();				 
			}
		} 
		void recordDraw (PrimaryCommandBuffer cmd, FrameBuffer fb) {
#if WITH_VKVG
			vkvgPipeline.Texture.SetLayout (cmd, VkImageAspectFlags.Color,
				VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader);
#endif
			pipeline.RenderPass.Begin (cmd, fb);

			cmd.SetViewport (fb.Width, fb.Height);
			cmd.SetScissor (fb.Width, fb.Height);
			cmd.BindDescriptorSet (pipeline.Layout, descriptorSet);

			pipeline.Bind (cmd);

			cmd.BindVertexBuffer (vbo, 0);
			cmd.Draw (36);

#if WITH_VKVG
			cmd.BindDescriptorSet (pipeline.Layout, dsVkvg);
			vkvgPipeline.RecordDraw (cmd);
			pipeline.RenderPass.End (cmd);
			vkvgPipeline.Texture.SetLayout (cmd, VkImageAspectFlags.Color,
				VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.ColorAttachmentOptimal,
				VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.ColorAttachmentOutput);
#else
			pipeline.RenderPass.End (cmd);
#endif
		}

		//in the thread of the keyboard
		void loadTexture (string path) {
			try {
				if (path.EndsWith ("ktx", StringComparison.OrdinalIgnoreCase))
					nextTexture = KTX.KTX.Load (presentQueue, cmdPool, path,
						VkImageUsageFlags.Sampled, VkMemoryPropertyFlags.DeviceLocal, true);
				else
					nextTexture = Image.Load (dev, path);
				updateViewRequested = true;
			} catch (Exception ex) {
				Console.WriteLine (ex);
				nextTexture = null;
			}
		}

		//in the main vulkan thread
		void updateTextureSet (){ 
			nextTexture.CreateView (VkImageViewType.Cube,VkImageAspectFlags.Color,6);
			nextTexture.CreateSampler ();

			nextTexture.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, dsLayout.Bindings[1]);				
			uboUpdate.Write (dev, nextTexture.Descriptor);

			texture?.Dispose ();
			texture = nextTexture;
			nextTexture = null;
		}
		void updateMatrices () { 
			matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (60f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 5.0f);
			matrices.view =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX);

			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
		}

		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
			base.configureEnabledFeatures (available_features, ref enabled_features);
			enabled_features.textureCompressionBC = available_features.textureCompressionBC;
		}

		public override void UpdateView () {
			if (nextTexture != null) {
				dev.WaitIdle ();
				updateTextureSet ();
				buildCommandBuffers ();
			}else 
				updateMatrices ();

			updateViewRequested = false;
			dev.WaitIdle ();
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

		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.Space:
					currentImgIndex++;
					if (currentImgIndex == imgPathes.Length)
						currentImgIndex = 0;
					loadTexture (imgPathes[currentImgIndex]);
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					break;
			}
		}

		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle();

#if WITH_VKVG
			vkvgPipeline.Resize ((int)Width, (int)Height, new DescriptorSetWrites (dsVkvg, dsLayout.Bindings[1]));
			PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
			vkvgPipeline.Texture.SetLayout (cmd, VkImageAspectFlags.Color,
				VkImageLayout.Undefined, VkImageLayout.ColorAttachmentOptimal,
				VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.ColorAttachmentOutput);
			presentQueue.EndSubmitAndWait (cmd, true);

#endif

			updateMatrices ();

			frameBuffers?.Dispose();
			frameBuffers = pipeline.RenderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers();
		}
			
		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					dev.WaitIdle ();
					pipeline.Dispose ();
					frameBuffers?.Dispose();
					descriptorPool.Dispose ();
					texture.Dispose ();
					uboMats.Dispose ();
					vbo.Dispose ();

#if WITH_VKVG
					vkvgPipeline.Dispose ();
#endif
				}
			}

			base.Dispose (disposing);
		}
	}
}
