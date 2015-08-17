public static class GenericTypeWithPropertyFluentProperties1
{
	public static AutoFluent.Tests.GenericTypeWithProperty<T> Property<T>(this AutoFluent.Tests.GenericTypeWithProperty<T> self, System.Boolean value)
	{
		self.Property = value;
		return self;
	}
}