public static class SealedTypeWithPropertyFluentProperties
{
	public static AutoFluent.Tests.SealedTypeWithProperty Property(this AutoFluent.Tests.SealedTypeWithProperty self, System.Boolean value)
	{
		self.Property = value;
		return self;
	}
}