public static class GenericTypeWithPropertyFluentProperties1
{
	public static SelfT Property<SelfT, T>(this SelfT self, System.Boolean value)
		where SelfT : AutoFluent.Tests.GenericTypeWithProperty<T>
	{
		self.Property = value;
		return self;
	}
}