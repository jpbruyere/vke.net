using System;
using Vulkan;

namespace vke
{
	public abstract class SampleBase : VkWindow {
		public SampleBase (string name = "VkWindow", uint _width = 800, uint _height = 600, bool vSync = false) :
			base (name, _width, _height, vSync){}
		protected override void initVulkan()
		{
			base.initVulkan();
#if DEBUG
			foreach (VkPhysicalDeviceToolPropertiesEXT toolProp in phy.GetToolProperties()) {
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine ($"Enabled Tool: {toolProp.name}({toolProp.version})");
				Console.ResetColor ();
			}
#endif
		}
	}
}
