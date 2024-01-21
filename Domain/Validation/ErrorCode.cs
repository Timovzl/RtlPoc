namespace Rtl.News.RtlPoc.Domain;

/// <summary>
/// <para>
/// The stable error codes defined by this bounded context.
/// </para>
/// <para>
/// Names are stable. Numeric values are meaningless.
/// </para>
/// <para>
/// DO NOT DELETE OR RENAME ITEMS.
/// </para>
/// </summary>
public enum ErrorCode
{
	// DO NOT DELETE OR RENAME ITEMS

	PartitionKey_ValueTooLong,
	PartitionKey_ValueInvalid,

	ExternalId_ValueNull,
	ExternalId_ValueEmpty,
	ExternalId_ValueTooLong,
	ExternalId_ValueInvalid,

	// DO NOT DELETE OR RENAME ITEMS
}
