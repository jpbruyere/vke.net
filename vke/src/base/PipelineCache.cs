// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Vulkan;

using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	/// <summary>
	/// Activable holding the pipeline cache handle. Activation is triggered by usage, so disposing pipelines that use this
	/// cache is enough to have the cache disposed correctly. 
	/// </summary>
	/// <remarks>
	/// Restore and Saving of the cache may be controled through the two static members:
	/// 	- `SaveOnDispose`
	/// 	- `LoadOnActivation`
	/// </remarks>
	public sealed class PipelineCache : Activable {
		/// <summary>
		/// If true, cache will be saved on dispose
		/// </summary>
		public static bool SaveOnDispose;
		/// <summary>
		/// If true, cache will be restored on activation
		/// </summary>
		public static bool LoadOnActivation;

		internal VkPipelineCache handle;
		readonly string globalConfigPath;
		readonly string cacheFile;

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
			=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.PipelineCache, handle.Handle);

		#region CTOR
		public PipelineCache (Device dev, string cacheFile = "pipelines.bin", string name = "pipeline cache") : base(dev, name) {
			string configRoot = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".config");
			string appName = Assembly.GetEntryAssembly ().GetName ().Name;
			globalConfigPath = Path.Combine (configRoot, appName);

			if (!Directory.Exists (globalConfigPath))
				Directory.CreateDirectory (globalConfigPath);

			this.cacheFile = cacheFile;
        }
		#endregion

		public override void Activate () {
			string path = Path.Combine (globalConfigPath, cacheFile);

			if (state != ActivableState.Activated) {
				VkPipelineCacheCreateInfo info = default;

				if (File.Exists (path) && LoadOnActivation) {
					using (FileStream fs = File.Open (path, FileMode.Open)) {
						using (BinaryReader br = new BinaryReader (fs)) {
							int length = (int)br.BaseStream.Length;
							info.pInitialData = Marshal.AllocHGlobal (length);
							info.initialDataSize = (UIntPtr)br.BaseStream.Length;
							Marshal.Copy(br.ReadBytes (length),0, info.pInitialData, length);
						}
					}
				}

				CheckResult (vkCreatePipelineCache (Dev.Handle, ref info, IntPtr.Zero, out handle));

				if (info.pInitialData != IntPtr.Zero)
					Marshal.FreeHGlobal (info.pInitialData);
			}
			base.Activate ();
		}
		/// <summary>
		/// Delete pipeline backend file from disk if file exist.
		/// </summary>
		public void Delete () {
			string path = Path.Combine (globalConfigPath, cacheFile);
			if (File.Exists (path))
				File.Delete (path);
		}
		/// <summary>
		/// Save pipeline cache on disk in the user/.config/application directory.
		/// </summary>
		public void Save () {
			if (state != ActivableState.Activated)
				return;

			string path = Path.Combine (globalConfigPath, cacheFile);

			if (File.Exists (path))
				File.Delete (path);

			UIntPtr dataSize;
			CheckResult (vkGetPipelineCacheData (Dev.Handle, handle, out dataSize, IntPtr.Zero));
			byte[] pData = new byte[(int)dataSize];
			CheckResult (vkGetPipelineCacheData (Dev.Handle, handle, out dataSize, pData.Pin ()));
			pData.Unpin ();

			using (FileStream fs = File.Open (path, FileMode.CreateNew)) 
				using (BinaryWriter br = new BinaryWriter (fs)) 
					br.Write (pData, 0, (int)dataSize);			
		}


		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (!disposing)
				System.Diagnostics.Debug.WriteLine ($"CVKL PipelineCache '{name}' disposed by finalizer");
			if (state == ActivableState.Activated) {
				if (SaveOnDispose)
					Save ();
				vkDestroyPipelineCache (Dev.Handle, handle, IntPtr.Zero);
			}
			base.Dispose (disposing);
		}
		#endregion

	}
}
