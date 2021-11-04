using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;


namespace vke {
	public abstract class SampleBase : VkWindow {
		public SampleBase (string name = "VkWindow", uint _width = 800, uint _height = 600, bool vSync = true) :
			base (name, _width, _height, vSync){}
	}
}
