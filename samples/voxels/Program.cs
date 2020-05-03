using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;

namespace voxels {
	class Program : VkCrowWindow {
		static void Main (string [] args)
		{
			Instance.VALIDATION = true;
			Instance.RENDER_DOC_CAPTURE = false;

			using (Program app = new Program ())
				app.Run ();
		}

		public override string [] EnabledInstanceExtensions => new string [] {
#if DEBUG
			Ext.I.VK_EXT_debug_utils
#endif
		};

		public override string [] EnabledDeviceExtensions => new string [] {
			Ext.D.VK_KHR_swapchain,
#if DEBUG
			Ext.D.VK_EXT_debug_marker
#endif
		};
		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features)
		{
			base.configureEnabledFeatures (available_features, ref enabled_features);
			enabled_features.geometryShader = available_features.geometryShader;
			if (!available_features.fragmentStoresAndAtomics)
				throw new Exception ("FragmentStoresAndAtomics extention is mandatory.");
			enabled_features.fragmentStoresAndAtomics = true;
		}

#if DEBUG
		vke.DebugUtils.Messenger dbgmsg;
#endif

		VkSampleCountFlags NUM_SAMPLES = VkSampleCountFlags.SampleCount1;
		static uint VOXEL_GRID_SIZE = 256;

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 2f;

		SimpleModel model;

		Program () : base ()
		{
#if DEBUG
			dbgmsg = new vke.DebugUtils.Messenger (instance, VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);
#endif

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);

			CreateInterface ();

			model = new SimpleModel (presentQueue, Utils.DataDirectory + "models/DamagedHelmet/glTF/DamagedHelmet.gltf");

