public static class GenericTypeWithPropertyFluentProperties1
{
	public static _SelfT Property<_SelfT, T>(this _SelfT self, System.Boolean value)
		where _SelfT : AutoFluent.Tests.GenericTypeWithProperty<T>
	{
		self.Property = value;
		return self;
	}
}