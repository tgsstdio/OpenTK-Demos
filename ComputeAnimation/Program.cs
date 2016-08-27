using System;

namespace ComputeAnimation
{
	class MainClass
	{
		[STAThread]
		public static void Main()
		{
			using (MyComputeAnimation example = new MyComputeAnimation())
			{
				//Utilities.SetWindowTitle(example);
				example.Run(30);
			}
		}
	}
}
