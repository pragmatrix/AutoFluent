﻿namespace XF
{
	public class BindableObject
	{
	}

	public class TableSectionBase<T> where T : BindableObject
	{
		public void CopyTo(T[] array, int arrayIndex)
		{}
	}
}
//--
namespace XF
{
	public static class TableSectionBaseFluentVoidMethods1
	{
		public static XF.TableSectionBase<T> DoCopyTo<T>(this XF.TableSectionBase<T> self, T[] array, System.Int32 arrayIndex)
			where T : XF.BindableObject
		{
			self.CopyTo(array, arrayIndex);
			return self;
		}
	}
}

