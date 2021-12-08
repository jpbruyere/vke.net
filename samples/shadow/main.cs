// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using vke.glTF;
using Glfw;
using Vulkan;
using System.Diagnostics;

namespace deferred {
	/// <summary>
	/// Deferred PBR rendering.
	/// </summary>
	class Deferred : VkWindow {

		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
			//Instance.RENDER_DOC_CAPTURE = true;
#endif
			SwapChain.PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;
			DeferredPbrRenderer.TEXTURE_ARRAY = true;
			DeferredPbrRenderer.NUM_SAMPLES = VkSampleCountFlags.SampleCount8;
			DeferredPbrRenderer.HDR_FORMAT = VkFormat.R32g32b32a32Sfloat;
			DeferredPbrRenderer.MRT_FORMAT = VkFormat.R32g32b32a32Sfloat;

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
		string[] cubemapPathes = {
			Utils.DataDirectory + "textures/papermill.ktx",
			Utils.DataDirectory + "textures/cubemap_yokohama_bc3_unorm.ktx",
			Utils.DataDirectory + "textures/gcanyon_cube.ktx",
			Utils.DataDirectory + "textures/pisa_cube.ktx",
			Utils.DataDirectory + "textures/uffizi_cube.ktx",
		};
		string[] modelPathes = {
				Utils.DataDirectory + "models/cubeOnPlane.glb",
				//"/mnt/devel/vkPinball/data/models/pinball.gltf",
				Utils.DataDirectory + "models/DamagedHelmet/glTF/DamagedHelmet.gltf",
				Utils.DataDirectory + "models/Hubble.glb",
				Utils.DataDirectory + "models/MER_static.glb",
				Utils.DataDirectory + "models/ISS_stationary.glb",
			};

		int curModelIndex = 0;
		bool reloadModel;
		bool rebuildBuffers;

		Queue transferQ, computeQ;
		DeferredPbrRenderer renderer;


		GraphicPipeline plToneMap;
		FrameBuffers frameBuffers;
		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;

		vke.DebugUtils.Messenger dbgmsg;

		protected override void initVulkan () {
			base.initVulkan ();

#if DEBUG
			dbgmsg = new vke.DebugUtils.Messenger (instance, VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);
#endif

			camera = new Camera (Helpers.DegreesToRadians (45f), 1f, 0.1f, 16f);
			camera.SetPosition (0, 0, -2);

			//renderer = new DeferredPbrRenderer (presentQueue, cubemapPathes[2], swapChain.Width, swapChain.Height, camera.NearPlane, camera.FarPlane);
			renderer = new DeferredPbrRenderer (presentQueue, cubemapPathes[2], swapChain.Width, swapChain.Height, camera.NearPlane, camera.FarPlane);
			renderer.LoadModel (transferQ, modelPathes[curModelIndex]);
			foreach (Model.Mesh mesh in renderer.model.Meshes) {
				Console.WriteLine (mesh.Name);
			}
			camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (renderer.modelAABB.Width, renderer.modelAABB.Height), renderer.modelAABB.Depth));

