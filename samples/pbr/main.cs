﻿/* Forward pbr sample inspire from https://github.com/SaschaWillems/Vulkan-glTF-PBR
 *
 * Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
 *
 * This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
 */

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using Vulkan;
using vke;

namespace pbrSample {
	class Program : VkWindow {

		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
			Instance.RENDER_DOC_CAPTURE = false;
#endif
			SwapChain.PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;

			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}
		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
			base.configureEnabledFeatures (available_features, ref enabled_features);
#if PIPELINE_STATS
			features.pipelineStatisticsQuery = true;
#endif
			enabled_features.samplerAnisotropy = available_features.samplerAnisotropy;
		}

		VkSampleCountFlags samples = VkSampleCountFlags.SampleCount8;

		FrameBuffers frameBuffers;
		PBRPipeline pbrPipeline;

		enum DebugView {
			none,
			color,
			normal,
			occlusion,
			emissive,
			metallic,
			roughness
		}

		string[] modelPathes = {
			Utils.DataDirectory + "models/DamagedHelmet/glTF/DamagedHelmet.gltf",
			Utils.DataDirectory + "models/Hubble.glb",
			Utils.DataDirectory + "models/ISS_stationary.glb",
			Utils.DataDirectory + "models/MER_static.glb",
			Utils.DataDirectory + "models/Box.gltf",

			/*"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Avocado/glTF/Avocado.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/BarramundiFish/glTF/BarramundiFish.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/BoomBoxWithAxes/glTF/BoomBoxWithAxes.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Box/glTF/Box.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/EnvironmentTest/glTF/EnvironmentTest.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/MetalRoughSpheres/glTF/MetalRoughSpheres.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/OrientationTest/glTF/OrientationTest.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Buggy/glTF/Buggy.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/2CylinderEngine/glTF-Embedded/2CylinderEngine.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/FlightHelmet/glTF/FlightHelmet.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/GearboxAssy/glTF/GearboxAssy.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Lantern/glTF/Lantern.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/SciFiHelmet/glTF/SciFiHelmet.gltf",
			"/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Sponza/glTF/Sponza.gltf",
			"/mnt/devel/vkChess/data/chess.gltf"*/
		};

		DebugView currentDebugView = DebugView.none;

#if PIPELINE_STATS
		PipelineStatisticsQueryPool statPool;
		TimestampQueryPool timestampQPool;
		ulong[] results;
#endif
		bool queryUpdatePrefilCube, showDebugImg;

#if WITH_VKVG
		//DescriptorSet dsDebugImg;
		//void initDebugImg () {
		//	dsDebugImg = descriptorPool.Allocate (descLayoutMain);
		//	pbrPipeline.envCube.debugImg.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
		//	DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsDebugImg, descLayoutMain);
		//	uboUpdate.Write (dev, pbrPipeline.envCube.debugImg.Descriptor);
		//}

		VkvgPipeline.VkvgPipeline vkvgPipeline;

		void vkvgDraw () {
			using (vkvg.Context ctx = vkvgPipeline.CreateContext ()) {
				ctx.Clear ();

				ctx.LineWidth = 1;
				ctx.SetSource (0.1, 0.1, 0.1, 0.8);
				ctx.Rectangle (5.5, 5.5, 320, 300);
				ctx.FillPreserve ();
				ctx.Flush ();
				ctx.SetSource (0.8, 0.8, 0.8);
				ctx.Stroke ();

				ctx.FontFace = "mono";
				ctx.FontSize = 8;
				int x = 16;
				int y = 40, dy = 16;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"fps:     {fps,5} "));
				ctx.MoveTo (x + 200, y - 0.5);
				ctx.Rectangle (x + 200, y - 8.5, 0.1 * fps, 10);
				ctx.SetSource (0.1, 0.9, 0.1);
				ctx.Fill ();
				ctx.SetSource (0.8, 0.8, 0.8);
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"Exposure:{pbrPipeline.matrices.exposure,5} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"Gamma:   {pbrPipeline.matrices.gamma,5} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"Light pos:   {lightPos.ToString ()} "));

#if PIPELINE_STATS
				if (results == null)
					return;

				y += dy*2;
				ctx.MoveTo (x, y);
				ctx.ShowText ("Pipeline Statistics");
				ctx.MoveTo (x-2, 4.5+y);
				ctx.LineTo (x+160, 4.5+y);
				ctx.Stroke ();
				y += 4;
				x += 20;

				for (int i = 0; i < statPool.RequestedStats.Length; i++) {
					y += dy;
					ctx.MoveTo (x, y);
					ctx.ShowText (string.Format ($"{statPool.RequestedStats[i].ToString(),-30} :{results[i],12:0,0} "));
				}
				/*y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Elapsed microsecond",-20} :{timestampQPool.ElapsedMiliseconds:0.0000} "));*/
