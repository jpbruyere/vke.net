/* Forward pbr sample inspire from https://github.com/SaschaWillems/Vulkan-glTF-PBR
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
	class Program : SampleBase {

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
		VkSampleCountFlags samples = VkSampleCountFlags.SampleCount4;

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

		DebugView currentDebugView = DebugView.none;

		bool queryUpdatePrefilCube, showDebugImg;

		Vector4 lightPos = new Vector4 (1, 0, 0, 0);
		uint curModelIndex = 0;

		protected override void initVulkan () {
			base.initVulkan ();

			//UpdateFrequency = 20;
			camera = new Camera (Helpers.DegreesToRadians (45f), 1f, 0.1f, 64f);
			camera.SetPosition (0, 0, -2);

			pbrPipeline = new PBRPipeline (presentQueue,
				new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), samples), vke.samples.Utils.CubeMaps[0]);

			loadCurrentModel ();
		}

		bool rebuildBuffers, reloadModel;

		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateAndStart ();
				recordDraw (cmds[i], frameBuffers[i]);
				cmds[i].End ();
			}
		}
		void recordDraw (PrimaryCommandBuffer cmd, FrameBuffer fb) {
			pbrPipeline.RenderPass.Begin (cmd, fb);

			cmd.SetViewport (fb.Width, fb.Height);
			cmd.SetScissor (fb.Width, fb.Height);

			pbrPipeline.RecordDraw (cmd);
			pbrPipeline.RenderPass.End (cmd);
		}

		void loadCurrentModel () {
			dev.WaitIdle ();
			pbrPipeline.LoadModel (presentQueue, vke.samples.Utils.GltfFiles[curModelIndex]);
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
			if (rebuildBuffers) {
				buildCommandBuffers ();
				rebuildBuffers = false;
			}
		}
		#endregion


		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle ();
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
			if (GetButton (MouseButton.Left) == InputAction.Press) {
				camera.Rotate ((float)-diffY, (float)-diffX);
				updateViewRequested = true;
			} else if (GetButton (MouseButton.Right) == InputAction.Press) {
				camera.SetZoom ((float)diffY);
				updateViewRequested = true;
			}
		}

		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
			case Key.Space:
				if (modifiers.HasFlag (Modifier.Shift))
					curModelIndex = curModelIndex == 0 ? (uint)vke.samples.Utils.GltfFiles.Length - 1 : curModelIndex - 1;
				else
					curModelIndex = curModelIndex < (uint)vke.samples.Utils.GltfFiles.Length - 1 ? curModelIndex + 1 : 0;
				reloadModel = true;
				break;
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
				}
			}

			base.Dispose (disposing);
		}
	}
}
