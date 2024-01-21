using System.Collections.Immutable;

namespace Rtl.News.RtlPoc.Domain.Shared;

public static class EnumExtensions
{
	private static class EnumValueCache<T>
		where T : struct, Enum
	{
		public static readonly ImmutableArray<T> SortedValues = Enum.GetValues<T>()
			.Order()
			.ToImmutableArray();
	}

	/// <summary>
	/// Enumerates the set of values for <typeparamref name="T"/> from <paramref name="currentValue"/> up to and including <paramref name="maximumValue"/>.
	/// </summary>
	/// <param name="currentValue">The inclusive minimum value.</param>
	/// <param name="maximumValue">The inclusive maximum value.</param>
	public static IEnumerable<T> Through<T>(this T currentValue, T maximumValue)
		where T : struct, Enum
	{
		return EnumValueCache<T>.SortedValues
			.SkipWhile(status => Comparer<T>.Default.Compare(status, currentValue) < 0)
			.TakeWhile(status => Comparer<T>.Default.Compare(status, maximumValue) <= 0);
	}

	/// <summary>
	/// Enumerates the set of values for <typeparamref name="T"/> from <paramref name="currentValue"/> up to (but excluding) <paramref name="toExclusive"/>.
	/// </summary>
	/// <param name="currentValue">The inclusive minimum value.</param>
	/// <param name="toExclusive">The exclusive upper bound.</param>
	public static IEnumerable<T> Until<T>(this T currentValue, T toExclusive)
		where T : struct, Enum
	{
		return EnumValueCache<T>.SortedValues
			.SkipWhile(status => Comparer<T>.Default.Compare(status, currentValue) < 0)
			.TakeWhile(status => Comparer<T>.Default.Compare(status, toExclusive) < 0);
	}
}
