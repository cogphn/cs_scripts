using System;

public class EvilClass
{
	public static bool Run() {
		Console.WriteLine("EVIL HAX!!");
		return true;
	}
}


public sealed class Diagnostics : AppDomainManager
{
	public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
	{
		Console.WriteLine("test1");
		bool flag = EvilClass.Run();
	}
}
