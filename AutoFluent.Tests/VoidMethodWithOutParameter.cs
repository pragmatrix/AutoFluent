public sealed class C
{
	public void M(out int outParam)
	{
		outParam = 0;
	}
}
//--
public static class CFluentVoidMethods
{
	public static C DoM(this C self, out System.Int32 outParam)
	{
		self.M(out outParam);
		return self;
	}
}
