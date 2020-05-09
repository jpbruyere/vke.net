// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Crow;
using Crow.Cairo;
using Vulkan;

namespace vkeEditor {
	public class VkFormatWidget : Widget {
		public VkFormatWidget () {
		}

		VkFormat format;
		public VkFormat Format {
			get => format;
			set {
				if (format == value)
					return;
				format = value;
				NotifyValueChanged ("Format", format);
				RegisterForGraphicUpdate ();
			}
		}

		protected override void onDraw (Context gr) {
			base.onDraw (gr);

			Rectangle r = ClientRectangle;


		}
	}
}
