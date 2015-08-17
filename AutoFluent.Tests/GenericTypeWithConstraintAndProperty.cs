public static class GenericTypeWithConstraintAndPropertyFluentProperties
{
	public static AutoFluent.Tests.GenericTypeWithConstraintAndProperty<T> Property<T>(this AutoFluent.Tests.GenericTypeWithConstraintAndProperty<T> self, System.Boolean value) where T : System.Exception
	{
		self.Property = value;
		return self;
	}
}