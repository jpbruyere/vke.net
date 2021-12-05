// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vulkan;

namespace vke {
	/// <summary>
	/// Hold shader specialization constant value and type
	/// </summary>
	public class SpecializationConstant<T> : SpecializationConstant {
		T val;

        public T Value => val;

		#region CTOR
		public SpecializationConstant (uint id, T value) : base(id) {
            val = value;
        }
		#endregion

		public override uint Size => (uint)Marshal.SizeOf<T> ();
		internal unsafe override void WriteTo (IntPtr ptr) {
			if (typeof (T) == typeof (float)) {
				float v = Convert.ToSingle (Value);
				System.Buffer.MemoryCopy (&v, ptr.ToPointer (), 4, 4);
			} else if (typeof (T) == typeof (int) || typeof (T) == typeof (uint)) {
				Marshal.WriteInt32 (ptr, Convert.ToInt32 (val));
			} else if (typeof (T) == typeof (long) || typeof (T) == typeof (ulong)) {
				Marshal.WriteInt64 (ptr, Convert.ToInt64 (val));
			} else if (typeof (T) == typeof (byte)) {
				Marshal.WriteByte (ptr, Convert.ToByte (val));
			}
		}
	}
	public abstract class SpecializationConstant {
		public uint id;
		public SpecializationConstant (uint id) {
			this.id = id;
		}
		public abstract uint Size { get; }
		internal abstract void WriteTo (IntPtr ptr);
	}

	/// <summary>
	/// Specialization constant infos, MUST be disposed after pipeline creation
	/// </summary>
	public class SpecializationInfo : IDisposable {
		IntPtr pData;
		VkSpecializationMapEntry[] entries;

		public VkSpecializationInfo infos;


		#region CTOR
		public SpecializationInfo (params SpecializationConstant[] constants) {
			uint offset = 0;
			entries = new VkSpecializationMapEntry[constants.Length];
			for (int i = 0; i < constants.Length; i++) {
				entries[i] = new VkSpecializationMapEntry { constantID = constants[i].id, offset = offset, size = (UIntPtr)constants[i].Size };
				offset += constants[i].Size;
			}
			int totSize = (int)offset;
			offset = 0;
			pData = Marshal.AllocHGlobal (totSize);
			IntPtr curPtr = pData;
			foreach (SpecializationConstant sc in constants) {
				sc.WriteTo (curPtr);
				curPtr += (int)sc.Size;
			}

			infos = new VkSpecializationInfo {
				pMapEntries = entries,
				pData = pData,
				dataSize = (UIntPtr)totSize
			};
		}
		#endregion

		public void Dispose () {
			infos.Dispose();
			Marshal.FreeHGlobal (pData);
		}
	}
}
