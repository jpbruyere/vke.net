// Copyright (c) 2023  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;
using System.Collections.Generic;

namespace FillTests {

	class Program : SampleBase {
		static void Main (string[] args) {
#if DEBUG
			//Instance.VALIDATION = true;
			//Instance.RENDER_DOC_CAPTURE = true;
#endif
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}


		FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		FrameBuffer fbContour;
		
		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			public Vector2 position;
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
		List<Vertex> vertices = new List<Vertex>(){
			new Vertex (100.0f, 100.0f,  1),
			new Vertex (500.0f, 400.0f,  1),
			new Vertex (450.0f,  150.0f,  1),
			new Vertex (120.0f, 420.0f,  1),
		};
		HostBuffer vbo;
		HostBuffer ibo;
		GraphicPipeline plContours;
		GraphicPipeline plFSQ;
		ComputePipeline plComp;
		DescriptorPool descriptorPool;
		DescriptorSet dsComp, dsFSQ;//descriptor set for the mvp matrice.
		vke.Image imgContour, imgColor;

		VkFormat rastFormat = VkFormat.B8g8r8a8Uint;
		const VkSampleCountFlags contourSamplesCount = VkSampleCountFlags.SampleCount1;

		void createIbo() {
			dev.WaitIdle();
			ibo?.Dispose();
			List<UInt16> indices = new List<ushort>(vertices.Count*2);
			for (int i = 0; i < vertices.Count-1; i++)
			{
				indices.Add((UInt16)i);
				indices.Add((UInt16)(i+1));
			}
			indices.Add((UInt16)(vertices.Count-1));
			indices.Add((UInt16)0);

			ibo = new HostBuffer<UInt16>(dev, VkBufferUsageFlags.IndexBuffer,indices.ToArray());
			ibo.SetName("ibo");
		}
		protected override void initVulkan () {
			base.initVulkan ();

			foreach (VkFormat format in Enum.GetValues(typeof(VkFormat)))
			{
				if (phy.TryGetImageFormatProperties(format, VkImageTiling.Optimal, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Storage, out VkImageFormatProperties imgProps)) {
					Console.Write($"- {format}:");
					VkFormatProperties p = phy.GetFormatProperties(format);
					if (p.optimalTilingFeatures.HasFlag(VkFormatFeatureFlags.ColorAttachmentBlend)) {
						Console.Write($"\tblending");
					}
					Console.WriteLine("");
				}

				
			} 


			UpdateFrequency = 20;

			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices.ToArray());
			vbo.SetName("vbo");

			createIbo();

			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.LineList, contourSamplesCount, false)) {
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
				
				cfg.blendAttachments[0] = (new VkPipelineColorBlendAttachmentState (true, VkBlendFactor.One, VkBlendFactor.One, VkBlendOp.Add,VkBlendFactor.One,VkBlendFactor.One,VkBlendOp.Add));

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
		Vector2 curMousePos;
		const float pointSizeSquared = 50;
		
		int curPoint = - 1, hoverPoint = -1;
		volatile bool updateVbo = false;
		void addPoint(float x, float y) {
			vertices.Add(new Vertex(x,y,1));
		}
		protected override async void onMouseMove(double xPos, double yPos)
		{
			base.onMouseMove(xPos, yPos);

			curMousePos = new Vector2((float)xPos, (float)yPos);

			if (curPoint < 0) {
				for (int i = 0; i < vertices.Count; i++)
				{
					if (Vector2.DistanceSquared(curMousePos, vertices[i].position) < pointSizeSquared) {
						hoverPoint = i;
						return;
					}
				}
				hoverPoint = -1;
			} else {
				vertices[curPoint] = new Vertex(curMousePos.X, curMousePos.Y, 1);
				updateVbo = true;
			}
		}
		protected override void onMouseButtonDown(MouseButton button, Modifier mods)
		{
			if (hoverPoint < 0) {
				addPoint(curMousePos.X, curMousePos.Y);
				updateVbo = true;
			} else {
				curPoint = hoverPoint;
			}
		}
		protected override void onMouseButtonUp(MouseButton button, Modifier mods)
		{
			curPoint = -1;
		}
		protected override void onKeyDown(Key key, int scanCode, Modifier modifiers)
		{
			switch (key)
			{
				case Key.KeypadSubtract:
					if (vertices.Count > 3) {
						vertices.RemoveAt(vertices.Count - 1);
						updateVbo = true;
					}
					break;
				default:
					base.onKeyDown(key, scanCode, modifiers);
					break;
			}
			
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
			cmd.BindIndexBuffer (ibo, VkIndexType.Uint16);
			cmd.DrawIndexed ((uint)vertices.Count*2);

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

			if (updateVbo) {
				vbo?.Dispose();
				vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices.ToArray());
				vbo.SetName("vbo");
				createIbo();
				updateVbo = false;
			}

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
