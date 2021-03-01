using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;


namespace vke {
	public abstract class SampleBase : VkWindow {
#if NETCOREAPP		
		static IntPtr resolveUnmanaged (Assembly assembly, String libraryName) {
			
			switch (libraryName)
			{
				case "glfw3":
					return  NativeLibrary.Load("glfw", assembly, null);
				case "rsvg-2.40":
					return  NativeLibrary.Load("rsvg-2", assembly, null);
			}
			Console.WriteLine ($"[UNRESOLVE] {assembly} {libraryName}");			
			return IntPtr.Zero;
		}

		static SampleBase () {
			System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll+=resolveUnmanaged;
		}
#endif
		public SampleBase (string name = "VkWindow", uint _width = 800, uint _height = 600, bool vSync = true) :
			base (name, _width, _height, vSync){}
	}
}
