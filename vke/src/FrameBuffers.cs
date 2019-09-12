// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.ObjectModel;

namespace vke
{
	public class FrameBuffers : Collection<FrameBuffer>, IDisposable
	{
		//public Framebuffer this[int index] => Items[index];

		public void Dispose()
		{
			foreach (FrameBuffer fb in Items)
				fb.Dispose();
			ClearItems();
		}
	}
}
