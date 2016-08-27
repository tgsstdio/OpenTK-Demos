using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace ComputeAnimation
{
	public class ConstantBufferInfo<TData> : IDisposable
		where TData : struct
	{
		public ConstantBufferInfo(TData[] staticData) : this(staticData.Length)
		{
			Set(0, staticData);
		}

		public ConstantBufferInfo(int capacity)
		{
			var buffers = new int[1];
			// ARB_direct_state_access
			// Allows buffer objects to be initialised without binding them
			GL.CreateBuffers(1, buffers);

			// TODO : 
			Offset = IntPtr.Zero;
			Handle = buffers[0];
			Stride = Marshal.SizeOf(typeof(TData));
			Capacity = capacity;
			BufferSize = Capacity * Stride;
			GL.NamedBufferStorage(Handle, BufferSize, IntPtr.Zero, BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.NamedBufferStorage " + error);
			}
		}

		~ConstantBufferInfo()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool mIsDisposed = false;
		protected virtual void Dispose(bool disposed)
		{
			if (mIsDisposed)
				return;

			var buffers = new int[1];
			buffers[0] = Handle;
			GL.DeleteBuffers(1, buffers);

			mIsDisposed = true;
		}

		public int Handle { get; private set; }
		public uint Count { get; private set; }
		public int Capacity { get; private set; }
		public int BufferSize { get; private set; }
		public IntPtr Offset { get; private set; }
		public int Stride { get; private set; }

		public void Set(int firstIndex, TData[] requests)
		{
			Debug.Assert(requests != null);

			var firstOffset = firstIndex * Stride;
			var arrayByteLength = requests.Length * Stride;
			var remainingLength = BufferSize - firstOffset;

			Debug.Assert((firstOffset + arrayByteLength) <= BufferSize);

			var localOffset = new IntPtr(firstOffset);

			var rangeFlags = BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit;

			var handle = GL.MapNamedBufferRange(Handle, localOffset, remainingLength, rangeFlags);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.MapNamedBufferRange " + error);
			}

			// COPY HERE

			var offset = 0;
			foreach (var request in requests)
			{
				var dest = IntPtr.Add(handle, offset);
				Marshal.StructureToPtr(request, dest, false);
				offset += Stride;
			}

			var result = GL.Ext.UnmapNamedBuffer(Handle);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.UnmapNamedBuffer " + error);
			}
		}


	}
}

