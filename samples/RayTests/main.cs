using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using vke.glTF;
using Glfw;
using Vulkan;
using vke.Environment;
using Image = vke.Image;

namespace deferred {
	class Deferred : VkWindow {
		static VkSampleCountFlags NUM_SAMPLES = VkSampleCountFlags.SampleCount4;
		static VkFormat HDR_FORMAT = VkFormat.R16g16b16a16Sfloat;
		static VkFormat MRT_FORMAT = VkFormat.R16g16b16a16Sfloat;
		static int MAX_MATERIAL_COUNT = 4;


		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
			//Instance.RENDER_DOC_CAPTURE = true;
#endif
			SwapChain.PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;
			PbrModelTexArray.TEXTURE_DIM = 1024;

			using (Deferred vke = new Deferred ()) {
				vke.Run ();
			}
		}

		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};

		public override string[] EnabledDeviceExtensions => new string[] {
			Ext.D.VK_KHR_swapchain,
		};

		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
			base.configureEnabledFeatures (available_features, ref enabled_features);

			enabled_features.samplerAnisotropy = available_features.samplerAnisotropy;
			enabled_features.sampleRateShading = available_features.sampleRateShading;
			enabled_features.geometryShader = available_features.geometryShader;

			enabled_features.textureCompressionBC = available_features.textureCompressionBC;
		}

		protected override void createQueues () {
			base.createQueues ();
			transferQ = new Queue (dev, VkQueueFlags.Transfer);
			computeQ = new Queue (dev, VkQueueFlags.Compute);
		}

		public enum DebugView {
			none,
			color,
			normal,
			pos,
			occlusion,
			emissive,
			metallic,
			roughness,
			depth,
			prefill,
			irradiance,
			shadowMap
		}
		DebugView currentDebugView = DebugView.none;
		int lightNumDebug = 0;
		int debugMip = 0;
		int debugFace = 0;

		const float lightMoveSpeed = 0.1f;
		float exposure = 2.0f;
		float gamma = 1.2f;

		public struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 model;
			public Matrix4x4 view;
			public Vector4 camPos;
			public float prefilteredCubeMipLevels;
			public float scaleIBLAmbient;
		}
		public struct Light {
			public Vector4 position;
			public Vector4 color;
			public Matrix4x4 mvp;
		}

		public Matrices matrices = new Matrices {
			scaleIBLAmbient = 0.5f,
		};

		public Light [] lights = {
			new Light {
				position = new Vector4(2.5f,5.5f,2,0f),
				color = new Vector4(1,1.0f,1.0f,1)
			},
			/*new Light {
				position = new Vector4(-2.5f,5.5f,2,0f),
				color = new Vector4(0.8f,0.8f,1,1)
			}*/
		};

		int curModelIndex = 0;
		bool reloadModel;
		bool rebuildBuffers;

		vke.DebugUtils.Messenger dbgmsg;
		Queue transferQ, computeQ;
		PipelineCache pipelineCache;


		GraphicPipeline plGBuff, plLighting;

		FrameBuffers fbsMain;

		RenderPass rpGBuff;
		FrameBuffer fbGBuff;
		Image gbColorRough, gbEmitMetal, gbN_AO, gbPos;


		DescriptorPool descriptorPool;
		DescriptorSetLayout dslMain, dslMaterial, dslLighting;
		DescriptorSet dsMain, dsMaterial, dsLighting;

		HostBuffer<Light> uboLights;
		HostBuffer uboMatrices;

		EnvironmentCube envCube;
		PbrModel model;
		BoundingBox modelAABB;


		Deferred () : base("deferred") {


		}
		protected override void initVulkan () {
			base.initVulkan ();
			dbgmsg = new vke.DebugUtils.Messenger (instance, VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT | VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT | VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);

			//pipelineCache = new PipelineCache (dev);

			camera = new Camera (Utils.DegreesToRadians (45f), 1f, 0.1f, 16f);
			camera.SetPosition (0, -0.1f, -1);

			init ();

			LoadModel (transferQ, vke.samples.Utils.GltfFiles[curModelIndex]);
		}

		void LoadModel (Queue transferQ, string path)
		{
			dev.WaitIdle ();
			model?.Dispose ();

			PbrModelTexArray mod = new PbrModelTexArray (transferQ, path);
			if (mod.texArray != null) {
				DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsMaterial, dslMaterial);
				uboUpdate.Write (dev, mod.materialUBO.Descriptor, mod.texArray.Descriptor);
			}
			model = mod;

			modelAABB = model.DefaultScene.AABB;

			camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (modelAABB.Width, modelAABB.Height), modelAABB.Depth));

			reloadModel = false;
		}


		void create_gbuff_pipeline ()
		{
			rpGBuff = new RenderPass (dev, NUM_SAMPLES);
			rpGBuff.AddAttachment (dev.GetSuitableDepthFormat (), VkImageLayout.DepthStencilAttachmentOptimal, NUM_SAMPLES);
			rpGBuff.AddAttachment (VkFormat.R8g8b8a8Unorm, VkImageLayout.ShaderReadOnlyOptimal, NUM_SAMPLES, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkImageLayout.ShaderReadOnlyOptimal);//GBuff0 (color + roughness) and final color before resolve
			rpGBuff.AddAttachment (VkFormat.R8g8b8a8Unorm, VkImageLayout.ShaderReadOnlyOptimal, NUM_SAMPLES, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkImageLayout.ShaderReadOnlyOptimal);//GBuff1 (emit + metal)
			rpGBuff.AddAttachment (MRT_FORMAT, VkImageLayout.ShaderReadOnlyOptimal, NUM_SAMPLES, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkImageLayout.ShaderReadOnlyOptimal);//GBuff2 (normals + AO)
			rpGBuff.AddAttachment (MRT_FORMAT, VkImageLayout.ShaderReadOnlyOptimal, NUM_SAMPLES, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkImageLayout.ShaderReadOnlyOptimal);//GBuff3 (Pos + depth)

			rpGBuff.ClearValues.Add (new VkClearValue { depthStencil = new VkClearDepthStencilValue (1.0f, 0) });
			rpGBuff.ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
			rpGBuff.ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
			rpGBuff.ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });
			rpGBuff.ClearValues.Add (new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.0f) });

			rpGBuff.AddSubpass (new SubPass ());
			rpGBuff.SubPasses [0].SetDepthReference (0, VkImageLayout.DepthStencilAttachmentOptimal);
			rpGBuff.SubPasses [0].AddColorReference (
									new VkAttachmentReference (1, VkImageLayout.ColorAttachmentOptimal),
									new VkAttachmentReference (2, VkImageLayout.ColorAttachmentOptimal),
									new VkAttachmentReference (3, VkImageLayout.ColorAttachmentOptimal),
									new VkAttachmentReference (4, VkImageLayout.ColorAttachmentOptimal));

			rpGBuff.AddDependency (Vk.SubpassExternal, 0,
				VkPipelineStageFlags.BottomOfPipe, VkPipelineStageFlags.ColorAttachmentOutput,
				VkAccessFlags.MemoryRead, VkAccessFlags.ColorAttachmentWrite);
			rpGBuff.AddDependency (0, Vk.SubpassExternal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.BottomOfPipe,
				VkAccessFlags.ColorAttachmentWrite, VkAccessFlags.MemoryRead);

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, NUM_SAMPLES);
			cfg.rasterizationState.cullMode = VkCullModeFlags.Back;
			if (NUM_SAMPLES != VkSampleCountFlags.SampleCount1) {
				cfg.multisampleState.sampleShadingEnable = true;
				cfg.multisampleState.minSampleShading = 0.5f;
			}
			cfg.Cache = pipelineCache;
			cfg.Layout = new PipelineLayout (dev, dslMain, dslMaterial);
			cfg.Layout.AddPushConstants (
				new VkPushConstantRange (VkShaderStageFlags.Vertex, 64),
				new VkPushConstantRange (VkShaderStageFlags.Fragment, sizeof (int), 64)
			);
			cfg.RenderPass = rpGBuff;
			cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));
			cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));
			cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));
			//cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));

			cfg.AddVertex<PbrModelTexArray.Vertex> ();
			using (SpecializationInfo constants = new SpecializationInfo (
						new SpecializationConstant<float> (0, camera.NearPlane),
						new SpecializationConstant<float> (1, camera.FarPlane),
						new SpecializationConstant<float> (2, MAX_MATERIAL_COUNT))) {

				cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#RayTests.GBuffPbr.vert.spv");
				cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#RayTests.GBuffPbrTexArray.frag.spv", constants);

				plGBuff = new GraphicPipeline (cfg);
			}
		}

		void create_lighting_pipeline ()
		{
			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, NUM_SAMPLES);
			cfg.rasterizationState.cullMode = VkCullModeFlags.Front;
			if (NUM_SAMPLES != VkSampleCountFlags.SampleCount1) {
				cfg.multisampleState.sampleShadingEnable = true;
				cfg.multisampleState.minSampleShading = 0.5f;
			}
			cfg.Cache = pipelineCache;
			cfg.Layout = new PipelineLayout (dev, dslMain, dslLighting);
			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, NUM_SAMPLES);
			//cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));
			cfg.depthStencilState.depthTestEnable = false;
			cfg.depthStencilState.depthWriteEnable = false;

			using (SpecializationInfo constants = new SpecializationInfo (
				new SpecializationConstant<uint> (0, (uint)lights.Length))) {
				cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
				cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#RayTests.compose.frag.spv", constants);
				plLighting = new GraphicPipeline (cfg);
			}
		}

		void init() {
			descriptorPool = new DescriptorPool (dev, 3,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer, 3),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 8)
			);

			dslMain = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer));//matrices and params
			dslMaterial = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer),//materials
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));//texture array)
			dslLighting = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//color + roughness
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//emit + metal
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//normals + AO
				new VkDescriptorSetLayoutBinding (3, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//Pos + depth
				new VkDescriptorSetLayoutBinding (4, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//irradiance
				new VkDescriptorSetLayoutBinding (5, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//prefiltCube
				new VkDescriptorSetLayoutBinding (6, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),//lutBRDF
				new VkDescriptorSetLayoutBinding (7, VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer));//lights

			uboMatrices = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices, true);
			uboLights = new HostBuffer<Light> (dev, VkBufferUsageFlags.UniformBuffer, lights, true);

			create_gbuff_pipeline ();

			create_lighting_pipeline ();

			dsMain = descriptorPool.Allocate (dslMain);
			dsMain.Handle.SetDebugMarkerName (dev, "dsMain");
			dsMaterial = descriptorPool.Allocate (dslMaterial);
			dsMain.Handle.SetDebugMarkerName (dev, "dsMaterial");
			dsLighting = descriptorPool.Allocate (dslLighting);
			dsMain.Handle.SetDebugMarkerName (dev, "dsLighting");

			EnvironmentCube.STR_FRAG_PATH = "#RayTests.skybox.frag.spv";
			envCube = new EnvironmentCube (vke.samples.Utils.CubeMaps[2], plLighting.Layout, presentQueue, plLighting.RenderPass);

			matrices.prefilteredCubeMipLevels = envCube.prefilterCube.CreateInfo.mipLevels;

			DescriptorSetWrites dsWrite = new DescriptorSetWrites (dsLighting, dslLighting.Bindings.GetRange (4, 4).ToArray());
			dsWrite.Write (dev,
				envCube.irradianceCube.Descriptor,
				envCube.prefilterCube.Descriptor,
				envCube.lutBrdf.Descriptor,
				uboLights.Descriptor);

			dsWrite = new DescriptorSetWrites (dsMain, dslMain);
			dsWrite.Write (dev, uboMatrices.Descriptor);
		}


		void createGBuff ()
		{
			fbGBuff?.Dispose ();

			gbColorRough?.Dispose ();
			gbEmitMetal?.Dispose ();
			gbN_AO?.Dispose ();
			gbPos?.Dispose ();

			gbColorRough = new Image (dev, VkFormat.R8g8b8a8Unorm, VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment , VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, NUM_SAMPLES);
			gbEmitMetal = new Image (dev, VkFormat.R8g8b8a8Unorm, VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment, VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, NUM_SAMPLES);
			gbN_AO = new Image (dev, MRT_FORMAT, VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment, VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, NUM_SAMPLES);
			gbPos = new Image (dev, MRT_FORMAT, VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment, VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, NUM_SAMPLES);


			gbColorRough.CreateView (); gbColorRough.CreateSampler ();
			gbColorRough.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			gbEmitMetal.CreateView (); gbEmitMetal.CreateSampler ();
			gbEmitMetal.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			gbN_AO.CreateView (); gbN_AO.CreateSampler ();
			gbN_AO.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			gbPos.CreateView (); gbPos.CreateSampler ();
			gbPos.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsLighting, dslLighting.Bindings.GetRange (0, 4).ToArray());
			uboUpdate.Write (dev,
				gbColorRough.Descriptor,
				gbEmitMetal.Descriptor,
				gbN_AO.Descriptor,
				gbPos.Descriptor);

			gbColorRough.SetName ("GBuffColorRough");
			gbEmitMetal.SetName ("GBuffEmitMetal");
			gbN_AO.SetName ("GBuffN");
			gbPos.SetName ("GBuffPos");

			fbGBuff = new FrameBuffer (rpGBuff, Width, Height, new Image [] {
					null, gbColorRough, gbEmitMetal, gbN_AO, gbPos});
		}

		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateAndStart ();
				PrimaryCommandBuffer cmd = cmds [i];

				rpGBuff.Begin (cmd, fbGBuff);

				cmd.BindDescriptorSets (VkPipelineBindPoint.Graphics, plGBuff.Layout, 0, dsMain, dsMaterial);

				cmd.SetViewport (fbsMain [i].Width, fbsMain [i].Height);
				cmd.SetScissor (fbsMain [i].Width, fbsMain [i].Height);

				if (model != null) {
					plGBuff.Bind (cmd);
					model.Bind (cmd);
					model.DrawAll (cmd, plGBuff.Layout);
				}

				rpGBuff.End (cmd);

				plLighting.RenderPass.Begin (cmd, fbsMain [i]);

				cmd.SetViewport (fbsMain [i].Width, fbsMain [i].Height);
				cmd.SetScissor (fbsMain [i].Width, fbsMain [i].Height);

				plLighting.Bind (cmd);
				plLighting.BindDescriptorSet (cmd, dsLighting, 1);

				cmd.Draw (3, 1, 0, 0);

				plLighting.RenderPass.End (cmd);

				cmd.End ();
			}
		}

		public override void UpdateView () {
			camera.AspectRatio = (float)Width / Height;

			matrices.projection = camera.Projection;
			matrices.view = camera.View;
			matrices.model = camera.Model;
			Matrix4x4.Invert (camera.View, out Matrix4x4 inv);
			matrices.camPos = new Vector4 (inv.M41, inv.M42, inv.M43, 0);

			uboMatrices.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());

			updateViewRequested = false;
		}

		public override void Update () {
			if (reloadModel) {
				LoadModel (transferQ, vke.samples.Utils.GltfFiles [curModelIndex]);

				updateViewRequested = true;
				rebuildBuffers = true;
			}

			if (rebuildBuffers) {
				buildCommandBuffers ();
				rebuildBuffers = false;
			}

		}

		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle ();

			UpdateView ();

			createGBuff ();

			fbsMain?.Dispose();
			fbsMain = plLighting.RenderPass.CreateFrameBuffers(swapChain);

			rebuildBuffers = true;

			dev.WaitIdle ();
		}

		#region Mouse and keyboard
		protected override void onScroll (double xOffset, double yOffset) {
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				camera.Rotate ((float)-diffX, (float)-diffY);
			} else if (MouseButton[1]) {
				camera.SetZoom ((float)diffY);
			} else
				return;

			updateViewRequested = true;
		}

		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			//switch (key) {
			//	case Key.F:
			//		if (modifiers.HasFlag (Modifier.Shift)) {
			//			renderer.debugFace--;
			//			if (renderer.debugFace < 0)
			//				renderer.debugFace = 5;
			//		} else {
			//			renderer.debugFace++;
			//			if (renderer.debugFace >= 5)
			//				renderer.debugFace = 0;
			//		}
			//		rebuildBuffers = true;
			//		break;
			//	case Key.M:
			//		if (modifiers.HasFlag (Modifier.Shift)) {
			//			renderer.debugMip--;
			//			if (renderer.debugMip < 0)
			//				renderer.debugMip = (int)renderer.envCube.prefilterCube.CreateInfo.mipLevels - 1;
			//		} else {
			//			renderer.debugMip++;
			//			if (renderer.debugMip >= renderer.envCube.prefilterCube.CreateInfo.mipLevels)
			//				renderer.debugMip = 0;
			//		}
			//		rebuildBuffers = true;
			//		break;
			//	case Key.L:
			//		if (modifiers.HasFlag (Modifier.Shift)) {
			//			renderer.lightNumDebug--;
			//			if (renderer.lightNumDebug < 0)
			//				renderer.lightNumDebug = (int)renderer.lights.Length - 1;
			//		} else {
			//			renderer.lightNumDebug++;
			//			if (renderer.lightNumDebug >= renderer.lights.Length)
			//				renderer.lightNumDebug = 0;
			//		}
			//		rebuildBuffers = true;
			//		break;
			//	case Key.Keypad0:
			//	case Key.Keypad1:
			//	case Key.Keypad2:
			//	case Key.Keypad3:
			//	case Key.Keypad4:
			//	case Key.Keypad5:
			//	case Key.Keypad6:
			//	case Key.Keypad7:
			//	case Key.Keypad8:
			//	case Key.Keypad9:
			//		renderer.currentDebugView = (DeferredPbrRenderer.DebugView)(int)key-320;
			//		rebuildBuffers = true;
			//		break;
			//	case Key.KeypadDivide:
			//		renderer.currentDebugView = DeferredPbrRenderer.DebugView.irradiance;
			//		rebuildBuffers = true;
			//		break;
			//	case Key.S:
			//		if (modifiers.HasFlag (Modifier.Control)) {
			//			renderer.pipelineCache.Save ();
			//			Console.WriteLine ($"Pipeline Cache saved.");
			//		} else {
			//			renderer.currentDebugView = DeferredPbrRenderer.DebugView.shadowMap;
			//			rebuildBuffers = true; 
			//		}
			//		break;
			//	case Key.Up:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight(-Vector4.UnitZ);
			//		else
			//			camera.Move (0, 0, 1);
			//		break;
			//	case Key.Down:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight (Vector4.UnitZ);
			//		else
			//			camera.Move (0, 0, -1);
			//		break;
			//	case Key.Left:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight (-Vector4.UnitX);
			//		else
			//			camera.Move (1, 0, 0);
			//		break;
			//	case Key.Right:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight (Vector4.UnitX);
			//		else
			//			camera.Move (-1, 0, 0);
			//		break;
			//	case Key.PageUp:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight (Vector4.UnitY);
			//		else
			//			camera.Move (0, 1, 0);
			//		break;
			//	case Key.PageDown:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.MoveLight (-Vector4.UnitY);
			//		else
			//			camera.Move (0, -1, 0);
			//		break;
			//	case Key.F2:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.exposure -= 0.3f;
			//		else
			//			renderer.exposure += 0.3f;
			//		rebuildBuffers = true;
			//		break;
			//	case Key.F3:
			//		if (modifiers.HasFlag (Modifier.Shift))
			//			renderer.gamma -= 0.1f;
			//		else
			//			renderer.gamma += 0.1f;
			//		rebuildBuffers = true;
			//		break;
			//	case Key.D:
			//		finalDebug = -finalDebug;
			//		rebuildBuffers = true;
			//		break;

			//	case Key.KeypadAdd:
			//		curModelIndex++;
			//		if (curModelIndex >= modelPathes.Length)
			//			curModelIndex = 0;
			//		reloadModel = true;
			//		break;
			//	case Key.KeypadSubtract:
			//		curModelIndex--;
			//		if (curModelIndex < 0)
			//			curModelIndex = modelPathes.Length -1;
			//		reloadModel = true;
			//		break;
			//	default:
			//		base.onKeyDown (key, scanCode, modifiers);
			//		return;
			//}
			updateViewRequested = true;
		}
		#endregion

		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					fbsMain?.Dispose();
					fbGBuff?.Dispose ();

					gbColorRough.Dispose ();
					gbEmitMetal.Dispose ();
					gbN_AO.Dispose ();
					gbPos.Dispose ();

					plGBuff.Dispose ();
					plLighting.Dispose ();

					uboMatrices.Dispose ();
					uboLights.Dispose ();
					model.Dispose ();
					envCube.Dispose ();

					descriptorPool.Dispose ();
					dbgmsg.Dispose ();

					pipelineCache.Dispose ();
				}
			}
			base.Dispose (disposing);
		}
	}
}
