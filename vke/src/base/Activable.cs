// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Xml.Serialization;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Tristate status of activables, reflecting vulkan operations
	/// </summary>
	public enum ActivableState {
		/// <summary>
		/// Class has been instanced, but no vulkan handle was created.
		/// </summary>
		Init,
		/// <summary>
		/// On the first activation, vulkan handle is created and ref count is one. Further activations will increment the reference count by one.
		/// </summary>
		Activated,
		/// <summary>
		/// Reference count is zero, handles have been destroyed. Such object may be reactivated.
		/// </summary>
		Disposed
	};
	/// <summary>
	/// Base class for most of the vulkan device's objects following the IDispose pattern. Each time an activable is used, it's reference count is incremented, and
	/// each time it is disposed, the count is decremented. When the count reach zero, the handle is destroyed and the finalizizer is unregistered. Once disposed, the
	/// objecte may still be reactivated.
	/// </summary>
	/// <remarks>
	/// Some of the activation trigger the first activation on creation, those activables have to be explicitly dispose at the end of the application.
	/// The activables that trigger activation only on usage does not require an additional dispose at the end.
	/// </remarks>
	public abstract class Activable : IDisposable {
		//count number of activation, only the first one will create a handle
		[XmlIgnore] protected uint references;
		//keep track of the current state of activation.
		protected ActivableState state;
		//With the debug utils extension, setting name to vulkan's object ease the debugging.
		protected string name;
		/// <summary>
		/// This property has to be implemented in every vulkan object. It must return the correct debug marker info to use
		/// if VK_EXT_debug_utils extension is enabled.
		/// </summary>
		/// <value>The debug marker info.</value>
		protected abstract VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo { get; }
		/// <summary>
		/// Vulkan logical device this activable is bound to.
		/// </summary>
		[XmlIgnore] public Device Dev { get; private set; }
		public PNextNode PNext;

		#region CTOR
		protected Activable (Device dev) {
			Dev = dev;
			name = GetType ().Name;
		}
		protected Activable (Device dev, string name) {
			Dev = dev;
			this.name = name;
		}
		#endregion

		/// <summary>
		/// if debug marker extension is activated, this will set the name for debuggers
		/// </summary>
		public void SetName (string name) {
			this.name = name;

			if (!Dev.debugUtilsEnabled)
				return;

			VkDebugUtilsObjectNameInfoEXT dmo = DebugUtilsInfo;
			dmo.pObjectName = name.Pin();
			CheckResult (vkSetDebugUtilsObjectNameEXT (Dev.Handle, ref dmo));
			name.Unpin ();
		}
		/// <summary>
		/// Activation of the object, the reference count is incremented and if Debug utils is enabled, name is set.
		/// </summary>
		public virtual void Activate () {
			references++;
			if (state == ActivableState.Activated)
				return;
			if (state == ActivableState.Disposed)
				GC.ReRegisterForFinalize (this);
			state = ActivableState.Activated;
			SetName (name);
		}

		public override string ToString () {
			return name;
		}

		#region IDisposable Support
		protected virtual void Dispose (bool disposing) {
			state = ActivableState.Disposed;
		}

		~Activable() {
			Dispose(false);
		}
		public void Dispose () {
			if (references>0)
				references--;
			if (references>0)
				return;
			Dispose (true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
