namespace XF
{
	public class Base
	{
		public bool Test { get; set; }
	}

	public class Instance : Base
	{
	}
}
//--
namespace XF
{
	public static class BaseFluentProperties
	{
		public static XF.Base Test(this XF.Base self, System.Boolean value)
		{
			self.Test = value;
			return self;
		}
	}

	public static class InstanceFluentProperties
	{
		public static XF.Base Test(this XF.Base self, System.Boolean value)
		{
			self.Test = value;
			return self;
		}
	}
}
