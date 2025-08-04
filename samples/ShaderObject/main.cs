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

//the traditional triangle sample with shader object
namespace ShaderObjectSample {
	class Program : SampleBase {
        public override string[] EnabledDeviceExtensions => new string[] {
			Ext.D.VK_KHR_swapchain,
			Ext.D.VK_EXT_shader_object,
			Ext.D.VK_KHR_dynamic_rendering,
			Ext.D.VK_EXT_extended_dynamic_state,
			Ext.D.VK_EXT_vertex_input_dynamic_state,
			Ext.D.VK_KHR_maintenance2,
			Ext.D.VK_KHR_create_renderpass2,
			Ext.D.VK_KHR_depth_stencil_resolve
		};
			public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};
        protected override void configureEnabledFeatures(VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features)
        {
			enabled_features.fillModeNonSolid = available_features.fillModeNonSolid;
        }


        static void Main(string[] args)
		{
			Instance.VK_MINOR = 3;
#if DEBUG
			Instance.VALIDATION = true;
#endif
			using (Program vke = new Program())
			{
				vke.Run();
			}
		}

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, zoom = 1f;

		//vertex structure
		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			Vector3 position;
			Vector3 color;

			public Vertex (float x, float y, float z, float r, float g, float b) {
				position = new Vector3 (x, y, z);
				color = new Vector3 (r, g, b);
			}
		}

		HostBuffer ibo;     //a host mappable buffer to hold the indices.
		HostBuffer vbo;     //a host mappable buffer to hold vertices.
		HostBuffer<Matrix4x4> uboMVPmatrix; //a host mappable buffer for mvp matrice.

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;//descriptor set for the mvp matrice.
		PipelineLayout pipelineLayout;

		//FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		//GraphicPipeline pipeline;   //the triangle rendering pipeline

		LinkedShaderObjects shaders;

		//triangle vertices (position + color per vertex) and indices.
		Vertex[] vertices = {
			new Vertex (-1.0f, -1.0f, 0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex ( 1.0f, -1.0f, 0.0f ,  0.0f, 1.0f, 0.0f),
			new Vertex ( 0.0f,  1.0f, 0.0f ,  0.0f, 0.0f, 1.0f),
		};
		ushort[] indices = new ushort[] { 0, 1, 2 };

		IntPtr deviceCreationPNext = IntPtr.Zero;
        public override IntPtr DeviceCreationPNext => deviceCreationPNext;

		

		protected override void selectPhysicalDevice()
		{
			base.selectPhysicalDevice();

			VkPhysicalDeviceFeatures2 phyFeat2 = VkPhysicalDeviceFeatures2.New;
			using (var dynRenderingFeat = new PNext<VkPhysicalDeviceDynamicRenderingFeatures>())
			{
				using (var shadObjFeat = new PNext<VkPhysicalDeviceShaderObjectFeaturesEXT>(dynRenderingFeat))
				{
					phyFeat2.pNext = shadObjFeat;
					Vk.vkGetPhysicalDeviceFeatures2(phy.Handle, ref phyFeat2);

					Console.WriteLine($"ShaderObject Support:\t{shadObjFeat.Val.shaderObject}");
					Console.WriteLine($"Dynamic Rendering Support:\t{dynRenderingFeat.Val.dynamicRendering}");

				}
			}
		}
		vke.DebugUtils.Messenger dbgmsg;
		
        protected override void initVulkan()
		{
			using (PinnedObjects pctx = new PinnedObjects())
			{
				VkPhysicalDeviceShaderObjectFeaturesEXT shadObjFeat = new VkPhysicalDeviceShaderObjectFeaturesEXT(true);
				VkPhysicalDeviceDynamicRenderingFeatures dynRenderFeat = new VkPhysicalDeviceDynamicRenderingFeatures(true);
				shadObjFeat.shaderObject = VkBool32.True;
				shadObjFeat.pNext = dynRenderFeat.Pin(pctx);
				deviceCreationPNext = shadObjFeat.Pin(pctx);
				base.initVulkan();
			}
			dbgmsg = new vke.DebugUtils.Messenger (instance, VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);			


			DescriptorSetLayout dsLayout = new DescriptorSetLayout(dev, new VkDescriptorSetLayoutBinding(0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer));

			ShaderObject vertexShader = new ShaderObject(dev, VkShaderStageFlags.Vertex, VkShaderStageFlags.Fragment, dsLayout);
			vertexShader.FileName = "#shaders.main.vert.spv";

			ShaderObject fragmentShader = new ShaderObject(dev, VkShaderStageFlags.Fragment, 0, dsLayout);
			fragmentShader.FileName = "#shaders.main.frag.spv";

			shaders = new LinkedShaderObjects(vertexShader, fragmentShader);
			shaders.Activate();

			pipelineLayout = new PipelineLayout(dev, dsLayout);
			pipelineLayout.Activate();


			vbo = new HostBuffer<Vertex>(dev, VkBufferUsageFlags.VertexBuffer, vertices);
			ibo = new HostBuffer<ushort>(dev, VkBufferUsageFlags.IndexBuffer, indices);

			uboMVPmatrix = new HostBuffer<Matrix4x4>(dev, VkBufferUsageFlags.UniformBuffer, 1, true);

			descriptorPool = new DescriptorPool(dev, 1, new VkDescriptorPoolSize(VkDescriptorType.UniformBuffer));

			/*
				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);//position + color
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.main.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv")
				);
			*/

			descriptorSet = descriptorPool.Allocate(dsLayout);
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites(descriptorSet, dsLayout);
			uboUpdate.Write(dev, uboMVPmatrix.Descriptor);
			cmds = cmdPool.AllocateCommandBuffer(swapChain.ImageCount);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			uboMVPmatrix.AsSpan()[0] =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom) *
				Helpers.CreatePerspectiveFieldOfView (Helpers.DegreesToRadians (45f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);

			base.UpdateView ();
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton (MouseButton.Left) == InputAction.Press) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
				updateViewRequested = true;
			} else if (GetButton (MouseButton.Right) == InputAction.Press) {
				zoom += zoomSpeed * (float)diffY;
				updateViewRequested = true;
			}
		}


		public static void GetDelegate(VkDevice dev, string name, ref IntPtr ptr)
		{
			GCHandle gCHandle = GCHandle.Alloc(System.Text.Encoding.UTF8.GetBytes(name + "\0"), GCHandleType.Pinned);
			IntPtr intPtr = Vk.vkGetDeviceProcAddr(dev, gCHandle.AddrOfPinnedObject());
			if (!(intPtr == IntPtr.Zero))
			{
				ptr = intPtr;
			}

			gCHandle.Free();
		}
		void buildCommandBuffers()
		{
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			for (int i = 0; i < swapChain.ImageCount; ++i)
				buildCommandBuffer(cmds[i], i);
		}
		public class RenderingInfo
		{
			List<VkRenderingAttachmentInfo> attachments = new List<VkRenderingAttachmentInfo>();
			VkRenderingInfo info;
			public RenderingInfo(VkRect2D renderArea)
			{
				info.renderArea = renderArea;
				info.layerCount = 1;
			}
			public void AddAttachment (
				VkImageView imageView,
				VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear,
				VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store,
				VkImageLayout imageLayout = VkImageLayout.ColorAttachmentOptimal,
				VkClearValue clearValue = default
				) {
				attachments.Add (new VkRenderingAttachmentInfo {
					imageView = imageView,
					imageLayout = imageLayout,
					loadOp = loadOp,
					storeOp = storeOp,
					clearValue = clearValue
				});
			}
			public void Begin(CommandBuffer cmd)
			{
				VkRenderingInfo i = info;
				i.pColorAttachments = attachments;
				Vk.vkCmdBeginRendering(cmd.Handle, ref i);// ref renderingInfo);			
			}
		}

		void buildCommandBuffer(PrimaryCommandBuffer cmd, int imageIndex) {
			//FrameBuffer fb = frameBuffers[i];
			cmd.Start ();
			swapChain.images[imageIndex].SetLayout(cmd, VkImageAspectFlags.Color, VkImageLayout.ColorAttachmentOptimal);

			/*VkRenderingAttachmentInfo colorAttachment = VkRenderingAttachmentInfo.New;
			colorAttachment.clearValue = new VkClearValue(0, 0, 0);
			colorAttachment.imageLayout = VkImageLayout.ColorAttachmentOptimal;
			colorAttachment.loadOp = VkAttachmentLoadOp.Clear;
			colorAttachment.storeOp = VkAttachmentStoreOp.Store;
			colorAttachment.imageView = swapChain.images[imageIndex].Descriptor.imageView;

			VkRenderingInfo renderingInfo = VkRenderingInfo.New;
			renderingInfo.renderArea = new VkRect2D(swapChain.Width, swapChain.Height);
			renderingInfo.pColorAttachments = colorAttachment;
			renderingInfo.layerCount = 1;*/
			
			RenderingInfo ri = new RenderingInfo(new VkRect2D(swapChain.Width, swapChain.Height));
			ri.AddAttachment(swapChain.images[imageIndex].Descriptor.imageView);
			ri.Begin(cmd);

				//colorAttachment.clearValue
			// New structures are used to define the attachments used in dynamic rendering
			/*	VkRenderingAttachmentInfo colorAttachment = new VkRenderingAttachmentInfo(VkImageLayout.ColorAttachmentOptimal, VkImageLayout.Undefined, VkAttachmentLoadOp.Clear,
				VkAttachmentStoreOp.Store, new VkClearValue(0, 0, 0));
			colorAttachment.imageView = swapChain.images[i].Descriptor.imageView;


			VkRenderingInfo renderingInfo = VkRenderingInfo.New;
			renderingInfo.renderArea = new VkRect2D(swapChain.Width, swapChain.Height);
			renderingInfo.pColorAttachments = colorAttachment;
			renderingInfo.layerCount = 1;*/
			/*IntPtr test = IntPtr.Zero;
			GetDelegate(dev.Handle, "vkCmdBeginRendering", ref test);

			Vk.vkCmdBeginRendering(cmd.Handle, ref renderingInfo);*/

			//cmd.SetViewport (swapChain.Width, swapChain.Height);
			//cmd.SetScissor (swapChain.Width, swapChain.Height);

			cmd.SetCullMode(VkCullModeFlags.None);
			cmd.SetFrontFace(VkFrontFace.Clockwise);
			cmd.SetDepthTestEnable(false);
			cmd.SetPrimitiveTopology(VkPrimitiveTopology.TriangleList);
			cmd.SetPrimitiveRestartEnable(false);
			cmd.SetPolygonModeEXT(VkPolygonMode.Fill);
			cmd.SetStencilTestEnable(false);
			cmd.SetRasterizerDiscardEnable(false);

			cmd.SetDepthWriteEnable(false);
			cmd.SetDepthBiasEnable(false);
			cmd.SetRasterizationSamplesEXT(VkSampleCountFlags.SampleCount1);
			cmd.SetSampleMaskEXT(VkSampleCountFlags.SampleCount1, 0xFF);
			cmd.SetAlphaToCoverageEnableEXT(false);
			cmd.SetColorBlendEnableEXT(0, false);

			cmd.SetColorWriteMaskEXT(0, 0xf);
			cmd.SetViewport(new VkViewport { width = swapChain.Width, height = swapChain.Height });
			cmd.SetScissor(new VkRect2D (swapChain.Width, swapChain.Height));

			cmd.SetLineWidth(1.0f);

			VkVertexInputBindingDescription2EXT vxBindingDesc = new VkVertexInputBindingDescription2EXT(
				0, (uint)Marshal.SizeOf<Vertex>(), VkVertexInputRate.Vertex, 1);
			
			VkVertexInputAttributeDescription2EXT[] vxAttribDesc = {
				new VkVertexInputAttributeDescription2EXT(0, 0, VkFormat.R32g32b32Sfloat, 0),
				new VkVertexInputAttributeDescription2EXT(1, 0, VkFormat.R32g32b32Sfloat, 12)
			};

			using (PinnedObjects pctx = new PinnedObjects()) {
				Vk.vkCmdSetVertexInputEXT(cmd.Handle, 1, vxBindingDesc.Pin(pctx), (uint)vxAttribDesc.Length, vxAttribDesc.Pin(pctx));
			
				cmd.BindDescriptorSet (pipelineLayout, descriptorSet);
				cmd.BindVertexBuffer (vbo);
				cmd.BindIndexBuffer (ibo, VkIndexType.Uint16);

								
				Vk.vkCmdBindShadersEXT(cmd.Handle, shaders.Count, shaders.Stages.Pin(pctx), shaders.ShaderHandles.Pin(pctx));
			}

			cmd.DrawIndexed ((uint)indices.Length);

			Vk.vkCmdEndRendering(cmd.Handle);
			swapChain.images[imageIndex].SetLayout(cmd, VkImageAspectFlags.Color, VkImageLayout.PresentSrcKHR);

			cmd.End ();
			
		}

		protected override void OnResize () {
			base.OnResize ();
			updateViewRequested = true;
			buildCommandBuffers ();
		}
		//clean up
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					//pipeline clean up will dispose PipelineLayout, DescriptorSet layouts and render pass automatically. If their reference count is zero, their handles will be destroyed.
					//pipeline.Dispose ();
					//frame buffers are automatically activated on creation as for resources, so it requests an explicit call to dispose.
					//frameBuffers?.Dispose();
					shaders.Dispose();
					pipelineLayout.Dispose();
					//the descriptor pool
					descriptorPool.Dispose ();
					//resources have to be explicityly disposed.
					vbo.Dispose ();
					ibo.Dispose ();
					uboMVPmatrix.Dispose ();
					dbgmsg.Dispose();
				}
			}

			base.Dispose (disposing);
		}
	}
}