			initVoxelizer ();
		}

		GraphicPipeline vxPL;
		CommandBuffer vxCMD;
		FrameBuffer vxFB;
		Image vxImg3D;

		void initVoxelizer () {
			vxImg3D = new Image (dev, VkFormat.R8g8b8a8Unorm, VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled, VkMemoryPropertyFlags.DeviceLocal,
				VOXEL_GRID_SIZE, VOXEL_GRID_SIZE, VkImageType.Image3D, VkSampleCountFlags.SampleCount1, VkImageTiling.Optimal, 1, 1, VOXEL_GRID_SIZE);
			vxImg3D.CreateView (VkImageViewType.ImageView3D);
			vxImg3D.CreateSampler ();
			vxImg3D.Descriptor.imageLayout = VkImageLayout.General;

			createVoxelizerPL ();

			vxFB = new FrameBuffer (vxPL.RenderPass, (uint)VOXEL_GRID_SIZE, (uint)VOXEL_GRID_SIZE);
			vxFB.Activate ();

			DescriptorSetWrites texturesUpdate = new DescriptorSetWrites (dsTextures, descLayoutTextures);
			texturesUpdate.Write (dev,
				model.textures [0].Descriptor,
				model.textures [1].Descriptor,
				model.textures [2].Descriptor,
				vxImg3D.Descriptor);

			createVoxelizerCmd ();

			GraphicQueue.Submit (vxCMD);

			dev.WaitIdle ();
		}
		void destroyVoxelizer() {
			vxFB.Dispose ();
			vxPL.Dispose ();
			vxImg3D.Dispose ();

			descLayoutMatrix.Dispose ();
			descLayoutTextures.Dispose ();
			descriptorPool.Dispose ();
			uboMats.Dispose ();
		}

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
		}

		public struct PushConstants {
			public Matrix4x4 matrix;
		}

		Matrices matrices;

		HostBuffer uboMats;

		DescriptorPool descriptorPool;
		DescriptorSet dsMatrices, dsTextures;
		DescriptorSetLayout descLayoutMatrix;
		DescriptorSetLayout descLayoutTextures;

		FrameBuffers frameBuffers;

		void createVoxelizerPL () {
			descriptorPool = new DescriptorPool (dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 3),
				new VkDescriptorPoolSize (VkDescriptorType.StorageImage));

			descLayoutMatrix = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer));

			descLayoutTextures = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (3, VkShaderStageFlags.Fragment, VkDescriptorType.StorageImage)
			);

			VkPushConstantRange pushConstantRange = new VkPushConstantRange {
				stageFlags = VkShaderStageFlags.Vertex,
				size = (uint)Marshal.SizeOf<PushConstants> (),
				offset = 0
			};

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, NUM_SAMPLES, false,
				(int)VOXEL_GRID_SIZE, (int)VOXEL_GRID_SIZE);

			cfg.rasterizationState.cullMode = VkCullModeFlags.Back;
			if (NUM_SAMPLES != VkSampleCountFlags.SampleCount1) {
				cfg.multisampleState.sampleShadingEnable = true;
				cfg.multisampleState.minSampleShading = 0.5f;
			}

			cfg.Layout = new PipelineLayout (dev, pushConstantRange, descLayoutMatrix, descLayoutTextures);

			cfg.RenderPass = new RenderPass (dev, cfg.Samples);
			cfg.RenderPass.AddSubpass (new SubPass());
			cfg.RenderPass.AddDependency (Vk.SubpassExternal, 0,
				VkPipelineStageFlags.BottomOfPipe, VkPipelineStageFlags.ColorAttachmentOutput,
				VkAccessFlags.MemoryRead, VkAccessFlags.ColorAttachmentWrite);
			cfg.RenderPass.AddDependency (0, Vk.SubpassExternal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.BottomOfPipe,
				VkAccessFlags.ColorAttachmentWrite, VkAccessFlags.MemoryRead);

			cfg.AddVertexBinding<Model.Vertex> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);

			cfg.AddShader (VkShaderStageFlags.Vertex, "#voxels.voxelizer.vert.spv");
			cfg.AddShader (VkShaderStageFlags.Geometry, "#voxels.voxelizer.geom.spv");
			cfg.AddShader (VkShaderStageFlags.Fragment, "#voxels.voxelizer.frag.spv");

			vxPL = new GraphicPipeline (cfg);

			dsMatrices = descriptorPool.Allocate (descLayoutMatrix);
			dsTextures = descriptorPool.Allocate (descLayoutTextures);

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsMatrices, descLayoutMatrix);
			uboUpdate.Write (dev, uboMats.Descriptor);

			uboMats.Map ();//permanent map
		}
		void createVoxelizerCmd () {
			if (vxCMD != null)
				cmdPool.FreeCommandBuffers (vxCMD);
			vxCMD = cmdPool.AllocateAndStart ();

			vxImg3D.SetLayout (vxCMD, VkImageAspectFlags.Color, VkImageLayout.General);

			vxPL.RenderPass.Begin (vxCMD, vxFB);

			//cmds [i].SetViewport (swapChain.Width, swapChain.Height);
			//cmds [i].SetScissor (swapChain.Width, swapChain.Height);

			vxCMD.BindDescriptorSet (vxPL.Layout, dsMatrices);
			vxCMD.BindDescriptorSet (vxPL.Layout, dsTextures, 1);

			PushConstants pc = new PushConstants { matrix = Matrix4x4.Identity };
			vxCMD.PushConstant (vxPL.Layout, VkShaderStageFlags.Vertex, pc, (uint)Marshal.SizeOf<Matrix4x4> ());

			vxCMD.BindPipeline (vxPL);

			model.DrawAll (vxCMD, vxPL.Layout);

			vxPL.RenderPass.End (vxCMD);

			vxCMD.End ();
		}


		public override void UpdateView ()
		{
			matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (45f),
				(float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f) * Camera.VKProjectionCorrection;
			matrices.view =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom);
			matrices.model = Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, -0.5f*(float)Math.PI);
			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			updateViewRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos)
		{
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton [0]) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (MouseButton [1]) {
				zoom += zoomSpeed * (float)diffY;
			}
			updateViewRequested = true;
		}
		void buildCommandBuffers ()
		{
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers [i];
				cmds [i].Start ();

				vxPL.RenderPass.Begin (cmds [i], fb);

				//cmds [i].SetViewport (swapChain.Width, swapChain.Height);
				//cmds [i].SetScissor (swapChain.Width, swapChain.Height);

				cmds [i].BindDescriptorSet (vxPL.Layout, dsMatrices);
				cmds [i].BindDescriptorSet (vxPL.Layout, dsTextures, 1);

				PushConstants pc = new PushConstants { matrix = Matrix4x4.Identity };
				cmds [i].PushConstant (vxPL.Layout, VkShaderStageFlags.Vertex, pc, (uint)Marshal.SizeOf<Matrix4x4> ());

				cmds [i].BindPipeline (vxPL);

				model.DrawAll (cmds [i], vxPL.Layout);

				vxPL.RenderPass.End (cmds [i]);

				cmds [i].End ();
			}
		}

		protected override void OnResize ()
		{
			base.OnResize ();

			frameBuffers?.Dispose ();
			frameBuffers = vxPL.RenderPass.CreateFrameBuffers (swapChain);

			buildCommandBuffers ();
		}


		protected override void Dispose (bool disposing)
		{
			dev.WaitIdle ();

			if (disposing) {
				if (!isDisposed) {
					destroyVoxelizer ();

					model?.Dispose ();
					frameBuffers?.Dispose ();
#if DEBUG
					dbgmsg?.Dispose ();
#endif
				}
			}


			base.Dispose (disposing);
		}
	}
}
