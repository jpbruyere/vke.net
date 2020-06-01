// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace vke {
    public class StbImage : IDisposable {
#if STB_SHARP
		GCHandle gcHandle;
		public IntPtr Handle => gcHandle.AddrOfPinnedObject ();
#else
		const string stblib = "stb";

        [DllImport (stblib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stbi_load")]
        static extern IntPtr Load ([MarshalAs (UnmanagedType.LPStr)] string filename, out int x, out int y, out int channels_in_file, int desired_channels);

		[DllImport (stblib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stbi_load_from_memory")]
        static extern IntPtr Load (IntPtr bitmap, int byteCount, out int x, out int y, out int channels_in_file, int desired_channels);

		[DllImport (stblib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stbi_load_from_memory")]
		static extern IntPtr Load (ref byte bitmap, int byteCount, out int x, out int y, out int channels_in_file, int desired_channels);

		[DllImport (stblib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stbi_image_free")]
        static extern void FreeImage (IntPtr img);

		public IntPtr Handle { get; private set; }
#endif

		public readonly int Width;
		public readonly int Height;
		public readonly int Channels;
		public int Size => Width * Height * Channels;

		/// <summary>
		/// Open image with STBI library
		/// </summary>
		/// <param name="path">file path</param>
		/// <param name="requestedChannels">Force returned channels count, set 0 for original count</param>
		public StbImage (string path, int requestedChannels = 4) {
#if STB_SHARP
			using (Stream stream = new FileStream (path, FileMode.Open)) {
				StbImageSharp.ImageResult stbi =
					StbImageSharp.ImageResult.FromStream (stream, (StbImageSharp.ColorComponents)requestedChannels);
				Width = stbi.Width;
				Height = stbi.Height;
				Channels = (int)stbi.Comp;
				gcHandle = GCHandle.Alloc (stbi.Data, GCHandleType.Pinned);
			}
#else
			Handle = StbImage.Load (path, out Width, out Height, out Channels, requestedChannels);
			if (Handle == IntPtr.Zero)
				throw new Exception ($"STBI image loading error.");
#endif
			if (requestedChannels > 0)
				Channels = requestedChannels;
		}
		/// <summary>
		/// Open image with STBI library
		/// </summary>
		/// <param name="bitmap">raw bitmap datas</param>
		/// <param name="bitmapByteCount">Bitmap byte count.</param>
		/// <param name="requestedChannels">Force returned channels count, set 0 for original count</param>
		public StbImage (IntPtr bitmap, ulong bitmapByteCount, int requestedChannels = 4) {
#if STB_SHARP
			unsafe {
				Span<byte> byteArray = new Span<byte> (bitmap.ToPointer (), (int)bitmapByteCount);
				StbImageSharp.ImageResult stbi =
					StbImageSharp.ImageResult.FromMemory (byteArray.ToArray (), (StbImageSharp.ColorComponents)requestedChannels);
				Width = stbi.Width;
				Height = stbi.Height;
				Channels = (int)stbi.Comp;
				gcHandle = GCHandle.Alloc (stbi.Data, GCHandleType.Pinned);
			}
#else
			Handle = StbImage.Load (bitmap, (int)bitmapByteCount, out Width, out Height, out Channels, requestedChannels);
			if (Handle == IntPtr.Zero)
				throw new Exception ($"STBI image loading error.");
#endif
			if (requestedChannels > 0)
				Channels = requestedChannels;
		}
		/// <summary>
		/// Open image with STBI library
		/// </summary>
		/// <param name="bitmap">raw bitmap datas</param>
		/// <param name="requestedChannels">Force returned channels count, set 0 for original count</param>
		public StbImage (Memory<byte> bitmap, int requestedChannels = 4) {
#if STB_SHARP
			StbImageSharp.ImageResult stbi =
				StbImageSharp.ImageResult.FromMemory (bitmap.ToArray (), (StbImageSharp.ColorComponents)requestedChannels);
			Width = stbi.Width;
			Height = stbi.Height;
			Channels = (int)stbi.Comp;
			gcHandle = GCHandle.Alloc (stbi.Data, GCHandleType.Pinned);
#else
			Handle = StbImage.Load (ref MemoryMarshal.GetReference(bitmap.Span), bitmap.Length, out Width, out Height, out Channels, requestedChannels);
			if (Handle == IntPtr.Zero)
				throw new Exception ($"STBI image loading error.");
#endif
			if (requestedChannels > 0)
				Channels = requestedChannels;
		}
		/// <summary>
		/// copy pixels to destination.
		/// </summary>
		/// <param name="destPtr">Destination pointer.</param>
		public void CoptyTo (IntPtr destPtr) {
			unsafe {
				System.Buffer.MemoryCopy (Handle.ToPointer (), destPtr.ToPointer (), Size, Size);
			}
		}
		public void Dispose () {

#if STB_SHARP
			gcHandle.Free ();
#else
			StbImage.FreeImage (Handle);
#endif
		}
	}
}
