namespace XF
{
	public class Button
	{
		public Button.ButtonContentLayout ContentLayout { get; set; }

		public class ButtonContentLayout
		{}
	}
}
//--
namespace XF
{
	public static class ButtonFluentProperties
	{
		public static XF.Button ContentLayout(this XF.Button self, XF.Button.ButtonContentLayout value)
		{
			self.ContentLayout = value;
			return self;
		}
	}
}
