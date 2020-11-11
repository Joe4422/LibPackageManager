using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Repositories
{
    /// <summary>
    /// Defines a repository of items that can be refreshed and queried.
    /// </summary>
    /// <typeparam name="T">The type of item provided by this repository. Must implement IRepositoryItem.</typeparam>
    public interface IRepository<T>
        where T : class, IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// Contains all items provided by the repository.
        /// </summary>
        List<T> Items { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Allows fetching a repository item by its ID.
        /// </summary>
        /// <param name="id">The item's ID.</param>
        /// <returns>The relevant item, or null if none is found.</returns>
        T this[string id]
        {
            get
            {
                try
                {
                    return Items.First(x => x.Id == id);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Refreshes the data provided by the repository.
        /// </summary>
        Task RefreshAsync();
        #endregion
    }
}
