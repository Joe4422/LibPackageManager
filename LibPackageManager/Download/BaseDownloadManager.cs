using LibPackageManager.Database;
using LibPackageManager.Provider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Download
{
    /// <summary>
    /// Base for a download manager class for a given IRepositoryItem.
    /// </summary>
    /// <typeparam name="T">The IRepositoryItem that this class supports downloading.</typeparam>
    public abstract class BaseDownloadManager<T>
        where T : class, IRepositoryItem
    {
        #region Variables
        protected string downloadDir;
        protected string installDir;
        #endregion

        #region Properties
        /// <summary>
        /// Contains all items that are currently being downloaded.
        /// </summary>
        public List<T> DownloadsInProgress { get; } = new List<T>();
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new download manager.
        /// </summary>
        /// <param name="downloadDir">The directory content should be downloaded to before installing.</param>
        /// <param name="installDir">The directory downloaded content should be installed to.</param>
        public BaseDownloadManager(string downloadDir, string installDir)
        {
            this.downloadDir = downloadDir ?? throw new ArgumentNullException(nameof(downloadDir));
            this.installDir = installDir ?? throw new ArgumentNullException(nameof(installDir));
        }
        #endregion

        #region Methods
        /// <summary>
        /// Downloads and installs an item and its dependencies.
        /// </summary>
        /// <param name="item">The item to download.</param>
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        public async Task<bool> GetItemAsync(T item)
        {
            // Check item is not null
            if (item is null) throw new ArgumentNullException(nameof(item));

            // Create download task list and start downloading item
            List<Task<bool>> itemDownloadTasks = new()
            {
                GetSingleItemAsync(item)
            };

            // Check if this item has dependencies
            if (item is IDependentRepositoryItem depItem)
            {
                // Start each dependency's download and add it to the download list
                foreach (string key in depItem.Dependencies.Keys)
                {
                    itemDownloadTasks.Add(GetItemAsync(depItem.Dependencies[key] as T));
                }
            }

            // Wait until all tasks are completed
            await Task.WhenAll(itemDownloadTasks);

            return !itemDownloadTasks.Select(x => x.Result).Contains(false);
        }

        /// <summary>
        /// Downloads and installs an item.
        /// </summary>
        /// <param name="item">The item to download and install.</param>
        /// <exception cref="ArgumentNullException"
        protected async Task<bool> GetSingleItemAsync(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            if (item.IsDownloaded) return true;
            else if (item.DownloadUrl == null) return false;
            else if (DownloadsInProgress.Contains(item)) return true;

            DownloadsInProgress.Add(item);

            string downloadPath = $"{downloadDir}/{Path.GetFileName(item.DownloadUrl)}";

            // Ensure directories exist by creating them
            try
            {
                Directory.CreateDirectory(downloadDir);
                Directory.CreateDirectory(installDir);
            }
            catch (Exception)
            {
                return false;
            }

            // Download file
            using (WebClient client = new WebClient())
            {
                try
                {
                    await client.DownloadFileTaskAsync(item.DownloadUrl, downloadPath);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            // Install item from file
            await InstallItemAsync(item, downloadPath);

            // Mark item as downloaded
            item.InstallPath = $"{installDir}/{item.Id}";

            DownloadsInProgress.Remove(item);

            // Remove downloaded file now we've installed its content
            File.Delete(downloadPath);

            return true;
        }

        /// <summary>
        /// Uninstalls an item.
        /// </summary>
        /// <param name="item">The item to uninstall.</param>
        public async Task RemoveItemAsync(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            if (!item.IsDownloaded) return;
            if (!Directory.Exists($"{installDir}/{item.Id}")) return;
            await Task.Run(() => Directory.Delete($"{installDir}/{item.Id}", true));
            item.InstallPath = null;
        }

        /// <summary>
        /// Installs an item to the specified directory.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="downloadedFilePath">Path to the item's downloaded content.</param>
        protected abstract Task InstallItemAsync(T item, string downloadedFilePath);
        #endregion
    }
}
