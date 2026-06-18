using System.Data.Common;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Translates <see cref="DbException"/>s to descendants of <see cref="DataAccessException"/>
	/// when the data provider is the Microsoft SQL Server client.
	/// </summary>
	public class MicrosoftSqlServerExceptionTransformer : IExceptionTransformer
	{
		#region IExceptionTransformer implementation

		/// <summary>
		/// Transform an exception from the database provider.
		/// </summary>
		/// <param name="dbException">The exception thrown from the database provider.</param>
		/// <returns>Returns the transformed exception.</returns>
		public DataAccessException TranslateDbException(DbException dbException)
		{
			var sqlException = dbException as SqlException;

			if (sqlException == null)
				return new DataAccessException(dbException.Message, dbException);

			var errors = sqlException.Errors.OfType<SqlError>().ToArray();

			if (errors.Any(e => e.Number == 2601 || e.Number == 2627))
				return new UniqueConstraintViolationException(sqlException);

			if (errors.Any(e => e.Number == 547))
				return new ReferentialConstraintViolationException(sqlException);

			return new IntegrityViolationException(sqlException);
		}

		#endregion
	}
}
