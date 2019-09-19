using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Vulkan {
	public class FixedUtf8String : IDisposable {
		GCHandle handle;
		readonly uint numBytes;

		public IntPtr Ptr => handle.AddrOfPinnedObject ();

		public FixedUtf8String (string s) {
			if (s == null)
				throw new ArgumentNullException (nameof (s));

			byte[] text = Encoding.UTF8.GetBytes (s + "\0");
			handle = GCHandle.Alloc (text, GCHandleType.Pinned);
			numBytes = (uint)text.Length;
		}

		private string GetString () {
			return Marshal.PtrToStringUni (Ptr);
		}

		public static implicit operator IntPtr (FixedUtf8String utf8String) => utf8String.Ptr;
		public static implicit operator FixedUtf8String (string s) => new FixedUtf8String (s);
		public static implicit operator string (FixedUtf8String utf8String) => utf8String.GetString ();

		#region IDisposable Support
		private bool disposedValue = false; // Pour détecter les appels redondants

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				handle.Free ();
				disposedValue = true;
			}
		}
		~FixedUtf8String () {
			Dispose (false);
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}
		#endregion
	}
}