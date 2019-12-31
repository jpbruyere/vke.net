// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Crow;
using Glfw;
using vke;
using Vulkan;

namespace vke {
	public class VkCrowWindow : VkWindow, Crow.IBackend, Crow.IValueChange {
		#region IValueChange implementation
		public event EventHandler<Crow.ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value)
		{
			ValueChanged?.Invoke (this, new Crow.ValueChangeEventArgs (MemberName, _value));
		}
		#endregion

		public Image uiImage;
		protected Crow.Interface iFace;
		public bool MouseIsInInterface =>
			iFace.HoverWidget != null;
		public Device Dev => dev;
		public CommandPool CmdPool => cmdPool;
		public Queue GraphicQueue => presentQueue;

		protected DescriptorSetWrites uiImageUpdate;
		public static VkCrowWindow CurWin;

		protected VkCrowWindow () : base ()
		{
			CurWin = this;
		}

		protected void CreateInterface () {
			iFace = new Crow.Interface ((int)Width, (int)Height, this);
			iFace.Init ();
		}

		public override void Update ()
		{
			NotifyValueChanged ("fps", fps);
			iFace.Update ();
		}

		protected override void onMouseMove (double xPos, double yPos)
		{
			iFace.OnMouseMove ((int)xPos, (int)yPos);
		}
		protected override void onMouseButtonDown (Glfw.MouseButton button)
		{
			iFace.OnMouseButtonDown ((Crow.MouseButton)button);
		}
		protected override void onMouseButtonUp (Glfw.MouseButton button)
		{
			iFace.OnMouseButtonUp ((Crow.MouseButton)button);
		}
		protected override void onScroll (double xOffset, double yOffset)
		{
			if (KeyModifiers.HasFlag (Modifier.Shift))
				iFace.OnMouseWheelChanged ((float)xOffset);
			else
				iFace.OnMouseWheelChanged ((float)yOffset);
		}
		protected override void onKeyDown (Glfw.Key key, int scanCode, Modifier modifiers)
		{
			iFace.OnKeyDown ((Crow.Key)key);
		}
		protected override void onKeyUp (Glfw.Key key, int scanCode, Modifier modifiers)
		{
			iFace.OnKeyUp ((Crow.Key)key);
		}

		protected override void OnResize ()
		{
			base.OnResize ();

			iFace.ProcessResize (new Crow.Rectangle (0, 0, (int)Width, (int)Height));
			initUISurface ();
		}

		protected override void Dispose (bool disposing)
		{
			dev.WaitIdle ();
			uiImage?.Dispose ();
			iFace.Dispose ();
			base.Dispose (disposing);
		}


		void initUISurface ()
		{
			iFace.surf?.Dispose ();
			uiImage?.Dispose ();

			uiImage = new Image (dev, VkFormat.B8g8r8a8Srgb, VkImageUsageFlags.Sampled,
				VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent, Width, Height, VkImageType.Image2D,
				VkSampleCountFlags.SampleCount1, VkImageTiling.Linear);
			uiImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
			uiImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);
			uiImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			uiImage.Map ();

			NotifyValueChanged ("uiImage", uiImage);

			uiImageUpdate?.Write (dev, uiImage.Descriptor);

			iFace.surf = new Crow.Cairo.ImageSurface (uiImage.MappedData, Crow.Cairo.Format.ARGB32,
				(int)Width, (int)Height, (int)uiImage.GetSubresourceLayout ().rowPitch);

			CommandBuffer cmd = cmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
			uiImage.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.ShaderReadOnlyOptimal);
			presentQueue.EndSubmitAndWait (cmd, true);
		}

		public Widget loadWindow (string path, object dataSource = null) {
			try {
				Widget w = iFace.FindByName (path);
				if (w != null) {
					iFace.PutOnTop (w);
					return w;
				}
				w = iFace.Load (path);
				w.Name = path;
				w.DataSource = dataSource;
				return w;

			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine (ex.ToString ());
			}
			return null;
		}
		public void closeWindow (string path) {
			Widget g = iFace.FindByName (path);
			if (g != null)
				iFace.DeleteWidget (g);
		}

		#region Crow.IBackend implementation
		public void Init (Crow.Interface iFace)
		{
			initUISurface ();
		}

		public void CleanUp ()
		{
			uiImage?.Dispose ();
		}

		public void Flush ()
		{
			//throw new NotImplementedException ();
		}

		public void ProcessEvents ()
		{
			//throw new NotImplementedException ();
		}

		public bool IsDown (Crow.Key key)
		{
			throw new NotImplementedException ();
		}

		public bool Shift => false;// throw new NotImplementedException ();

		public bool Ctrl => false;//throw new NotImplementedException ();

		public bool Alt => false;// throw new NotImplementedException ();

		Crow.MouseCursor Crow.IBackend.Cursor {
			set {
				CursorShape cs = CursorShape.Arrow;

				switch (value) {
				case MouseCursor.IBeam:
					cs = CursorShape.IBeam;
					break;
				case MouseCursor.Crosshair:
					cs = CursorShape.Crosshair;
					break;
				case MouseCursor.Hand:
					cs = CursorShape.Hand;
					break;
				case MouseCursor.H:
				case MouseCursor.Right:
				case MouseCursor.Left:
					cs = CursorShape.HResize;
					break;
				case MouseCursor.V:
				case MouseCursor.Top:
				case MouseCursor.Bottom:
					cs = CursorShape.VResize;
					break;
				}
				SetCursor (cs);
			}
		}
		#endregion
	}
}
