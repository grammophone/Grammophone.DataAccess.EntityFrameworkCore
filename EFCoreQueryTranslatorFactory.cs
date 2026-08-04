using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Grammophone.DataAccess.QueryExtensions;
using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Factory for the Entity Framework Core query translator.
	/// </summary>
	public static class EFCoreQueryTranslatorFactory
	{
		#region Private fields

		private static readonly QueryTranslator QueryTranslator = new QueryTranslator(
			new EFCoreTerminalMethodsAdapter(),
			new EFCoreShapingMethodsAdapter(),
			new EFCoreSetOperationMethodsAdapter(),
			CreateMethodMappings());

		#endregion

		#region Public methods

		/// <summary>
		/// Get the shared Entity Framework Core query translator.
		/// </summary>
		/// <returns>Returns the shared Entity Framework Core query translator.</returns>
		public static QueryTranslator GetQueryTranslator()
		{
			return QueryTranslator;
		}

		#endregion

		#region Private methods

		private static IReadOnlyDictionary<MethodInfo, MethodMapping> CreateMethodMappings()
		{
			var mappings = new Dictionary<MethodInfo, MethodMapping>();

			AddDbFunctionsMapping(
				mappings,
				QueryFunctionsMethodInfos.Like,
				MethodInfoCatalog.GetMethodInfo(typeof(DbFunctionsExtensions), nameof(DbFunctionsExtensions.Like), typeof(DbFunctions), typeof(string), typeof(string)));

			AddDbFunctionsMapping(
				mappings,
				QueryFunctionsMethodInfos.LikeWithEscape,
				MethodInfoCatalog.GetMethodInfo(typeof(DbFunctionsExtensions), nameof(DbFunctionsExtensions.Like), typeof(DbFunctions), typeof(string), typeof(string), typeof(string)));

			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffYearsDateTime, "DateDiffYear", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffYearsDateTimeOffset, "DateDiffYear", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMonthsDateTime, "DateDiffMonth", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMonthsDateTimeOffset, "DateDiffMonth", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffDaysDateTime, "DateDiffDay", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffDaysDateTimeOffset, "DateDiffDay", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffHoursDateTime, "DateDiffHour", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffHoursDateTimeOffset, "DateDiffHour", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMinutesDateTime, "DateDiffMinute", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMinutesDateTimeOffset, "DateDiffMinute", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffSecondsDateTime, "DateDiffSecond", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffSecondsDateTimeOffset, "DateDiffSecond", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMillisecondsDateTime, "DateDiffMillisecond", typeof(DateTime?), typeof(DateTime?));
			AddDateDiffMapping(mappings, QueryFunctionsMethodInfos.DiffMillisecondsDateTimeOffset, "DateDiffMillisecond", typeof(DateTimeOffset?), typeof(DateTimeOffset?));
			AddCreateDateTimeMapping(mappings);

			AddDateTimeAddMapping(mappings, QueryFunctionsMethodInfos.AddDays, nameof(DateTime.AddDays), typeof(double));
			AddDateTimeAddMapping(mappings, QueryFunctionsMethodInfos.AddMonths, nameof(DateTime.AddMonths), typeof(int));
			AddDateTimeAddMapping(mappings, QueryFunctionsMethodInfos.AddYears, nameof(DateTime.AddYears), typeof(int));

			AddTruncateTimeMapping(mappings);

			return mappings;
		}

		private static void AddMapping(
			IDictionary<MethodInfo, MethodMapping> mappings,
			MethodInfo portableMethodInfo,
			MethodInfo nativeMethodInfo)
		{
			mappings.Add(portableMethodInfo, new IsomorphicMethodMapping(portableMethodInfo, nativeMethodInfo));
		}

		private static void AddDbFunctionsMapping(
			IDictionary<MethodInfo, MethodMapping> mappings,
			MethodInfo portableMethodInfo,
			MethodInfo nativeMethodInfo)
		{
			mappings.Add(
				portableMethodInfo,
				new ExpressionMethodMapping(
					portableMethodInfo,
					(_, arguments) => Expression.Call(
						null,
						nativeMethodInfo,
						new[] { Expression.Property(null, typeof(EF), nameof(EF.Functions)) }.Concat(arguments))));
		}

		private static void AddDateDiffMapping(
			IDictionary<MethodInfo, MethodMapping> mappings,
			MethodInfo portableMethodInfo,
			string nativeMethodName,
			params Type[] dateTypes)
		{
			AddDbFunctionsMapping(
				mappings,
				portableMethodInfo,
				MethodInfoCatalog.GetMethodInfo(
					typeof(SqlServerDbFunctionsExtensions),
					nativeMethodName,
					new[] { typeof(DbFunctions) }.Concat(dateTypes).ToArray()));
		}

		/// <summary>
		/// Map a portable date-addition function onto the corresponding <see cref="DateTime"/> instance method,
		/// which the SQL Server provider translates to <c>DATEADD</c>.
		/// </summary>
		/// <param name="mappings">The mappings being built.</param>
		/// <param name="portableMethodInfo">The portable <see cref="QueryFunctions"/> method.</param>
		/// <param name="nativeMethodName">The name of the <see cref="DateTime"/> instance method to call.</param>
		/// <param name="nativeAddValueType">The parameter type of the <see cref="DateTime"/> instance method.</param>
		/// <remarks>
		/// Without a mapping the call reaches Entity Framework Core unchanged. When every argument comes from a
		/// closure, its parameter extraction treats the whole call as evaluatable and invokes the portable method,
		/// which by contract throws. Rewriting the call to a translatable form both produces <c>DATEADD</c> when an
		/// argument belongs to the query and stays correct when the arguments are constants.
		/// </remarks>
		private static void AddDateTimeAddMapping(
			IDictionary<MethodInfo, MethodMapping> mappings,
			MethodInfo portableMethodInfo,
			string nativeMethodName,
			Type nativeAddValueType)
		{
			var nativeMethodInfo = typeof(DateTime).GetMethod(nativeMethodName, new[] { nativeAddValueType });

			mappings.Add(
				portableMethodInfo,
				new ExpressionMethodMapping(
					portableMethodInfo,
					(_, arguments) =>
					{
						var argumentArray = arguments.ToArray();
						var dateArgument = argumentArray[0];
						var addArgument = argumentArray[1];

						var addValue = GetUnderlyingValue(addArgument);

						if (addValue.Type != nativeAddValueType)
						{
							addValue = Expression.Convert(addValue, nativeAddValueType);
						}

						var call = Expression.Convert(
							Expression.Call(GetUnderlyingValue(dateArgument), nativeMethodInfo, addValue),
							typeof(DateTime?));

						return GuardAgainstNulls(call, typeof(DateTime?), dateArgument, addArgument);
					}));
		}

		/// <summary>
		/// Map the portable time truncation function onto the <see cref="DateTime.Date"/> property,
		/// which the SQL Server provider translates to a cast to <c>date</c>.
		/// </summary>
		/// <param name="mappings">The mappings being built.</param>
		/// <remarks>
		/// Only the <see cref="DateTime"/> overload is mapped. The <see cref="DateTimeOffset"/> one has no
		/// equivalent, because <see cref="DateTimeOffset.Date"/> yields a <see cref="DateTime"/> and would
		/// therefore change the result type of the portable function.
		/// </remarks>
		private static void AddTruncateTimeMapping(IDictionary<MethodInfo, MethodMapping> mappings)
		{
			mappings.Add(
				QueryFunctionsMethodInfos.TruncateDateTime,
				new ExpressionMethodMapping(
					QueryFunctionsMethodInfos.TruncateDateTime,
					(_, arguments) =>
					{
						var dateArgument = arguments.ToArray()[0];

						var truncation = Expression.Convert(
							Expression.Property(GetUnderlyingValue(dateArgument), nameof(DateTime.Date)),
							typeof(DateTime?));

						return GuardAgainstNulls(truncation, typeof(DateTime?), dateArgument);
					}));
		}

		/// <summary>
		/// Unwrap a nullable expression to its underlying value, leaving non-nullable expressions untouched.
		/// </summary>
		private static Expression GetUnderlyingValue(Expression expression)
			=> Nullable.GetUnderlyingType(expression.Type) != null
				? Expression.Property(expression, nameof(Nullable<int>.Value))
				: expression;

		/// <summary>
		/// Yield null when any of the nullable <paramref name="arguments"/> is null, otherwise the given expression,
		/// preserving the null propagation of the portable functions.
		/// </summary>
		private static Expression GuardAgainstNulls(Expression expression, Type resultType, params Expression[] arguments)
		{
			var nullTests = arguments
				.Where(argument => Nullable.GetUnderlyingType(argument.Type) != null)
				.Select(argument => (Expression)Expression.Equal(argument, Expression.Constant(null, argument.Type)))
				.ToArray();

			if (nullTests.Length == 0) return expression;

			return Expression.Condition(
				nullTests.Aggregate(Expression.OrElse),
				Expression.Constant(null, resultType),
				expression);
		}

		private static void AddCreateDateTimeMapping(IDictionary<MethodInfo, MethodMapping> mappings)
		{
			var nativeMethodInfo = MethodInfoCatalog.GetMethodInfo(
				typeof(SqlServerDbFunctionsExtensions),
				nameof(SqlServerDbFunctionsExtensions.DateTimeFromParts),
				typeof(DbFunctions),
				typeof(int),
				typeof(int),
				typeof(int),
				typeof(int),
				typeof(int),
				typeof(int),
				typeof(int));

			mappings.Add(
				QueryFunctionsMethodInfos.CreateDateTime,
				new ExpressionMethodMapping(
					QueryFunctionsMethodInfos.CreateDateTime,
					(_, arguments) =>
					{
						var argumentArray = arguments.ToArray();
						var nullDate = Expression.Constant(null, typeof(DateTime?));
						var secondValue = Expression.Property(argumentArray[5], nameof(Nullable<double>.Value));
						var secondWholePart = Expression.Convert(secondValue, typeof(int));
						var millisecondPart = Expression.Convert(
							Expression.Multiply(
								Expression.Subtract(secondValue, Expression.Convert(secondWholePart, typeof(double))),
								Expression.Constant(1000D)),
							typeof(int));

						var call = Expression.Convert(
							Expression.Call(
								null,
								nativeMethodInfo,
								new Expression[]
								{
									Expression.Property(null, typeof(EF), nameof(EF.Functions)),
									Expression.Property(argumentArray[0], nameof(Nullable<int>.Value)),
									Expression.Property(argumentArray[1], nameof(Nullable<int>.Value)),
									Expression.Property(argumentArray[2], nameof(Nullable<int>.Value)),
									Expression.Property(argumentArray[3], nameof(Nullable<int>.Value)),
									Expression.Property(argumentArray[4], nameof(Nullable<int>.Value)),
									secondWholePart,
									millisecondPart
								}),
							typeof(DateTime?));

						return Expression.Condition(
							argumentArray
								.Select(argument => Expression.Equal(argument, Expression.Constant(null, argument.Type)))
								.Aggregate(Expression.OrElse),
							nullDate,
							call);
					}));
		}
		#endregion
	}
}
