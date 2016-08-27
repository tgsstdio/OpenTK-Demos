using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace ComputeAnimation
{
	class MyComputeAnimation : GameWindow
	{
		public MyComputeAnimation ()
			: base(640, 480,
			new GraphicsMode(), "MyComputeAnimation", 0,
			DisplayDevice.Default, 3, 2,
			GraphicsContextFlags.ForwardCompatible | GraphicsContextFlags.Debug)
		{
			
		}

		string vertexShaderSource = @"
#version 430

precision highp float;

uniform mat4 projection_matrix;
uniform mat4 modelview_matrix;

in vec3 in_position;
in vec3 in_normal;

out vec3 normal;

void main(void)
{
  //works only for orthogonal modelview
  normal = (modelview_matrix * vec4(in_normal, 0)).xyz;
  
  gl_Position = projection_matrix * modelview_matrix * vec4(in_position, 1);
}";

		string fragmentShaderSource = @"
#version 430

precision highp float;

const vec3 ambient = vec3(0.1, 0.1, 0.1);
const vec3 lightVecNormalized = normalize(vec3(0.5, 0.5, 2.0));
const vec3 lightColor = vec3(0.9, 0.9, 0.7);

in vec3 normal;

layout(r32ui) coherent uniform uimage1D currentBoneAnimation;
uniform double clockTime;

out vec4 out_frag_color;

void main(void)
{
  float diffuse = clamp(dot(lightVecNormalized, normalize(normal)), 0.0, 1.0);
  out_frag_color = vec4(ambient + diffuse * lightColor, 1.0);

  uvec4 value = imageLoad(currentBoneAnimation, 0);
  uint factor = value.r;

  double alpha = mod(clockTime, 3.0) / 3.0f;

  if (factor == 1)
  {
	out_frag_color = vec4(alpha,0,0, alpha);
  }
  else if (factor == 2)
  {
	out_frag_color = vec4(0,alpha,0, alpha);
  }
  else if (factor == 3)
  {
	out_frag_color = vec4(alpha,alpha,0, alpha);
  }
  else if (factor == 4)
  {
	out_frag_color = vec4(0,0,alpha, alpha);
  }
  else if (factor == 5)
  {
	out_frag_color = vec4(alpha,0,alpha, alpha);
  }
  else if (factor == 6)
  {
	out_frag_color = vec4(0,alpha,alpha, alpha);
  }
  else if (factor == 7)
  {
	out_frag_color = vec4(alpha,alpha,alpha, alpha);
  }
 // else 
 // {
	//out_frag_color = vec4(0,1,1, alpha);
 // }
}";
		int 
			mRender_ProgramID,
			mRender_Loc_modelviewMatrix,
			mRender_Loc_projectionMatrix,
			mRender_VertexArray,
			mRender_VBO_position;

		int mRender_Loc_clockTime;
		int mRender_Loc_currentBoneAnimation;

		// normalVboHandle,
		//     eboHandle;

		Vector3[] positionVboData = new Vector3[]{
			new Vector3(-1.0f, -1.0f,  1.0f),
			new Vector3( 1.0f, -1.0f,  1.0f),
			new Vector3( 1.0f,  1.0f,  1.0f),
			new Vector3(-1.0f,  1.0f,  1.0f),
			new Vector3(-1.0f, -1.0f, -1.0f),
			new Vector3( 1.0f, -1.0f, -1.0f),
			new Vector3( 1.0f,  1.0f, -1.0f),
			new Vector3(-1.0f,  1.0f, -1.0f) };

		int[] indicesVboData = new int[]{
             // front face
                0, 1, 2, 2, 3, 0,
                // top face
                3, 2, 6, 6, 7, 3,
                // back face
                7, 6, 5, 5, 4, 7,
                // left face
                4, 0, 3, 3, 7, 4,
                // bottom face
                0, 1, 5, 5, 4, 0,
                // right face
                1, 5, 6, 6, 2, 1, };

		Matrix4 projectionMatrix, modelviewMatrix;

		[StructLayout(LayoutKind.Sequential)]
		struct BoneTransform
		{
			public int Parent;
			public Vector3 Scale;
			public Vector3 Offset;
			public float Padding0;
			public Vector4 Quaternion;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct BoneTransition
		{
			public int MasterIndex;
			public uint FrameIndex;
			public uint Padding0;
			public uint Padding1;
			public float SegmentStart;
			public float SegmentEnd;
			public float SegmentLength;
			public float Padding2;
		};


		protected override void OnLoad(System.EventArgs e)
		{
			VSync = VSyncMode.On;

			CreateComputeShaders();
			//CreateTextureStorage();
			//CreateMappableBuffer();
			CreateShaders();

			CreateVBOs();
			CreateVAOs();

			// Other state
			GL.Enable(EnableCap.Texture1D);
			GL.Enable(EnableCap.DepthTest);
			GL.ClearColor(Color.MidnightBlue);

			GL.BindVertexArray(mRender_VertexArray);

		}

		const int SELECTREQUEST_CB0_BINDING_INDEX = 0;
		int mSelectRequest_Tex_CurrentBoneAnimation;

		int mSelectRequest_View_CurrentBoneAnimation;

		int mSelectRequest_Loc_CurrentBoneAnimation;
		int mSelectRequest_Loc_ClockTime;
		int mSelectRequest_ProgramID;
		int mSelectRequest_VBO;
		ConstantBufferInfo<BoneTransition> mCB0;

		private void CleanupGraphics()
		{
			GL.UseProgram(0);
			Console.WriteLine(nameof(this.CleanupGraphics));
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.CleanupGraphics " + error);
			}

			GL.DeleteProgram(mSelectRequest_ProgramID);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.DeleteProgram " + error);
			}

			var textures = new int[2];
			textures[0] = mSelectRequest_Tex_CurrentBoneAnimation;
			textures[1] = mSelectRequest_View_CurrentBoneAnimation;
			GL.DeleteTextures(2, textures);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.DeleteTextures " + error);
			}

			GL.DeleteVertexArray(mSelectRequest_VBO);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.DeleteVertexArray " + error);
			}

			mCB0.Dispose();
		}

		const string COMPUTE1_SRC = @"
