using Com.Gabosgab.UnityCache.Server;
using System;

namespace Com.Gabosgab.UnityCache
{

	public static class MainClass
	{
		public static void Main ()
		{
			UnityCacheServer server = new UnityCacheServer ();

			server.Start ();

			Console.WriteLine ("Press any key to shutdown...");
			Console.ReadKey ();

			Console.WriteLine ("Shutting down server...");
			server.Stop ();

		}
	}
}