			init_final_pl ();
		}

		void init_final_pl() {
			descriptorPool = new DescriptorPool (dev, 3,
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 2),
				new VkDescriptorPoolSize (VkDescriptorType.StorageImage, 4)
			);

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, DeferredPbrRenderer.NUM_SAMPLES);
			if (DeferredPbrRenderer.NUM_SAMPLES != VkSampleCountFlags.SampleCount1) {
				cfg.multisampleState.sampleShadingEnable = true;
				cfg.multisampleState.minSampleShading = 0.5f;
			}
			cfg.Layout = new PipelineLayout (dev,
				new VkPushConstantRange (VkShaderStageFlags.Fragment, 2 * sizeof (float)),
				new DescriptorSetLayout (dev, 0,
					new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
					));

			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, DeferredPbrRenderer.NUM_SAMPLES);

			cfg.AddShaders (new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv"));
			cfg.AddShaders (new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.tone_mapping.frag.spv"));

			plToneMap = new GraphicPipeline (cfg);

			cfg.DisposeShaders ();

			descriptorSet = descriptorPool.Allocate (cfg.Layout.DescriptorSetLayouts[0]);

		}

		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateAndStart ();

				renderer.buildCommandBuffers (cmds[i]);

				plToneMap.RenderPass.Begin (cmds[i], frameBuffers[i]);

				cmds[i].SetViewport (frameBuffers[i].Width, frameBuffers[i].Height);
				cmds[i].SetScissor (frameBuffers[i].Width, frameBuffers[i].Height);

				plToneMap.Bind (cmds[i]);
				plToneMap.BindDescriptorSet (cmds[i], descriptorSet);

				cmds[i].PushConstant (plToneMap.Layout, VkShaderStageFlags.Fragment, 8, new float[] { renderer.exposure, renderer.gamma }, 0);

				cmds[i].Draw (3, 1, 0, 0);

				plToneMap.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		public override void UpdateView () {
			dev.WaitIdle ();

			renderer.UpdateView (camera);
			updateViewRequested = false;
#if WITH_SHADOWS
			if (renderer.shadowMapRenderer.updateShadowMap)
				renderer.shadowMapRenderer.update_shadow_map (cmdPool);
#endif
		}

		public override void Update () {
			if (reloadModel) {
				renderer.LoadModel (transferQ, modelPathes[curModelIndex]);
				reloadModel = false;
				camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (renderer.modelAABB.Width, renderer.modelAABB.Height), renderer.modelAABB.Depth));
				updateViewRequested = true;
				rebuildBuffers = true;
#if WITH_SHADOWS
				renderer.shadowMapRenderer.updateShadowMap = true;
#endif
			}

			if (rebuildBuffers) {
				buildCommandBuffers ();
				rebuildBuffers = false;
			}

		}

		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle ();

			renderer.Resize (Width, Height);

			UpdateView ();

			frameBuffers?.Dispose();
			frameBuffers = plToneMap.RenderPass.CreateFrameBuffers (swapChain);

			DescriptorSetWrites dsUpdate = new DescriptorSetWrites (plToneMap.Layout.DescriptorSetLayouts[0].Bindings[0]);
			dsUpdate.Write (dev, descriptorSet, renderer.hdrImgResolved.Descriptor);

			buildCommandBuffers ();

			dev.WaitIdle ();
		}

		#region Mouse and keyboard
		protected override void onScroll (double xOffset, double yOffset) {
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				camera.Rotate ((float)-diffY, (float)-diffX, 0);
			} else if (MouseButton[1]) {
				camera.SetZoom ((float)diffY);
			} else
				return;

			updateViewRequested = true;
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.F:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.debugFace--;
						if (renderer.debugFace < 0)
							renderer.debugFace = 5;
					} else {
						renderer.debugFace++;
						if (renderer.debugFace >= 5)
							renderer.debugFace = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.M:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.debugMip--;
						if (renderer.debugMip < 0)
							renderer.debugMip = (int)renderer.envCube.prefilterCube.CreateInfo.mipLevels - 1;
					} else {
						renderer.debugMip++;
						if (renderer.debugMip >= renderer.envCube.prefilterCube.CreateInfo.mipLevels)
							renderer.debugMip = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.L:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.lightNumDebug--;
						if (renderer.lightNumDebug < 0)
							renderer.lightNumDebug = (int)renderer.lights.Length - 1;
					} else {
						renderer.lightNumDebug++;
						if (renderer.lightNumDebug >= renderer.lights.Length)
							renderer.lightNumDebug = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.Keypad0:
				case Key.Keypad1:
				case Key.Keypad2:
				case Key.Keypad3:
				case Key.Keypad4:
				case Key.Keypad5:
				case Key.Keypad6:
				case Key.Keypad7:
				case Key.Keypad8:
				case Key.Keypad9:
					renderer.currentDebugView = (DeferredPbrRenderer.DebugView)(int)key-320;
					rebuildBuffers = true;
					break;
				case Key.KeypadDivide:
					renderer.currentDebugView = DeferredPbrRenderer.DebugView.irradiance;
					rebuildBuffers = true;
					break;
				case Key.S:
					if (modifiers.HasFlag (Modifier.Control)) {
						renderer.pipelineCache.Save ();
						Console.WriteLine ($"Pipeline Cache saved.");
					} else {
						renderer.currentDebugView = DeferredPbrRenderer.DebugView.shadowMap;
						rebuildBuffers = true; 
					}
					break;
				case Key.Up:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight(-Vector4.UnitZ);
					else
						camera.Move (0, 0, 1);
					break;
				case Key.Down:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitZ);
					else
						camera.Move (0, 0, -1);
					break;
				case Key.Left:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (-Vector4.UnitX);
					else
						camera.Move (1, 0, 0);
					break;
				case Key.Right:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitX);
					else
						camera.Move (-1, 0, 0);
					break;
				case Key.PageUp:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitY);
					else
						camera.Move (0, 1, 0);
					break;
				case Key.PageDown:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (-Vector4.UnitY);
					else
						camera.Move (0, -1, 0);
					break;
				case Key.F2:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.exposure -= 0.3f;
					else
						renderer.exposure += 0.3f;
					rebuildBuffers = true;
					break;
				case Key.F3:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.gamma -= 0.1f;
					else
						renderer.gamma += 0.1f;
					rebuildBuffers = true;
					break;
				case Key.KeypadAdd:
					curModelIndex++;
					if (curModelIndex >= modelPathes.Length)
						curModelIndex = 0;
					reloadModel = true;
					break;
				case Key.KeypadSubtract:
					curModelIndex--;
					if (curModelIndex < 0)
						curModelIndex = modelPathes.Length -1;
					reloadModel = true;
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					return;
			}
			updateViewRequested = true;
		}
		#endregion

		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					frameBuffers?.Dispose();
					renderer?.Dispose ();
					plToneMap?.Dispose ();
					descriptorPool?.Dispose ();
					dbgmsg?.Dispose ();
				}
			}
			base.Dispose (disposing);
		}
	}
}
