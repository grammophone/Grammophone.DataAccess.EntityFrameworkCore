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
	public static class EFQueryTranslatorFactory
	{
		#region Private fields

		private static readonly QueryTranslator QueryTranslator = new QueryTranslator(
			new EFTerminalMethodsAdapter(),
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

			AddMapping(
				mappings,
				QueryExtensionMethodInfos.IncludeString,
				GetGenericMethodDefinition(
					typeof(EntityFrameworkQueryableExtensions),
					nameof(EntityFrameworkQueryableExtensions.Include),
					typeof(IQueryable<>),
					typeof(string)));

			AddMapping(
				mappings,
				QueryExtensionMethodInfos.IncludeExpression,
				GetGenericMethodDefinition(
					typeof(EntityFrameworkQueryableExtensions),
					nameof(EntityFrameworkQueryableExtensions.Include),
					typeof(IQueryable<>),
					typeof(Expression<>)));

			AddMapping(
				mappings,
				QueryExtensionMethodInfos.AsNoTracking,
				GetGenericMethodDefinition(
					typeof(EntityFrameworkQueryableExtensions),
					nameof(EntityFrameworkQueryableExtensions.AsNoTracking),
					typeof(IQueryable<>)));

			AddDbFunctionsMapping(
				mappings,
				QueryFunctionsMethodInfos.Like,
				GetMethod(typeof(DbFunctionsExtensions), nameof(DbFunctionsExtensions.Like), typeof(DbFunctions), typeof(string), typeof(string)));

			AddDbFunctionsMapping(
				mappings,
				QueryFunctionsMethodInfos.LikeWithEscape,
				GetMethod(typeof(DbFunctionsExtensions), nameof(DbFunctionsExtensions.Like), typeof(DbFunctions), typeof(string), typeof(string), typeof(string)));

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
				GetMethod(
					typeof(SqlServerDbFunctionsExtensions),
					nativeMethodName,
					new[] { typeof(DbFunctions) }.Concat(dateTypes).ToArray()));
		}

		private static MethodInfo GetGenericMethodDefinition(
			Type declaringType,
			string methodName,
			params Type[] parameterTypeDefinitions)
		{
			return GetMethod(
				declaringType,
				methodName,
				methodInfo => methodInfo.IsGenericMethodDefinition,
				parameterTypeDefinitions);
		}

		private static MethodInfo GetMethod(
			Type declaringType,
			string methodName,
			params Type[] parameterTypes)
		{
			return GetMethod(
				declaringType,
				methodName,
				methodInfo => !methodInfo.IsGenericMethod,
				parameterTypes);
		}

		private static MethodInfo GetMethod(
			Type declaringType,
			string methodName,
			Func<MethodInfo, bool> methodPredicate,
			Type[] parameterTypes)
		{
			foreach (var methodInfo in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (methodInfo.Name != methodName || !methodPredicate(methodInfo)) continue;

				var parameters = methodInfo.GetParameters();

				if (parameters.Length != parameterTypes.Length) continue;

				if (parameters.Select(p => NormalizeParameterType(p.ParameterType)).SequenceEqual(parameterTypes))
				{
					return methodInfo;
				}
			}

			throw new InvalidOperationException(
				$"Method '{methodName}' with the requested signature was not found in type '{declaringType.FullName}'.");
		}

		private static Type NormalizeParameterType(Type parameterType)
		{
			if (parameterType.IsGenericType)
			{
				return parameterType.GetGenericTypeDefinition();
			}

			return parameterType;
		}

		#endregion
	}
}
