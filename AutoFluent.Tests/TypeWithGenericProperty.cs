public static class TypeWithGenericPropertyFluentProperties
{
	public static _SelfT Property<_SelfT>(this _SelfT self, System.Action<System.Boolean> value)
		where _SelfT : AutoFluent.Tests.TypeWithGenericProperty
	{
		self.Property = value;
		return self;
	}
}