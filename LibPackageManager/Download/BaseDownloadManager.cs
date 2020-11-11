using LibPackageManager.Database;
using LibPackageManager.Provider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Download
{
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
        public async Task<string> DownloadItemAsync(T item)
        {
            List<Task> itemDownloadTasks = new List<Task>
            {
                DownloadSingleItemAsync(item)
            };

            if (item is IDependentRepositoryItem depItem)
            {
                foreach (string key in depItem.Dependencies.Keys)
                {
                    itemDownloadTasks.Add(DownloadItemAsync(depItem.Dependencies[key] as T));
                }
            }

            await Task.WhenAll(itemDownloadTasks);

            return null;
        }

        /// <summary>
        /// Downloads and installs an item.
        /// </summary>
        /// <param name="item">The item to download and install.</param>
        protected async Task DownloadSingleItemAsync(T item)
        {
            if (item.IsDownloaded) return;
            else if (item.DownloadUrl == null) return;
            else if (DownloadsInProgress.Contains(item)) return;

            DownloadsInProgress.Add(item);

            string downloadPath = $"{downloadDir}/{Path.GetFileName(item.DownloadUrl)}";

            // Ensure directories exist by creating them
            Directory.CreateDirectory(downloadDir);
            Directory.CreateDirectory(installDir);

            // Download file
            using (WebClient client = new WebClient())
            {
                await client.DownloadFileTaskAsync(item.DownloadUrl, downloadPath);
            }

            // Install item from file
            await InstallItemAsync(item, downloadPath);

            // Mark item as downloaded
            item.InstallPath = $"{installDir}/{item.Id}";

            DownloadsInProgress.Remove(item);
        }

        public async Task RemoveItemAsync(T item)
        {
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
