// Copyright (c) 2019 Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;

namespace vke {
	public class MarshaledObject<T> : IDisposable where T : struct {

        GCHandle handle;

        public IntPtr Pointer {
            get {
                if (!handle.IsAllocated)
                    throw new InvalidOperationException ("Unalocated MarshaledObject");
                return handle.AddrOfPinnedObject ();
            }
        }

        public MarshaledObject (T mobj) {
            handle = GCHandle.Alloc (mobj, GCHandleType.Pinned);
        }
		~MarshaledObject () {
			handle.Free ();
		}
		public void Dispose () {
			handle.Free ();
			GC.SuppressFinalize (this);
		}        
    }
}
