namespace DataBuilder.Model
{
	/// <summary>시트 컬럼이 관계형 스키마에서 맡는 역할.</summary>
	public enum EColumnRole
	{
		None,
		PrimaryKey,
		ForeignKey,
		DataKey,
	}
}