#version 450
layout (local_size_x = 1) in;

struct BoneTransition
{
	int MasterIndex;
	uint FrameIndex;
	uint Padding0;
	uint Padding1;
	float SegmentStart;
	float SegmentEnd;
	float SegmentLength;
	float Padding2;
};

layout(std430, binding = 0) buffer CB0 {
    BoneTransition requests[];
};

layout(r32ui, binding = 1) coherent uniform uimage1D currentBoneAnimation;
uniform double clockTime;

void main(void)
{
	uint index = gl_GlobalInvocationID.x;
	BoneTransition req = requests[index];
	
	imageStore(currentBoneAnimation, 0, uvec4(0, 0, 0, 0));
	
	memoryBarrierImage();
	barrier();
	
	if (clockTime >= req.SegmentStart && clockTime <= req.SegmentEnd)
	{
		imageAtomicMax(currentBoneAnimation, req.MasterIndex, req.FrameIndex);
	}

	memoryBarrierImage();
	barrier();
}
";

		void CreateComputeShaders()
		{
			var compute1 = GL.CreateShader(ShaderType.ComputeShader);

			GL.ShaderSource(compute1, COMPUTE1_SRC);
			GL.CompileShader(compute1);

			Console.WriteLine(GL.GetShaderInfoLog(compute1));

			mSelectRequest_ProgramID = GL.CreateProgram();
			GL.AttachShader(mSelectRequest_ProgramID, compute1);
			GL.LinkProgram(mSelectRequest_ProgramID);

			Console.WriteLine(GL.GetProgramInfoLog(mSelectRequest_ProgramID));

			mSelectRequest_Loc_ClockTime = GL.GetUniformLocation(mSelectRequest_ProgramID, "clockTime");
			{
				Console.WriteLine("clockTime  : " + mSelectRequest_Loc_ClockTime);
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.GetUniformLocation " + error);
			}

			mSelectRequest_Loc_CurrentBoneAnimation = GL.GetUniformLocation(mSelectRequest_ProgramID, "currentBoneAnimation");

			{
				Console.WriteLine("currentBoneAnimation  : " + mSelectRequest_Loc_CurrentBoneAnimation);
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.GetUniformLocation " + error);
			}

			GL.DeleteShader(compute1);

			// VBOs 
			mCB0 = new ConstantBufferInfo<BoneTransition>(
				new []
				{
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 1,
						SegmentStart = 0f,
						SegmentEnd = 3f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 2,
						SegmentStart = 3f,
						SegmentEnd = 6f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 3,
						SegmentStart = 6f,
						SegmentEnd = 9f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 4,
						SegmentStart = 9f,
						SegmentEnd = 12f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 5,
						SegmentStart = 12f,
						SegmentEnd = 15f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 6,
						SegmentStart = 15f,
						SegmentEnd = 18f,
						SegmentLength = 3f,
					},
					new BoneTransition
					{
						MasterIndex = 0,
						FrameIndex = 7,
						SegmentStart = 18f,
						SegmentEnd = 21f,
						SegmentLength = 3f,
					},
				}
			);

			int[] result = new int[1];
			GL.CreateVertexArrays(1, result);

			mSelectRequest_VBO = result[0];

			//GL.VertexArrayVertexBuffer(mSelectRequest_VBO, SELECTREQUEST_CB0_BINDING_INDEX, mCB0.Handle, mCB0.Offset, mCB0.Stride);

			//{
			//	var error = GL.GetError();
			//	if (error != ErrorCode.NoError)
			//		Console.WriteLine("GL.VertexArrayVertexBuffer " + error);
			//}

			//GL.DeleteVertexArray(mSelectRequestVBO);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.DeleteVertexArray " + error);
			}

			CreateTextureStorage(10);

			GL.ProgramUniform1(mSelectRequest_ProgramID, mSelectRequest_Loc_CurrentBoneAnimation, mSelectRequest_Tex_CurrentBoneAnimation);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine(nameof(mSelectRequest_Loc_CurrentBoneAnimation) + " GL.ProgramUniform1 " + error);
			}
		}

		void CreateTextureStorage(int noOfBones)
		{
			int[] textureID = new int[1];
			GL.CreateTextures(TextureTarget.Texture1D, 1, textureID);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GenTextures " + error);
			}

			int width = noOfBones;
			GL.Ext.TextureStorage1D(textureID[0], (ExtDirectStateAccess)All.Texture1D, 1, SizedInternalFormat.R32ui, width);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("Ext.TextureStorage1D " + error);
			}

			var initialData = new uint[noOfBones];
			for (var i = 0; i < noOfBones; ++i)
			{
				initialData[i] = 0;
			}

			ResetTextureData(textureID[0], initialData);

			mSelectRequest_View_CurrentBoneAnimation  = GL.GenTexture();
			mSelectRequest_Tex_CurrentBoneAnimation = textureID[0];

			GL.TextureView(mSelectRequest_View_CurrentBoneAnimation, TextureTarget.Texture1D, mSelectRequest_Tex_CurrentBoneAnimation, PixelInternalFormat.R32ui, 0, 1, 0, 1);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.TextureView " + error);
			}
		}

		static void ResetTextureData(int textureID, uint[] initialData)
		{
			var width = (int) initialData.Length;
			GL.Ext.TextureSubImage1D<uint>(textureID, TextureTarget.Texture1D, 0, 0, width​, PixelFormat.RedInteger, PixelType.UnsignedInt, initialData);
			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("Ext.TextureSubImage1D " + error);
			}
		}

		void CreateShaders()
		{
			var vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
			var fragmentShaderHandle = GL.CreateShader(ShaderType.FragmentShader);

			GL.ShaderSource(vertexShaderHandle, vertexShaderSource);
			GL.ShaderSource(fragmentShaderHandle, fragmentShaderSource);

			GL.CompileShader(vertexShaderHandle);
			GL.CompileShader(fragmentShaderHandle);

			Console.WriteLine(GL.GetShaderInfoLog(vertexShaderHandle));
			Console.WriteLine(GL.GetShaderInfoLog(fragmentShaderHandle));

			// Create program
			mRender_ProgramID = GL.CreateProgram();

			GL.AttachShader(mRender_ProgramID, vertexShaderHandle);
			GL.AttachShader(mRender_ProgramID, fragmentShaderHandle);

			GL.BindAttribLocation(mRender_ProgramID, 0, "in_position");
			GL.BindAttribLocation(mRender_ProgramID, 1, "in_normal");

			GL.LinkProgram(mRender_ProgramID);
			Console.WriteLine(GL.GetProgramInfoLog(mRender_ProgramID));

			GL.DeleteShader(vertexShaderHandle);
			GL.DeleteShader(fragmentShaderHandle);

			GL.UseProgram(mRender_ProgramID);

			// Set uniforms
			mRender_Loc_projectionMatrix = GL.GetUniformLocation(mRender_ProgramID, "projection_matrix");
			mRender_Loc_modelviewMatrix = GL.GetUniformLocation(mRender_ProgramID, "modelview_matrix");
			mRender_Loc_currentBoneAnimation = GL.GetUniformLocation(mRender_ProgramID, "currentBoneAnimation");
			mRender_Loc_clockTime = GL.GetUniformLocation(mRender_ProgramID, "clockTime");

			float aspectRatio = ClientSize.Width / (float)(ClientSize.Height);
			Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, aspectRatio, 1, 100, out projectionMatrix);
			modelviewMatrix = Matrix4.LookAt(new Vector3(0, 3, 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

			GL.UniformMatrix4(mRender_Loc_projectionMatrix, false, ref projectionMatrix);
			GL.UniformMatrix4(mRender_Loc_modelviewMatrix, false, ref modelviewMatrix);

			GL.ProgramUniform1(mRender_ProgramID, mRender_Loc_currentBoneAnimation, mSelectRequest_View_CurrentBoneAnimation);
			{
				Console.WriteLine("mRender_Loc_currentBoneAnimation  : " + mRender_Loc_currentBoneAnimation);
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine(nameof(mRender_Loc_currentBoneAnimation) + " GL.ProgramUniform1 " + error);
			}
		}

		void CreateVBOs()
		{

			var totalLength = (sizeof(uint) * indicesVboData.Length)
				+ (Marshal.SizeOf(typeof(Vector3)) * positionVboData.Length);

			var buffers = new int[1];
			// ARB_direct_state_access
			// Allows buffer objects to be initialised without binding them
			GL.CreateBuffers(1, buffers);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.CreateBuffers " + error);
			}

			mRender_VBO_position = buffers[0];

			BufferStorageFlags flags = BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit;
			GL.NamedBufferStorage(mRender_VBO_position, totalLength, IntPtr.Zero, flags);

			{
				var error = GL.GetError();
				if (error != ErrorCode.NoError)
					Console.WriteLine("GL.NamedBufferStorage " + error);
			}
		}

		void CreateVAOs()
		{
			int[] result = new int[1];
			GL.CreateVertexArrays(1, result);

			Debug.Assert(GL.IsVertexArray(result[0]));
			mRender_VertexArray = result[0];

			var indexOffset = IntPtr.Zero;
			var indexSize = sizeof(uint) * indicesVboData.Length;

			var bufferIndex = 0;

			GL.VertexArrayElementBuffer(mRender_VertexArray, mRender_VBO_position);

			var positionOffset = new IntPtr(indexSize);
			GL.VertexArrayVertexBuffer(mRender_VertexArray, bufferIndex, mRender_VBO_position, positionOffset, Vector3.SizeInBytes);


			var stride = Marshal.SizeOf(typeof(Vector3));
			int positionSize = stride * positionVboData.Length;

			var positionIndex = 0;

			GL.VertexArrayAttribBinding(mRender_VertexArray, positionIndex, bufferIndex);
			GL.EnableVertexArrayAttrib(mRender_VertexArray, positionIndex);
			GL.VertexArrayAttribFormat(mRender_VertexArray, positionIndex, 3, VertexAttribType.Float, true, 0);

			// SAME DATA
			var normalOffset = positionOffset;
			var normalSize = positionSize;
			var normalIndex = 1;

			GL.VertexArrayAttribBinding(mRender_VertexArray, normalIndex, bufferIndex);
			GL.EnableVertexArrayAttrib(mRender_VertexArray, normalIndex);
			//GL.VertexArrayVertexBuffer (vaoHandle, normalIndex, positionVboHandle, positionOffset, Vector3.SizeInBytes);
			GL.VertexArrayAttribFormat(mRender_VertexArray, normalIndex, 3, VertexAttribType.Float, true, 0);


			BufferAccessMask rangeFlags = BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit;
			var indexDest = GL.MapNamedBufferRange(mRender_VBO_position, indexOffset, indexSize, rangeFlags);
			// Copy here
			const int START_INDEX = 0;
			Marshal.Copy(indicesVboData, START_INDEX, indexDest, indicesVboData.Length);
			GL.Ext.UnmapNamedBuffer(mRender_VBO_position);

			// Copy here 
			IntPtr vertexDest = GL.MapNamedBufferRange(mRender_VBO_position, positionOffset, positionSize, rangeFlags);

			// Copy the struct to unmanaged memory.	
			int copyOffset = 0;
			int elementCount = positionVboData.Length;
			var dest = vertexDest;
			var data = positionVboData;
			var startIndex = 0;
			for (int i = 0; i < elementCount; ++i)
			{
				IntPtr localDest = IntPtr.Add(dest, copyOffset);
				Marshal.StructureToPtr(data[i + startIndex], localDest, false);
				copyOffset += stride;
			}

			GL.Ext.UnmapNamedBuffer(mRender_VBO_position);

			{
				var error = GL.GetError();
				//Debug.WriteLineIf (error != ErrorCode.NoError, "GL.Ext.UnmapNamedBuffer " + error);
			}
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			//Matrix4 rotation = Matrix4.CreateRotationY((float)e.Time);
			//Matrix4.Mult(ref rotation, ref modelviewMatrix, out modelviewMatrix);
			//GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);

			var keyboard = OpenTK.Input.Keyboard.GetState();
			if (keyboard[OpenTK.Input.Key.Escape])
			{
				CleanupGraphics();
				Exit();
			}
		}

		private double mClockTime = 0;
		protected override void OnRenderFrame(FrameEventArgs e)
		{
			GL.Viewport(0, 0, Width, Height);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			GL.UseProgram(mSelectRequest_ProgramID);
			GL.BindVertexArray(mSelectRequest_VBO);

			double clockTime = (mClockTime % 21.0);
			GL.ProgramUniform1(mSelectRequest_ProgramID, mSelectRequest_Loc_ClockTime, clockTime);
			GL.ProgramUniform1(mSelectRequest_ProgramID, mSelectRequest_Loc_CurrentBoneAnimation, mSelectRequest_Tex_CurrentBoneAnimation);

			GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, mCB0.Handle);
			GL.BindImageTexture(1, mSelectRequest_Tex_CurrentBoneAnimation, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.R32ui);
				
			GL.DispatchCompute(7, 1, 1);

			GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

			GL.UseProgram(mRender_ProgramID);
			GL.BindVertexArray(mRender_VertexArray);
			GL.ProgramUniform1(mRender_ProgramID, mRender_Loc_currentBoneAnimation, mSelectRequest_Tex_CurrentBoneAnimation);
			GL.ProgramUniform1(mRender_ProgramID, mRender_Loc_clockTime, mClockTime);
			//GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, mCB0.Handle);
		//	GL.BindImageTexture(0, mSelectRequest_Tex_CurrentBoneAnimation, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32ui);

			GL.DrawElements(PrimitiveType.Triangles, indicesVboData.Length,
				DrawElementsType.UnsignedInt, IntPtr.Zero);

			GL.UseProgram(0);

			SwapBuffers();

			mClockTime += e.Time;
			//Console.WriteLine(mClockTime);
		}
	}
}