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
