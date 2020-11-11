using System;
using System.Collections.Generic;
using System.Text;

namespace LibPackageManager.Repositories
{
    /// <summary>
    /// An item provided by a repository.
    /// </summary>
    public interface IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// The item's unique identifier.
        /// </summary>
        string Id { get; }
        /// <summary>
        /// The directory path where this item is installed.
        /// </summary>
        string InstallPath { get; set; }
        /// <summary>
        /// True if this item has been downloaded, false otherwise.
        /// </summary>
        bool IsDownloaded => InstallPath != null;
        /// <summary>
        /// The URL at which the item exists.
        /// </summary>
        string DownloadUrl { get; }
        #endregion
    }
}
