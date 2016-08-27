// This code was written for the OpenTK library and has been released
// to the Public Domain.
// It is provided "as is" without express or implied warranty of any kind.
// PLUS Changes made by tgs_stdio, 2016 (David Young)

using System;
using System.Diagnostics;
using System.IO;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Examples.Tutorial
{
    public class HelloGL3 : GameWindow
    {
        string vertexShaderSource = @"
#version 140

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
#version 140

precision highp float;

const vec3 ambient = vec3(0.1, 0.1, 0.1);
const vec3 lightVecNormalized = normalize(vec3(0.5, 0.5, 2.0));
const vec3 lightColor = vec3(0.9, 0.9, 0.7);

in vec3 normal;

out vec4 out_frag_color;

void main(void)
{
  float diffuse = clamp(dot(lightVecNormalized, normalize(normal)), 0.0, 1.0);
  out_frag_color = vec4(ambient + diffuse * lightColor, 1.0);
}";

        int vertexShaderHandle,
            fragmentShaderHandle,
            shaderProgramHandle,
            modelviewMatrixLocation,
            projectionMatrixLocation,
            vaoHandle,
		positionVboHandle;
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

        public HelloGL3()
            : base(640, 480,
            new GraphicsMode(), "OpenGL 3 Example", 0,
			DisplayDevice.Default, 3, 2,
            GraphicsContextFlags.ForwardCompatible | GraphicsContextFlags.Debug)
        { }

		void CreateMappableBuffer ()
		{
//			int localBuffer = GL.GenBuffer ();
//			GL.BindBuffer( BufferTarget.ElementArrayBuffer, localBuffer);
//
			var indices = new ushort[]{ 0, 1, 2, 1, 2, 3 };
//
//			var bufferSize = (IntPtr)(indices.Length * sizeof(ushort));
//			GL.BufferData (BufferTarget.ElementArrayBuffer, bufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
//			GL.Arb.BufferStorage(
//
//			IntPtr VideoMemoryIntPtr = GL.Ext.MapNamedBufferRange(localBuffer, IntPtr.Zero, bufferSize, BufferAccessMask.MapWriteBit);
//			GL.Ext.UnmapNamedBuffer(localBuffer);
//
//			GL.DeleteBuffer (localBuffer);
			var buffers = new int[1];

			// ARB_direct_state_access
				// Allows buffer objects to be initialised without binding them
			GL.CreateBuffers (1, buffers);
			var targetType = BufferTarget.ElementArrayBuffer;
			//GL.BindBuffer (targetType, buffers[0]);
			var bufferSize = (indices.Length * sizeof(ushort));
			BufferStorageFlags flags = BufferStorageFlags.MapCoherentBit;
				
			GL.NamedBufferStorage (buffers[0], bufferSize, IntPtr.Zero, BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);

			var error = GL.GetError ();
			Debug.WriteLineIf (error != ErrorCode.NoError, "NamedBufferStorage " + error);

			BufferAccessMask rangeFlags = BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit;
			IntPtr Handle = GL.MapNamedBufferRange (buffers[0], IntPtr.Zero, bufferSize, rangeFlags);

			error = GL.GetError ();
			Debug.WriteLineIf (error != ErrorCode.NoError, "MapNamedBufferRange " + error);

			var result = GL.Ext.UnmapNamedBuffer(buffers[0]);

			error = GL.GetError ();
			Debug.WriteLineIf (error != ErrorCode.NoError, "UnmapNamedBuffer " + error);


			GL.DeleteBuffer (buffers[0]);
		}

		void CreateTextureStorage ()
		{
			int[] textureID = new int[1];
			GL.CreateTextures(TextureTarget.Texture2D, 1, textureID);
			{
				var error = GL.GetError ();
				Debug.WriteLineIf (error != ErrorCode.NoError, "GenTextures " + error);
			}

			//GL.BindTexture(TextureTarget.Texture2D, textureID[0]);
//			{
//				var error = GL.GetError ();
//				Debug.WriteLineIf (error != ErrorCode.NoError, "BindTexture " + error);
//			}

			const int width = 2;
			const int height = 1;
			GL.Ext.TextureStorage2D(textureID[0], (ExtDirectStateAccess) All.Texture2D, 1, SizedInternalFormat.Rgba8, width, height);
			{
				var error = GL.GetError ();
				Debug.WriteLineIf (error != ErrorCode.NoError, "Ext.TextureStorage2D " + error);
			}

			byte[] pixels = new byte[]{ 255, 255, 255, 255, 255, 255, 255, 255 };

			GL.Ext.TextureSubImage2D(textureID[0], TextureTarget.Texture2D, 0, 0, 0, width​, height​, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
			{
				var error = GL.GetError ();
				Debug.WriteLineIf (error != ErrorCode.NoError, "Ext.TextureSubImage2D " + error);
			}

			GL.DeleteTextures(1, textureID);
			{
				var error = GL.GetError ();
				Debug.WriteLineIf (error != ErrorCode.NoError, "DeleteTextures " + error);
			}
		}
        
        protected override void OnLoad (System.EventArgs e)
        {
            VSync = VSyncMode.On;

			//CreateMappableBuffer ();
			//CreateTextureStorage ();

            CreateShaders();
            CreateVBOs();
            CreateVAOs();

            // Other state
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(Color.MidnightBlue);

			GL.BindVertexArray(vaoHandle);
        }

        void CreateShaders()
        {
            vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
            fragmentShaderHandle = GL.CreateShader(ShaderType.FragmentShader);

            GL.ShaderSource(vertexShaderHandle, vertexShaderSource);
            GL.ShaderSource(fragmentShaderHandle, fragmentShaderSource);

            GL.CompileShader(vertexShaderHandle);
            GL.CompileShader(fragmentShaderHandle);

            Debug.WriteLine(GL.GetShaderInfoLog(vertexShaderHandle));
            Debug.WriteLine(GL.GetShaderInfoLog(fragmentShaderHandle));

            // Create program
            shaderProgramHandle = GL.CreateProgram();

            GL.AttachShader(shaderProgramHandle, vertexShaderHandle);
            GL.AttachShader(shaderProgramHandle, fragmentShaderHandle);

            GL.BindAttribLocation(shaderProgramHandle, 0, "in_position");
            GL.BindAttribLocation(shaderProgramHandle, 1, "in_normal");

            GL.LinkProgram(shaderProgramHandle);
            Debug.WriteLine(GL.GetProgramInfoLog(shaderProgramHandle));
            GL.UseProgram(shaderProgramHandle);

            // Set uniforms
            projectionMatrixLocation = GL.GetUniformLocation(shaderProgramHandle, "projection_matrix");
            modelviewMatrixLocation = GL.GetUniformLocation(shaderProgramHandle, "modelview_matrix");

            float aspectRatio = ClientSize.Width / (float)(ClientSize.Height);
            Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, aspectRatio, 1, 100, out projectionMatrix);
            modelviewMatrix = Matrix4.LookAt(new Vector3(0, 3, 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

            GL.UniformMatrix4(projectionMatrixLocation, false, ref projectionMatrix);
            GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);
        }

        void CreateVBOs()
        {
//            positionVboHandle = GL.GenBuffer();
//            GL.BindBuffer(BufferTarget.ArrayBuffer, positionVboHandle);

			var totalLength = (sizeof(uint) * indicesVboData.Length) 
				+ (Marshal.SizeOf (typeof(Vector3)) * positionVboData.Length);

			var buffers = new int[1];
			// ARB_direct_state_access
			// Allows buffer objects to be initialised without binding them
			GL.CreateBuffers (1, buffers);

			{
				var error = GL.GetError ();
				//Debug.WriteLineIf (error != ErrorCode.NoError, "GL.CreateBuffers " + error);
			}

			positionVboHandle = buffers [0];

			BufferStorageFlags flags = BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit;
			GL.NamedBufferStorage (positionVboHandle, totalLength, IntPtr.Zero, flags);

			{
				var error = GL.GetError ();
				//Debug.WriteLineIf (error != ErrorCode.NoError, "GL.NamedBufferStorage " + error);
			}

//            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer,
//                new IntPtr(positionVboData.Length * Vector3.SizeInBytes),
//                positionVboData, BufferUsageHint.StaticDraw);

           // normalVboHandle = GL.GenBuffer();
//            GL.BindBuffer(BufferTarget.ArrayBuffer, normalVboHandle);
//            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer,
//                new IntPtr(positionVboData.Length * Vector3.SizeInBytes),
//                positionVboData, BufferUsageHint.StaticDraw);

           // eboHandle = GL.GenBuffer();
//			GL.BindBuffer(BufferTarget.ElementArrayBuffer, positionVboHandle);
//            GL.BufferData(BufferTarget.ElementArrayBuffer,
//                new IntPtr(sizeof(uint) * indicesVboData.Length),
//                indicesVboData, BufferUsageHint.StaticDraw);

//            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
//            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        void CreateVAOs()
        {
			int[] result = new int[1];
			GL.CreateVertexArrays(1, result);

			Debug.Assert (GL.IsVertexArray (result [0]));
			vaoHandle = result [0];


            // GL3 allows us to store the vertex layout in a "vertex array object" (VAO).
            // This means we do not have to re-issue VertexAttribPointer calls
            // every time we try to use a different vertex layout - these calls are
            // stored in the VAO so we simply need to bind the correct VAO.
            //GL.BindVertexArray(vaoHandle);

			var indexOffset = IntPtr.Zero;
			var indexSize = sizeof(uint) * indicesVboData.Length;

			var bufferIndex = 0;

			GL.VertexArrayElementBuffer (vaoHandle, positionVboHandle);

			var positionOffset = new IntPtr(indexSize);
			GL.VertexArrayVertexBuffer (vaoHandle, bufferIndex, positionVboHandle, positionOffset, Vector3.SizeInBytes);

			//GL.BindBufferRange(BufferTarget.ElementArrayBuffer, 0, positionVboHandle, indexOffset, indexSize);

//			GL.EnableVertexAttribArray(0);
//            GL.BindBuffer(BufferTarget.ArrayBuffer, positionVboHandle);


			var stride = Marshal.SizeOf (typeof(Vector3));
			int positionSize = stride * positionVboData.Length;

			var positionIndex = 0;

			GL.VertexArrayAttribBinding(vaoHandle, positionIndex, bufferIndex);
			GL.EnableVertexArrayAttrib (vaoHandle, positionIndex);
			GL.VertexArrayAttribFormat (vaoHandle, positionIndex, 3, VertexAttribType.Float, true, 0);
			//GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, positionOffset);

//            GL.EnableVertexAttribArray(1);
//			GL.BindBuffer(BufferTarget.ArrayBuffer, positionVboHandle);

			// SAME DATA
			var normalOffset = positionOffset;
			var normalSize = positionSize;
			var normalIndex = 1;
			//GL.VertexArrayAttribFormat (vaoHandle, 0, 3, VertexAttribType.Float, true, positionOffset);
			//GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, normalOffset);

			GL.VertexArrayAttribBinding(vaoHandle, normalIndex, bufferIndex);
			GL.EnableVertexArrayAttrib (vaoHandle, normalIndex);
			//GL.VertexArrayVertexBuffer (vaoHandle, normalIndex, positionVboHandle, positionOffset, Vector3.SizeInBytes);
			GL.VertexArrayAttribFormat (vaoHandle, normalIndex, 3, VertexAttribType.Float, true, 0);


			BufferAccessMask rangeFlags = BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit;
			var indexDest = GL.MapNamedBufferRange (positionVboHandle, indexOffset, indexSize, rangeFlags);
			// Copy here
			const int START_INDEX = 0;
			Marshal.Copy (indicesVboData, START_INDEX, indexDest, indicesVboData.Length);
			GL.Ext.UnmapNamedBuffer (positionVboHandle);

			// Copy here 
			IntPtr vertexDest = GL.MapNamedBufferRange (positionVboHandle, positionOffset, positionSize, rangeFlags);

			// Copy the struct to unmanaged memory.	
			int copyOffset = 0;
			int elementCount = positionVboData.Length;
			var dest = vertexDest;
			var data = positionVboData;
			var startIndex = 0;
			for(int i = 0; i < elementCount; ++i)
			{
				IntPtr localDest = IntPtr.Add(dest, copyOffset);
				Marshal.StructureToPtr(data[i + startIndex], localDest, false);
				copyOffset += stride;
			}

			GL.Ext.UnmapNamedBuffer (positionVboHandle);

			{
				var error = GL.GetError ();
				//Debug.WriteLineIf (error != ErrorCode.NoError, "GL.Ext.UnmapNamedBuffer " + error);
			}

			// Copy here

//			var normalDest = GL.MapNamedBufferRange (positionVboHandle, normalOffset, normalSize, rangeFlags);
//
//			GL.Ext.UnmapNamedBuffer (positionVboHandle);


           // GL.BindVertexArray(0);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            Matrix4 rotation = Matrix4.CreateRotationY((float)e.Time);
            Matrix4.Mult(ref rotation, ref modelviewMatrix, out modelviewMatrix);
            GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);

            var keyboard = OpenTK.Input.Keyboard.GetState();
            if (keyboard[OpenTK.Input.Key.Escape])
                Exit();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


			GL.DrawElements(PrimitiveType.Triangles, indicesVboData.Length,
				DrawElementsType.UnsignedInt, IntPtr.Zero);

            SwapBuffers();
        }
        
        [STAThread]
        public static void Main()
        {
            using (HelloGL3 example = new HelloGL3())
            {
                //Utilities.SetWindowTitle(example);
                example.Run(30);
            }
        }
    }
}