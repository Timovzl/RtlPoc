using System.Linq.Expressions;
using System.Reflection;

namespace Rtl.News.RtlPoc.Domain.Shared;

public static class JsonUtilities
{
	/// <summary>
	/// Returns the JSON path to a property, e.g. '() => order.Data.Item' might produce "/Ord_Data/Itm".
	/// </summary>
	public static string GetPropertyPath<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> property)
	{
		return GetPropertyPathCore(property.Body as MemberExpression, expectedForm: "(Entity entity) => entity.Data.Value");
	}

	/// <summary>
	/// Returns the JSON path to a property, e.g. '() => order.Data.Item' might produce "/Ord_Data/Itm".
	/// </summary>
	public static string GetPropertyPath<TProperty>(Expression<Func<TProperty>> property)
	{
		return GetPropertyPathCore(property.Body as MemberExpression, expectedForm: "() => entity.Data.Value");
	}

	private static string GetPropertyPathCore(MemberExpression? memberExpression, string expectedForm)
	{
		var path = "";

		do
		{
			if (memberExpression is not MemberExpression { Member: PropertyInfo propertyInfo } || propertyInfo.GetGetMethod()?.IsStatic != false)
				throw new ArgumentException($"A non-static property member expression in a form like '{expectedForm}' was expected.");

			if (propertyInfo.GetCustomAttribute<JsonPropertyAttribute>() is not JsonPropertyAttribute { PropertyName: string propertyName })
				throw new ArgumentException($"Property '{propertyInfo.ReflectedType?.Name}.{propertyInfo.Name}' is missing the {nameof(JsonPropertyAttribute)}.");

			// Simple concatenation, since nested paths are rare
			path = $"/{propertyName}{path}";
		} while (TryDigToNextNestedMemberExpression(ref memberExpression));

		return path;
	}

	/// <summary>
	/// <para>
	/// Digs through a given <see cref="MemberExpression"/> (e.g. entity.Data.Value), returning false if a constant or parameter (e.g. entity) is reached, or true otherwise.
	/// </para>
	/// <para>
	/// Overwrites <paramref name="memberExpression"/> with the next part of the chain (e.g. entity.Data), or null if it is not a <see cref="MemberExpression"/>.
	/// </para>
	/// </summary>
	private static bool TryDigToNextNestedMemberExpression(ref MemberExpression? memberExpression)
	{
		// Once we reach a parameter, a constant, or a field, then we can terminate, since that is the symbol whose member was being accessed
		// The reason to also include field is that constants can turn into fields when variables are captured
		if (memberExpression is null || memberExpression.Expression is ConstantExpression or ParameterExpression or MemberExpression { Member: FieldInfo, Expression: ConstantExpression })
		{
			memberExpression = null;
			return false;
		}

		memberExpression = memberExpression.Expression as MemberExpression;
		return true;
	}
}
