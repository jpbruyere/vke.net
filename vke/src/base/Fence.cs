// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Managed activable fence.
	/// </summary>
	public class Fence : Activable {
		internal VkFence handle;
		VkFenceCreateInfo info = default;

		public Fence (Device dev, bool signaled = false, string name = "fence") : base (dev, name) {
			info.flags = signaled ? VkFenceCreateFlags.Signaled : 0;
			Activate ();
		}

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.Fence, handle.Handle);

		public sealed override void Activate () {
			if (state != ActivableState.Activated) {
				CheckResult (vkCreateFence (Dev.Handle, ref info, IntPtr.Zero, out handle));
			}
			base.Activate ();
		}
		/// <summary>
		/// Wait this fence to become signaled.
		/// </summary>
		/// <param name="timeOut">Time out before cancelling the wait.</param>
		public void Wait (ulong timeOut = UInt64.MaxValue) {
			vkWaitForFences (Dev.Handle, 1, ref handle, 1, timeOut);
		}
		/// <summary>
		/// put this fence in the unsignaled state.
		/// </summary>
		public void Reset () {
			vkResetFences (Dev.Handle, 1, ref handle);
		}

		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString ("x")}]");
		}

		public static implicit operator VkFence (Fence f) => f == null ? 0 : f.handle;

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (state == ActivableState.Activated)
				vkDestroyFence (Dev.Handle, handle, IntPtr.Zero);
			if (!disposing)
				System.Diagnostics.Debug.WriteLine ("VKE Activable object disposed by finalizer");
			base.Dispose (disposing);
		}
		#endregion
	}


	public class Fences : Collection<Fence>, IDisposable {
		public void Wait (ulong timeOut = UInt64.MaxValue) {
			VkFence[] fences = Items.Cast<VkFence> ().ToArray ();
			vkWaitForFences (Items[0].Dev.Handle, (uint)Count, fences.Pin (), 1, timeOut);
			fences.Unpin ();
		}
		public void Reset () {
			VkFence[] fences = Items.Cast<VkFence> ().ToArray ();
			vkResetFences (Items[0].Dev.Handle, (uint)Count, fences.Pin ());
			fences.Unpin ();
		}

		public void Dispose () {
			foreach (Fence f in Items)
				f.Dispose ();
			ClearItems ();
		}
	}
}
