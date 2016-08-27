using System;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;

namespace ComputeDemo
{
	class MainClass
	{
		[STAThread]
		public static void Main (string[] args)
		{
			Console.WriteLine ("Hello World!");

			int width = 1024;
			int height = 1024;
			using (var game = new GameWindow (width, height))
			{
				Demo d = new Demo (game.Width, game.Height);
				game.Load += (sender, e) =>
				{					
					// setup settings, load textures, sounds
					d.Initialize();
					game.VSync = VSyncMode.On;

				};

				game.Unload += (sender, e) => 
				{
				};

				game.KeyDown += (object sender, KeyboardKeyEventArgs e) => 
				{
					if (e.Key == Key.Space)
					{
						game.Exit();
					}
				};

				int i = 0;
				game.UpdateFrame += (sender, e) =>
				{
					// add game logic, input handling

					// update shader uniforms

					// update shader mesh

					d.Update(i % 2014);
					++i;
				};


				game.RenderFrame += (sender, e) =>
				{
					GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

					d.Draw();

					game.SwapBuffers();
				};

				game.Resize += (sender, e) =>
				{
					GL.Viewport(0, 0, game.Width, game.Height);
				};

				game.Run(60.0);
			}
		}
	}
}
