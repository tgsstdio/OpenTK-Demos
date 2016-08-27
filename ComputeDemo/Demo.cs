using System;
using OpenTK.Graphics.OpenGL;

namespace ComputeDemo
{
	public class Demo
	{
		private int mRenderProgramId;
		private int mComputeProgramId;
		private int mHeight;
		private int mWidth;

		public Demo (int width, int height)
		{
			this.mHeight = height;
			this.mWidth = width;
		}

		public void Initialize()
		{
			int texHandle = GenerateDestTex();
			mRenderProgramId = SetupRenderProgram(texHandle);
			mComputeProgramId = SetupComputeProgram(texHandle);
		}

		public void Update(int frame) {
			GL.UseProgram(mComputeProgramId);
			GL.Uniform1(GL.GetUniformLocation(mComputeProgramId, "roll"), (float)frame*0.01f);
			GL.DispatchCompute(mWidth/16, mHeight/16, 1); // width * height threads in blocks of 16^2
			//checkErrors("Dispatch compute shader");
		}

		public void Draw() {
			GL.UseProgram(mRenderProgramId);
			GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
			//checkErrors("Draw screen");
		}

		private int SetupRenderProgram(int texHandle) {
			int progHandle =  GL.CreateProgram();
			int vp = GL.CreateShader(ShaderType.VertexShader);
			int fp = GL.CreateShader(ShaderType.FragmentShader);

			string vpSrc = 
			"#version 430\n"+
			"in vec2 pos; "+
			"out vec2 texCoord; "+
			"void main() { "+
				"texCoord = pos*0.5f + 0.5f; "+
				"gl_Position = vec4(pos.x, pos.y, 0.0, 1.0); " +
			"} ";


			string fpSrc = 
				"#version 430\n" +
				"uniform sampler2D srcTex; " +
				"in vec2 texCoord; " + 
				"out vec4 color; " +
				"void main() { " + 
				"float c = texture(srcTex, texCoord).x; " +
				"color = vec4(c, 1.0, 1.0, 1.0); " +
				"} ";


 			GL.ShaderSource(vp, vpSrc);
			GL.ShaderSource(fp, fpSrc);

			GL.CompileShader(vp);
			int rvalue;
			GL.GetShader(vp, ShaderParameter.CompileStatus, out rvalue);
			if (rvalue != (int) All.True) {
				Console.WriteLine("Error in compiling vp");
				Console.WriteLine((All) rvalue);
				Console.WriteLine(GL.GetShaderInfoLog (vp));
			}
			GL.AttachShader(progHandle, vp);

			GL.CompileShader(fp);
			GL.GetShader(fp, ShaderParameter.CompileStatus, out rvalue);
			if (rvalue != (int) All.True) {
				Console.WriteLine("Error in compiling fp");
				Console.WriteLine((All) rvalue);
				Console.WriteLine(GL.GetShaderInfoLog (fp));
			}
			GL.AttachShader(progHandle, fp);

			GL.BindFragDataLocation(progHandle, 0, "color");
			GL.LinkProgram(progHandle);

			GL.GetProgram(progHandle,GetProgramParameterName.LinkStatus, out rvalue);
			if (rvalue != (int) All.True) {
				Console.WriteLine("Error in linking sp");
				Console.WriteLine((All) rvalue);
				Console.WriteLine(GL.GetProgramInfoLog(progHandle));
			}

			GL.UseProgram(progHandle);
			GL.Uniform1(GL.GetUniformLocation(progHandle, "srcTex"), 0);

			int vertArray;
			vertArray = GL.GenVertexArray();
			GL.BindVertexArray(vertArray);

			int posBuf;
			posBuf = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, posBuf);
			float[] data = {
				-1.0f, -1.0f,
				-1.0f, 1.0f,
				1.0f, -1.0f,
				1.0f, 1.0f
			};
			IntPtr dataSize = (IntPtr) (sizeof(float) * 8);

			GL.BufferData<float>(BufferTarget.ArrayBuffer, dataSize, data, BufferUsageHint.StreamDraw);
			int posPtr = GL.GetAttribLocation(progHandle, "pos");
			GL.VertexAttribPointer(posPtr, 2,VertexAttribPointerType.Float, false, 0, 0);
			GL.EnableVertexAttribArray(posPtr);

			//checkErrors("Render shaders");
			return progHandle;
		}

		private int GenerateDestTex() {
			// We create a single float channel 512^2 texture
			int texHandle;
			texHandle = GL.GenTexture();

			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, texHandle);
			GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter, (int) All.Linear);
			GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter, (int) All.Linear);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, 512, 512, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);

			// Because we're also using this tex as an image (in order to write to it),
			// we bind it to an image unit as well
			GL.BindImageTexture(0, texHandle, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);
			//checkErrors("Gen texture");	
			return texHandle;
		}

		private int SetupComputeProgram(int texHandle) {
			// Creating the compute shader, and the program object containing the shader
			int progHandle = GL.CreateProgram();
			int cs = GL.CreateShader(ShaderType.ComputeShader);

			// In order to write to a texture, we have to introduce it as image2D.
			// local_size_x/y/z layout variables define the work group size.
			// gl_GlobalInvocationID is a uvec3 variable giving the global ID of the thread,
			// gl_LocalInvocationID is the local index within the work group, and
			// gl_WorkGroupID is the work group's index
			string csSrc = 
				"#version 430\n" +
				"uniform float roll; " +
				"uniform writeonly image2D destTex; " +
				"layout (local_size_x = 16, local_size_y = 16) in; " +
				"void main() { " +
				"ivec2 storePos = ivec2(gl_GlobalInvocationID.xy); " +
				"float localCoef = length(vec2(ivec2(gl_LocalInvocationID.xy)-8)/8.0); " +
				"float globalCoef = sin(float(gl_WorkGroupID.x+gl_WorkGroupID.y)*0.1 + roll)*0.5; " +
				"imageStore(destTex, storePos, vec4(1.0-globalCoef*localCoef, 0.0, 0.0, 0.0)); " +
				"} ";
			
			GL.ShaderSource(cs, csSrc);
			GL.CompileShader(cs);
			int rvalue;
			GL.GetShader(cs,ShaderParameter.CompileStatus, out rvalue);
			if (rvalue != (int) All.True)
			{
				Console.WriteLine(GL.GetShaderInfoLog (cs));
			}
			GL.AttachShader(progHandle, cs);

			GL.LinkProgram(progHandle);
			GL.GetProgram(progHandle,GetProgramParameterName.LinkStatus, out rvalue);
			if (rvalue != (int) All.True)
			{ 
				Console.WriteLine(GL.GetProgramInfoLog(progHandle));
			}

			GL.UseProgram(progHandle);

			GL.Uniform1(GL.GetUniformLocation(progHandle, "destTex"), 0);

			//checkErrors("Compute shader");
			return progHandle;
		}
	}
}

