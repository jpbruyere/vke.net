// Copyright (c) 2023  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;

namespace FillTests {

	class Program : SampleBase {
		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
			Instance.RENDER_DOC_CAPTURE = true;
#endif
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}


		FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		FrameBuffer fbContour;
		
		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			Vector2 position;
			UInt32 contourID;
			public Vertex (float x, float y, UInt32 _contourID) {
				position = new Vector2 (x, y);
				contourID = _contourID;
			}
		}
				//triangle vertices (position + color per vertex) and indices.
		/*Vertex[] vertices = {
			new Vertex (10.0f, 210.0f,  1),
			new Vertex (100.0f, 220.0f,  1),
			new Vertex (50.0f,  270.0f,  1),
			new Vertex (10.0f, 210.0f,  1),
		};*/
		/*Vertex[] vertices = {
			new Vertex (100.0f, 210.0f,  1),
			new Vertex (200.0f, 120.0f,  1),
			new Vertex (350.0f,  270.0f,  1),
			new Vertex (150.0f, 310.0f,  1),
			new Vertex (100.0f, 210.0f,  1)
		};*/
		Vertex[] vertices = {
			new Vertex (100.0f, 100.0f,  1),
			new Vertex (500.0f, 400.0f,  1),
			new Vertex (450.0f,  150.0f,  1),
			new Vertex (120.0f, 420.0f,  1),
			new Vertex (100.0f, 100.0f,  1)
		};
		HostBuffer vbo;
		GraphicPipeline plContours;
		GraphicPipeline plFSQ;
		ComputePipeline plComp;
		DescriptorPool descriptorPool;
		DescriptorSet dsComp, dsFSQ;//descriptor set for the mvp matrice.
		vke.Image imgContour, imgColor;

		VkFormat rastFormat = VkFormat.B8g8r8a8Uint;
		const VkSampleCountFlags contourSamplesCount = VkSampleCountFlags.SampleCount1;


		protected override void initVulkan () {
			base.initVulkan ();

			UpdateFrequency = 1;

			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			vbo.SetName("vbo");

			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.LineStrip, contourSamplesCount, false)) {
				cfg.Layout = new PipelineLayout (dev,
					new VkPushConstantRange (VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Vector2>()));
				cfg.RenderPass = new RenderPass (dev, contourSamplesCount);
				cfg.RenderPass.AddAttachment(rastFormat, VkImageLayout.General, contourSamplesCount, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkImageLayout.General);
				cfg.RenderPass.ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
				cfg.RenderPass.AddSubpass(new SubPass (VkImageLayout.ColorAttachmentOptimal));
				cfg.RenderPass.AddDependency (Vk.SubpassExternal, 0,
					VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.ColorAttachmentOutput,
					VkAccessFlags.MemoryRead, VkAccessFlags.ColorAttachmentWrite);
				cfg.RenderPass.AddDependency (0, Vk.SubpassExternal,
					VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.ComputeShader,
					VkAccessFlags.ColorAttachmentWrite, VkAccessFlags.MemoryWrite);				
				

				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32Sfloat, VkFormat.R32Uint);//position + color
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.contour.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.contour.frag.spv")
				);
				plContours = new GraphicPipeline (cfg);
			}

			//uboMVPmatrix = new HostBuffer<Matrix4x4> (dev, VkBufferUsageFlags.UniformBuffer, 1, true);

			descriptorPool = new DescriptorPool (dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.StorageImage, 2),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 1)
			);

			
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, contourSamplesCount, false)) {
				cfg.Layout = new PipelineLayout (dev, new DescriptorSetLayout(dev, 
					new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)));
				cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, VkSampleCountFlags.SampleCount1);
				cfg.RenderPass.ClearValues[0] = new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) };
				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32Sfloat, VkFormat.R32g32b32Sfloat);//position + color
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.simpletexture.frag.spv")
				);
				plFSQ = new GraphicPipeline (cfg);
			}

		
			plComp = new ComputePipeline (
				new PipelineLayout (dev,
					new VkPushConstantRange (VkShaderStageFlags.Compute, sizeof (float)),
					 new DescriptorSetLayout(dev, 
					new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Compute, VkDescriptorType.StorageImage),
					new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Compute, VkDescriptorType.StorageImage))),
				"#shaders.test.comp.spv");

			dsComp = descriptorPool.Allocate (plComp.Layout.DescriptorSetLayouts[0]);
			dsFSQ =  descriptorPool.Allocate (plFSQ.Layout.DescriptorSetLayouts[0]);

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {

			base.UpdateView ();
		}
		protected override void onMouseMove (double xPos, double yPos) {

		}

		void stroke() {
			PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart(VkCommandBufferUsageFlags.OneTimeSubmit);
			
			imgContour.SetLayout(cmd, VkImageAspectFlags.Color, VkImageLayout.ColorAttachmentOptimal);

			plContours.RenderPass.Begin (cmd, fbContour);

			cmd.SetViewport (swapChain.Width, swapChain.Height);
			cmd.SetScissor (swapChain.Width, swapChain.Height);
			//cmd.BindDescriptorSet (pipeline.Layout, descriptorSet);

			cmd.BindPipeline (plContours);
			cmd.PushConstant (plContours.Layout, VkShaderStageFlags.Vertex, new Vector2(swapChain.Width, swapChain.Height));

			cmd.BindVertexBuffer (vbo);
			cmd.Draw ((uint)vertices.Length);

			plContours.RenderPass.End (cmd);

			imgContour.SetLayout(cmd, VkImageAspectFlags.Color,VkImageLayout.ColorAttachmentOptimal, VkImageLayout.General);
			imgColor.SetLayout(cmd, VkImageAspectFlags.Color, VkImageLayout.General);

			cmd.BindPipeline(plComp, VkPipelineBindPoint.Compute);
			plComp.BindDescriptorSet(cmd,dsComp);
			
			cmd.Dispatch(swapChain.Height);

			this.presentQueue.EndSubmitAndWait(cmd, true);
		}
		void buildCommandBuffer(PrimaryCommandBuffer cmd, FrameBuffer fb) {
				cmd.Start ();

				imgColor.SetLayout(cmd, VkImageAspectFlags.Color, VkImageLayout.ColorAttachmentOptimal);

				plFSQ.RenderPass.Begin (cmd, fb);

				cmd.BindPipeline (plFSQ);
				cmd.SetViewport (swapChain.Width, swapChain.Height);
				cmd.SetScissor (swapChain.Width, swapChain.Height);
				cmd.BindDescriptorSet (plFSQ.Layout, dsFSQ);

				cmd.Draw (3);

				plFSQ.RenderPass.End (cmd);

				cmd.End ();
		}		
		void buildCommandBuffers() {
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			stroke();

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				buildCommandBuffer(cmds[i], frameBuffers[i]);
			}
		}
		public override void Update()
		{
			dev.WaitIdle();
			stroke();
		}
		protected override void OnResize () {
			base.OnResize ();

			updateViewRequested = true;

			frameBuffers?.Dispose();
			frameBuffers = plFSQ.RenderPass.CreateFrameBuffers(swapChain);

			imgContour?.Dispose();
			imgContour = new vke.Image (dev, rastFormat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Storage, VkMemoryPropertyFlags.DeviceLocal, swapChain.Width, swapChain.Height, VkImageType.Image2D, contourSamplesCount);
			imgContour.CreateView();

			fbContour = new FrameBuffer(plContours.RenderPass, swapChain.Width, swapChain.Height, 1, imgContour);

			imgColor?.Dispose();
			imgColor = new vke.Image (dev, VkFormat.R8g8b8a8Unorm, VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage, VkMemoryPropertyFlags.DeviceLocal, swapChain.Width, swapChain.Height, VkImageType.Image2D, contourSamplesCount);
			imgColor.CreateSampler();
			imgColor.CreateView();


			DescriptorSetWrites compUpdate = new DescriptorSetWrites (dsComp, plComp.Layout.DescriptorSetLayouts[0]);
			imgColor.Descriptor.imageLayout = VkImageLayout.General;
			imgContour.Descriptor.imageLayout = VkImageLayout.General;
			compUpdate.Write (dev, imgContour.Descriptor, imgColor.Descriptor);

			DescriptorSetWrites fsqUpdate = new DescriptorSetWrites (dsFSQ, plFSQ.Layout.DescriptorSetLayouts[0]);
			imgColor.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			fsqUpdate.Write (dev, imgColor.Descriptor);

			buildCommandBuffers ();
			
		}
		//clean up
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					plContours.Dispose();
					plComp.Dispose ();
					plFSQ.Dispose();
					imgContour?.Dispose();
					imgColor?.Dispose();
					frameBuffers?.Dispose();
					fbContour.Dispose();
					descriptorPool.Dispose ();
					vbo.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
