public static class TypeWithGenericPropertyFluentProperties
{
	public static SelfT Property<SelfT>(this SelfT self, System.Action<System.Boolean> value)
		where SelfT : AutoFluent.Tests.TypeWithGenericProperty
	{
		self.Property = value;
		return self;
	}
}