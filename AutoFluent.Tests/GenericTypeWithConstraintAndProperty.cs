public static class GenericTypeWithConstraintAndPropertyFluentProperties1
{
	public static _SelfT Property<_SelfT, T>(this _SelfT self, System.Boolean value)
		where _SelfT : AutoFluent.Tests.GenericTypeWithConstraintAndProperty<T>
		where T : System.Exception
	{
		self.Property = value;
		return self;
	}
}