using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Provider
{
    /// <summary>
    /// Indicates whether a repository's items are fetched from a local or remote source.
    /// </summary>
    public enum RepositoryItemSource
    {
        Local,
        Remote
    }

    /// <summary>
    /// Defines a repository of items that can be refreshed and queried.
    /// </summary>
    /// <typeparam name="T">The type of item provided by this repository. Must implement IRepositoryItem.</typeparam>
    public interface IRepository<T>
        where T : class, IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// Defines the source of the repository's items.
        /// </summary>
        RepositoryItemSource ItemSource { get; }

        /// <summary>
        /// Contains all items provided by the repository.
        /// </summary>
        List<T> Items { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Refreshes the data provided by the repository.
        /// </summary>
        Task RefreshAsync();

        /// <summary>
        /// Allows fetching a repository item by its ID.
        /// </summary>
        /// <param name="id">The item's ID.</param>
        /// <returns>The relevant item, or null if none is found.</returns>
        T this[string id] { get; }
        #endregion
    }
}
