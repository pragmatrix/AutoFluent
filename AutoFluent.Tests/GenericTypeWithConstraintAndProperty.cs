public static class GenericTypeWithConstraintAndPropertyFluentProperties1
{
	public static SelfT Property<SelfT, T>(this SelfT self, System.Boolean value)
		where SelfT : AutoFluent.Tests.GenericTypeWithConstraintAndProperty<T>
		where T : System.Exception
	{
		self.Property = value;
		return self;
	}
}