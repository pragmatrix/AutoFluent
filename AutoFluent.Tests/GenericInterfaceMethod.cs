namespace XF
{
	public interface IMessagingCenter
	{
		void Send<TSender>(TSender sender, string message) where TSender : class;
	}
}
//--
namespace XF
{
	public static class IMessagingCenterFluentVoidMethods
	{
		public static XF.IMessagingCenter DoSend<TSender>(this XF.IMessagingCenter self, TSender sender, System.String message)
			where TSender : class
		{
			self.Send(sender, message);
			return self;
		}
	}
}
