// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;
using Version = Vulkan.Version;
using Context = vke.Context;
using Glfw;

namespace Tesselation {
	class Program {
		static void Main (string[] args) {
			using (Context ctx = new Context()) {
				ctx.Run();
				
			}
		}
	}
}
