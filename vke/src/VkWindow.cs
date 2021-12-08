// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Glfw;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	/// <summary>
	/// Base class to build vulkan application.
	/// Provide default swapchain with its command pool and buffers per image and the main present queue
	/// </summary>
	public abstract class VkWindow : IDisposable {
		/** GLFW callback may return a custom pointer, this list makes the link between the GLFW window pointer and the
			manage VkWindow instance. */
		static Dictionary<IntPtr,VkWindow> windows = new Dictionary<IntPtr, VkWindow>();
		/** GLFW window native pointer. */
		IntPtr hWin;
		/**Vulkan Surface */
		protected VkSurfaceKHR hSurf;
		/**vke Instance encapsulating a VkInstance. */
		protected Instance instance;
		/**vke Physical device associated with this window*/
		protected PhysicalDevice phy;
		/**vke logical device */
		protected Device dev;
		protected PresentQueue presentQueue;
		protected SwapChain swapChain;
		protected CommandPool cmdPool;
		protected PrimaryCommandBuffer[] cmds;
		protected VkSemaphore[] drawComplete;
		protected Fence drawFence;

		protected uint fps { get; private set; }
		protected bool updateViewRequested = true;
		protected double lastMouseX { get; private set; }
		protected double lastMouseY { get; private set; }

		/// <summary>readonly GLFW window handle</summary>
		public IntPtr WindowHandle => hWin;

		/**Default camera initialized with a Field of view of 40° and and aspect ratio of 1. */
		protected Camera camera = new Camera (Helpers.DegreesToRadians (45f), 1f);

		public Modifier KeyModifiers = 0;
		IntPtr currentCursor;
		uint frameCount;
		Stopwatch frameChrono;

		/// <summary>
		/// Override this property to change the list of enabled instance extensions
		/// </summary>
		public virtual string[] EnabledLayers => null;

		/// <summary>
		/// Override this property to change the list of enabled instance extensions
		/// </summary>
		public virtual string[] EnabledInstanceExtensions => null;

		/// <summary>
		/// Override this property to change the list of enabled device extensions
		/// </summary>
		public virtual string[] EnabledDeviceExtensions => new string[] { Ext.D.VK_KHR_swapchain };

		/// <summary>
		/// Frequency in millisecond of the call to the Update method
		/// </summary>
		public long UpdateFrequency = 200;

		public uint Width { get; private set; }
		public uint Height { get; private set; }
		public bool VSync { get; private set; }
		public string Title {
			set {
				Glfw3.SetWindowTitle (hWin, value);
			}
		}
		/// <summary>
		/// Create a new vulkan enabled window with GLFW.
		/// </summary>
		/// <param name="name">Caption of the window</param>
		/// <param name="_width">Width.</param>
		/// <param name="_height">Height.</param>
		/// <param name="vSync">Vertical synchronisation status for creating the swapchain.</param>
		public VkWindow (string name = "VkWindow", uint _width = 800, uint _height = 600, bool vSync = true) {

			Width = _width;
			Height = _height;
			VSync = vSync;

			Glfw3.Init ();

			Glfw3.WindowHint (WindowAttribute.ClientApi, 0);
			Glfw3.WindowHint (WindowAttribute.Resizable, 1);

			hWin = Glfw3.CreateWindow ((int)Width, (int)Height, name, MonitorHandle.Zero, IntPtr.Zero);

			if (hWin == IntPtr.Zero)
				throw new Exception ("[GLFW3] Unable to create vulkan Window");

			Glfw3.SetKeyCallback (hWin, HandleKeyDelegate);
			Glfw3.SetMouseButtonPosCallback (hWin, HandleMouseButtonDelegate);
			Glfw3.SetCursorPosCallback (hWin, HandleCursorPosDelegate);
			Glfw3.SetScrollCallback (hWin, HandleScrollDelegate);
			Glfw3.SetCharCallback (hWin, HandleCharDelegate);

			windows.Add (hWin, this);
		}
		/// <summary>
		/// Set current mouse cursor in the GLFW window.
		/// </summary>
		/// <param name="cursor">New mouse cursor to set.</param>
		public void SetCursor (CursorShape cursor) {
			if (currentCursor != IntPtr.Zero)
				Glfw3.DestroyCursor (currentCursor);
			currentCursor = Glfw3.CreateStandardCursor (cursor);
			Glfw3.SetCursor (hWin, currentCursor);
		}
		/// <summary>
		/// Ask GLFW to close the native window.
		/// </summary>
		public void Close ()
		{
			Glfw3.SetWindowShouldClose (hWin, 1);
		}
		/// <summary>
		/// Create the minimum vulkan objects to quickly start a new application. The folowing objects are created:
		/// - Vulkan Instance with extensions present in the `EnabledInstanceExtensions` property.
		/// - Vulkan Surface for the GLFW native window.
		/// - Vulkan device for the selected physical one with configured enabledFeatures and extensions present in `EnabledDeviceExtensions` list. Selection of the default physical device
		///   may be replaced by the `selectPhysicalDevice` method override.
		/// - Create a default Graphic Queue with presentable support. The default queue creation may be customized by overriding the `createQueues` method.
		/// - Default vulkan Swapchain creation. Some swapchain's parameters are controled through static fields of the `SwapChain` class (ex: `SwapChain.PREFERED_FORMAT`).
		/// - Create a default command pool for the `presentQueue` family index.
		/// - Create an empty command buffer collection (`cmds`).
		/// - Create one unsignaled vulkan semaphore (named `drawComplete` per swapchain image used to control presentation submission to the graphic queue.
		/// - Create a signaled vulkan fence (`drawFence`). (TODO: improve this.
		/// With all these objects, vulkan application programming startup is reduced to the minimal.
		/// </summary>
		protected virtual void initVulkan () {
			List<string> instExts = new List<string> (Glfw3.GetRequiredInstanceExtensions ());
			if (EnabledInstanceExtensions != null)
				instExts.AddRange (EnabledInstanceExtensions);

			instance = new Instance (EnabledLayers, instExts.ToArray());

			hSurf = instance.CreateSurface (hWin);

			selectPhysicalDevice ();

			VkPhysicalDeviceFeatures enabledFeatures = default;
			configureEnabledFeatures (phy.Features, ref enabledFeatures);

			//First create the c# device class
			dev = new Device (phy);
			dev.debugUtilsEnabled = instance.debugUtilsEnabled;//store a boolean to prevent testing against the extension string presence.

			//create queue class
			createQueues ();

			//activate the device to have effective queues created accordingly to what's available
			dev.Activate (enabledFeatures, EnabledDeviceExtensions);

			swapChain = new SwapChain (presentQueue as PresentQueue, Width, Height, SwapChain.PREFERED_FORMAT,
				VSync ? VkPresentModeKHR.FifoKHR : VkPresentModeKHR.ImmediateKHR);
			swapChain.Activate ();

			Width = swapChain.Width;
			Height = swapChain.Height;

			cmdPool = new CommandPool (dev, presentQueue.qFamIndex, VkCommandPoolCreateFlags.ResetCommandBuffer);

			cmds = new PrimaryCommandBuffer[swapChain.ImageCount];
			drawComplete = new VkSemaphore[swapChain.ImageCount];
			drawFence = new Fence (dev, true, "draw fence");

			for (int i = 0; i < swapChain.ImageCount; i++) {
				drawComplete[i] = dev.CreateSemaphore ();
				drawComplete[i].SetDebugMarkerName (dev, "Semaphore DrawComplete" + i);
			}

			cmdPool.SetName ("main CmdPool");
		}
		/// <summary>
		/// Override this method to modify enabled features before device creation. Feature availability is given by the first argument.
		/// By default, no features is selected.
		/// </summary>
		/// <param name="available_features">Available features for the selected vulkan physical device associated with this window</param>
		/// <param name="enabled_features">Set boolean fileds of this structure to true to enable features.</param>
		protected virtual void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
		}
		/// <summary>
		/// Override this method to select another device than the first one with a swapchain support.
		/// </summary>
		protected virtual void selectPhysicalDevice () {
			phy = instance.GetAvailablePhysicalDevice ().FirstOrDefault (p => p.HasSwapChainSupport);
		}
		/// <summary>
		/// Override this method to create additional queue. Dedicated queue of the requested type will be selected first, created queues may excess
		/// available physical queues.
		/// </summary>
		protected virtual void createQueues () {
			presentQueue = new PresentQueue (dev, VkQueueFlags.Graphics, hSurf);
		}

		/// <summary>
		/// Main render method called each frame. get next swapchain image, process resize if needed, submit and present to the presentQueue.
		/// Wait QueueIdle after presenting.
		/// </summary>
		protected virtual void render () {
			int idx = swapChain.GetNextImage ();
			if (idx < 0) {
				OnResize ();
				return;
			}

			if (cmds[idx] == null)
				return;

			drawFence.Wait ();
			drawFence.Reset ();

			presentQueue.Submit (cmds[idx], swapChain.presentComplete, drawComplete[idx], drawFence);
			presentQueue.Present (swapChain, drawComplete[idx]);

			//presentQueue.WaitIdle ();
		}

		protected virtual void onScroll (double xOffset, double yOffset) { }
		protected virtual void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton(MouseButton.Left) == InputAction.Press) {
				camera.Rotate ((float)-diffX, (float)-diffY);
				updateViewRequested = true;
			} else if (GetButton(MouseButton.Right) == InputAction.Press) {
				camera.Move ((float)diffX,0,0);
				camera.Move (0, 0, (float)-diffY);
				updateViewRequested = true;
			}
		}
		protected virtual void onMouseButtonDown (MouseButton button) { }
		protected virtual void onMouseButtonUp (MouseButton button) { }
		protected virtual void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.F4:
					if (modifiers == Modifier.Alt)
						Glfw3.SetWindowShouldClose (hWin, 1);
					break;
				case Key.Escape:
					Close ();
					break;
				case Key.Up:
					camera.Move (0, 0, 1);
					break;
				case Key.Down:
					camera.Move (0, 0, -1);
					break;
				case Key.Left:
					camera.Move (1, 0, 0);
					break;
				case Key.Right:
					camera.Move (-1, 0, 0);
					break;
				case Key.PageUp:
					camera.Move (0, 1, 0);
					break;
				case Key.PageDown:
					camera.Move (0, -1, 0);
					break;
				case Key.F3:
					if (camera.Type == Camera.CamType.FirstPerson)
						camera.Type = Camera.CamType.LookAt;
					else
						camera.Type = Camera.CamType.FirstPerson;
					break;
			}
			updateViewRequested = true;
		}
		protected virtual void onKeyUp (Key key, int scanCode, Modifier modifiers) { }
		protected virtual void onChar (CodePoint cp) { }

		protected InputAction GetButton (MouseButton button) =>
			Glfw3.GetMouseButton (hWin, button);

		#region events delegates

		static CursorPosDelegate HandleCursorPosDelegate = (window, xPosition, yPosition) => {
			windows[window].onMouseMove (xPosition, yPosition);
			windows[window].lastMouseX = xPosition;
			windows[window].lastMouseY = yPosition;
		};
		static MouseButtonDelegate HandleMouseButtonDelegate = (IntPtr window, Glfw.MouseButton button, InputAction action, Modifier mods) => {
			if (action == InputAction.Press)
				windows[window].onMouseButtonDown (button);
			else
				windows[window].onMouseButtonUp (button);
		};
		static ScrollDelegate HandleScrollDelegate = (IntPtr window, double xOffset, double yOffset) => {
			windows[window].onScroll (xOffset, yOffset);
		};
		static KeyDelegate HandleKeyDelegate = (IntPtr window, Key key, int scanCode, InputAction action, Modifier modifiers) => {
			windows[window].KeyModifiers = modifiers;
			if (action == InputAction.Press || action == InputAction.Repeat) {
				windows[window].onKeyDown (key, scanCode, modifiers);
			} else {
				windows[window].onKeyUp (key, scanCode, modifiers);
			}
		};
		static CharDelegate HandleCharDelegate = (IntPtr window, CodePoint codepoint) => {
			windows[window].onChar (codepoint);
		};
		#endregion

		/// <summary>
		/// main window loop, exits on GLFW3 exit event. Before entering the rendering loop, the following methods are called:
		/// - initVulkan (device, queues and swapchain creations).
		/// - OnResize (create there your frame buffers couple to the swapchain and trigger the recording of your command buffers for the presentation.
		/// - UpdateView (generaly used when the camera setup has changed to update MVP matrices)
		/// The rendering loop consist of the following steps:
		/// - render (the default one will submit the default command buffers to the presentQueue and trigger a queue present for each swapchain images.
		/// - if the `updateViewRequested` field is set to 'true', call the `UpdateView` method.
		/// - frame counting and chrono.
		/// - if elapsed time reached `UpdateFrequency` value, the `Update` method is called and the elapsed time chrono is reseet.
		/// - GLFW events are polled at the end of the loop.
		/// </summary>
		public virtual void Run () {
			initVulkan ();

			OnResize ();
			UpdateView ();

			frameChrono = Stopwatch.StartNew ();
			long totTime = 0;

			while (!Glfw3.WindowShouldClose (hWin)) {
				render ();

				if (updateViewRequested)
					UpdateView ();

				frameCount++;

				if (frameChrono.ElapsedMilliseconds > UpdateFrequency) {
					Update ();

					frameChrono.Stop ();
					totTime += frameChrono.ElapsedMilliseconds;
					fps = (uint)((double)frameCount / (double)totTime * 1000.0);
					Glfw3.SetWindowTitle (hWin, "FPS: " + fps.ToString ());
					if (totTime > 2000) {
						frameCount = 0;
						totTime = 0;
					}
					frameChrono.Restart ();
				}
				Glfw3.PollEvents ();
			}
		}
		/// <summary>
		/// Suitable for updating the matrices, called at least once before the rendering loop just
		/// after 'OnResize'. Then, triggered in the render loop each time the 'updateViewRequested' field is set to 'true', don't forget to
		/// reset 'updateViewRequested' to 'false' or call the 'base.UpdateView()' which will reset this boolean.
		/// </summary>
		public virtual void UpdateView () {
			updateViewRequested = false;
		}
		/// <summary>
		/// custom update method called controled by the `UpdateFrequency` field, base method is empty.
		/// </summary>
		public virtual void Update () { }

		/// <summary>
		/// Called when swapchain has been resized, override this method to resize your framebuffers coupled to the swapchain.
		/// The base method will update Window 'Width' and 'Height' properties with new swapchain's dimensions. This method is guarantied to
		/// be called once just after `initVulkan` and before the first render.
		/// </summary>
		protected virtual void OnResize () {
			Width = swapChain.Width;
			Height = swapChain.Height;
		}


		#region IDisposable Support
		protected bool isDisposed;

		protected virtual void Dispose (bool disposing) {
			if (!isDisposed) {
				dev.WaitIdle ();

				for (int i = 0; i < swapChain.ImageCount; i++) {
					dev.DestroySemaphore (drawComplete[i]);
					cmds[i].Free ();
				}
				drawFence.Dispose ();
				swapChain.Dispose ();

				vkDestroySurfaceKHR (instance.Handle, hSurf, IntPtr.Zero);

				if (disposing) {
					cmdPool.Dispose ();
					dev.Dispose ();
					instance.Dispose ();
				} else
					Debug.WriteLine ("a VkWindow has not been correctly disposed");

				if (currentCursor != IntPtr.Zero)
					Glfw3.DestroyCursor (currentCursor);

				windows.Remove (hWin);

				Glfw3.DestroyWindow (hWin);
				Glfw3.Terminate ();


				isDisposed = true;
			}
		}

		~VkWindow () {
			Dispose (false);
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}
		#endregion
	}
}