#endif
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug draw (numpad 0->6)",-30} : {currentDebugView.ToString ()} "));
				/*y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug Prefil Face: (f)",-30} : {pbrPipeline.envCube.debugFace.ToString ()} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug Prefil Mip: (m)",-30} : {pbrPipeline.envCube.debugMip.ToString ()} "));
				*/
				vkvgPipeline.DrawResources (ctx, (int)Width, (int)Height);
			}
		}
#endif


		Vector4 lightPos = new Vector4 (1, 0, 0, 0);
		uint curModelIndex = 0;

		protected override void initVulkan () {
			base.initVulkan ();

			//UpdateFrequency = 20;
			camera = new Camera (Utils.DegreesToRadians (45f), 1f, 0.1f, 64f);
			camera.SetPosition (0, 0, -2);

			pbrPipeline = new PBRPipeline (presentQueue,
				new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), samples));

			loadCurrentModel ();

#if PIPELINE_STATS
			statPool = new PipelineStatisticsQueryPool (dev,
				VkQueryPipelineStatisticFlags.InputAssemblyVertices |
				VkQueryPipelineStatisticFlags.InputAssemblyPrimitives |
				VkQueryPipelineStatisticFlags.ClippingInvocations |
				VkQueryPipelineStatisticFlags.ClippingPrimitives |
				VkQueryPipelineStatisticFlags.FragmentShaderInvocations);

			timestampQPool = new TimestampQueryPool (dev);
#endif

#if WITH_VKVG
			vkvgPipeline = new VkvgPipeline.VkvgPipeline (instance, dev, presentQueue, pbrPipeline);
#endif
		}

		bool rebuildBuffers, reloadModel;

		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateAndStart ();
#if PIPELINE_STATS
				statPool.Begin (cmds[i]);
				recordDraw (cmds[i], frameBuffers[i]);
				statPool.End (cmds[i]);
#else
				recordDraw (cmds[i], frameBuffers[i]);
#endif

				cmds[i].End ();
			}
		}
		void recordDraw (CommandBuffer cmd, FrameBuffer fb) {
			pbrPipeline.RenderPass.Begin (cmd, fb);

			cmd.SetViewport (fb.Width, fb.Height);
			cmd.SetScissor (fb.Width, fb.Height);

			pbrPipeline.RecordDraw (cmd);

#if WITH_VKVG
			vkvgPipeline.RecordDraw (cmd);
#endif
			pbrPipeline.RenderPass.End (cmd);
		}

		void loadCurrentModel () {
			dev.WaitIdle ();
			pbrPipeline.LoadModel (presentQueue, modelPathes[curModelIndex]);
			BoundingBox modelAABB = pbrPipeline.model.DefaultScene.AABB;
			camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (modelAABB.max.X, modelAABB.max.Y), modelAABB.max.Z));
			updateViewRequested = true;
		}

		#region update
		public override void UpdateView () {
			camera.AspectRatio = (float)swapChain.Width / swapChain.Height;

			pbrPipeline.matrices.lightDir = lightPos;
			pbrPipeline.matrices.projection = camera.Projection;
			pbrPipeline.matrices.view = camera.View;
			pbrPipeline.matrices.model = camera.Model;


			Matrix4x4.Invert (camera.View, out Matrix4x4 inv);
			pbrPipeline.matrices.camPos = new Vector4 (inv.M41, inv.M42, inv.M43, 0);
			pbrPipeline.matrices.debugViewInputs = (float)currentDebugView;

			pbrPipeline.uboMats.Update (pbrPipeline.matrices, (uint)Marshal.SizeOf<PBRPipeline.Matrices> ());

			updateViewRequested = false;
		}

		public override void Update () {
			if (reloadModel) {
				loadCurrentModel ();
				reloadModel = false;
				rebuildBuffers = true;
			}
#if PIPELINE_STATS
			results = statPool.GetResults ();
#endif
			if (rebuildBuffers) {
				buildCommandBuffers ();
				rebuildBuffers = false;
			}
#if WITH_VKVG
			vkvgDraw ();
#endif
		}
		#endregion


		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle ();
#if WITH_VKVG
			vkvgPipeline.Resize ((int)swapChain.Width, (int)swapChain.Height,
				new DescriptorSetWrites (pbrPipeline.dsMain, pbrPipeline.Layout.DescriptorSetLayouts[0].Bindings[5]));
