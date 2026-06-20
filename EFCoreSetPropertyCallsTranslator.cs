using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Grammophone.DataAccess.QueryExtensions;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Translates portable set property calls to Entity Framework Core set property calls.
	/// </summary>
	internal static class EFCoreSetPropertyCallsTranslator
	{
		#region Public methods

		/// <summary>
		/// Translate portable set property calls.
		/// </summary>
		public static Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>>> Translate<T>(
			Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> expression)
		{
			if (expression == null) throw new ArgumentNullException(nameof(expression));

			var sourceParameter = expression.Parameters.Single();
			var targetParameter = Expression.Parameter(
				typeof(Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>),
				sourceParameter.Name);

			var body = new Visitor<T>(sourceParameter, targetParameter).Visit(expression.Body);

			return Expression.Lambda<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>>>(
				body,
				targetParameter);
		}

		#endregion

		#region Private types

		private sealed class Visitor<T> : ExpressionVisitor
		{
			private readonly ParameterExpression sourceParameter;

			private readonly ParameterExpression targetParameter;

			public Visitor(ParameterExpression sourceParameter, ParameterExpression targetParameter)
			{
				this.sourceParameter = sourceParameter;
				this.targetParameter = targetParameter;
			}

			protected override Expression VisitParameter(ParameterExpression node)
			{
				return node == sourceParameter ? targetParameter : base.VisitParameter(node);
			}

			protected override Expression VisitMethodCall(MethodCallExpression node)
			{
				if (node.Method.DeclaringType?.IsGenericType == true
					&& node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(SetPropertyCalls<>))
				{
					var visitedArguments = node.Arguments.Select(Visit).ToArray();
					var nativeMethod = GetNativeSetPropertyMethod(node.Method, visitedArguments);

					return Expression.Call(visitedArguments[0], nativeMethod, visitedArguments.Skip(1));
				}

				return base.VisitMethodCall(node);
			}

			private static MethodInfo GetNativeSetPropertyMethod(MethodInfo portableMethod, Expression[] visitedArguments)
			{
				var genericArguments = portableMethod.GetGenericArguments();
				var nativeType = typeof(Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>);

				return nativeType.GetMethods()
					.Where(m => m.Name == nameof(Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>.SetProperty))
					.Where(m => m.IsGenericMethodDefinition)
					.Select(m => m.MakeGenericMethod(genericArguments))
					.Single(m => ParametersMatch(m, visitedArguments.Skip(1).ToArray()));
			}

			private static bool ParametersMatch(MethodInfo methodInfo, Expression[] arguments)
			{
				var parameters = methodInfo.GetParameters();

				if (parameters.Length != arguments.Length) return false;

				for (int i = 0; i < parameters.Length; i++)
				{
					if (!parameters[i].ParameterType.IsAssignableFrom(arguments[i].Type)) return false;
				}

				return true;
			}
		}

		#endregion
	}
}
