public static class TypeWithGenericPropertyFluentProperties
{
	public static AutoFluent.Tests.TypeWithGenericProperty Property(this AutoFluent.Tests.TypeWithGenericProperty self, System.Action<System.Boolean> value)
	{
		self.Property = value;
		return self;
	}
}