#endif

			UpdateView ();

			frameBuffers?.Dispose ();
			frameBuffers = pbrPipeline.RenderPass.CreateFrameBuffers (swapChain);

			buildCommandBuffers ();
			dev.WaitIdle ();
		}

		#region Mouse and keyboard
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
			switch (key) {
			case Key.Space:
				if (modifiers.HasFlag (Modifier.Shift))
					curModelIndex = curModelIndex == 0 ? (uint)modelPathes.Length - 1 : curModelIndex - 1;
				else
					curModelIndex = curModelIndex < (uint)modelPathes.Length - 1 ? curModelIndex + 1 : 0;
				reloadModel = true;
				break;
			/*
				case Key.F:
					if (modifiers.HasFlag (Modifier.Shift)) {
						pbrPipeline.envCube.debugFace --;
						if (pbrPipeline.envCube.debugFace < 0)
							pbrPipeline.envCube.debugFace = 5;
					} else {
						pbrPipeline.envCube.debugFace ++;
						if (pbrPipeline.envCube.debugFace > 5)
							pbrPipeline.envCube.debugFace = 0;
					}
					queryUpdatePrefilCube = updateViewRequested = true;
					break;
				case Key.M:
					if (modifiers.HasFlag (Modifier.Shift)) {
						pbrPipeline.envCube.debugMip --;
						if (pbrPipeline.envCube.debugMip < 0)
							pbrPipeline.envCube.debugMip = (int)pbrPipeline.envCube.prefilterCube.CreateInfo.mipLevels - 1;
					} else {
						pbrPipeline.envCube.debugMip ++;
						if (pbrPipeline.envCube.debugMip > pbrPipeline.envCube.prefilterCube.CreateInfo.mipLevels)
							pbrPipeline.envCube.debugMip = 0;
					}
					queryUpdatePrefilCube = updateViewRequested = true;
					break;*/
			case Key.P:
				showDebugImg = !showDebugImg;
				queryUpdatePrefilCube = updateViewRequested = true;
				break;
			case Key.Keypad0:
				currentDebugView = DebugView.none;
				break;
			case Key.Keypad1:
				currentDebugView = DebugView.color;
				break;
			case Key.Keypad2:
				currentDebugView = DebugView.normal;
				break;
			case Key.Keypad3:
				currentDebugView = DebugView.occlusion;
				break;
			case Key.Keypad4:
				currentDebugView = DebugView.emissive;
				break;
			case Key.Keypad5:
				currentDebugView = DebugView.metallic;
				break;
			case Key.Keypad6:
				currentDebugView = DebugView.roughness;
				break;
			case Key.Up:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos -= Vector4.UnitZ;
				else
					camera.Move (0, 0, 1);
				break;
			case Key.Down:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos += Vector4.UnitZ;
				else
					camera.Move (0, 0, -1);
				break;
			case Key.Left:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos -= Vector4.UnitX;
				else
					camera.Move (1, 0, 0);
				break;
			case Key.Right:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos += Vector4.UnitX;
				else
					camera.Move (-1, 0, 0);
				break;
			case Key.PageUp:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos += Vector4.UnitY;
				else
					camera.Move (0, 1, 0);
				break;
			case Key.PageDown:
				if (modifiers.HasFlag (Modifier.Shift))
					lightPos -= Vector4.UnitY;
				else
					camera.Move (0, -1, 0);
				break;
			case Key.S:
				if (modifiers.HasFlag (Modifier.Shift))
					pbrPipeline.matrices.scaleIBLAmbient -= 0.1f;
				else
					pbrPipeline.matrices.scaleIBLAmbient += 0.1f;
				break;
			case Key.F2:
				if (modifiers.HasFlag (Modifier.Shift))
					pbrPipeline.matrices.exposure -= 0.3f;
				else
					pbrPipeline.matrices.exposure += 0.3f;
				break;
			case Key.F3:
				if (modifiers.HasFlag (Modifier.Shift))
					pbrPipeline.matrices.gamma -= 0.1f;
				else
					pbrPipeline.matrices.gamma += 0.1f;
				break;
			case Key.F4:
				if (camera.Type == Camera.CamType.FirstPerson)
					camera.Type = Camera.CamType.LookAt;
				else
					camera.Type = Camera.CamType.FirstPerson;
				Console.WriteLine ($"camera type = {camera.Type}");
				break;
			default:
				base.onKeyDown (key, scanCode, modifiers);
				return;
			}
			updateViewRequested = true;
		}
		#endregion

		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					dev.WaitIdle ();
					frameBuffers?.Dispose ();
					pbrPipeline.Dispose ();
#if WITH_VKVG
					vkvgPipeline.Dispose ();
#endif

#if PIPELINE_STATS
					timestampQPool?.Dispose ();
					statPool?.Dispose ();
#endif
				}
			}

			base.Dispose (disposing);
		}
	}
}
