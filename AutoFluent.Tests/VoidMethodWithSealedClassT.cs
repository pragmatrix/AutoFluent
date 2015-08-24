public sealed class Sealed<T>
{
	public void Invoke()
	{}
}
//--
public static class SealedFluentVoidMethods1
{
	public static Sealed<T> DoInvoke<T>(this Sealed<T> self)
	{
		self.Invoke();
		return self;
	}
}