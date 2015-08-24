public sealed class C
{
	public void M()
	{ }
}
//--
public static class CFluentVoidMethods
{
	public static C DoM(this C self)
	{
		self.M();
		return self;
	}
}
