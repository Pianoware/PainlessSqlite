namespace Pianoware.PainlessSqlite
{
	class ContextInfo
	{
		internal SetInfo[] Sets { get; }
		internal ContextInfo(SetInfo[] sets)
		{
			this.Sets = sets;
		}
	}
}
