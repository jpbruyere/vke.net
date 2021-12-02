// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using Vulkan;

namespace vke {
	public abstract class PNextNode {
		protected bool isPinned;
		public abstract IntPtr GetPointer ();
		public abstract void ReleasePointer ();
	}
	public class PNextNode<T> : PNextNode {
		protected T nodeStruct;
		public PNextNode (T thisNodeStructure) {
			nodeStruct = thisNodeStructure;
		}
		public override IntPtr GetPointer () {
			isPinned = true;
			return nodeStruct.Pin();
		}
		public override void ReleasePointer () {
			nodeStruct.Unpin ();
			isPinned = false;
		}
	}
	public class PNextNode<T,U> : PNextNode<T> {
		PNextNode<U> nextNodeStruct;
		//T.pNext to store pinned reference of next struct
		FieldInfo fiPnextFromNodeStruct;
		public PNextNode (T thisNodeStruct, PNextNode<U> nextStruct) : base (thisNodeStruct) {
			nextNodeStruct = nextStruct;
			fiPnextFromNodeStruct = typeof(T).GetField ("pNext");
		}

		public override IntPtr GetPointer()
		{
			fiPnextFromNodeStruct.SetValue (nodeStruct, nextNodeStruct.GetPointer ());
			return base.GetPointer();
		}
		public override void ReleasePointer()
		{
			nextNodeStruct.ReleasePointer ();
			base.ReleasePointer();
		}
	}
}