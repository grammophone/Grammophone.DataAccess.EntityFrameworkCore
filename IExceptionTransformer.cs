using System.Data.Common;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Contract for translators of exceptions from various database types to
	/// normalized <see cref="DataAccessException"/> descendants.
	/// </summary>
	public interface IExceptionTransformer
	{
		/// <summary>
		/// Transform an exception from the database provider.
		/// </summary>
		/// <param name="dbException">The exception thrown from the database provider.</param>
		/// <returns>Returns the transformed exception.</returns>
		DataAccessException TranslateDbException(DbException dbException);
	}
}
